using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BepInEx.Bootstrap;
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
        ModAudio.SourceDetectionRate = Config.Bind("Engine", nameof(ModAudio.SourceDetectionRate), SourceDetectionRate.Fast, "How fast to detect new audio sources. Slower detection is less resource demanding, but can sometimes fail to detect playOnAwake audio sources. Realtime will make it frame accurate, but will cause a significant impact on FPS.");
        ModAudio.EasterEggsEnabled = Config.Bind("Misc", nameof(ModAudio.EasterEggsEnabled), true, "Whenever ModAudio's easter egg features are enabled or not.");
        ModAudio.EnableTestPacks = Config.Bind("Misc", nameof(ModAudio.EnableTestPacks), false, "Enable built-in test packs for ModAudio. These are used internally to test the functionality of the mod.");

        ModAudio.AudioStreamingLimitBytes = Config.Bind(
            "Experimental",
            nameof(ModAudio.AudioStreamingLimitBytes),
            44100 * 2 * 4 * 20,
            "The uncompressed audio size threshold (in bytes) for streaming. ModAudio will stream audio if it would take up more space than this in memory. " +
            "Default value corresponds to 20 seconds of stereo audio at 44100 Hz (assuming float samples at 4 bytes / sample). " +
            "Lower values reduce memory usage and load times due to switching to streaming for more clips, but may trigger issues with Unity's audio system. " +
            "**Do not modify this unless you know what you're doing!**"
        );
        
        // Default values corresponds to 44100 Hz, stereo, float samples, 20 seconds of audio
        ModAudio.AudioCacheTimeInSeconds = Config.Bind(
            "Experimental",
            nameof(ModAudio.AudioCacheTimeInSeconds),
            150,
            "Determines how long an audio clip will stay loaded for before it's cleaned up (measured in seconds). " +
            "This duration is reset whenever an audio clip is used, i.e. when playing it. " +
            "Once this threshold is reached, ModAudio will try unloading the clip to reduce memory usage." +
            "Higher durations reduce stuttering associated with reloading files, lower durations might reduce memory usage." +
            "**Do not modify this unless you know what you're doing!**"
        );
        
        ModAudio.UseSystemAcmCodecs = Config.Bind(
            "Experimental",
            nameof(ModAudio.UseSystemAcmCodecs),
            false, // Use NLayer by default
            "Use the codecs available on the system for decoding MP3 files instead of NLayer's custom implementation. " +
            "**Do not modify this unless you know what you're doing!**"
        );
        
        ModAudio.WriteAudioLogsToBepinexLog = Config.Bind(
            "Logging",
            nameof(ModAudio.WriteAudioLogsToBepinexLog),
            false,
            "Should ModAudio write audio played to the BepInEx log? (Note: this *will* spam your log with stuff)"
        );
        ModAudio.WritePackLogsToBepinexLog = Config.Bind(
            "Logging",
            nameof(ModAudio.WritePackLogsToBepinexLog),
            true,
            "Should ModAudio write audio pack info to the BepInEx log?"
        );
        ModAudio.WriteScriptLogsToBepinexLog = Config.Bind(
            "Logging",
            nameof(ModAudio.WriteScriptLogsToBepinexLog),
            false,
            "Should ModAudio write logs coming from Lua scripts to the BepInEx log? (Note: this may be spammy if scripts log a lot of data)"
        );
        ModAudio.WriteEngineLogsToBepinexLog = Config.Bind(
            "Logging",
            nameof(ModAudio.WriteEngineLogsToBepinexLog),
            true,
            "Should ModAudio write information coming from its audio engine to the BepInEx log?"
        );
    }

    public void Awake()
    {
        DontDestroyOnLoad(new GameObject("ModAudio", typeof(ModAudio), typeof(AudioDebugDisplay)));
    }
}