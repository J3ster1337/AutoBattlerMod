using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
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

    private static void PopulateStartingRelicsPostfix(Player __instance)
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
        var recipients = AutoBattlerMod.Config.GiveRelicOnlyToTheseSteam64Ids;

        AutoBattlerMod.Log(
            $"{nameof(ShouldGrantRelic)} check: player NetId={player.NetId}, " +
            $"captured NetGameType={NetGameTypeTracker.LastCapturedNetGameType}, " +
            $"configured {nameof(AutoBattlerMod.Config.GiveRelicOnlyToTheseSteam64Ids)}=[{string.Join(",", recipients)}]");

        if (NetGameTypeTracker.LastCapturedNetGameType == NetGameType.Singleplayer || recipients.Count == 0)
        {
            AutoBattlerMod.Log($"Singleplayer session detected, or {nameof(AutoBattlerMod.Config.GiveRelicOnlyToTheseSteam64Ids)} is empty, falling back to {nameof(AutoBattlerMod.Config.AddRelicOnRunStartByDefault)}={AutoBattlerMod.Config.AddRelicOnRunStartByDefault}");

            return AutoBattlerMod.Config.AddRelicOnRunStartByDefault;
        }

        bool result = recipients.Contains(player.NetId);
        AutoBattlerMod.Log($"Multiplayer session, player {player.NetId} {(result ? "IS" : "is NOT")} in recipient list, granting={result}");
        return result;
    }

    public static class NetGameTypeTracker
    {
        public static NetGameType? LastCapturedNetGameType;

        public static void PatchNetGameTypeCapture(Harmony harmony)
        {
            harmony.Patch(
                AccessTools.Constructor(typeof(NetSingleplayerGameService)),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureSingleplayer)));

            harmony.Patch(
                AccessTools.Method(typeof(NetHostGameService), nameof(NetHostGameService.StartENetHost)),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureHost)));

            harmony.Patch(
                AccessTools.Method(typeof(NetHostGameService), nameof(NetHostGameService.StartSteamHost)),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureHost)));

            harmony.Patch(
                AccessTools.Method(typeof(NetClientGameService), nameof(NetClientGameService.Initialize)),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureClient)));
        }

        private static void CaptureSingleplayer(NetSingleplayerGameService __instance)
        {
            LastCapturedNetGameType = NetGameType.Singleplayer;
            AutoBattlerMod.Log($"NetGameType captured: Singleplayer, NetId={__instance.NetId}"); // should be just 1
        }

        private static void CaptureHost(NetHostGameService __instance)
        {
            LastCapturedNetGameType = NetGameType.Host;
            AutoBattlerMod.Log($"NetGameType captured: Host, NetId={__instance.NetId}");
        }

        private static void CaptureClient(NetClientGameService __instance)
        {
            LastCapturedNetGameType = NetGameType.Client;
            AutoBattlerMod.Log($"NetGameType captured: Client, NetId={__instance.NetId}, HostNetId={__instance.HostNetId}");
        }
    }
}