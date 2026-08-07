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
    public override string PackedIconPath => "res://AutoBattlerMod/images/relics/auto_battler_item.png";
    protected override string PackedIconOutlinePath => "res://AutoBattlerMod/images/relics/auto_battler_item_outline.png";
    protected override string BigIconPath => "res://AutoBattlerMod/images/relics/auto_battler_item_large.png";

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.ForEnergy(this)
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new EnergyVar((int)ModConfig.RelicGivesXBonusEnergy)
    ];

    public override decimal ModifyMaxEnergy(Player player, decimal amount)
    {
        if (player != Owner) return amount;
        return amount + (decimal)ModConfig.RelicGivesXBonusEnergy;
    }

    public override async Task AfterAutoPrePlayPhaseEnteredLate(
        PlayerChoiceContext choiceContext, Player player)
    {
        if (ModConfig.AutoPlayCards == false
           || player != Owner
           || CombatManager.Instance.IsOverOrEnding == true)
            return;

        Flash();
        await AutoBattlerMod.TurnPlayer.PlayTurn(choiceContext, player, player.Creature.CombatState!, this);
    }
}