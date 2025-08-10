using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Marioalexsan.ModAudio.Scripting;
using Marioalexsan.ModAudio.SoftDependencies;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

namespace Marioalexsan.ModAudio;

[BepInPlugin(ModInfo.GUID, ModInfo.NAME, ModInfo.VERSION)]
[BepInDependency(EasySettings.ModID, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("Marioalexsan.AtlyssJint")]
public class ModAudio : BaseUnityPlugin
{
    public static ModAudio Plugin => _plugin ?? throw new InvalidOperationException($"{nameof(ModAudio)} hasn't been initialized yet. Either wait until initialization, or check via ChainLoader instead.");
    private static ModAudio? _plugin;

    public const float MinWeight = 0.001f;
    public const float MaxWeight = 1000f;
    public const float DefaultWeight = 1f;
    public const string DefaultClipIdentifier = "___default___";

    internal new ManualLogSource Logger { get; private set; }

    private readonly Harmony _harmony;

    private bool _reloadRequired = true;

    public string ModAudioConfigFolder => Path.Combine(Paths.ConfigPath, $"{ModInfo.GUID}_UserAudioPack");
    public string ModAudioPluginFolder => Path.GetDirectoryName(Info.Location);

    public ModAudio()
    {
        _plugin = this;

        LogPackLoading = Config.Bind("Logging", nameof(LogPackLoading), true, Texts.LogAudioLoadingDescription);
        LogAudioPlayed = Config.Bind("Logging", nameof(LogAudioPlayed), false, Texts.LogAudioPlayedDescription);
        AlwaysLogCustomEventsPlayed = Config.Bind("Logging", nameof(AlwaysLogCustomEventsPlayed), false, Texts.AlwaysLogCustomEventsPlayedDescription);
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

        VanillaClipNames.GenerateReferenceFile(Path.Combine(ModAudioConfigFolder, "clip_names.txt"));
    }

    private void Awake()
    {
        _harmony.PatchAll(typeof(ModAudio).Assembly);

        SetupBaseAudioPack();
        InitializeConfiguration();

        var stopwatch = Stopwatch.StartNew();
        Logging.LogInfo("Data: " + string.Join(" ", Resources.LoadAll<AudioClip>("/").Select(x => x.name)));
        stopwatch.Stop();

        Logging.LogInfo(stopwatch.ElapsedMilliseconds + " ms time");
    }

    public ConfigEntry<bool> LogPackLoading { get; private set; }
    public ConfigEntry<bool> LogAudioPlayed { get; private set; }
    public ConfigEntry<bool> AlwaysLogCustomEventsPlayed { get; private set; }

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

                        if (enabled != pack.Enabled)
                        {
                            Logger.LogInfo($"Pack {pack.Config.Id} is now {(enabled ? "enabled" : "disabled")}");
                            softReloadRequired = true;
                        }


                        pack.Enabled = enabled;
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
                EasySettings.AddToggle(Texts.AlwaysLogCustomEventsTitle, AlwaysLogCustomEventsPlayed);
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
            AudioPackEnabledRoot = EasySettings.AddButton(Texts.ReloadTitle, () => _reloadRequired = true);
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

                pack.Enabled = enabled.Value;
            }
            else
            {
                pack.Enabled = existingEntry.Value;
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

    private void CheckForObsoleteStuff()
    {
        static bool IsAudioPackFile(string name)
        {
            return
                Path.GetFileName(name) == AudioPackLoader.AudioPackConfigNameJson
                || Path.GetFileName(name) == AudioPackLoader.RoutesConfigName
                || AudioClipLoader.SupportedLoadExtensions.Contains(Path.GetExtension(name));
        }

        var pluginPath = Path.GetDirectoryName(Info.Location);
        var audioPath = Path.Combine(pluginPath, "audio");

        var hasAudioFilesInPlugins = false;

        if (Directory.GetFiles(pluginPath).Any(IsAudioPackFile))
            hasAudioFilesInPlugins = true;

        if (Directory.Exists(audioPath) && Directory.GetFiles(audioPath).Any(IsAudioPackFile))
            hasAudioFilesInPlugins = true;

        if (hasAudioFilesInPlugins)
        {
            Logger.LogWarning("There is an audio pack under ModAudio's plugin folder!");
            Logger.LogWarning("Please use the folder from BepInEx/config for your custom packs instead of the plugin folder.");
            Logger.LogWarning("When using r2modman, the plugin folder might be deleted mercilessly when you update or uninstall your mod, which will delete your custom audio.");
        }
    }

    private void Update()
    {
        try
        {
            if (_reloadRequired)
            {
                _reloadRequired = false;
                CheckForObsoleteStuff();

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