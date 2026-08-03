using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Random;

namespace AutoBattlerMod.AutoBattlerModCode.TurnPlayer;

public static class CustomTargeting
{
    // TODO: What happens for non single target? What happens with osty potions? what with "self"? make fallback for all enums. what happens with trader potion? make a separate check to not use trader potion
    // Imitation of original GetTarget from WhisperingEarring class for potion usage
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

    // for now, just a copy of original GetTarget from WhisperingEarring class
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