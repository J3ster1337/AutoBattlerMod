using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace AutoBattlerMod.AutoBattlerModCode.Patches;

public static class StartingRelicsPatch
{
    public static void PatchStartingRelics(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(Player), "PopulateStartingRelics"),
            postfix: new HarmonyMethod(typeof(StartingRelicsPatch), nameof(PopulateStartingRelicsPostfix)));

        NetGameTypeTracker.PatchNetGameTypeCapture(harmony);
    }

    private static void PopulateStartingRelicsPostfix(Player __instance)
    {
        if (ShouldGrantRelic(__instance) == false || __instance.Relics.OfType<AutoBattlerItem>().Any()) { return; }

        RelicModel relic = ModelDb.Relic<AutoBattlerItem>().ToMutable();
        relic.FloorAddedToDeck = 1;
        __instance.AddRelicInternal(relic);

        AutoBattlerMod.Log($"Added {nameof(AutoBattlerItem)} to player {__instance.NetId}.");
    }

    private static bool ShouldGrantRelic(Player player)
    {
        var recipients = ModConfig.MultiplayerRecipientIds;

        AutoBattlerMod.Log(
            $"{nameof(ShouldGrantRelic)} check: player NetId={player.NetId}, " +
            $"captured NetGameType={NetGameTypeTracker.LastCapturedNetGameType}, " +
            $"configured {nameof(ModConfig.Steam64IdsMultiplayerFilter)}=[{string.Join(",", recipients)}], " +
            $"{nameof(ModConfig.AddRelicOnRunStart)}={ModConfig.AddRelicOnRunStart}");

        if (ModConfig.AddRelicOnRunStart == false)
            return false;

        return NetGameTypeTracker.LastCapturedNetGameType switch
        {
            not NetGameType.Singleplayer when recipients.Count == 0 => true,
            not NetGameType.Singleplayer => recipients.Contains(player.NetId),
            _ => true, // NetGameType is singleplayer or null
        };
    }

    public static class NetGameTypeTracker
    {
        public static NetGameType? LastCapturedNetGameType { get; private set { field = value; AutoBattlerMod.Log($"NetGameType captured: {value}"); } }

        public static void PatchNetGameTypeCapture(Harmony harmony)
        {
            harmony.Patch(AccessTools.Constructor(typeof(NetSingleplayerGameService), Type.EmptyTypes),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureSingleplayer)));

            harmony.Patch(AccessTools.Method(typeof(NetHostGameService), nameof(NetHostGameService.StartENetHost)),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureHostENet)));

            harmony.Patch(AccessTools.Method(typeof(NetHostGameService), nameof(NetHostGameService.StartSteamHost)),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureHostSteam)));

            harmony.Patch(AccessTools.Method(typeof(NetClientGameService), nameof(NetClientGameService.Initialize)),
                postfix: new HarmonyMethod(typeof(NetGameTypeTracker), nameof(CaptureClient)));
        }

        private static void CaptureSingleplayer(NetSingleplayerGameService __instance) => LastCapturedNetGameType = NetGameType.Singleplayer;
        private static void CaptureHostENet(NetHostGameService __instance) => LastCapturedNetGameType = NetGameType.Host;
        private static void CaptureHostSteam(NetHostGameService __instance) => LastCapturedNetGameType = NetGameType.Host;
        private static void CaptureClient(NetClientGameService __instance) => LastCapturedNetGameType = NetGameType.Client;
    }
}