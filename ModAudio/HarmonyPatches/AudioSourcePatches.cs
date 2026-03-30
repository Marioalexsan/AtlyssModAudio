using HarmonyLib;
using Unity.Profiling;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayHelper))]
static class AudioSource_PlayHelper
{
    static bool Prefix(AudioSource source)
    {
        using var marker = Profiling.AudioSourcePlayHelper.Auto();
        return AudioEngine.AudioPlayed(source);
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), typeof(double))]
static class AudioSource_Play
{
    static bool Prefix(AudioSource __instance)
    {
        using var marker = Profiling.AudioSourcePlay.Auto();
        return AudioEngine.AudioPlayed(__instance);
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShotHelper))]
static class AudioSource_PlayOneShotHelper
{
    static bool Prefix(AudioSource source, AudioClip clip, float volumeScale)
    {
        using var marker = Profiling.AudioSourcePlayOneShotHelper.Auto();
        return AudioEngine.OneShotClipPlayed(clip, source, volumeScale);
    }
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Stop), typeof(bool))]
static class AudioSource_Stop
{
    static bool Prefix(AudioSource __instance, bool stopOneShots)
    {
        using var marker = Profiling.AudioSourceStop.Auto();
        return AudioEngine.AudioStopped(__instance, stopOneShots);
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