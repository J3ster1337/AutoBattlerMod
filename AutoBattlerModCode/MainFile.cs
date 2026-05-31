using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace AutoBattlerMod.AutoBattlerModCode
{
    //You're recommended but not required to keep all your code in this package and all your assets in the AutoBattlerMod folder.
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "AutoBattlerMod"; //At the moment, this is used only for the Logger and harmony names.
        private const int ReplacementTurnLimit = 999;

        public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

        public static void Initialize()
        {
            Harmony harmony = new Harmony("EditWhisperingEarringTurnLimit");

            System.Reflection.MethodInfo method = AccessTools.Method(
                typeof(WhisperingEarring),
                "AfterAutoPrePlayPhaseEnteredLate",
                new[]
                {
                typeof(PlayerChoiceContext),
                typeof(Player)
                });

            Type stateMachine =
                method.GetCustomAttribute<AsyncStateMachineAttribute>()
                      ?.StateMachineType;

            System.Reflection.MethodInfo moveNext =
                AccessTools.Method(stateMachine, "MoveNext");

            harmony.Patch(
                moveNext,
                transpiler: new HarmonyMethod(
                    typeof(MainFile),
                    nameof(Transpiler))
            );
        }

        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_I4_1)
                {
                    yield return new CodeInstruction(
                        OpCodes.Ldc_I4,
                        ReplacementTurnLimit);
                }
                else
                {
                    yield return instruction;
                }
            }
        }
    }
}
