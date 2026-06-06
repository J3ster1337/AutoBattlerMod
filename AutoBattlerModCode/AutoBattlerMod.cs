using AutoBattlerMod.AutoBattlerModCode.Patches;
using AutoBattlerMod.AutoBattlerModCode.TurnPlayer;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace AutoBattlerMod.AutoBattlerModCode;

[ModInitializer(nameof(Initialize))]
public partial class AutoBattlerMod : Node
{
    public const string ModId = "AutoBattlerMod";
    public static ModConfig Config = ModConfig.Load();

    public static void Log(string message) => new MegaCrit.Sts2.Core.Logging.Logger(ModId, LogType.Generic).LogMessage(LogLevel.Info, message, 1);

    public static ITurnPlayer TurnPlayer { get; set; } = new DefaultTurnPlayer();

    public static void Initialize()
    {
        Harmony harmony = new(ModId);

        MaxEnergyPatch.PatchMaxEnergy(harmony);

        harmony.Patch(
            AccessTools.Method(typeof(WhisperingEarring),
            "AfterAutoPrePlayPhaseEnteredLate",
            [typeof(PlayerChoiceContext), typeof(Player)]),
            prefix: new HarmonyMethod(typeof(AutoBattlerMod),
            nameof(AfterAutoPrePlayPhaseEnteredLatePrefix)));

        if (Config.AddRelicToAllCharacters)
            StartingRelicsPatch.PatchStartingRelics(harmony);
    }

    private static bool AfterAutoPrePlayPhaseEnteredLatePrefix(WhisperingEarring __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
    {
        __result = RunAutoPlay(__instance, choiceContext, player);
        return false;

        static async Task RunAutoPlay(WhisperingEarring relic, PlayerChoiceContext choiceContext, Player player)
        {
            if (player != relic.Owner) return;
            if (CombatManager.Instance.IsOverOrEnding) return;

            relic.Flash();

            int cardsPlayed = await AutoBattlerMod.TurnPlayer.PlayTurn(choiceContext, player, player.Creature.CombatState!, relic);

            LocString line = cardsPlayed >= 13
                ? new LocString("relics", "WHISPERING_EARRING.warning")
                : new LocString("relics", "WHISPERING_EARRING.approval");
            TalkCmd.Play(line, player.Creature, VfxColor.Purple);
        }
    }
}