using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;
using System.Reflection;

namespace AutoBattlerMod.AutoBattlerModCode
{
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "AutoBattlerMod";

        public static void Initialize()
        {
            Harmony harmony = new(ModId);

            MethodInfo method = AccessTools.Method(
                typeof(WhisperingEarring),
                "AfterAutoPrePlayPhaseEnteredLate",
                new[] { typeof(PlayerChoiceContext), typeof(Player) });

            harmony.Patch(method,
                prefix: new HarmonyMethod(typeof(MainFile), nameof(AfterAutoPrePlayPhaseEnteredLatePrefix)));
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

            // Play all potions first
            foreach (PotionModel potion in player.Potions.Where(p => p.Usage != PotionUsage.Automatic).ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding) break;
                Creature potionTarget = GetPotionTarget(potion, player);
                await potion.OnUseWrapper(choiceContext, potionTarget);
            }

            // Play all cards
            relic.Flash();
            ICombatState combatState = player.Creature.CombatState;

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

                if (cardsPlayed == 0) return;
            }

            // End turn
            if (!CombatManager.Instance.IsOverOrEnding &&
                !CombatManager.Instance.IsPlayerReadyToEndTurn(player))
            {
                PlayerCmd.EndTurn(player, canBackOut: false);
            }
        }

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

        private static Creature GetPotionTarget(PotionModel potion, Player player)
        {
            if (!potion.TargetType.IsSingleTarget()) return null;

            Creature target = potion.TargetType switch
            {
                TargetType.AnyEnemy => player.Creature.CombatState.HittableEnemies.FirstOrDefault(),
                TargetType.AnyAlly => player.Creature,
                TargetType.AnyPlayer => player.Creature,
                _ => player.Creature
            };

            if (target != null && !target.CombatId.HasValue)
                target = player.Creature.CombatState.HittableEnemies
                    .FirstOrDefault(c => c.CombatId.HasValue) ?? player.Creature;

            return target;
        }
    }
}