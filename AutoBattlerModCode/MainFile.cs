using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace AutoBattlerMod.AutoBattlerModCode
{
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "AutoBattlerMod";

        public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
            new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

        public static void Initialize()
        {
            Harmony harmony = new Harmony("EditWhisperingEarringTurnLimit");

            MethodInfo method = AccessTools.Method(
                typeof(WhisperingEarring),
                "AfterAutoPrePlayPhaseEnteredLate",
                new[] { typeof(PlayerChoiceContext), typeof(Player) });

            Type stateMachine =
                method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;

            MethodInfo moveNext = AccessTools.Method(stateMachine, "MoveNext");

            harmony.Patch(
                moveNext,
                transpiler: new HarmonyMethod(typeof(MainFile), nameof(Transpiler))
            );
        }

        /// <summary>
        /// Patches the MoveNext state machine to remove the TurnNumber > 1 guard.
        ///
        /// The original source has:
        ///     if (base.Owner.PlayerCombatState.TurnNumber > 1) return;
        ///
        /// In IL this compiles to roughly:
        ///     call  get_TurnNumber
        ///     ldc.i4.1          <-- the literal '1' we must NOT touch
        ///     bgt   [return]    <-- branch-if-greater: skips body when turn > 1
        ///
        /// We flip the branch opcode (bgt -> ble / bgt.s -> ble.s) so the guard
        /// reads "if TurnNumber <= 1 return", which is always false for turn >= 1,
        /// effectively disabling the restriction without touching any other
        /// Ldc_I4_1 in the method (the loop limit '13', the '== 0' check, etc.).
        /// </summary>
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();

            // Find the index of the get_TurnNumber call so we can locate
            // the branch that immediately follows the ldc.i4.1 after it.
            for (int i = 0; i < list.Count - 2; i++)
            {
                bool isTurnNumberCall =
                    list[i].opcode == OpCodes.Call &&
                    list[i].operand is MethodInfo mi &&
                    mi.Name.Contains("TurnNumber");

                if (!isTurnNumberCall)
                    continue;

                // Expect:  [i] call get_TurnNumber
                //          [i+1] ldc.i4.1
                //          [i+2] bgt / bgt.s  (branch when turn > 1 -> early return)
                if ((list[i + 1].opcode == OpCodes.Ldc_I4_1) &&
                    (list[i + 2].opcode == OpCodes.Bgt ||
                     list[i + 2].opcode == OpCodes.Bgt_S))
                {
                    // Flip the branch so the condition is never true:
                    //   bgt  -> ble   (branch when turn <= 1, i.e. turn 0 only — impossible in practice)
                    //   bgt.s -> ble.s
                    list[i + 2].opcode = list[i + 2].opcode == OpCodes.Bgt
                        ? OpCodes.Ble
                        : OpCodes.Ble_S;

                    break;
                }
            }

            return list;
        }
    }
}