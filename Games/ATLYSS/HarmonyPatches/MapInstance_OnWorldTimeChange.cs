using HarmonyLib;

namespace Marioalexsan.ModAudio.Atlyss.HarmonyPatches;

[HarmonyPatch(typeof(MapInstance), nameof(MapInstance.OnWorldTimeChange))]
static class MapInstance_OnWorldTimeChange
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);
        
        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._nullMusic)))),
            new CodeMatch((ins) => ins.Calls(AccessTools.Method(typeof(UnityEngine.Object), "op_Implicit")))
        );
        
        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find '(bool)this._nullMusic)'");
        }
        else
        {
            matcher.Advance(1);
            matcher.SetInstruction(CodeInstruction.Call(typeof(MapInstance_Handle_AudioSettings), nameof(MapInstance_Handle_AudioSettings.NullMusicSourceIsSet)));
        }
            
        matcher.Advance(1);

        return matcher.InstructionEnumeration();
    }
    
    private static void LogTranspilerFail(string details)
    {
        Logging.LogWarning($"Failed to patch MapInstance::OnWorldTimeChange - {details}!");
        Logging.LogWarning("This likely means that null music replacements will fail to be applied correctly.");
        Logging.LogWarning("Please notify the mod creator about this!");
    }
}