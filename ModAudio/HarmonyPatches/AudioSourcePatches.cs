using HarmonyLib;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayHelper))]
static class AudioSource_PlayHelper
{
    static bool Prefix(AudioSource source) => AudioEngine.AudioPlayed(source);
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Play), typeof(double))]
static class AudioSource_Play
{
    static bool Prefix(AudioSource __instance) => AudioEngine.AudioPlayed(__instance);
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.PlayOneShotHelper))]
static class AudioSource_PlayOneShotHelper
{
    static bool Prefix(AudioSource source, AudioClip clip, float volumeScale) => AudioEngine.OneShotClipPlayed(clip, source, volumeScale);
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.Stop), typeof(bool))]
static class AudioSource_Stop
{
    static bool Prefix(AudioSource __instance, bool stopOneShots) => AudioEngine.AudioStopped(__instance, stopOneShots);
}

[HarmonyPatch(typeof(AudioSource), nameof(AudioSource.volume), MethodType.Setter)]
static class AudioSource_VolumeSetter
{
    // The line between genius and stupidity is often blurry
    static bool Prefix(AudioSource __instance) => !AudioEngine.IsVolumeLocked(__instance);
}