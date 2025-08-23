using HarmonyLib;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

// Allows manually toggling on combat music
// Also change some lines to use interpolation for day / night volume instead of jumping to 0.75f

[HarmonyPatch(typeof(MapInstance), nameof(MapInstance.Handle_AudioSettings))]
static class MapInstance_Handle_AudioSettings
{
    private static void LogTranspilerFail(string details)
    {
        Logging.LogWarning($"Failed to patch MapInstance::Handle_AudioSettings - {details}!");
        Logging.LogWarning("This likely means that boss music replacements and/or audio QoL for map instances will fail to be applied correctly.");
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

        var fieldPos = -1;

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._playActionMusic'");
        }
        else
        {
            fieldPos = matcher.Pos;
        }

        matcher.Start();
        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._daytimeMusic)))),
            new CodeMatch((ins) => ins.LoadsConstant(0.75f)),
            new CodeMatch((ins) => ins.Calls(AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.volume))))
            );

        var dayTimeLerpPos = -1;

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._daytimeMusic.volume = 0.75f'");
        }
        else
        {
            dayTimeLerpPos = matcher.Pos;
        }

        matcher.Start();
        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._nightMusic)))),
            new CodeMatch((ins) => ins.LoadsConstant(0.75f)),
            new CodeMatch((ins) => ins.Calls(AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.volume))))
            );

        var nightTimeLerpPos = -1;

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._nightMusic.volume = 0.75f'");
        }
        else
        {
            nightTimeLerpPos = matcher.Pos;
        }

        // Replacements - from end to start unless you want to fuck up your positions

        if (nightTimeLerpPos != -1)
        {
            matcher.Start();
            matcher.Advance(nightTimeLerpPos + 1);
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
            matcher.Advance(1);
            matcher.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapInstance_Handle_AudioSettings), nameof(LerpVolume))));
        }

        if (dayTimeLerpPos != -1)
        {
            matcher.Start();
            matcher.Advance(dayTimeLerpPos + 1);
            matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
            matcher.Advance(1);
            matcher.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapInstance_Handle_AudioSettings), nameof(LerpVolume))));
        }

        if (fieldPos != -1)
        {
            matcher.Start();
            matcher.Advance(fieldPos + 1);
            matcher.Insert(
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapInstance_Handle_AudioSettings), nameof(CheckCombatMusic)))
                );
        }

        return matcher.InstructionEnumeration();
    }

    private static float LerpVolume(AudioSource source, float targetVolume)
    {
        return Mathf.Lerp(source.volume, targetVolume, 1.5f * Time.deltaTime);
    }

    // Handle null music correctly
    static void Postfix(MapInstance __instance)
    {
        if (__instance._nullMusic)
        {
            if (!(__instance._actionMusic && CheckCombatMusic(__instance._playActionMusic)))
            {
                __instance._nullMusic.volume = Mathf.Lerp(__instance._nullMusic.volume, 0.75f, 1.5f * Time.deltaTime);
            }
            else
            {
                __instance._nullMusic.volume = Mathf.Lerp(__instance._nullMusic.volume, 0f, 1.5f * Time.deltaTime);
            }
        }
    }
}
