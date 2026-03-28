using HarmonyLib;
using UnityEngine;

namespace Marioalexsan.ModAudio.Atlyss.HarmonyPatches;

[HarmonyPatch(typeof(MapInstance), nameof(MapInstance.Awake))]
internal static class MapInstance_Awake
{
    static void Postfix(MapInstance __instance)
    {
        SetupAudioSource(__instance, ref __instance._actionMusic, "action");
        SetupAudioSource(__instance, ref __instance._daytimeMusic, "day");
        SetupAudioSource(__instance, ref __instance._nightMusic, "night");
        SetupAudioSource(__instance, ref __instance._nullMusic, "null");
    }
    
    private static void SetupAudioSource(MapInstance map, ref AudioSource source, string type)
    {
        if (source != null)
            return;
            
        const int EmptyClipSizeInSamples = 16384; // 0.37 seconds
            
        source = map.gameObject.AddComponent<AudioSource>();
        source.clip = AudioClipLoader.GenerateEmptyClip($"modaudio_internal_map_{type}", EmptyClipSizeInSamples);
        source.playOnAwake = false;
        source.volume = 0;
        source.loop = true;

        if (AtlyssGame.LoadedMixerGroups.TryGetValue("music", out var group))
            source.outputAudioMixerGroup = group;
            
        Logging.LogDebug($"Map {map._mapName} doesn't have {type} music: placeholder was set!");
    }
}