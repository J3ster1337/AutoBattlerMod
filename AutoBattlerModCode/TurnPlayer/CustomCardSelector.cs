using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.TestSupport;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public class CustomCardSelector : ICardSelector // Just a copy of VakuuCardSelector. Can be used in combat context to force auto-selection of cards, for example when playing potions that require card selection
{
    public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
    {
        return Task.FromResult((IEnumerable<CardModel>)options.Take(maxSelect).ToList());
    }

    public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives)
    {
        return new CardRewardSelection
        {
            card = options.FirstOrDefault()?.Card
        };
    }
}