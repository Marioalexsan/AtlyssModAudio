using HarmonyLib;
using UnityEngine;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch(typeof(MapInstance), nameof(MapInstance.Awake))]
internal static class MapInstance_Awake
{
    static void Prefix(MapInstance __instance)
    {
        const int EmptyClipSizeInSamples = 16384; // 0.37 seconds

        // Ensure that all regions have replaceable music
        // Naming pattern: mapinstance_[region name with only a-z chars]_dayclip_[null/daytime/night/action]

        var cleanName = string.Concat(__instance._mapName.ToLower().Where(x => 'a' <= x && x <= 'z'));

        if (!__instance._actionMusic)
        {
            __instance._actionMusic = __instance.gameObject.AddComponent<AudioSource>();
            __instance._actionMusic.clip = AudioClipLoader.GenerateEmptyClip($"modaudio_map_{cleanName}_action", EmptyClipSizeInSamples);
            __instance._actionMusic.loop = true;

            Logging.LogInfo($"Map {__instance._mapName} doesn't have action music: placeholder source/clip {__instance._actionMusic.clip} was created!");
        }

        if (!__instance._daytimeMusic)
        {
            __instance._daytimeMusic = __instance.gameObject.AddComponent<AudioSource>();
            __instance._daytimeMusic.clip = AudioClipLoader.GenerateEmptyClip($"modaudio_map_{cleanName}_day", EmptyClipSizeInSamples);
            __instance._daytimeMusic.loop = true;

            Logging.LogInfo($"Map {__instance._mapName} doesn't have daytime music: placeholder source/clip {__instance._daytimeMusic.clip} was created!");
        }

        if (!__instance._nightMusic)
        {
            __instance._nightMusic = __instance.gameObject.AddComponent<AudioSource>();
            __instance._nightMusic.clip = AudioClipLoader.GenerateEmptyClip($"modaudio_map_{cleanName}_night", EmptyClipSizeInSamples);
            __instance._nightMusic.loop = true;

            Logging.LogInfo($"Map {__instance._mapName} doesn't have night music: placeholder source/clip {__instance._nightMusic.clip} was created!");
        }
    }
}
