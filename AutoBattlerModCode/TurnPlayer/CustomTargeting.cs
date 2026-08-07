using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public static class CustomTargeting
{
    public static Creature? GetPotionTarget(PotionModel potion, ICombatState combatState, Player player)
    {
        if (potion.TargetType.IsSingleTarget() == false) return null; // explosion ampule validly returns null here, cause it does not use target variable

        switch (potion.TargetType)
        {
            case TargetType.AnyEnemy:
                {
                    Creature? target = combatState.HittableEnemies.FirstOrDefault();
                    if (target != null && !target.CombatId.HasValue)
                        target = combatState.HittableEnemies.FirstOrDefault(c => c.CombatId.HasValue);
                    return target;
                }

            case TargetType.AnyAlly:
                return potion.Owner.RunState.Rng.CombatTargets
                    .NextItem(combatState.Allies.Where(c => c != null && c.IsAlive && c.IsPlayer && c != potion.Owner.Creature));

            case TargetType.AnyPlayer:
            case TargetType.Self:
                return player.Creature;

            case TargetType.TargetedNoCreature:
            default:
                return null;
        }
    }

    // Just a copy of original GetTarget from WhisperingEarring class
    public static Creature? GetCardTarget(CardModel card, ICombatState combatState, RelicModel relic)
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