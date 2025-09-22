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
    // The line between genius and stupidity is often blurry
    static bool Prefix(AudioSource __instance) => !AudioEngine.IsVolumeLocked(__instance);
}