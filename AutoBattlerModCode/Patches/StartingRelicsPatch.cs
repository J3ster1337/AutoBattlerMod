using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using System.Reflection;

namespace AutoBattlerMod.AutoBattlerModCode.Patches;

public static class StartingRelicsPatch
{
    public static void PatchStartingRelics(Harmony harmony)
    {
        HarmonyMethod relicsPostfix = new(typeof(StartingRelicsPatch), nameof(StartingRelicsPostfix));
        foreach (Type characterType in AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(SafeGetTypes)
            .Where(t => !t.IsAbstract && typeof(CharacterModel).IsAssignableFrom(t)))
        {
            MethodInfo startingRelics = AccessTools.Method(characterType, "get_StartingRelics");
            if (startingRelics == null) continue;
            harmony.Patch(startingRelics, postfix: relicsPostfix);
            AutoBattlerMod.Log($"Patched StartingRelics for {characterType.Name}");
        }
    }

    public static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
        catch { return []; }
    }

    public static void StartingRelicsPostfix(ref IReadOnlyList<RelicModel> __result)
    {
        __result = new List<RelicModel>(__result) { ModelDb.Relic<WhisperingEarring>() }.AsReadOnly();
    }
}
