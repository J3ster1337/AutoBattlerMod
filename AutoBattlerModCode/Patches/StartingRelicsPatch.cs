using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;

namespace AutoBattlerMod.AutoBattlerModCode.Patches;

public static class StartingRelicsPatch
{
    public static void PatchStartingRelics(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(Player),
            "PopulateStartingRelics"),
            postfix: new HarmonyMethod(
            typeof(StartingRelicsPatch),
            nameof(PopulateStartingRelicsPostfix)));
    }

    private static async Task PopulateStartingRelicsPostfix(Player __instance)
    {
        if (!ShouldGrantRelic(__instance)) return;

        RelicModel relic = ModelDb.Relic<AutoBattlerItem>().ToMutable();
        relic.FloorAddedToDeck = 1;
        SaveManager.Instance.MarkRelicAsSeen(relic);
        __instance.AddRelicInternal(relic);
        AutoBattlerMod.Log($"Added {nameof(AutoBattlerItem)} to player {__instance.NetId}.");
    }

    private static bool ShouldGrantRelic(Player player)
    {
        var recipients = AutoBattlerMod.Config.GiveRelicOnlyToNetIds;

        AutoBattlerMod.Log(
            $"{nameof(ShouldGrantRelic)} check: player NetId={player.NetId}, " +
            $"configured {nameof(AutoBattlerMod.Config.GiveRelicOnlyToNetIds)}=[{string.Join(",", recipients)}]");

        // Empty list = treat as singleplayer/default behavior: everyone gets it
        if (recipients.Count == 0)
        {
            AutoBattlerMod.Log($"{nameof(AutoBattlerMod.Config.GiveRelicOnlyToNetIds)} is empty, falling back to {nameof(AutoBattlerMod.Config.AddRelicOnRunStart)}={AutoBattlerMod.Config.AddRelicOnRunStart}");
            return AutoBattlerMod.Config.AddRelicOnRunStart;
        }

        return recipients.Contains(player.NetId);
    }
}