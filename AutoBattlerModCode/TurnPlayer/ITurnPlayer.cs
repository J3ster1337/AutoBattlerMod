using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public interface ITurnPlayer
{
    Task<int> PlayTurn(PlayerChoiceContext choiceContext, Player player, ICombatState combatState, WhisperingEarring relic);
}
