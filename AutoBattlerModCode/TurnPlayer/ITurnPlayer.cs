using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public interface ITurnPlayer
{
    Task PlayTurn(PlayerChoiceContext choiceContext, Player player, ICombatState combatState, RelicModel relic);
}
