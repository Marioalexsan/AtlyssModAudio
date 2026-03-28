using HarmonyLib;
using System.Reflection.Emit;
using UnityEngine;

namespace Marioalexsan.ModAudio.Atlyss.HarmonyPatches;

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

    // ReSharper disable once UnusedMember.Local
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code);

        // There should be only one load field for this._playActionMusic; wrap it with a custom method

        var patchOrder = new List<(int Position, Action Patch)>();

        int playActionFound = 0;
        
        matcher.Start();
        while (true)
        {
            matcher.MatchForward(false,
                new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._playActionMusic))))
            );

            if (matcher.IsInvalid)
            {
                if (playActionFound != 2)
                    LogTranspilerFail("couldn't find 'this._playActionMusic'");
                break;
            }
            else
            {
                playActionFound++;
                patchOrder.Add((matcher.Pos, Patch));
                void Patch()
                {
                    matcher.Advance(1);
                    matcher.Insert(
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapInstance_Handle_AudioSettings), nameof(CheckCombatMusic)))
                    );
                }
            }
            
            matcher.Advance(1);
        }
            
        matcher.Start();
        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._daytimeMusic)))),
            new CodeMatch((ins) => ins.LoadsConstant(0.75f)),
            new CodeMatch((ins) => ins.Calls(AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.volume))))
            );

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._daytimeMusic.volume = 0.75f'");
        }
        else
        {
            patchOrder.Add((matcher.Pos, Patch));
            void Patch()
            {
                matcher.Advance(1);
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
                matcher.Advance(1);
                matcher.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapInstance_Handle_AudioSettings), nameof(LerpVolume))));
            }
        }

        int nullMusicFound = 0;
        int skipsDoneOnAdvancedNullChecks = 0;
        
        matcher.Start();
        while (true)
        {
            matcher.MatchForward(false,
                new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._nullMusic)))),
                new CodeMatch((ins) => ins.Calls(AccessTools.Method(typeof(UnityEngine.Object), "op_Implicit")))
            );
        
            if (matcher.IsInvalid)
            {
                if (nullMusicFound != 5)
                    LogTranspilerFail("couldn't find '(bool)this._nullMusic)'");
                break;
            }
            else
            {
                nullMusicFound++;
                patchOrder.Add((matcher.Pos, Patch));
                void Patch()
                {
                    // TODO: This sucks ass
                    // Play() would normally be at offset 8, but another patch in this transpiler is replacing it with CheckNullMusicAndPlay()
                    // I don't care enough to match it correctly, I just know there should be a CheckNullMusicAndPlay() call to match
                    // And that I don't want to do the advanced null check in that case
                    if (matcher.Pos - 3 >= 0 && matcher.Pos + 4 < matcher.Length
                        && matcher.InstructionAt(4).LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._musicStarted)))
                        && matcher.InstructionAt(-3).LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._timeBeforeMusicStart)))
                       )
                    {
                        skipsDoneOnAdvancedNullChecks++;
                        return;
                    }
                    
                    matcher.Advance(1);
                    matcher.SetInstruction(CodeInstruction.Call(typeof(MapInstance_Handle_AudioSettings), nameof(NullMusicSourceIsSet)));
                }
            }
            
            matcher.Advance(1);
        }

        matcher.Start();
        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._nightMusic)))),
            new CodeMatch((ins) => ins.LoadsConstant(0.75f)),
            new CodeMatch((ins) => ins.Calls(AccessTools.PropertySetter(typeof(AudioSource), nameof(AudioSource.volume))))
            );

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._nightMusic.volume = 0.75f'");
        }
        else
        {
            patchOrder.Add((matcher.Pos, Patch));
            void Patch()
            {
                matcher.Advance(1);
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Dup));
                matcher.Advance(1);
                matcher.Insert(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MapInstance_Handle_AudioSettings), nameof(LerpVolume))));
            }
        }
        
        matcher.Start();
        matcher.MatchForward(false,
            new CodeMatch((ins) => ins.LoadsField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._nullMusic)))),
            new CodeMatch((ins) => ins.Calls(AccessTools.Method(typeof(AudioSource), nameof(AudioSource.Play), []))),
            new CodeMatch((ins) => ins.IsLdarg(0)),
            new CodeMatch((ins) => ins.LoadsConstant(1)),
            new CodeMatch((ins) => ins.StoresField(AccessTools.Field(typeof(MapInstance), nameof(MapInstance._musicStarted))))
        );

        if (matcher.IsInvalid)
        {
            LogTranspilerFail("couldn't find 'this._nullMusic.Play()'");
        }
        else
        {
            patchOrder.Add((matcher.Pos, Patch));
            void Patch()
            {
                matcher.Advance(1);
                matcher.RemoveInstructions(4);
                matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0));
                matcher.Insert(CodeInstruction.Call(typeof(MapInstance_Handle_AudioSettings), nameof(CheckNullMusicAndPlay)));
            }
        }

        // Replacements - from end to start unless you want to fuck up your positions
        foreach (var patch in patchOrder.OrderByDescending(x => x.Position))
        {
            matcher.Start();
            matcher.Advance(patch.Position);
            patch.Patch();
        }

        if (skipsDoneOnAdvancedNullChecks != 1)
            LogTranspilerFail($"Got {skipsDoneOnAdvancedNullChecks} skips done on null source check instead of the expected count of 1!");

        return matcher.InstructionEnumeration();
    }
    
    private static float LerpVolume(AudioSource source, float targetVolume)
    {
        return Mathf.Lerp(source.volume, targetVolume, 1.5f * Time.deltaTime);
    }

    internal static bool NullMusicSourceIsSet(AudioSource nullMusic)
    {
        bool returnValue = nullMusic && !nullMusic.clip.name.StartsWith("modaudio_internal_map_") && nullMusic.clip != AudioEngine.DisableClip;
        return returnValue;
    }

    // Handle null music correctly
    // ReSharper disable once UnusedMember.Local
    private static void Postfix(MapInstance __instance)
    {
        if (!NullMusicSourceIsSet(__instance._nullMusic))
            return;
        
        bool inCombat = __instance._actionMusic && CheckCombatMusic(__instance._playActionMusic);
        __instance._nullMusic.volume = Mathf.Lerp(__instance._nullMusic.volume, inCombat ? 0f : 0.75f, 1.5f * Time.deltaTime);
    }
    
    private static void CheckNullMusicAndPlay(AudioSource source, MapInstance instance)
    {
        // TODO: Genuinely deranged solution
        var modSource = AudioEngine.GetOrCreateModAudioSource(source);
        AudioEngine.Route(modSource, true, skipOverlays: true);

        if (NullMusicSourceIsSet(source))
        {
            modSource.RevertSource();
            AudioEngine.Route(modSource, true, skipOverlays: false);
            modSource.PlayWithoutRouting();
            instance._musicStarted = true;
        }
    }
}
