using HarmonyLib;
using Unity.Profiling;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayHelper))]
static class AudioSource_PlayHelper
{
    private static readonly ProfilerMarker Profiler = new ProfilerMarker("ModAudio hook AudioSource.PlayHelper()");

    static bool Prefix(AudioSource source)
    {
        Profiler.Begin();
        bool result = AudioEngine.AudioPlayed(source);
        Profiler.End();
        return result;
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), typeof(double))]
static class AudioSource_Play
{
    private static readonly ProfilerMarker Profiler = new ProfilerMarker("ModAudio hook AudioSource.Play()");

    static bool Prefix(AudioSource __instance)
    {
        Profiler.Begin();
        bool result = AudioEngine.AudioPlayed(__instance);
        Profiler.End();
        return result;
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShotHelper))]
static class AudioSource_PlayOneShotHelper
{
    private static readonly ProfilerMarker Profiler = new ProfilerMarker("ModAudio hook AudioSource.PlayOneShotHelper()");

    static bool Prefix(AudioSource source, AudioClip clip, float volumeScale)
    {
        Profiler.Begin();
        bool result = AudioEngine.OneShotClipPlayed(clip, source, volumeScale);
        Profiler.End();
        return result;
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Stop), typeof(bool))]
static class AudioSource_Stop
{
    private static readonly ProfilerMarker Profiler = new ProfilerMarker("ModAudio hook AudioSource.Stop()");

    static bool Prefix(AudioSource __instance, bool stopOneShots)
    {
        Profiler.Begin();
        bool result = AudioEngine.AudioStopped(__instance, stopOneShots);
        Profiler.End();
        return result;
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.volume), MethodType.Setter)]
static class AudioSource_VolumeSetter
{
    static bool Prefix(AudioSource __instance, ref float value) => AudioEngine.SetVolumeCallback(__instance, ref value);
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.pitch), MethodType.Setter)]
static class AudioSource_PitchSetter
{
    static bool Prefix(AudioSource __instance, ref float value) => AudioEngine.SetPitchCallback(__instance, ref value);
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.volume), MethodType.Getter)]
static class AudioSource_VolumeGetter
{
    static void Postfix(AudioSource __instance, ref float __result) => AudioEngine.GetVolumeCallback(__instance, ref __result);
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.pitch), MethodType.Getter)]
static class AudioSource_PitchGetter
{
    static void Postfix(AudioSource __instance, ref float __result) => AudioEngine.GetPitchCallback(__instance, ref __result);
}