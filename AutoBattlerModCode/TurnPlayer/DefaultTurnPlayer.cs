using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Random;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public class DefaultTurnPlayer : ITurnPlayer
{
    public async Task<int> PlayTurn(PlayerChoiceContext choiceContext, Player player, ICombatState combatState, WhisperingEarring relic)
    {
        if (AutoBattlerMod.Config.AutoUsePotions)
        {
            foreach (PotionModel potion in player.Potions.Where(p => p.Usage != PotionUsage.Automatic).ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding) break;
                Creature? potionTarget = GetPotionTarget(potion, combatState, player);
                await potion.OnUseWrapper(choiceContext, potionTarget);
            }
        }

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

        if (AutoBattlerMod.Config.AutoEndTurn)
            TurnEnder.EndTurn(player);

        return cardsPlayed;
    }


    // TODO: What happens for non single target? What happens with osty potions? what with "self"? make fallback for all enums. what happens with trader potion?
    // imitation of original GetTarget from WhisperingEarring class for potion usage
    public static Creature? GetPotionTarget(PotionModel potion, ICombatState combatState, Player player)
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
            target = player.Creature.CombatState!.HittableEnemies.FirstOrDefault(c => c.CombatId.HasValue) ?? player.Creature;

        return target;
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
}
