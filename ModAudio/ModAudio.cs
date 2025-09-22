using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.ModAudio.SoftDependencies;
using UnityEngine;
using UnityEngine.UI;

namespace Marioalexsan.ModAudio;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("Marioalexsan.AtlyssLua", BepInDependency.DependencyFlags.HardDependency)]
public class ModAudio : BaseUnityPlugin
{
    internal const string HomebreweryGUID = "Homebrewery";

    public static ModAudio Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(ModAudio)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static ModAudio? _plugin;

    public const float MinWeight = 0.001f;
    public const float MaxWeight = 1000f;
    public const float DefaultWeight = 1f;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony;

    private bool _firstTimeUpdate = true;

    public bool Knuckles { get; internal set; } = false;

    public string ModAudioConfigFolder => Path.Combine(Paths.ConfigPath, $"{ModInfo.GUID}_UserAudioPack");
    public string ModAudioPluginFolder => Path.GetDirectoryName(Info.Location);
    public string ModAudioAssetsFolder => Path.Combine(ModAudioPluginFolder, "Assets");

    public bool CurrentlyEnabled { get; private set; } = false;

    private AudioDebugDisplay? _display;

    public ModAudio()
    {
        _plugin = this;

        ModAudioEnabled = Config.Bind("General", nameof(ModAudioEnabled), true, "Whenever ModAudio is enabled or not. Disabling this will unload audio packs and undo any changes to the audio engine.");
        DebugMenuButton = Config.Bind("General", nameof(DebugMenuButton), KeyCode.None, "Button to use for toggling on/off the debug menu for ModAudio. This menu contains various logs for the mod, and can be useful for debugging audio packs, clips and other issues.");
        SourceDetectionRate = Config.Bind("Engine", nameof(SourceDetectionRate), Marioalexsan.ModAudio.SourceDetectionRate.Fast, "How fast to detect new audio sources. Slower detection is less resource demanding, but can sometimes fail to detect playOnAwake audio sources. Realtime will make it frame accurate, but will cause a significant impact on FPS.");
        EasterEggsEnabled = Config.Bind("Misc", nameof(EasterEggsEnabled), true, "Huh?");

        Logger = base.Logger;

        CurrentlyEnabled = ModAudioEnabled.Value;
        _harmony = new Harmony(ModInfo.GUID);
    }

    private void SetupBaseAudioPack()
    {
        if (!Directory.Exists(ModAudioConfigFolder))
            Directory.CreateDirectory(ModAudioConfigFolder);

        if (File.Exists(Path.Combine(ModAudioConfigFolder, "clip_names.txt")))
        {
            try
            {
                File.Move(Path.Combine(ModAudioConfigFolder, "clip_names.txt"), Path.Combine(ModAudioConfigFolder, "clip_names_obsolete.txt"));
            }
            catch (Exception) { }
        }

        VanillaClips.GenerateReferenceFile(Path.Combine(ModAudioConfigFolder, "clip_names.md"));
    }

    private void Awake()
    {
        _harmony.PatchAll(typeof(ModAudio).Assembly);
        _display = gameObject.AddComponent<AudioDebugDisplay>();

        SetupBaseAudioPack();
        InitializeConfiguration();
    }

    public ConfigEntry<bool> ModAudioEnabled { get; }
    public ConfigEntry<bool> EasterEggsEnabled { get; }
    public ConfigEntry<KeyCode> DebugMenuButton { get; }
    public ConfigEntry<SourceDetectionRate> SourceDetectionRate { get; }

    public Dictionary<string, ConfigEntry<bool>> AudioPackEnabled { get; } = [];
    public Dictionary<string, (GameObject Toggle, string DisplayName)> AudioPackEnabledObjects { get; } = [];

    public GameObject? AudioPackEnabledRoot { get; set; }

    private void InitializeConfiguration()
    {
        if (EasySettings.IsAvailable)
        {
            EasySettings.OnApplySettings.AddListener(() =>
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
                        var enabled = !AudioPackEnabled.TryGetValue(pack.Config.Id, out var config) || config.Value;

                        if (!CurrentlyEnabled)
                            enabled = false;

                        if (enabled != pack.HasFlag(PackFlags.Enabled) && !pack.HasFlag(PackFlags.RemoveConfigEntry))
                        {
                            Logger.LogInfo($"Pack {pack.Config.Id} is now {(enabled ? "enabled" : "disabled")}");
                            softReloadRequired = true;
                        }

                        pack.AssignFlag(PackFlags.Enabled, enabled);
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
            EasySettings.OnInitialized.AddListener(() =>
            {
                EasySettings.AddHeader(ModInfo.NAME);
                EasySettings.AddToggle("Enable ModAudio", ModAudioEnabled);
                EasySettings.AddKeyButton("Debug Menu Toggle", DebugMenuButton);
                EasySettings.AddDropdown("Audio Source Detection Rate", SourceDetectionRate);
            });
        }
    }

    internal void InitializePackConfiguration()
    {
        if (EasySettings.IsAvailable && !AudioPackEnabledRoot)
        {
            EasySettings.AddHeader($"{ModInfo.NAME} audio packs");
            EasySettings.AddButton(Texts.OpenCustomAudioPackTitle, () =>
            {
                SetupBaseAudioPack();
                Application.OpenURL(new Uri($"{ModAudioConfigFolder}").AbsoluteUri);
            });
            EasySettings.AddButton(Texts.HardReloadTitle, () => _firstTimeUpdate = true);
            AudioPackEnabledRoot = EasySettings.AddButton(Texts.ReloadScripts, AudioEngine.SoftReloadScripts);
        }

        foreach (var pack in AudioEngine.AudioPacks)
        {
            if (pack.HasFlag(PackFlags.RemoveConfigEntry))
                continue; // Not configurable

            if (!AudioPackEnabled.TryGetValue(pack.Config.Id, out var existingEntry))
            {
                var enabled = Config.Bind("EnabledAudioPacks", pack.Config.Id, pack.Config.EnabledByDefault, Texts.EnablePackDescription(pack.Config.DisplayName));

                AudioPackEnabled[pack.Config.Id] = enabled;

                if (EasySettings.IsAvailable)
                {
                    if (!AudioPackEnabledObjects.ContainsKey(pack.Config.Id))
                        AudioPackEnabledObjects[pack.Config.Id] = (EasySettings.AddToggle(pack.Config.DisplayName, enabled), pack.Config.DisplayName);
                }

                pack.AssignFlag(PackFlags.Enabled, enabled.Value);
            }
            else
            {
                pack.AssignFlag(PackFlags.Enabled, existingEntry.Value);
            }
        }

        if (EasySettings.IsAvailable && AudioPackEnabledRoot != null)
        {
            int siblingIndex = AudioPackEnabledRoot.transform.GetSiblingIndex() + 1;

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

    private void Update()
    {
        try
        {

            if (_firstTimeUpdate)
            {
                _firstTimeUpdate = false;

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