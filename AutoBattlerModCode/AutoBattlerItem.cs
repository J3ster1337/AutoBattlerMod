using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace AutoBattlerMod.AutoBattlerModCode;

[Pool(typeof(SharedRelicPool))]
public sealed class AutoBattlerItem : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override string PackedIconPath => "res://AutoBattlerMod/auto_battler_item.png";
    protected override string BigIconPath => PackedIconPath;
    protected override string PackedIconOutlinePath => PackedIconPath;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars => // can this be removed???
    [
        new EnergyVar((int)ModConfig.RelicGivesXBonusEnergy),
        new CardsVar((int)ModConfig.RelicGivesXBonusDraw)
    ];

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner) return amount;
        return amount + (decimal)ModConfig.RelicGivesXBonusEnergy;
    }

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != base.Owner) return count;
        return count + (int)ModConfig.RelicGivesXBonusDraw;
    }

    public override async Task AfterAutoPrePlayPhaseEnteredLate(
        PlayerChoiceContext choiceContext, Player player)
    {
        if (ModConfig.AutoPlayCards == false
           || player != Owner
           || CombatManager.Instance.IsOverOrEnding == true)
            return;

        await AutoBattlerMod.TurnPlayer.PlayTurn(choiceContext, player, player.Creature.CombatState!, this);
    }
}