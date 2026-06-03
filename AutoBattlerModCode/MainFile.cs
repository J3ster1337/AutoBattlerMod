using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Runs;
using System.Reflection;

namespace AutoBattlerMod.AutoBattlerModCode
{
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "AutoBattlerMod";
        public static ModConfig Config = ModConfig.Load(Log);

        private static void Log(string message) => new MegaCrit.Sts2.Core.Logging.Logger(ModId, LogType.Generic).LogMessage(LogLevel.Info, message, 1);

        public static void Initialize()
        {
            Harmony harmony = new(ModId);

            harmony.Patch(
                AccessTools.Method(typeof(WhisperingEarring),
                "AfterAutoPrePlayPhaseEnteredLate",
                [typeof(PlayerChoiceContext), typeof(Player)]),
                prefix: new HarmonyMethod(typeof(MainFile),
                nameof(AfterAutoPrePlayPhaseEnteredLatePrefix)));

            harmony.Patch(
                AccessTools.Method(typeof(WhisperingEarring),
                nameof(WhisperingEarring.ModifyMaxEnergy)),
                prefix: new HarmonyMethod(typeof(MainFile),
                nameof(ModifyMaxEnergyPrefix)));

            if (Config.AddRelicToAllCharacters)
                PatchStartingRelics(harmony);
        }

        private static bool AfterAutoPrePlayPhaseEnteredLatePrefix(WhisperingEarring __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
        {
            __result = RunAutoPlay(__instance, choiceContext, player);
            return false;

            static async Task RunAutoPlay(WhisperingEarring relic, PlayerChoiceContext choiceContext, Player player)
            {
                if (player != relic.Owner) return;
                if (CombatManager.Instance.IsOverOrEnding) return;

                relic.Flash();
                ICombatState combatState = player.Creature.CombatState!;

                // Play all potions first
                if (Config.AutoUsePotions)
                {
                    foreach (PotionModel potion in player.Potions.Where(p => p.Usage != PotionUsage.Automatic).ToList())
                    {
                        if (CombatManager.Instance.IsOverOrEnding) break;
                        Creature? potionTarget = GetPotionTarget(potion, combatState, player);
                        await potion.OnUseWrapper(choiceContext, potionTarget);
                    }
                }

                // Play all cards
                int cardsPlayed = 0;
                using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
                {
                    while (true)
                    {
                        if (CombatManager.Instance.IsOverOrEnding) break;
                        if (CombatManager.Instance.IsPlayerReadyToEndTurn(player)) break;

                        CardPile pile = PileType.Hand.GetPile(relic.Owner);
                        CardModel? card = pile.Cards.FirstOrDefault(c => c.CanPlay());
                        if (card == null) break;

                        Creature? target = GetCardTarget(card, combatState, relic);
                        await card.SpendResources();
                        await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, skipXCapture: true);
                        cardsPlayed++;
                    }
                }

                LocString line = cardsPlayed >= 13
                    ? new LocString("relics", "WHISPERING_EARRING.warning")
                    : new LocString("relics", "WHISPERING_EARRING.approval");
                TalkCmd.Play(line, player.Creature, VfxColor.Purple);

                // End turn
                if (Config.AutoEndTurn)
                {
                    TryHidingEndTurnButton();

                    if (!CombatManager.Instance.IsOverOrEnding &&
                        !CombatManager.Instance.IsPlayerReadyToEndTurn(player))
                    {
                        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
                            new EndPlayerTurnAction(player, player.PlayerCombatState!.TurnNumber)
                        );
                    }
                }
            }
        }

        private static void TryHidingEndTurnButton()
        {
            try
            {
                NEndTurnButton? button = FindEndTurnButton(NCombatRoom.Instance!.Ui);

                if (button == null)
                {
                    Log("TryHidingEndTurnButton: Could not find NEndTurnButton.");
                    return;
                }

                button.Hide();

                Log($"TryHidingEndTurnButton: Successfully called Hide() on button at '{button.GetPath()}'.");
            }
            catch (Exception ex)
            {
                Log($"TryHidingEndTurnButton: Unexpected error: {ex}");
            }

            static NEndTurnButton? FindEndTurnButton(Node node)
            {
                if (node is NEndTurnButton button)
                    return button;

                foreach (Node child in node.GetChildren())
                {
                    NEndTurnButton? found = FindEndTurnButton(child);
                    if (found != null)
                        return found;
                }

                return null;
            }
        }

        // reuse of original GetTarget from WhisperingEarring class just to see it
        private static Creature? GetCardTarget(CardModel card, ICombatState combatState, WhisperingEarring relic)
        {
            Rng combatTargets = relic.Owner.RunState.Rng.CombatTargets;
            return card.TargetType switch
            {
                TargetType.AnyEnemy => combatState.HittableEnemies.FirstOrDefault(),
                TargetType.AnyAlly => combatTargets.NextItem(combatState.Allies.Where(c => c != null && c.IsAlive && c.IsPlayer && c != relic.Owner.Creature)),
                TargetType.AnyPlayer => relic.Owner.Creature,
                _ => null
            };
        }

        // TODO: What happens for non single target? What happens with osty potions? what with "self"? make fallback for all enums
        // imitation of original GetTarget from WhisperingEarring class for potion usage
        private static Creature? GetPotionTarget(PotionModel potion, ICombatState combatState, Player player)
        {
            Rng combatTargets = potion.Owner.RunState.Rng.CombatTargets;
            if (!potion.TargetType.IsSingleTarget()) return null;

            Creature? target = potion.TargetType switch
            {
                TargetType.AnyEnemy => player.Creature.CombatState!.HittableEnemies.FirstOrDefault(),
                TargetType.AnyAlly => combatTargets.NextItem(combatState.Allies.Where(c => c != null && c.IsAlive && c.IsPlayer && c != potion.Owner.Creature)),
                TargetType.AnyPlayer => player.Creature,
                _ => player.Creature
            };

            if (target != null && !target.CombatId.HasValue)
                target = player.Creature.CombatState!.HittableEnemies
                    .FirstOrDefault(c => c.CombatId.HasValue) ?? player.Creature;

            return target;
        }

        private static bool ModifyMaxEnergyPrefix(WhisperingEarring __instance, Player player, decimal amount, ref decimal __result)
        {
            __result = amount;
            if (player == __instance.Owner)
                __result += Config.BonusEnergy;

            return false;
        }

        private static void PatchStartingRelics(Harmony harmony)
        {
            HarmonyMethod relicsPostfix = new(typeof(MainFile), nameof(StartingRelicsPostfix));
            foreach (Type characterType in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .SelectMany(SafeGetTypes)
                .Where(t => !t.IsAbstract && typeof(CharacterModel).IsAssignableFrom(t)))
            {
                MethodInfo startingRelics = AccessTools.Method(characterType, "get_StartingRelics");
                if (startingRelics == null) continue;
                harmony.Patch(startingRelics, postfix: relicsPostfix);
                Log($"Patched StartingRelics for {characterType.Name}");
            }
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
            catch { return []; }
        }

        private static void StartingRelicsPostfix(ref IReadOnlyList<RelicModel> __result)
        {
            __result = new List<RelicModel>(__result) { ModelDb.Relic<WhisperingEarring>() }.AsReadOnly();
        }
    }
}