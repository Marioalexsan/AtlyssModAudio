using System.Collections;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.ModAudio.HarmonyPatches;
using Nessie.ATLYSS.EasySettings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Marioalexsan.ModAudio;

public class ModAudio : MonoBehaviour
{
    public static ModAudio Current { get; private set; } = null!;

    public const float MinWeight = 0.001f;
    public const float MaxWeight = 1000f;
    public const float DefaultWeight = 1f;

    internal static ManualLogSource Logger { get; set; } = null!;
    internal static ConfigFile Config { get; set; } = null!;
    internal static readonly Harmony Harmony = new Harmony(ModInfo.GUID);

    internal static bool ShouldHardReloadNextFrame = true;

    public static bool Knuckles { get; internal set; }

    public static string ConfigFolder => Path.Combine(Paths.ConfigPath, $"{ModInfo.GUID}_UserAudioPack");
    public static string PluginFolder { get; internal set; } = null!;
    public static string AssetsFolder => Path.Combine(PluginFolder, "Assets");
    public static string TestPacksFolder => Path.Combine(PluginFolder, "TestPacks");
    public static bool CurrentlyEnabled { get; private set; }

    public static GameObject? EasySettingsAudioPacksRoot { get; set; }

    public static void RegisterGameImplementation(ModAudioGame game)
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (AudioEngine.Game != null)
        {
            Logging.LogWarning($"Failed to register game implementation {game}! Implementation {AudioEngine.Game} is already in use.");
            return;
        }

        AudioEngine.Game = game;
        Logging.LogInfo($"Registered game implementation {game}!");
    }

    private void Awake()
    {
        Current = this;
        CurrentlyEnabled = ModAudioEnabled.Value;

        // TODO: Check if patching the classes directly instead of scanning the assembly is still needed
        ModAudio.Harmony.PatchAll(typeof(AudioSource_PlayHelper));
        ModAudio.Harmony.PatchAll(typeof(AudioSource_Play));
        ModAudio.Harmony.PatchAll(typeof(AudioSource_PlayOneShotHelper));
        ModAudio.Harmony.PatchAll(typeof(AudioSource_Stop));
        ModAudio.Harmony.PatchAll(typeof(AudioSource_VolumeSetter));
        ModAudio.Harmony.PatchAll(typeof(AudioSource_PitchSetter));
        ModAudio.Harmony.PatchAll(typeof(AudioSource_VolumeGetter));
        ModAudio.Harmony.PatchAll(typeof(AudioSource_PitchGetter));

        SetupBaseAudioPack();

        if (SoftDependencies.HasEasySettings())
        {
            EasySettings_Initialize();
        }
        else if (SoftDependencies.HasDependency(SoftDependencies.EasySettings, out var version))
        {
            Logging.LogWarning($"The currently installed EasySettings version {version} is not supported! ModAudio requires at least EasySettings 1.3.0!");
            Logging.LogWarning("EasySettings functionality is not going to work until you update it!");
        }

        SceneManager.sceneLoaded += OnNewScene;
    }

    [MethodImpl(SoftDependencies.MethodOpts)]
    internal static void EasySettings_External_TogglePack(string packId)
    {
        var pack = AudioEngine.AudioPacks.FirstOrDefault(x => x.Config.Id == packId);

        if (pack == null)
            return;

        if (!AudioPackEnabledObjects.TryGetValue(pack.Config.Id, out var obj))
            return;

        var toggle = obj.Toggle.GetComponentInChildren<Toggle>();

        if (!toggle)
            return;

        toggle.isOn = !toggle.isOn;
    }

    internal static void ApplyConfiguration()
    {
        try
        {
            Config.Save();

            bool hardReloadRequired = false;
            bool softReloadRequired = false;

            // If ModAudio was just disabled or enabled, we should do a hard reload to cleanup any leftover packs
            if (CurrentlyEnabled != ModAudioEnabled.Value)
            {
                CurrentlyEnabled = ModAudioEnabled.Value;
                hardReloadRequired = true;
            }

            foreach (var pack in AudioEngine.AudioPacks)
            {
                if (pack.HasFlag(PackFlags.NotConfigurable))
                    continue; // Not configurable - do not touch

                var packEnabled = !AudioPackEnabled.TryGetValue(pack.Config.Id, out var config) || config.Value;

                if (!CurrentlyEnabled)
                    packEnabled = false;

                if (packEnabled != pack.HasFlag(PackFlags.Enabled))
                {
                    Logger.LogInfo($"Pack {pack.Config.Id} is now {(packEnabled ? "enabled" : "disabled")}");
                    softReloadRequired = true;
                }

                pack.AssignFlag(PackFlags.Enabled, packEnabled);
            }

            if (hardReloadRequired)
            {
                AudioEngine.HardReload();
            }
            else if (softReloadRequired)
            {
                AudioEngine.SoftReload();
            }
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in ApplyConfiguration! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
        }
    }

    private void OnNewScene(Scene scene, LoadSceneMode loadMode)
    {
        StartCoroutine(NewSceneCheck());
    }

    private IEnumerator NewSceneCheck()
    {
        // Wait exactly one frame
        yield return null;
        
        AudioEngine.TriggerGarbageCollection();
        AudioEngine.DetectNewSources();
        AudioEngine.TryPreloadSceneClips();
    }

    private static void SetupBaseAudioPack()
    {
        if (!Directory.Exists(ConfigFolder))
            Directory.CreateDirectory(ConfigFolder);
    }

    public static ConfigEntry<bool> ModAudioEnabled { get; internal set; } = null!;
    public static ConfigEntry<bool> EasterEggsEnabled { get; internal set; } = null!;
    public static ConfigEntry<KeyCode> DebugMenuButton { get; internal set; } = null!;
    public static ConfigEntry<SourceDetectionRate> SourceDetectionRate { get; internal set; } = null!;
    public static ConfigEntry<bool> EnableTestPacks { get; internal set; } = null!;

    public static Dictionary<string, ConfigEntry<bool>> AudioPackEnabled { get; } = [];
    public static Dictionary<string, (GameObject Toggle, string DisplayName)> AudioPackEnabledObjects { get; } = [];

    // For debugging / tuning - not present in EasySettings
    public static ConfigEntry<int> AudioStreamingLimitBytes { get; internal set; } = null!;
    public static ConfigEntry<int> AudioCacheTimeInSeconds { get; internal set; } = null!;

    public static ConfigEntry<bool> WriteAudioLogsToBepinexLog { get; internal set; } = null!;
    public static ConfigEntry<bool> WritePackLogsToBepinexLog { get; internal set; } = null!;
    public static ConfigEntry<bool> WriteScriptLogsToBepinexLog { get; internal set; } = null!;
    public static ConfigEntry<bool> WriteEngineLogsToBepinexLog { get; internal set; } = null!;
    
    public static ConfigEntry<bool> UseSystemAcmCodecs { get; internal set; } = null!;

    internal static void InitializePackConfiguration()
    {
        if (SoftDependencies.HasEasySettings())
        {
            SetupAudioPackRoot();

            [MethodImpl(SoftDependencies.MethodOpts)]
            static void SetupAudioPackRoot()
            {
                if (EasySettingsAudioPacksRoot)
                    return;

                var modAudioTab = Settings.GetOrAddCustomTab(ModInfo.NAME);

                modAudioTab.AddHeader($"{ModInfo.NAME} audio packs");
                modAudioTab.AddButton("Open custom audio pack folder", () =>
                {
                    SetupBaseAudioPack();
                    Application.OpenURL(new Uri($"{ConfigFolder}").AbsoluteUri);
                });
                modAudioTab.AddButton("Hard reload audio packs", () => ShouldHardReloadNextFrame = true);
                EasySettingsAudioPacksRoot = modAudioTab.AddButton("Reload scripts from disk", AudioEngine.SoftReloadScripts).Root.gameObject;
            }
        }

        foreach (var pack in AudioEngine.AudioPacks)
        {
            if (pack.HasFlag(PackFlags.NotConfigurable))
                continue; // Not configurable

            var overrides = AudioEngine.ModpackOverrides.FirstOrDefault(x => x.TargetPackId == pack.Config.Id);

            if (!AudioPackEnabled.TryGetValue(pack.Config.Id, out var existingEntry))
            {
                var enabledByDefault = pack.Config.EnabledByDefault;
                bool? forceEnableState = null;

                if (overrides != null && overrides.EnableState.HasValue)
                {
                    switch (overrides.EnableState.Value)
                    {
                        case ModpackOverride.EnableStates.AlwaysEnabled:
                            forceEnableState = true;
                            enabledByDefault = true;
                            break;
                        case ModpackOverride.EnableStates.EnableByDefault:
                            enabledByDefault = true;
                            break;
                        case ModpackOverride.EnableStates.AlwaysDisabled:
                            forceEnableState = false;
                            enabledByDefault = false;
                            break;
                        case ModpackOverride.EnableStates.DisableByDefault:
                            enabledByDefault = false;
                            break;
                    }
                }

                var packEnabled = Config.Bind("EnabledAudioPacks", pack.Config.Id, enabledByDefault, $"Set to true to enable {pack.Config.DisplayName}, false to disable it.");

                if (forceEnableState != null)
                    packEnabled.Value = forceEnableState.Value;

                AudioPackEnabled[pack.Config.Id] = packEnabled;

                if (SoftDependencies.HasEasySettings())
                {
                    AddAudioPackToggle(pack, packEnabled);

                    [MethodImpl(SoftDependencies.MethodOpts)]
                    static void AddAudioPackToggle(AudioPack pack, ConfigEntry<bool> packEnabled)
                    {
                        var modAudioTab = Settings.GetOrAddCustomTab(ModInfo.NAME);

                        if (!AudioPackEnabledObjects.ContainsKey(pack.Config.Id))
                            AudioPackEnabledObjects[pack.Config.Id] = (modAudioTab.AddToggle(pack.Config.DisplayName, packEnabled).Root.gameObject, pack.Config.DisplayName);
                    }
                }

                pack.AssignFlag(PackFlags.Enabled, CurrentlyEnabled && packEnabled.Value);
            }
            else
            {
                pack.AssignFlag(PackFlags.Enabled, CurrentlyEnabled && existingEntry.Value);
            }
        }

        if (SoftDependencies.HasEasySettings())
        {
            SortAudioPackToggles();

            [MethodImpl(SoftDependencies.MethodOpts)]
            static void SortAudioPackToggles()
            {
                if (EasySettingsAudioPacksRoot == null)
                    return;

                int siblingIndex = EasySettingsAudioPacksRoot.transform.GetSiblingIndex() + 1;

                foreach (var config in AudioPackEnabledObjects.OrderBy(x => x.Value.DisplayName))
                {
                    // Reorder so that it's in the audio pack list, sorted by display name
                    config.Value.Toggle.transform.SetSiblingIndex(siblingIndex++);
                }

                foreach (var config in AudioPackEnabledObjects)
                {
                    // Show or hide if pack is present
                    config.Value.Toggle.SetActive(AudioEngine.AudioPacks.Any(x => x.Config.Id == config.Key));
                }
            }
        }
    }

    private void Update()
    {
        try
        {
            if (ShouldHardReloadNextFrame)
            {
                ShouldHardReloadNextFrame = false;
                AudioEngine.HardReload();
            }

            AudioEngine.Update();
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in {nameof(Update)}! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
        }
    }
    
    [MethodImpl(SoftDependencies.MethodOpts)]
    static void EasySettings_Initialize()
    {
        Settings.OnInitialized.AddListener(() =>
        {
            var modAudioTab = Settings.GetOrAddCustomTab(ModInfo.NAME);

            modAudioTab.AddToggle("Enable ModAudio?", ModAudioEnabled);
            modAudioTab.AddToggle("Enable Easter Eggs?", EasterEggsEnabled);
            modAudioTab.AddKeyButton("Debug Menu Toggle", DebugMenuButton);
            modAudioTab.AddDropdown("Audio Source Detection Rate", SourceDetectionRate);
            modAudioTab.AddToggle("Write Audio logs to Bepinex", WriteAudioLogsToBepinexLog);
            modAudioTab.AddToggle("Write Pack logs to Bepinex", WritePackLogsToBepinexLog);
            modAudioTab.AddToggle("Write Script logs to Bepinex", WriteScriptLogsToBepinexLog);
            modAudioTab.AddToggle("Write Engine logs to Bepinex", WriteEngineLogsToBepinexLog);
        });
        Settings.OnApplySettings.AddListener(ApplyConfiguration);
    }
}