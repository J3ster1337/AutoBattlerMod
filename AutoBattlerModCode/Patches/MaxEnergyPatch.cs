using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Relics;

namespace AutoBattlerMod.AutoBattlerModCode.Patches;

public static class MaxEnergyPatch
{
    public static void PatchMaxEnergy(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(WhisperingEarring),
            nameof(WhisperingEarring.ModifyMaxEnergy)),
            prefix: new HarmonyMethod(typeof(MaxEnergyPatch),
            nameof(MaxEnergyPatch.ModifyMaxEnergyPrefix)));
    }

    public static bool ModifyMaxEnergyPrefix(WhisperingEarring __instance, Player player, decimal amount, ref decimal __result)
    {
        __result = amount;
        if (player == __instance.Owner)
            __result += AutoBattlerMod.Config.BonusEnergy;

        return false;
    }
}
