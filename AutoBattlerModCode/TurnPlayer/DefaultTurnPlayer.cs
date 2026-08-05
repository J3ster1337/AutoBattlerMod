using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public class DefaultTurnPlayer : ITurnPlayer
{
    public async Task PlayTurn(PlayerChoiceContext choiceContext, Player player, ICombatState combatState, RelicModel relic)
    {
        if (AutoBattlerMod.Config.AutoUsePotionsOnTurnStart)
        {
            foreach (PotionModel potion in player.Potions.Where(p => p.Usage != PotionUsage.Automatic).ToList())
            {
                if (CombatManager.Instance.IsOverOrEnding) break;
                Creature? potionTarget = CustomTargeting.GetPotionTarget(potion, combatState, player);
                await potion.OnUseWrapper(choiceContext, potionTarget);
            }
        }

        using (CardSelectCmd.PushSelector(new CustomCardSelector()))
        {
            while (true)
            {
                if (CombatManager.Instance.IsOverOrEnding) break;
                if (CombatManager.Instance.IsPlayerReadyToEndTurn(player)) break;

                CardPile pile = PileType.Hand.GetPile(relic.Owner);
                CardModel? card = pile.Cards.FirstOrDefault(c => c.CanPlay());
                if (card == null) break;

                Creature? target = CustomTargeting.GetCardTarget(card, combatState, relic);
                await card.SpendResources();
                await CardCmd.AutoPlay(choiceContext, card, target, AutoPlayType.Default, skipXCapture: true);
            }
        }

        if (AutoBattlerMod.Config.AutoEndTurnWhenNoPlayableCardsLeft)
            TurnEnder.EndTurn(player);
    }
}
