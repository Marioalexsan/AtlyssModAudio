using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Awake))]
static class MainMenuPatches
{
    [HarmonyPriority(Priority.Last)]
    static void Postfix()
    {
        ModAudio.Plugin.Knuckles = false;

        if (ModAudio.Plugin.EasterEggsEnabled.Value && Chainloader.PluginInfos.ContainsKey(ModAudio.HomebreweryGUID))
        {
            var label = GameObject.Find("HomebreweryMainMenuLabel");
            var text = label ? label.GetComponent<Text>() : null;

            if (text != null && text.text.Contains("Knuckles", StringComparison.InvariantCultureIgnoreCase))
            {
                text.text += "\n& BugAudio";
                label.transform.localPosition += new Vector3(0f, -text.fontSize, 0f);
                ModAudio.Plugin.Knuckles = true;
            }
        }

        AudioEngine.AudioPacks.FirstOrDefault(x => x.Config.Id == "ModAudio_Knuckles")?.AssignFlag(PackFlags.Enabled, ModAudio.Plugin.Knuckles);
    }
}
