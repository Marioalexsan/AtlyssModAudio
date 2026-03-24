using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BepInEx.Bootstrap;
using Marioalexsan.ModAudio.HarmonyPatches;
using UnityEngine;

namespace Marioalexsan.ModAudio;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency("Marioalexsan.AtlyssLua")] // Provides Lua-CSharp
[BepInDependency("EasySettings", BepInDependency.DependencyFlags.SoftDependency)] // Provides a mod settings menu for Atlyss
public class BepinexEntrypoint : BaseUnityPlugin
{
    public BepinexEntrypoint()
    {
        ModAudio.PluginFolder = Path.GetDirectoryName(Info.Location)!;
        ModAudio.Logger = Logger;
        ModAudio.Config = Config;
        SoftDependencies.DependencyHandler = (string modId, [NotNullWhen(true)] out Version? version) =>
        {
            if (!Chainloader.PluginInfos.TryGetValue(modId, out PluginInfo info))
            {
                version = null;
                return false;
            }

            version = info.Metadata.Version;
            return true;
        };
        ModAudio.ModAudioEnabled = Config.Bind("General", nameof(ModAudio.ModAudioEnabled), true, "Whenever ModAudio is enabled or not. Disabling this will unload audio packs and undo any changes to the audio engine.");
        ModAudio.DebugMenuButton = Config.Bind("General", nameof(ModAudio.DebugMenuButton), KeyCode.None, "Button to use for toggling on/off the debug menu for ModAudio. This menu contains various logs for the mod, and can be useful for debugging audio packs, clips and other issues.");
        ModAudio.SourceDetectionRate = Config.Bind("Engine", nameof(ModAudio.SourceDetectionRate), Marioalexsan.ModAudio.SourceDetectionRate.Fast, "How fast to detect new audio sources. Slower detection is less resource demanding, but can sometimes fail to detect playOnAwake audio sources. Realtime will make it frame accurate, but will cause a significant impact on FPS.");
        ModAudio.EasterEggsEnabled = Config.Bind("Misc", nameof(ModAudio.EasterEggsEnabled), true, "Whenever ModAudio's easter egg features are enabled or not.");
        ModAudio.EnableTestPacks = Config.Bind("Misc", nameof(ModAudio.EnableTestPacks), false, "Enable built-in test packs for ModAudio. These are used internally to test the functionality of the mod.");

    }

    public void Awake()
    {
        DontDestroyOnLoad(new GameObject("ModAudio", typeof(ModAudio), typeof(AudioDebugDisplay)));
    }
}