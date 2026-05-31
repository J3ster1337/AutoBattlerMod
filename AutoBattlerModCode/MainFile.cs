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

        // ── Configurable constants ──────────────────────────────────────────
        /// <summary>
        /// How much extra energy WhisperingEarring grants the player.
        /// The base game value is 1. Increase to give more energy per turn.
        /// </summary>
        private const decimal BonusEnergy = 3m;
        // ───────────────────────────────────────────────────────────────────

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
                transpiler: new HarmonyMethod(typeof(MainFile), nameof(AutoPlayTranspiler))
            );

            // Patch ModifyMaxEnergy to apply our configurable BonusEnergy
            MethodInfo modifyMaxEnergy = AccessTools.Method(
                typeof(WhisperingEarring),
                nameof(WhisperingEarring.ModifyMaxEnergy));

            harmony.Patch(
                modifyMaxEnergy,
                prefix: new HarmonyMethod(typeof(MainFile), nameof(ModifyMaxEnergyPrefix))
            );
        }

        /// <summary>
        /// Transpiler for the AfterAutoPrePlayPhaseEnteredLate state machine.
        ///
        /// Applies two patches:
        ///
        /// 1. TURN GUARD — flips the bgt after get_TurnNumber so the
        ///    "only run on turn 1" restriction is disabled.
        ///
        ///    Original IL:
        ///        call  get_TurnNumber
        ///        ldc.i4.1
        ///        bgt   [early return]   // if turn > 1, bail
        ///
        ///    Patched:
        ///        call  get_TurnNumber
        ///        ldc.i4.1
        ///        ble   [early return]   // if turn <= 1 bail — never true for turn >= 1
        ///
        /// 2. LOOP LIMIT — removes the cardsPlayed < 13 upper-bound so the
        ///    loop runs until one of the natural exit conditions fires
        ///    (combat over, ready to end turn, or no playable card in hand).
        ///
        ///    Original IL (end of for-loop):
        ///        ldc.i4  13         // (or ldc.i4.s 13)
        ///        blt / blt.s        // loop back if cardsPlayed < 13
        ///
        ///    Patched: both instructions are replaced with nop so the branch
        ///    is never evaluated and the loop continues unconditionally.
        /// </summary>
        private static IEnumerable<CodeInstruction> AutoPlayTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            bool turnGuardPatched = false;
            bool loopLimitPatched = false;

            for (int i = 0; i < list.Count - 2; i++)
            {
                // ── Patch 1: turn guard ──────────────────────────────────
                if (!turnGuardPatched)
                {
                    bool isTurnNumberCall =
                        list[i].opcode == OpCodes.Call &&
                        list[i].operand is MethodInfo mi &&
                        mi.Name.Contains("TurnNumber");

                    if (isTurnNumberCall &&
                        list[i + 1].opcode == OpCodes.Ldc_I4_1 &&
                        (list[i + 2].opcode == OpCodes.Bgt ||
                         list[i + 2].opcode == OpCodes.Bgt_S))
                    {
                        list[i + 2].opcode = list[i + 2].opcode == OpCodes.Bgt
                            ? OpCodes.Ble
                            : OpCodes.Ble_S;

                        turnGuardPatched = true;
                        i += 2; // skip the two instructions we just examined
                        continue;
                    }
                }

                // ── Patch 2: loop limit (cardsPlayed < 13) ───────────────
                if (!loopLimitPatched)
                {
                    bool isThirteen =
                        (list[i].opcode == OpCodes.Ldc_I4_S &&
                         list[i].operand is sbyte sb && sb == 13) ||
                        (list[i].opcode == OpCodes.Ldc_I4 &&
                         list[i].operand is int iv && iv == 13);

                    if (isThirteen &&
                        (list[i + 1].opcode == OpCodes.Blt ||
                         list[i + 1].opcode == OpCodes.Blt_S))
                    {
                        // Nop both instructions so the loop never terminates
                        // here — only the three guard checks inside it can stop it.
                        list[i].opcode = OpCodes.Nop;
                        list[i].operand = null;
                        list[i + 1].opcode = OpCodes.Nop;
                        list[i + 1].operand = null;

                        loopLimitPatched = true;
                        i += 1;
                        continue;
                    }
                }

                if (turnGuardPatched && loopLimitPatched)
                    break;
            }

            return list;
        }

        /// <summary>
        /// Prefix for WhisperingEarring.ModifyMaxEnergy.
        /// Overrides the return value entirely so we control the bonus energy
        /// via BonusEnergy without touching the EnergyVar/DynamicVars system.
        ///
        /// Returning false from a prefix skips the original method; Harmony
        /// writes the value we assign to __result into the caller.
        /// </summary>
        private static bool ModifyMaxEnergyPrefix(
            WhisperingEarring __instance,
            Player player,
            decimal amount,
            ref decimal __result)
        {
            // Replicate the original ownership guard
            if (player != __instance.Owner)
            {
                __result = amount;
                return false; // skip original
            }

            __result = amount + BonusEnergy;
            return false; // skip original
        }
    }
}