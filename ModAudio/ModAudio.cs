using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Marioalexsan.ModAudio.SoftDependencies;
using UnityEngine;

namespace Marioalexsan.ModAudio;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("Marioalexsan.AtlyssJint", BepInDependency.DependencyFlags.HardDependency)]
public class ModAudio : BaseUnityPlugin
{
    public static ModAudio Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(ModAudio)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static ModAudio? _plugin;

    public const float MinWeight = 0.001f;
    public const float MaxWeight = 1000f;
    public const float DefaultWeight = 1f;

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony;

    private bool _firstTimeUpdate = true;

    public string ModAudioConfigFolder => Path.Combine(Paths.ConfigPath, $"{ModInfo.GUID}_UserAudioPack");
    public string ModAudioPluginFolder => Path.GetDirectoryName(Info.Location);

    public ModAudio()
    {
        _plugin = this;

        LogPackLoading = Config.Bind("Logging", nameof(LogPackLoading), true, Texts.LogAudioLoadingDescription);
        LogAudioPlayed = Config.Bind("Logging", nameof(LogAudioPlayed), false, Texts.LogAudioPlayedDescription);
        UseMaxDistanceForLogging = Config.Bind("Logging", nameof(UseMaxDistanceForLogging), false, Texts.UseMaxDistanceForLoggingDescription);
        MaxDistanceForLogging = Config.Bind("Logging", nameof(MaxDistanceForLogging), 32f, new ConfigDescription(Texts.MaxDistanceForLoggingDescription, new AcceptableValueRange<float>(32f, 2048)));

        LogAmbience = Config.Bind("Logging", nameof(LogAmbience), true, Texts.LogAmbienceDescription);
        LogGame = Config.Bind("Logging", nameof(LogGame), true, Texts.LogGameDescription);
        LogGUI = Config.Bind("Logging", nameof(LogGUI), true, Texts.LogGUIDescription);
        LogMusic = Config.Bind("Logging", nameof(LogMusic), true, Texts.LogMusicDescription);
        LogVoice = Config.Bind("Logging", nameof(LogVoice), true, Texts.LogVoiceDescription);

        Logger = base.Logger;
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

        SetupBaseAudioPack();
        InitializeConfiguration();
    }

    public ConfigEntry<bool> LogPackLoading { get; private set; }
    public ConfigEntry<bool> LogAudioPlayed { get; private set; }

    public ConfigEntry<bool> UseMaxDistanceForLogging { get; private set; }
    public ConfigEntry<float> MaxDistanceForLogging { get; private set; }

    public ConfigEntry<bool> LogAmbience { get; private set; }
    public ConfigEntry<bool> LogGame { get; private set; }
    public ConfigEntry<bool> LogGUI { get; private set; }
    public ConfigEntry<bool> LogMusic { get; private set; }
    public ConfigEntry<bool> LogVoice { get; private set; }

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

                    bool softReloadRequired = false;

                    foreach (var pack in AudioEngine.AudioPacks)
                    {
                        var enabled = !AudioPackEnabled.TryGetValue(pack.Config.Id, out var config) || config.Value;

                        if (enabled != pack.HasFlag(PackFlags.Enabled))
                        {
                            Logger.LogInfo($"Pack {pack.Config.Id} is now {(enabled ? "enabled" : "disabled")}");
                            softReloadRequired = true;
                        }

                        pack.AssignFlag(PackFlags.Enabled, enabled);
                    }

                    if (softReloadRequired)
                        AudioEngine.SoftReload();
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
                EasySettings.AddToggle(Texts.LogAudioLoadingTitle, LogPackLoading);
                EasySettings.AddToggle(Texts.LogAudioPlayedTitle, LogAudioPlayed);
                EasySettings.AddToggle(Texts.UseMaxDistanceForLoggingTitle, UseMaxDistanceForLogging);
                EasySettings.AddAdvancedSlider(Texts.MaxDistanceForLoggingTitle, MaxDistanceForLogging, true);

                EasySettings.AddToggle(Texts.LogAmbienceTitle, LogAmbience);
                EasySettings.AddToggle(Texts.LogGameTitle, LogGame);
                EasySettings.AddToggle(Texts.LogGUITitle, LogGUI);
                EasySettings.AddToggle(Texts.LogMusicTitle, LogMusic);
                EasySettings.AddToggle(Texts.LogVoiceTitle, LogVoice);
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
            AudioPackEnabledRoot = EasySettings.AddButton(Texts.ReloadTitle, () => _firstTimeUpdate = true);
        }

        foreach (var pack in AudioEngine.AudioPacks)
        {
            if (!AudioPackEnabled.TryGetValue(pack.Config.Id, out var existingEntry))
            {
                var enabled = Config.Bind("EnabledAudioPacks", pack.Config.Id, true, Texts.EnablePackDescription(pack.Config.DisplayName));

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