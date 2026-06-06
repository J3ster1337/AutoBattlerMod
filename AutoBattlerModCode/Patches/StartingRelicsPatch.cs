using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace AutoBattlerMod.AutoBattlerModCode.Patches;

public static class StartingRelicsPatch
{
    public static void PatchStartingRelics(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(RunManager),
                nameof(RunManager.Launch)),
                postfix: new HarmonyMethod(
                typeof(StartingRelicsPatch),
                nameof(LaunchPostfix)));
    }

    private static async void LaunchPostfix(RunManager __instance)
    {
        if (__instance.State == null) return;

        foreach (Player player in __instance.State.Players)
        {
            if (player.Relics.OfType<WhisperingEarring>().Any())
            {
                AutoBattlerMod.Log($"Player {player.NetId} already has WhisperingEarring, skipping.");
                continue;
            }

            RelicModel relic = ModelDb.Relic<WhisperingEarring>().ToMutable();
            relic.FloorAddedToDeck = 1;
            SaveManager.Instance.MarkRelicAsSeen(relic);
            player.AddRelicInternal(relic);
            await relic.AfterObtained();
            AutoBattlerMod.Log($"Added WhisperingEarring to player {player.NetId}.");
        }
    }
}