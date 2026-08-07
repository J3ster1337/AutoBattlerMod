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
        if (__instance.Relics.OfType<AutoBattlerItem>().Any())
        {
            AutoBattlerMod.Log($"Player {__instance.NetId} already has {nameof(AutoBattlerItem)}, skipping.");
            return;
        }

        RelicModel relic = ModelDb.Relic<AutoBattlerItem>().ToMutable();
        relic.FloorAddedToDeck = 1;
        SaveManager.Instance.MarkRelicAsSeen(relic);
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

        if (ModConfig.AddRelicOnRunStart == false) // disabled
        {
            AutoBattlerMod.Log("Starting relic disabled globally, not adding");
            return false;
        }
        else // enabled
        {
            if (NetGameTypeTracker.LastCapturedNetGameType == NetGameType.Singleplayer) // singleplayer
            {
                AutoBattlerMod.Log("Singleplayer session, adding");
                return true;
            }
            else // multiplayer
            {
                if (recipients.Count == 0) // no filter
                {
                    AutoBattlerMod.Log("Multiplayer session with empty recipient list, granting=true for everyone");
                    return true;
                }
                else // filter
                {
                    bool result = recipients.Contains(player.NetId);
                    AutoBattlerMod.Log($"Multiplayer session, player {player.NetId} {(result ? "IS" : "is NOT")} in recipient list, granting={result}");
                    return result;
                }
            }
        }
    }

    public static class NetGameTypeTracker
    {
        public static NetGameType? LastCapturedNetGameType;

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

        private static void CaptureSingleplayer(NetSingleplayerGameService __instance) =>
            Capture(NetGameType.Singleplayer, __instance.NetId, "Singleplayer"); // should be just 1

        private static void CaptureHostENet(NetHostGameService __instance) =>
            Capture(NetGameType.Host, __instance.NetId, "Host (ENet)"); // should be just 1

        private static void CaptureHostSteam(NetHostGameService __instance) =>
            Capture(NetGameType.Host, __instance.NetId, "Host (Steam)");

        private static void CaptureClient(NetClientGameService __instance) =>
            Capture(NetGameType.Client, __instance.NetId, $"Client, HostNetId={__instance.HostNetId}");

        private static void Capture(NetGameType type, ulong netId, string label)
        {
            LastCapturedNetGameType = type;
            AutoBattlerMod.Log($"NetGameType captured: {label}, NetId={netId}");
        }
    }
}