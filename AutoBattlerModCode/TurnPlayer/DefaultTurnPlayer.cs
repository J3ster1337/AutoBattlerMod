using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Potions;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public class DefaultTurnPlayer : ITurnPlayer
{
    public async Task PlayTurn(PlayerChoiceContext choiceContext, Player player, ICombatState combatState, RelicModel relic)
    {
        if (ModConfig.PotionsAutoPlay)
        {
            int potionsUsed = 0;

            using (CardSelectCmd.PushSelector(new CustomCardSelector()))
            {
                while (true)
                {
                    if (CombatManager.Instance.IsOverOrEnding) break;

                    PotionModel? potion = player.Potions.FirstOrDefault(p => p.Usage != PotionUsage.Automatic && p is not FoulPotion && p is not Ashwater);
                    if (potion == null) break;

                    Creature? potionTarget = CustomTargeting.GetPotionTarget(potion, combatState, player);
                    await potion.OnUseWrapper(choiceContext, potionTarget);

                    potionsUsed++;
                    if (ModConfig.MaxPotionsPerTurn != 0 && potionsUsed >= ModConfig.MaxPotionsPerTurn) break;
                }
            }
        }

        if (ModConfig.CardsAutoPlay)
        {
            using (CardSelectCmd.PushSelector(new CustomCardSelector()))
            {
                while (true)
                {
                    if (CombatManager.Instance.IsOverOrEnding || CombatManager.Instance.IsPlayerReadyToEndTurn(player)) break;

                    CardPile hand = PileType.Hand.GetPile(relic.Owner);
                    CardModel? card = hand.Cards.FirstOrDefault(c => c.CanPlay());
                    if (card == null) break;

                    Creature? target = CustomTargeting.GetCardTarget(card, combatState, relic);
                    await card.SpendResources();
                    await CardCmd.AutoPlay(choiceContext, card, target, skipXCapture: true);
                }
            }

            if (ModConfig.AutoEndTurnWhenNoPlayableCardsLeft)
                TurnEnder.EndTurn(player);
        }
    }
}
