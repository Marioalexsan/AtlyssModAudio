using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.ModAudio.Atlyss.HarmonyPatches;

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Awake))]
static class MainMenuPatches
{
    [HarmonyPriority(Priority.Last)]
    private static void Postfix()
    {
        ModAudio.Knuckles = false;

        if (ModAudio.EasterEggsEnabled.Value && SoftDependencies.HasHomebrewery())
        {
            var label = GameObject.Find("HomebreweryMainMenuLabel");
            var text = label ? label.GetComponent<Text>() : null;

            if (text != null && text.text.Contains("Knuckles", StringComparison.InvariantCultureIgnoreCase))
            {
                text.text += "\n& BugAudio";
                label.transform.localPosition += new Vector3(0f, -text.fontSize, 0f);
                ModAudio.Knuckles = true;
            }
        }

        AudioEngine.AudioPacks.FirstOrDefault(x => x.Config.Id == "ModAudio_Knuckles")?.AssignFlag(PackFlags.Enabled, ModAudio.Knuckles);
    }
}
