using AutoBattlerMod.AutoBattlerModCode.Patches;
using AutoBattlerMod.AutoBattlerModCode.TurnPlayer;
using BaseLib.Config;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace AutoBattlerMod.AutoBattlerModCode;

[ModInitializer(nameof(Initialize))]
public partial class AutoBattlerMod : Node
{
    public const string ModId = "AutoBattlerMod";

    public static void Log(string message) => new MegaCrit.Sts2.Core.Logging.Logger(ModId, LogType.Generic).LogMessage(LogLevel.Info, message, 1);

    public static ITurnPlayer TurnPlayer { get; set; } = new DefaultTurnPlayer();

    public static void Initialize()
    {
        Harmony harmony = new(ModId);
        ModConfigRegistry.Register(ModId, new ModConfig());

        if (ModConfig.AddRelicOnRunStart)
            StartingRelicsPatch.PatchStartingRelics(harmony);

        StartingRelicsPatch.NetGameTypeTracker.PatchNetGameTypeCapture(harmony);
    }
}