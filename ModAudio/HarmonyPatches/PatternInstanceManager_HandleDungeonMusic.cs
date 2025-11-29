using HarmonyLib;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

// Why is this the only place that requires a specific patch for ATLYSS? Ugh
// Applies the following changes:
// - Makes the post-boss defeat music switch apply one time only, instead of continuously
// - Fixes a vanilla behaviour where the hard boss music would be set constantly, instead of just once

[HarmonyPatch(typeof(PatternInstanceManager), nameof(PatternInstanceManager.Client_HandleInstanceMusic))]
static class PatternInstanceManager_HandleDungeonMusic
{
    // Utils

    private static PatternInstanceManager? Manager { get; set; }

    private static bool HardBossMusicApplied { get; set; }
    private static bool MusicSwitched { get; set; }

    private static AudioClip? ManipulateClearMusic(AudioClip clip)
    {
        return MusicSwitched ? Manager?._muBossSrc.clip : clip;
    }

    private static void SetMusicSwitched()
    {
        Logging.LogInfo("Patch: Boss clear music set.");
        MusicSwitched = true;
    }

    private static void LogTranspilerFail(string details)
    {
        Logging.LogWarning($"Failed to patch PatternInstanceManager::HandleDungeonMusic - {details}!");
        Logging.LogWarning("This likely means that boss music replacements will fail to be applied correctly.");
        Logging.LogWarning("Please notify the mod creator about this!");
    }

    private static AudioClip? SetHardMusicOnce(AudioClip hardBossMusic)
    {
        if (HardBossMusicApplied)
        {
            return Manager?._muBossSrc.clip;
        }
        else
        {
            Logging.LogInfo("Patch: Hard boss music set.");
            HardBossMusicApplied = true;
            return hardBossMusic;
        }
    }

    // Harmony junk

    static void Prefix(PatternInstanceManager __instance)
    {
        if (!Manager)
        {
            Logging.LogInfo("Patch: Boss clear music and hard boss music cleared.");
            MusicSwitched = false;
            HardBossMusicApplied = false;
        }

        Manager = __instance;

        if (!__instance._muAmbienceSrc || !__instance._muActionSrc)
        {
            Logging.LogInfo("Patch: Boss clear music and hard boss music cleared.");
            MusicSwitched = false;
            HardBossMusicApplied = false;
        }
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(PatternInstanceManager), nameof(PatternInstanceManager._muBossSrc)))),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(PatternInstanceManager), nameof(PatternInstanceManager._hardBossTheme)))),
            new CodeMatch((ins) => ins.Calls(AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.clip))))
            );

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._muBossSrc.clip = this._hardBossTheme'");
            return matcher.InstructionEnumeration();
        }

        var hardMusicSetterPos = matcher.Pos;

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(PatternInstanceManager), nameof(PatternInstanceManager._muBossSrc)))),
            new CodeMatch((ins) => ins.Calls(AccessTools.PropertyGetter(typeof(AudioSource), nameof(AudioSource.clip)))),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(PatternInstanceManager), nameof(PatternInstanceManager._clearMusic)))),
            new CodeMatch((ins) => ins.Calls(AccessTools.Method(typeof(UnityEngine.Object), "op_Inequality"))),
            new CodeMatch(OpCodes.Brfalse)
            );

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'if (this._muBossSrc.clip != this._clearMusic)'");
            return matcher.InstructionEnumeration();
        }

        var checkerPos = matcher.Pos;

        matcher.MatchForward(false,
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(PatternInstanceManager), nameof(PatternInstanceManager._muBossSrc)))),
            new CodeMatch(OpCodes.Ldarg_0),
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(PatternInstanceManager), nameof(PatternInstanceManager._clearMusic)))),
            new CodeMatch((ins) => ins.Calls(AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.clip))))
            );

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._muBossSrc.clip = this._clearMusic'");
            return matcher.InstructionEnumeration();
        }

        var clearMusicSetterPos = matcher.Pos;

        // Do some edits - mind the order of insertions (need to insert from the "end" of of the method towards the "start")

        matcher.Start();
        matcher.Advance(clearMusicSetterPos);

        matcher.Insert(
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatternInstanceManager_HandleDungeonMusic), nameof(SetMusicSwitched)))
            );

        matcher.Start();
        matcher.Advance(checkerPos + 5);

        matcher.Insert(
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatternInstanceManager_HandleDungeonMusic), nameof(ManipulateClearMusic)))
            );

        matcher.Start();
        matcher.Advance(hardMusicSetterPos + 4);

        matcher.Insert(
            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PatternInstanceManager_HandleDungeonMusic), nameof(SetHardMusicOnce)))
            );

        return matcher.InstructionEnumeration();
    }
}
