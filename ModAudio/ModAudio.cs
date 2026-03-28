using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.ModAudio.HarmonyPatches;
using Nessie.ATLYSS.EasySettings;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private static bool ScheduleHardReload = true;

    public static bool Knuckles { get; internal set; } = false;

    public static string ConfigFolder => Path.Combine(Paths.ConfigPath, $"{ModInfo.GUID}_UserAudioPack");
    public static string PluginFolder { get; internal set; } = null!;
    public static string AssetsFolder => Path.Combine(PluginFolder, "Assets");
    public static string TestPacksFolder => Path.Combine(PluginFolder, "TestPacks");
    public static bool CurrentlyEnabled { get; private set; } = false;

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

        // Patch the classes directly
        // The reason we don't use PatchAll on the assembly is that this would make Harmony hit Lua-CSharp types
        // and would crash if scripting is not actually available
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

            [MethodImpl(SoftDependencies.MethodOpts)]
            static void EasySettings_Initialize()
            {
                Settings.OnInitialized.AddListener(() =>
                {
                    Settings.ModTab.AddHeader(ModInfo.NAME);
                    Settings.ModTab.AddToggle("Enable ModAudio?", ModAudioEnabled);
                    Settings.ModTab.AddToggle("Enable Easter Eggs?", EasterEggsEnabled);
                    Settings.ModTab.AddKeyButton("Debug Menu Toggle", DebugMenuButton);
                    Settings.ModTab.AddDropdown("Audio Source Detection Rate", SourceDetectionRate);
                });
                Settings.OnApplySettings.AddListener(() =>
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
                        Logging.LogError($"ModAudio crashed in OnApplySettings! Please report this error to the mod developer:");
                        Logging.LogError(e.ToString());
                    }
                });
            }
        }
        
        SceneManager.sceneLoaded += OnNewScene;
    }

    private void OnNewScene(Scene scene, LoadSceneMode loadMode)
    {
        AudioEngine.TriggerGarbageCollection();
        AudioEngine.DetectNewSources();
        AudioEngine.TryPreloadSceneClips();
    }

    private static void SetupBaseAudioPack()
    {
        if (!Directory.Exists(ConfigFolder))
            Directory.CreateDirectory(ConfigFolder);

        if (File.Exists(Path.Combine(ConfigFolder, "clip_names.txt")))
        {
            try
            {
                File.Move(Path.Combine(ConfigFolder, "clip_names.txt"), Path.Combine(ConfigFolder, "clip_names_obsolete.txt"));
            }
            catch (Exception)
            {
            }
        }
    }
    
    // For debugging / tuning - not present in EasySettings
    public static ConfigEntry<int> AudioStreamingLimitBytes { get; internal set; } = null!;
    public static ConfigEntry<int> AudioCacheTimeInSeconds { get; internal set; } = null!;

    public static ConfigEntry<bool> ModAudioEnabled { get; internal set; } = null!;
    public static ConfigEntry<bool> EasterEggsEnabled { get; internal set; } = null!;
    public static ConfigEntry<KeyCode> DebugMenuButton { get; internal set; } = null!;
    public static ConfigEntry<SourceDetectionRate> SourceDetectionRate { get; internal set; } = null!;
    public static ConfigEntry<bool> EnableTestPacks { get; internal set; } = null!;

    public static Dictionary<string, ConfigEntry<bool>> AudioPackEnabled { get; } = [];
    public static Dictionary<string, (GameObject Toggle, string DisplayName)> AudioPackEnabledObjects { get; } = [];

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
                
                Settings.ModTab.AddHeader($"{ModInfo.NAME} audio packs");
                Settings.ModTab.AddButton(Texts.OpenCustomAudioPackTitle, () =>
                {
                    SetupBaseAudioPack();
                    Application.OpenURL(new Uri($"{ConfigFolder}").AbsoluteUri);
                });
                Settings.ModTab.AddButton(Texts.HardReloadTitle, () => ScheduleHardReload = true);
                EasySettingsAudioPacksRoot = Settings.ModTab.AddButton(Texts.ReloadScripts, AudioEngine.SoftReloadScripts).Root.gameObject;
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
                
                var packEnabled = Config.Bind("EnabledAudioPacks", pack.Config.Id, enabledByDefault, Texts.EnablePackDescription(pack.Config.DisplayName));

                if (forceEnableState != null)
                    packEnabled.Value = forceEnableState.Value;

                AudioPackEnabled[pack.Config.Id] = packEnabled;
                
                if (SoftDependencies.HasEasySettings())
                {
                    AddAudioPackToggle(pack, packEnabled);
                    
                    [MethodImpl(SoftDependencies.MethodOpts)]
                    static void AddAudioPackToggle(AudioPack pack, ConfigEntry<bool> packEnabled)
                    {
                        if (!AudioPackEnabledObjects.ContainsKey(pack.Config.Id))
                            AudioPackEnabledObjects[pack.Config.Id] = (Settings.ModTab.AddToggle(pack.Config.DisplayName, packEnabled).Root.gameObject, pack.Config.DisplayName);
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
            if (ScheduleHardReload)
            {
                ScheduleHardReload = false;
                AudioEngine.HardReload();
            }

            AudioEngine.Update();
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(Update)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
        }
    }
}