using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using System.Reflection;
using System.Text.Json;

namespace AutoBattlerMod.AutoBattlerModCode
{
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "AutoBattlerMod";
        public const string ConfigFileName = "config.cfg";
        public static decimal BonusEnergy = 1m;
        public static bool AddRelicToAllCharacters = true;
        public static bool AutoEndTurn = true;
        public static bool AutoUsePotions = true;

        public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, LogType.Generic);
        private static void Log(string message) => Logger.LogMessage(LogLevel.Info, message, 1);

        public static void Initialize()
        {
            LoadConfig();

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

            if (AddRelicToAllCharacters)
                PatchStartingRelics(harmony);
        }

        private static bool AfterAutoPrePlayPhaseEnteredLatePrefix(
            WhisperingEarring __instance,
            PlayerChoiceContext choiceContext,
            Player player,
            ref Task __result)
        {
            __result = RunAutoPlay(__instance, choiceContext, player);
            return false;
        }

        private static async Task RunAutoPlay(
            WhisperingEarring relic,
            PlayerChoiceContext choiceContext,
            Player player)
        {
            if (player != relic.Owner) return;
            if (CombatManager.Instance.IsOverOrEnding) return;

            relic.Flash();
            ICombatState combatState = player.Creature.CombatState;

            // Play all potions first
            if (AutoUsePotions)
            {
                foreach (PotionModel potion in player.Potions.Where(p => p.Usage != PotionUsage.Automatic).ToList())
                {
                    if (CombatManager.Instance.IsOverOrEnding) break;
                    Creature potionTarget = GetPotionTarget(potion, combatState, player);
                    await potion.OnUseWrapper(choiceContext, potionTarget);
                }
            }

            // Play all cards
            using (CardSelectCmd.PushSelector(new VakuuCardSelector()))
            {
                int cardsPlayed = 0;
                while (true)
                {
                    if (CombatManager.Instance.IsOverOrEnding) break;
                    if (CombatManager.Instance.IsPlayerReadyToEndTurn(player)) break;

                    CardPile pile = PileType.Hand.GetPile(relic.Owner);
                    CardModel card = pile.Cards.FirstOrDefault(c => c.CanPlay());
                    if (card == null) break;

                    Creature target = GetCardTarget(card, combatState, relic);
                    await card.SpendResources();
                    await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, skipXCapture: true);
                    cardsPlayed++;
                }
            }

            // End turn
            if (AutoEndTurn)
            {
                if (!CombatManager.Instance.IsOverOrEnding &&
                    !CombatManager.Instance.IsPlayerReadyToEndTurn(player))
                {
                    PlayerCmd.EndTurn(player, canBackOut: false);
                }
            }
        }

        // reuse of original GetTarget from WhisperingEarring class just to see it
        private static Creature GetCardTarget(CardModel card, ICombatState combatState, WhisperingEarring relic)
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
        private static Creature GetPotionTarget(PotionModel potion, ICombatState combatState, Player player)
        {
            Rng combatTargets = potion.Owner.RunState.Rng.CombatTargets;
            if (!potion.TargetType.IsSingleTarget()) return null;

            Creature target = potion.TargetType switch
            {
                TargetType.AnyEnemy => player.Creature.CombatState.HittableEnemies.FirstOrDefault(),
                TargetType.AnyAlly => combatTargets.NextItem(combatState.Allies.Where(c => c != null && c.IsAlive && c.IsPlayer && c != potion.Owner.Creature)),
                TargetType.AnyPlayer => player.Creature,
                _ => player.Creature
            };

            if (target != null && !target.CombatId.HasValue)
                target = player.Creature.CombatState.HittableEnemies
                    .FirstOrDefault(c => c.CombatId.HasValue) ?? player.Creature;

            return target;
        }

        private static bool ModifyMaxEnergyPrefix(WhisperingEarring __instance, Player player, decimal amount, ref decimal __result)
        {
            __result = amount;
            if (player == __instance.Owner)
                __result += BonusEnergy;

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
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
            catch
            {
                return [];
            }
        }

        private static void StartingRelicsPostfix(ref IReadOnlyList<RelicModel> __result)
        {
            __result = new List<RelicModel>(__result) { ModelDb.Relic<WhisperingEarring>() }.AsReadOnly();
        }

        private static void LoadConfig()
        {
            try
            {
                string? modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(modDir))
                {
                    Log("Could not determine mod directory. Using default BonusEnergy = 1.");
                    BonusEnergy = 1m;
                    return;
                }

                string configPath = Path.Combine(modDir, ConfigFileName);

                if (!File.Exists(configPath))
                {
                    // Write a default config so the user knows the file exists and what to edit
                    var defaults = new { BonusEnergy = 1.0, AddRelicToAllCharacters = true, AutoEndTurn = true, AutoUsePotions = true };
                    File.WriteAllText(
                        configPath,
                        JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true })
                    );
                    BonusEnergy = 1m;
                    AddRelicToAllCharacters = true;
                    Log($"Created default config at {configPath} with BonusEnergy = 1 and AddRelicToAllCharacters = true.");
                    return;
                }

                string json = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("BonusEnergy", out JsonElement value))
                {
                    double raw = value.GetDouble();
                    if (raw < 0)
                    {
                        Log($"Invalid BonusEnergy value ({raw}), must be >= 0. Using default 1.");
                        BonusEnergy = 1m;
                    }
                    else
                    {
                        BonusEnergy = (decimal)raw;
                        Log($"Loaded BonusEnergy = {BonusEnergy} from config.");
                    }
                }
                else
                {
                    Log("Config missing BonusEnergy field. Using default 1.");
                    BonusEnergy = 1m;
                }

                if (doc.RootElement.TryGetProperty("AddRelicToAllCharacters", out JsonElement relicValue))
                {
                    AddRelicToAllCharacters = relicValue.GetBoolean();
                    Log($"Loaded AddRelicToAllCharacters = {AddRelicToAllCharacters} from config.");
                }
                else
                {
                    Log("Config missing AddRelicToAllCharacters field. Using default true.");
                    AddRelicToAllCharacters = true;
                }

                if (doc.RootElement.TryGetProperty("AutoEndTurn", out JsonElement endTurnValue))
                {
                    AutoEndTurn = endTurnValue.GetBoolean();
                    Log($"Loaded AutoEndTurn = {AutoEndTurn} from config.");
                }
                else
                {
                    Log("Config missing AutoEndTurn field. Using default true.");
                    AutoEndTurn = true;
                }

                if (doc.RootElement.TryGetProperty("AutoUsePotions", out JsonElement potionsValue))
                {
                    AutoUsePotions = potionsValue.GetBoolean();
                    Log($"Loaded AutoUsePotions = {AutoUsePotions} from config.");
                }
                else
                {
                    Log("Config missing AutoUsePotions field. Using default true.");
                    AutoUsePotions = true;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to load config: {ex.Message}. Using defaults.");
                BonusEnergy = 1m;
                AddRelicToAllCharacters = true;
                AutoEndTurn = true;
                AutoUsePotions = true;
            }
        }
    }
}