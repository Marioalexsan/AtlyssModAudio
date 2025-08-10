using BepInEx;
using Jint;
using Jint.Native.Function;
using System.IO;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public static class AudioPackLoader
{
    private static readonly char[] BepInExCharsToSanitize = ['=', '\n', '\t', '\\', '"', '\'', '[', ']'];
    private static readonly char[] BannedChars = [.. BepInExCharsToSanitize];

    public const float OneMB = 1024f * 1024f;

    public const string JSModuleName = "modaudio.js";
    public const string AudioPackConfigNameJson = "modaudio.config.json";
    public const string AudioPackConfigNameToml = "modaudio.config.toml";
    public const string RoutesConfigName = "__routes.txt";

    public const int FileSizeLimitForLoading = 1024 * 1024;

    public static string ReplaceRootPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace($"{Paths.PluginPath}/", "plugin://")
            .Replace($"{Paths.ConfigPath}/", "config://");
    }

    private static bool IsSanitizedId(string id)
    {
        return SanitizeId(id) == id;
    }

    private static string SanitizeId(string id)
    {
        var sanitizedId = id;

        for (int i = 0; i < BannedChars.Length; i++)
        {
            int codePoint = char.ConvertToUtf32($"{BannedChars[i]}", 0);
            sanitizedId = sanitizedId.Replace($"{BannedChars[i]}", $"_{codePoint}");
        }

        return sanitizedId;
    }

    private static string ConvertPathToId(string path)
    {
        return SanitizeId(ReplaceRootPath(path).Replace("\\", "/"));
    }

    private static string ConvertPathToDisplayName(string path)
    {
        var cleanPath = ReplaceRootPath(path);

        var index = cleanPath.IndexOf('/');
        return index == -1 ? cleanPath : cleanPath[..index];
    }

    public static List<AudioPack> LoadAudioPacks()
    {
        List<AudioPack> audioPacks = [];
        List<string> loadPaths = [
            ..Directory.GetDirectories(Paths.ConfigPath),
            ..Directory.GetDirectories(Paths.PluginPath),
        ];

        foreach (var rootPath in loadPaths)
        {
            LoadAudioPacksFromRoot(audioPacks, rootPath);
        }

        foreach (var audioPack in audioPacks)
        {
            FinalizePack(audioPack);
            Logging.LogInfo(Texts.PackLoaded(audioPack), ModAudio.Plugin.LogPackLoading);
        }

        return audioPacks;
    }

    private static void FinalizePack(AudioPack pack)
    {
        // Validate / normalize / remap stuff

        foreach (var route in pack.Config.Routes)
        {
            var clampedWeight = Mathf.Clamp(route.ReplacementWeight, ModAudio.MinWeight, ModAudio.MaxWeight);

            if (clampedWeight != route.ReplacementWeight)
                Logging.LogWarning(Texts.WeightClamped(clampedWeight, pack));

            route.ReplacementWeight = clampedWeight;

            foreach (var selection in route.ReplacementClips)
            {
                clampedWeight = Mathf.Clamp(selection.Weight, ModAudio.MinWeight, ModAudio.MaxWeight);

                if (clampedWeight != selection.Weight)
                    Logging.LogWarning(Texts.WeightClamped(clampedWeight, pack));

                selection.Weight = clampedWeight;
            }

            foreach (var selection in route.OverlayClips)
            {
                clampedWeight = Mathf.Clamp(selection.Weight, ModAudio.MinWeight, ModAudio.MaxWeight);

                if (clampedWeight != selection.Weight)
                    Logging.LogWarning(Texts.WeightClamped(clampedWeight, pack));

                selection.Weight = clampedWeight;
            }

            if (!string.IsNullOrEmpty(route.TargetGroupScript) && !pack.ScriptMethods.ContainsKey(route.TargetGroupScript))
            {
                Logging.LogWarning(Texts.MissingTargetGroupScript(route.TargetGroupScript, pack));
                route.TargetGroupScript = "";
            }
        }
    }

    private static void LoadAudioPacksFromRoot(List<AudioPack> existingPacks, string rootPath)
    {
        Queue<string> paths = new();
        paths.Enqueue(rootPath);

        while (paths.Count > 0)
        {
            var path = paths.Dequeue();

            foreach (var directory in Directory.GetDirectories(path))
            {
                paths.Enqueue(directory);
            }

            var pack = SearchAndLoadPack(existingPacks, path);

            if (pack != null)
                existingPacks.Add(pack);
        }
    }

    private static AudioPack? SearchAndLoadPack(List<AudioPack> existingPacks, string folderPath)
    {
        var tomlPath = Path.Combine(folderPath, AudioPackConfigNameToml);
        if (File.Exists(tomlPath))
            return LoadAudioPack(existingPacks, tomlPath, AudioPackConfig.ReadJSON);

        var jsonPath = Path.Combine(folderPath, AudioPackConfigNameJson);
        if (File.Exists(jsonPath))
            return LoadAudioPack(existingPacks, jsonPath, AudioPackConfig.ReadTOML);

        var routesFormatPath = Path.Combine(folderPath, RoutesConfigName);
        if (File.Exists(routesFormatPath))
            return LoadAudioPack(existingPacks, routesFormatPath, AudioPackConfig.ReadRouteConfig);

        return null;
    }

    private static AudioPack? LoadAudioPack(List<AudioPack> existingPacks, string path, Func<Stream, AudioPackConfig> configReader)
    {
        Logging.LogInfo(Texts.LoadingPack(path), ModAudio.Plugin.LogPackLoading);

        AudioPackConfig config;
        try
        {
            using var stream = File.OpenRead(path);
            config = configReader(stream);
        }
        catch (Exception e)
        {
            Logging.LogWarning($"Failed to read audio pack config for {ReplaceRootPath(path)}.");
            Logging.LogWarning(e.ToString());
            return null;
        }

        AudioPack pack = new()
        {
            Config = config,
            PackPath = path
        };

        if (string.IsNullOrEmpty(pack.Config.Id))
        {
            // Assign an ID based on location
            pack.Config.Id = ConvertPathToId(pack.PackPath);
        }
        else if (!IsSanitizedId(pack.Config.Id))
        {
            Logging.LogWarning(Texts.InvalidPackId(pack.PackPath, pack.Config.Id));
            return null;
        }

        if (string.IsNullOrEmpty(pack.Config.DisplayName))
        {
            // Assign a display name based on folder
            pack.Config.DisplayName = ConvertPathToDisplayName(pack.PackPath);
        }

        if (existingPacks.Any(x => x.Config.Id == pack.Config.Id))
        {
            Logging.LogWarning(Texts.DuplicatePackId(pack.PackPath, pack.Config.Id));
            return null;
        }

        var rootPath = Path.GetFullPath(Path.GetDirectoryName(path));

        LoadScriptData(path, pack);
        LoadCustomClips(rootPath, pack, false);

        return pack;
    }

    private static void LoadCustomClips(string rootPath, AudioPack pack, bool extensionless)
    {
        foreach (var clipData in pack.Config.CustomClips)
        {
            if (pack.ReadyClips.Any(x => x.Value.name == clipData.Name))
            {
                Logging.LogWarning(Texts.DuplicateClipId(clipData.Path, clipData.Name));
                continue;
            }

            var clipPath = Path.GetFullPath(Path.Combine(rootPath, clipData.Path));

            if (!clipPath.StartsWith(rootPath))
            {
                Logging.LogWarning(Texts.InvalidPackPath(clipData.Path, clipData.Name));
                continue;
            }

            if (clipData.IgnoreClipExtension)
            {
                // Search for a file that is supported
                foreach (var ext in AudioClipLoader.SupportedStreamExtensions)
                {
                    if (File.Exists(clipPath + ext))
                    {
                        clipPath = clipPath + ext;
                        break;
                    }
                }
            }

            if (!AudioClipLoader.SupportedExtensions.Any(clipPath.EndsWith))
            {
                Logging.LogWarning(Texts.UnsupportedAudioFile(clipData.Path, clipData.Name));
                continue;
            }

            long fileSize = new FileInfo(clipPath).Length;
            bool useStreaming = fileSize >= FileSizeLimitForLoading;

            if (useStreaming && !AudioClipLoader.SupportedStreamExtensions.Any(clipPath.EndsWith))
            {
                Logging.LogWarning(Texts.AudioCannotBeStreamed(clipPath, fileSize));
                useStreaming = false;
            }

            try
            {
                Logging.LogInfo(Texts.LoadingClip(clipPath, clipData.Name, useStreaming), ModAudio.Plugin.LogPackLoading);

                if (useStreaming)
                {
                    pack.PendingClipsToStream[clipData.Name] = () =>
                    {
                        var clip = AudioClipLoader.StreamFromFile(clipData.Name, clipPath, clipData.Volume, out var stream);
                        pack.OpenStreams.Add(stream);
                        return clip;
                    };
                }
                else
                {
                    pack.PendingClipsToLoad[clipData.Name] = () =>
                    {
                        return AudioClipLoader.LoadFromFile(clipData.Name, clipPath, clipData.Volume);
                    };
                }
            }
            catch (Exception e)
            {
                Logging.LogWarning($"Failed to load {clipData.Name} from {ReplaceRootPath(clipPath)}!");
                Logging.LogWarning($"Exception: {e}");
            }
        }
    }

    private static void LoadScriptData(string path, AudioPack pack)
    {
        try
        {
            var scriptPath = Path.Combine(Path.GetDirectoryName(path), JSModuleName);

            if (!File.Exists(scriptPath))
                return;

            var script = File.ReadAllText(scriptPath);

            AudioEngine.ScriptEngine.Modules.Add(scriptPath, script);
            var module = AudioEngine.ScriptEngine.Modules.Import(scriptPath);

            foreach (var key in module.GetOwnPropertyKeys(Jint.Runtime.Types.String))
            {
                if (module[key] is Function function)
                    pack.ScriptMethods[key.AsString()] = function;
            }

            Logging.LogInfo($"Loaded {pack.ScriptMethods.Count} script methods from {ReplaceRootPath(scriptPath)}.");
        }
        catch (Exception e)
        {
            Logging.LogWarning($"Failed to read audio pack scripts for {ReplaceRootPath(path)}.");
            Logging.LogWarning(e.ToString());
            pack.ScriptMethods.Clear();
            pack.ForceDisableScripts = true;
        }
    }
}
