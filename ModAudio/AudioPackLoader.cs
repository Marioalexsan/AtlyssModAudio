using BepInEx;
using BepInEx.Logging;
using Marioalexsan.ModAudio.Scripting;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public static class AudioPackLoader
{
    private static readonly char[] BepInExCharsToSanitize = ['=', '\n', '\t', '\\', '"', '\'', '[', ']'];
    private static readonly char[] BannedChars = [.. BepInExCharsToSanitize];

    public const float OneMB = 1024f * 1024f;

    public const string RoutesScriptName = "__routes.lua";
    public const string RoutesConfigName = "__routes.txt";

    public const int FileSizeLimitForLoading = 1024 * 1024;

    public static string ReplaceRootPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace($"{Path.GetFullPath(Paths.PluginPath).Replace('\\', '/').TrimEnd('/')}/", "plugin://")
            .Replace($"{Path.GetFullPath(Paths.ConfigPath).Replace('\\', '/').TrimEnd('/')}/", "config://");
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
        const string RootSep = "://";

        var cleanPath = ReplaceRootPath(path);

        var index = cleanPath.IndexOf(RootSep);
        var removedRoot = index == -1 ? cleanPath : cleanPath[(index + RootSep.Length)..];

        if (removedRoot.EndsWith(RoutesConfigName))
            return removedRoot[..^(RoutesConfigName.Length + 1)];

        return removedRoot;
    }

    private static void LoadBuiltinAudioPacks(List<AudioPack> existingPacks)
    {
        if (ModAudio.Plugin.EasterEggsEnabled.Value)
        {
            try
            {
                var knuckles = AudioClipLoader.StreamFromFile("knuckles", Path.Combine(ModAudio.Plugin.ModAudioAssetsFolder, "knuckles.ogg"), 1f, out var openedStream);
                AudioPack knucklesPack;

                existingPacks.Add(knucklesPack = new AudioPack()
                {
                    Flags = PackFlags.NotConfigurable | PackFlags.BuiltinPack,
                    Config =
                    {
                        DisplayName = "ModAudio Builtin",
                        Id = "ModAudio_Knuckles",
                        Routes =
                        [
                            new Route()
                            {
                                OriginalClips = ["_mu_flyby"],
                                ReplacementClips = [
                                    new ReplacementClipSelection()
                                    {
                                        Name = "knuckles"
                                    }
                                ],
                                // Virtually guaranteed chance since I don't want to implement audio pack priorities
                                ReplacementWeight = 1e20f,
                            }
                        ]
                    },
                    OpenStreams =
                    {
                        openedStream
                    },
                    ReadyClips =
                    {
                        ["knuckles"] = knuckles
                    }
                });

                if (ModAudio.Plugin.Knuckles)
                    knucklesPack.SetFlag(PackFlags.Enabled);
            }
            catch (Exception e)
            {
                Logging.LogWarning("Couldn't load a builtin clip!");
                Logging.LogWarning(e);
            }
        }
    }

    public static List<AudioPack> LoadAudioPacks()
    {
        List<AudioPack> audioPacks = [];
        List<string> loadPaths = [
            ..Directory.GetDirectories(Paths.ConfigPath),
            ..Directory.GetDirectories(Paths.PluginPath),
        ];

        LoadBuiltinAudioPacks(audioPacks);

        foreach (var rootPath in loadPaths)
        {
            LoadAudioPacksFromRoot(audioPacks, rootPath);
        }

        foreach (var audioPack in audioPacks)
        {
            FinalizePack(audioPack);
            AudioDebugDisplay.LogPack(LogLevel.Info, Texts.PackLoaded(audioPack));
        }

        return audioPacks;
    }

    public static void FinalizePack(AudioPack pack)
    {
        pack.Script?.Start();


        foreach (var route in pack.Config.Routes)
        {
            // Validate / normalize / remap stuff
            // ...unless it's a builtin pack, those get to skip validation
            if (!pack.HasFlag(PackFlags.BuiltinPack))
            {
                var clampedWeight = Mathf.Clamp(route.ReplacementWeight, ModAudio.MinWeight, ModAudio.MaxWeight);

                if (clampedWeight != route.ReplacementWeight)
                {
                    AudioDebugDisplay.LogPack(LogLevel.Warning, Texts.WeightClamped(clampedWeight, pack));
                    pack.SetFlag(PackFlags.HasEncounteredErrors);
                }

                route.ReplacementWeight = clampedWeight;

                foreach (var selection in route.ReplacementClips)
                {
                    clampedWeight = Mathf.Clamp(selection.Weight, ModAudio.MinWeight, ModAudio.MaxWeight);

                    if (clampedWeight != selection.Weight)
                    {
                        AudioDebugDisplay.LogPack(LogLevel.Warning, Texts.WeightClamped(clampedWeight, pack));
                        pack.SetFlag(PackFlags.HasEncounteredErrors);
                    }

                    selection.Weight = clampedWeight;
                }

                foreach (var selection in route.OverlayClips)
                {
                    clampedWeight = Mathf.Clamp(selection.Weight, ModAudio.MinWeight, ModAudio.MaxWeight);

                    if (clampedWeight != selection.Weight)
                    {
                        AudioDebugDisplay.LogPack(LogLevel.Warning, Texts.WeightClamped(clampedWeight, pack));
                        pack.SetFlag(PackFlags.HasEncounteredErrors);
                    }

                    selection.Weight = clampedWeight;
                }
            }

            if (!string.IsNullOrEmpty(route.TargetGroupScript) && (pack.Script == null || !pack.Script.HasExportedMethod(route.TargetGroupScript)))
            {
                AudioDebugDisplay.LogPack(LogLevel.Warning, Texts.MissingTargetGroupScript(route.TargetGroupScript, pack));
                pack.SetFlag(PackFlags.HasEncounteredErrors);
            }
        }

        if (!string.IsNullOrEmpty(pack.Config.PackScripts.Update) && (pack.Script == null || !pack.Script.HasExportedMethod(pack.Config.PackScripts.Update)))
        {
            AudioDebugDisplay.LogPack(LogLevel.Warning, Texts.MissingUpdateScript(pack.Config.PackScripts.Update, pack));
            pack.SetFlag(PackFlags.HasEncounteredErrors);
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
        var routesFormatPath = Path.Combine(folderPath, RoutesConfigName);

        if (File.Exists(routesFormatPath))
            return LoadAudioPack(existingPacks, routesFormatPath, AudioPackConfig.ReadRouteConfig);

        return null;
    }

    private static AudioPack? LoadAudioPack(List<AudioPack> existingPacks, string path, Func<Stream, AudioPackConfig> configReader)
    {
        AudioDebugDisplay.LogPack(LogLevel.Info, Texts.LoadingPack(path));

        AudioPackConfig config;
        try
        {
            using var stream = File.OpenRead(path);
            config = configReader(stream);
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Failed to read audio pack config for {ReplaceRootPath(path)}.");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
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
            AudioDebugDisplay.LogPack(LogLevel.Error, Texts.InvalidPackId(pack.PackPath, pack.Config.Id));
            return null;
        }

        if (string.IsNullOrEmpty(pack.Config.DisplayName))
        {
            // Assign a display name based on folder
            pack.Config.DisplayName = ConvertPathToDisplayName(pack.PackPath);
        }

        if (existingPacks.Any(x => x.Config.Id == pack.Config.Id))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, Texts.DuplicatePackId(pack.PackPath, pack.Config.Id));
            return null;
        }

        var rootPath = Path.GetFullPath(Path.GetDirectoryName(path));

        LoadScriptData(path, pack);
        LoadCustomClips(rootPath, pack, false);

        return pack;
    }

    public static void LoadCustomClips(string rootPath, AudioPack pack, bool extensionless)
    {
        foreach (var clipData in pack.Config.CustomClips)
        {
            if (pack.ReadyClips.Any(x => x.Value.name == clipData.Name))
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, Texts.DuplicateClipId(clipData.Path, clipData.Name));
                pack.SetFlag(PackFlags.HasEncounteredErrors);
                continue;
            }

            var clipPath = Path.GetFullPath(Path.Combine(rootPath, clipData.Path));

            if (!clipPath.StartsWith(rootPath))
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, Texts.InvalidPackPath(clipData.Path, clipData.Name));
                pack.SetFlag(PackFlags.HasEncounteredErrors);
                continue;
            }


            bool gotExtension = AudioClipLoader.SupportedExtensions.Any(clipPath.EndsWith);

            if (!gotExtension)
            {
                // If it doesn't end explicitly with an extension, check if a related audio file exists
                foreach (var ext in AudioClipLoader.SupportedStreamExtensions)
                {
                    if (File.Exists(clipPath + ext))
                    {
                        clipPath += ext;
                        gotExtension = true;
                        break;
                    }
                }
            }

            if (!gotExtension)
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, Texts.UnsupportedAudioFile(clipData.Path, clipData.Name));
                pack.SetFlag(PackFlags.HasEncounteredErrors);
                continue;
            }

            if (!File.Exists(clipPath))
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, Texts.AudioFileNotFound(clipData.Path, clipData.Name));
                pack.SetFlag(PackFlags.HasEncounteredErrors);
                continue;
            }

            long fileSize = new FileInfo(clipPath).Length;
            bool useStreaming = fileSize >= FileSizeLimitForLoading;

            if (useStreaming && !AudioClipLoader.SupportedStreamExtensions.Any(clipPath.EndsWith))
            {
                AudioDebugDisplay.LogPack(LogLevel.Warning, Texts.AudioCannotBeStreamed(clipPath, fileSize));
                useStreaming = false;
            }

            try
            {
                AudioDebugDisplay.LogPack(LogLevel.Info, Texts.LoadingClip(clipPath, clipData.Name, useStreaming));

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
                AudioDebugDisplay.LogPack(LogLevel.Error, $"Failed to load {clipData.Name} from {ReplaceRootPath(clipPath)}!");
                AudioDebugDisplay.LogPack(LogLevel.Error, $"Exception: {e}");
                pack.SetFlag(PackFlags.HasEncounteredErrors);
            }
        }
    }

    public static void LoadScriptData(string path, AudioPack pack)
    {
        try
        {
            var scriptPath = Path.Combine(Path.GetDirectoryName(path), RoutesScriptName);

            if (!File.Exists(scriptPath))
                return;

            var rootScript = File.ReadAllText(scriptPath);

            pack.Script = new ModAudioScript(pack, rootScript);

            AudioDebugDisplay.LogPack(LogLevel.Info, $"Loaded Lua script from {ReplaceRootPath(scriptPath)}.");
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Failed to read audio pack scripts for {ReplaceRootPath(path)}.");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
            pack.SetFlag(PackFlags.ForceDisableScripts | PackFlags.HasEncounteredErrors);
        }
    }
}
