using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AutoBattlerMod.AutoBattlerModCode
{
    [ModInitializer(nameof(Initialize))]
    public partial class MainFile : Node
    {
        public const string ModId = "AutoBattlerMod";
        private const string ConfigFileName = "config.cfg";
        private static decimal _bonusEnergy = 1m;
        private static bool _addRelicToAllCharacters = true;

        public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);
        private static void Log(string message) => Logger.LogMessage(LogLevel.Info, message, 1);

        public static void Initialize()
        {
            LoadConfig();

            Harmony harmony = new Harmony("EditWhisperingEarringTurnLimit");

            MethodInfo method = AccessTools.Method(
                typeof(WhisperingEarring),
                "AfterAutoPrePlayPhaseEnteredLate",
                new[] { typeof(PlayerChoiceContext), typeof(Player) });

            Type stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType;

            MethodInfo moveNext = AccessTools.Method(stateMachine, "MoveNext");

            harmony.Patch(moveNext, transpiler: new HarmonyMethod(typeof(MainFile), nameof(AutoPlayTranspiler)));

            MethodInfo modifyMaxEnergy = AccessTools.Method(typeof(WhisperingEarring), nameof(WhisperingEarring.ModifyMaxEnergy));

            harmony.Patch(modifyMaxEnergy, prefix: new HarmonyMethod(typeof(MainFile), nameof(ModifyMaxEnergyPrefix)));

            if (_addRelicToAllCharacters)
                PatchStartingRelics(harmony);
        }

        private static void PatchStartingRelics(Harmony harmony)
        {
            HarmonyMethod relicsPostfix = new HarmonyMethod(typeof(MainFile), nameof(StartingRelicsPostfix));

            foreach (Type characterType in AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && typeof(CharacterModel).IsAssignableFrom(t)))
            {
                MethodInfo startingRelics = AccessTools.Method(characterType, "get_StartingRelics");
                if (startingRelics == null) continue;

                harmony.Patch(startingRelics, postfix: relicsPostfix);
                Log($"Patched StartingRelics for {characterType.Name}");
            }
        }

        private static void StartingRelicsPostfix(ref IReadOnlyList<RelicModel> __result)
        {
            var list = new List<RelicModel>(__result)
            {
                ModelDb.Relic<WhisperingEarring>()
            };
            __result = list.AsReadOnly();
        }

        private static void LoadConfig()
        {
            try
            {
                string modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(modDir))
                {
                    Log("Could not determine mod directory. Using default BonusEnergy = 1.");
                    _bonusEnergy = 1m;
                    return;
                }

                string configPath = Path.Combine(modDir, ConfigFileName);

                if (!File.Exists(configPath))
                {
                    // Write a default config so the user knows the file exists and what to edit
                    var defaults = new { BonusEnergy = 1.0, AddRelicToAllCharacters = true };
                    File.WriteAllText(
                        configPath,
                        JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true })
                    );
                    _bonusEnergy = 1m;
                    _addRelicToAllCharacters = true;
                    Log($"Created default config at {configPath} with BonusEnergy = 1 and AddRelicToAllCharacters = true.");
                    return;
                }

                string json = File.ReadAllText(configPath);
                using JsonDocument doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("BonusEnergy", out JsonElement value))
                {
                    double raw = value.GetDouble();
                    if (raw < 0)
                    {
                        Log($"Invalid BonusEnergy value ({raw}), must be >= 0. Using default 1.");
                        _bonusEnergy = 1m;
                    }
                    else
                    {
                        _bonusEnergy = (decimal)raw;
                        Log($"Loaded BonusEnergy = {_bonusEnergy} from config.");
                    }
                }
                else
                {
                    Log("Config missing BonusEnergy field. Using default 1.");
                    _bonusEnergy = 1m;
                }

                if (doc.RootElement.TryGetProperty("AddRelicToAllCharacters", out JsonElement relicValue))
                {
                    _addRelicToAllCharacters = relicValue.GetBoolean();
                    Log($"Loaded AddRelicToAllCharacters = {_addRelicToAllCharacters} from config.");
                }
                else
                {
                    Log("Config missing AddRelicToAllCharacters field. Using default true.");
                    _addRelicToAllCharacters = true;
                }
            }
            catch (Exception ex)
            {
                Log($"Failed to load config: {ex.Message}. Using default BonusEnergy = 1.");
                _bonusEnergy = 1m;
            }
        }

        /// <summary>
        /// PATCH 1 — Turn guard:
        ///   [0024] callvirt  get_TurnNumber
        ///   [0025] ldc.i4.1
        ///   [0026] ble.s     Label(3)   ← continue to loop if TurnNumber &lt;= 1
        ///   [0027] leave     Label(4)   ← exit try-block if TurnNumber > 1
        ///
        ///   `leave` is required to exit a try/finally region — replacing it
        ///   with `br` produces invalid IL. Instead we keep `leave` but point
        ///   it at Label(3) (the continue target) so both branches go to the
        ///   loop. The ble.s at [0026] is then redundant but harmless.
        ///
        /// PATCH 2 — Loop limit:
        ///   [0174] ldarg.0          ← push `this` (state machine)
        ///   [0175] ldfld cardsPlayed ← push cardsPlayed  (net: 1 value on stack)
        ///   [0176] ldc.i4.s 13      ← push 13            (net: 2 values on stack)
        ///   [0177] blt Label(16)    ← pops both, branches if cardsPlayed &lt; 13
        ///
        ///   Changing blt→br leaves 2 values on stack = invalid IL.
        ///   Fix: nop [0175] ldfld and [0176] ldc.i4.s (nothing pushed),
        ///   then change blt→br (pops nothing, branches unconditionally).
        ///   But wait — ldarg.0 at [0174] is still there pushing `this`.
        ///   We must also nop [0174] ldarg.0, or add a pop before br.
        ///   Cleanest: nop all three [0174..0176] and change [0177] blt→br.
        ///   Net stack effect: 0 pushed, 0 popped — clean.
        /// </summary>
        private static IEnumerable<CodeInstruction> AutoPlayTranspiler(
            IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            bool turnGuardPatched = false;
            bool loopLimitPatched = false;

            for (int i = 0; i < list.Count; i++)
            {
                // ── Patch 1: redirect the leave so turn guard is bypassed ────
                // Pattern: callvirt get_TurnNumber, ldc.i4.1, ble/ble.s, leave/leave.s
                // We redirect the leave to the same label as the ble.s.
                if (!turnGuardPatched && i >= 2)
                {
                    bool prevIsTurnNumber =
                        list[i - 2].opcode == OpCodes.Callvirt &&
                        list[i - 2].operand is MethodInfo mi &&
                        mi.Name.Contains("TurnNumber");

                    bool prevIsLdc1 = list[i - 1].opcode == OpCodes.Ldc_I4_1;

                    bool currIsBle =
                        list[i].opcode == OpCodes.Ble_S ||
                        list[i].opcode == OpCodes.Ble;

                    if (prevIsTurnNumber && prevIsLdc1 && currIsBle)
                    {
                        if (i + 1 < list.Count &&
                            (list[i + 1].opcode == OpCodes.Leave ||
                             list[i + 1].opcode == OpCodes.Leave_S))
                        {
                            // Keep `leave` (required to exit the try region cleanly),
                            // but point it at Label(3) — the loop continue target —
                            // instead of Label(4) — the early return target.
                            object continueLabel = list[i].operand; // Label(3)
                            list[i + 1].operand = continueLabel;
                            // leave.s can only reach labels within ~128 bytes;
                            // upgrade to leave to be safe with the new target.
                            list[i + 1].opcode = OpCodes.Leave;

                            turnGuardPatched = true;
                        }
                    }
                }

                // ── Patch 2: make the loop back-jump unconditional ───────────
                // Pattern: ldarg.0, ldfld cardsPlayed, ldc.i4.s 13, blt Label(16)
                // We nop the three push instructions and change blt → br so
                // the stack stays balanced and the loop never exits here.
                if (!loopLimitPatched)
                {
                    bool isThirteen =
                        (list[i].opcode == OpCodes.Ldc_I4_S &&
                         list[i].operand is sbyte sb && sb == 13) ||
                        (list[i].opcode == OpCodes.Ldc_I4 &&
                         list[i].operand is int iv && iv == 13);

                    if (isThirteen &&
                        i + 1 < list.Count &&
                        (list[i + 1].opcode == OpCodes.Blt ||
                         list[i + 1].opcode == OpCodes.Blt_S))
                    {
                        // Nop the ldc.i4.s 13 — leaves cardsPlayed on stack from ldfld
                        list[i].opcode = OpCodes.Pop;  // pop cardsPlayed cleanly
                        list[i].operand = null;

                        // Change blt → br (unconditional, pops nothing)
                        object loopTopLabel = list[i + 1].operand;
                        list[i + 1].opcode = OpCodes.Br;
                        list[i + 1].operand = loopTopLabel;

                        loopLimitPatched = true;
                    }
                }

                if (turnGuardPatched && loopLimitPatched)
                    break;
            }

            return list;
        }

        private static bool ModifyMaxEnergyPrefix(
            WhisperingEarring __instance,
            Player player,
            decimal amount,
            ref decimal __result)
        {
            if (player != __instance.Owner)
            {
                __result = amount;
                return false;
            }

            __result = amount + _bonusEnergy;
            return false;
        }
    }
}