using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

// Allows manually toggling on combat music

[HarmonyPatch(typeof(MapInstance), nameof(MapInstance.Handle_AudioSettings))]
static class MapInstance_Handle_AudioSettings
{
    private static void LogTranspilerFail(string details)
    {
        Logging.LogWarning($"Failed to patch MapInstance::Handle_AudioSettings - {details}!");
        Logging.LogWarning("This likely means that boss music replacements will fail to be applied correctly.");
        Logging.LogWarning("Please notify the mod creator about this!");
    }

    internal static bool ForceCombatMusic { get; set; }

    private static bool CheckCombatMusic(bool playActionMusic)
    {
        return playActionMusic || ForceCombatMusic;
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        // There should be only one load field for this._playActionMusic; wrap it with a custom method

        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._playActionMusic))))
            );

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._playActionMusic'");
            return matcher.InstructionEnumeration();
        }

        var fieldPos = matcher.Pos;

        // Replacements

        matcher.Start();
        matcher.Advance(fieldPos + 1);
        matcher.Insert(
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapInstance_Handle_AudioSettings), nameof(CheckCombatMusic)))
            );

        return matcher.InstructionEnumeration();
    }
}
