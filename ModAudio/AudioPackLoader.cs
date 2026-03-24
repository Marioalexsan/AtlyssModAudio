using System.Runtime.CompilerServices;
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

    public static string AliasRootPath(string path)
    {
        return Path.GetFullPath(path)
            .Replace('\\', '/')
            .Replace($"{Path.GetFullPath(Paths.PluginPath).Replace('\\', '/').TrimEnd('/')}/", "plugin://")
            .Replace($"{Path.GetFullPath(Paths.ConfigPath).Replace('\\', '/').TrimEnd('/')}/", "config://");
    }

    public static string ResolvePathAlias(string path)
    {
        path = path.Replace('\\', '/');
        
        if (path.StartsWith("plugin://"))
            return Path.Combine(Paths.PluginPath, path.Substring("plugin://".Length));
        
        if (path.StartsWith("config://"))
            return Path.Combine(Paths.ConfigPath, path.Substring("config://".Length));

        return path;
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
        return SanitizeId(AliasRootPath(path).Replace("\\", "/"));
    }

    private static string ConvertPathToDisplayName(string path)
    {
        const string RootSep = "://";

        var cleanPath = AliasRootPath(path);

        var index = cleanPath.IndexOf(RootSep, StringComparison.Ordinal);
        var removedRoot = index == -1 ? cleanPath : cleanPath[(index + RootSep.Length)..];

        if (removedRoot.EndsWith(RoutesConfigName))
            return removedRoot[..^(RoutesConfigName.Length + 1)];

        return removedRoot;
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

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (clampedWeight != route.ReplacementWeight)
                {
                    AudioDebugDisplay.LogPack(LogLevel.Warning, Texts.WeightClamped(clampedWeight, pack));
                    pack.SetFlag(PackFlags.HasEncounteredErrors);
                }

                route.ReplacementWeight = clampedWeight;

                foreach (var selection in route.ReplacementClips)
                {
                    clampedWeight = Mathf.Clamp(selection.Weight, ModAudio.MinWeight, ModAudio.MaxWeight);

                    // ReSharper disable once CompareOfFloatsByEqualityOperator
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

                    // ReSharper disable once CompareOfFloatsByEqualityOperator
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
                if (!ModAudio.EnableTestPacks.Value && Path.GetFullPath(directory).StartsWith(Path.GetFullPath(ModAudio.TestPacksFolder)))
                    continue;
                
                paths.Enqueue(directory);
            }

            SearchAndLoadPacks(existingPacks, path);
        }
    }

    private static void SearchAndLoadPacks(List<AudioPack> existingPacks, string folderPath)
    {
        var routesFormatRootRouteFile = Path.Combine(folderPath, RoutesConfigName);
        var routesFormatRootScriptFile = Path.Combine(folderPath, RoutesScriptName);

        var routeFiles = Directory.EnumerateFiles(folderPath, "__routes.*.txt");

        if (File.Exists(routesFormatRootRouteFile))
            routeFiles = Enumerable.Prepend(routeFiles, routesFormatRootRouteFile);

        var routesToLoad = routeFiles.ToList();

        if (routesToLoad.Count == 0)
            return; // Need at least one

        var pack = LoadAudioPack(existingPacks, folderPath, routesToLoad, AudioPackConfig.ReadRouteConfig);
                
        if (pack != null)
            existingPacks.Add(pack);
    }

    private static AudioPack? LoadAudioPack(List<AudioPack> existingPacks, string folderPath, List<string> routePaths, Func<List<string>, AudioPackConfig> configReader)
    {
        if (routePaths.Count == 0)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Failed to read audio pack config for {AliasRootPath(folderPath)}. No route files were specified.");
            return null;
        }
        
        AudioDebugDisplay.LogPack(LogLevel.Info, Texts.LoadingPack(folderPath, routePaths.Count));

        AudioPackConfig config;
        try
        {
            config = configReader(routePaths);
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Failed to read audio pack configurations from {AliasRootPath(folderPath)}.");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
            return null;
        }

        AudioPack pack = new()
        {
            Config = config,
            ConfigFiles = routePaths.ToList(),
            PackPath = folderPath
        };

        var virtualRoutesConfig = Path.Combine(folderPath, RoutesConfigName);

        if (string.IsNullOrEmpty(pack.Config.Id))
        {
            // Assign an ID based on location
            pack.Config.Id = ConvertPathToId(virtualRoutesConfig);
        }
        else if (!IsSanitizedId(pack.Config.Id))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, Texts.InvalidPackId(pack.PackPath, pack.Config.Id));
            return null;
        }

        if (string.IsNullOrEmpty(pack.Config.DisplayName))
        {
            // Assign a display name based on folder
            pack.Config.DisplayName = ConvertPathToDisplayName(virtualRoutesConfig);
        }

        if (existingPacks.Any(x => x.Config.Id == pack.Config.Id))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, Texts.DuplicatePackId(pack.PackPath, pack.Config.Id));
            return null;
        }

        LoadScriptData(folderPath, pack);
        LoadCustomClips(folderPath, pack, false);

        return pack;
    }

    public static string? ResolvePath(AudioPack pack, string clipName)
    {
        var clipData = pack.Config.CustomClips.FirstOrDefault(x => x.Name == clipName);

        if (clipData == null)
            return null;
        
        string clipPath = ResolvePathAlias(clipData.Path);

        if (!Path.IsPathFullyQualified(clipPath))
            clipPath = Path.GetFullPath(Path.Combine(pack.PackPath, clipData.Path));

        // Disallow loading stuff outside of mod folders
        if (!clipPath.StartsWith(Paths.PluginPath) && !clipPath.StartsWith(Paths.ConfigPath))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, Texts.InvalidPackPath(clipData.Path, clipData.Name));
            pack.SetFlag(PackFlags.HasEncounteredErrors);
            return null;
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
            return null;
        }

        if (!File.Exists(clipPath))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, Texts.AudioFileNotFound(clipData.Path, clipData.Name));
            pack.SetFlag(PackFlags.HasEncounteredErrors);
            return null;
        }

        return clipPath;
    }

    public static void LoadCustomClips(string rootPath, AudioPack pack, bool extensionless)
    {
        HashSet<string> handledClips = [];
        
        foreach (var clipData in pack.Config.CustomClips)
        {
            if (!handledClips.Add(clipData.Name))
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, Texts.DuplicateClipId(clipData.Path, clipData.Name));
                pack.SetFlag(PackFlags.HasEncounteredErrors);
                continue;
            }

            string? clipPath = ResolvePath(pack, clipData.Name);

            if (clipPath == null)
                continue;

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
                
                // TODO: Preload clips?
            }
            catch (Exception e)
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, $"Failed to load {clipData.Name} from {AliasRootPath(clipPath)}!");
                AudioDebugDisplay.LogPack(LogLevel.Error, $"Exception: {e}");
                pack.SetFlag(PackFlags.HasEncounteredErrors);
            }
        }
    }

    public static void LoadScriptData(string folderPath, AudioPack pack)
    {
        try
        {
            var scriptPath = Path.Combine(folderPath, RoutesScriptName);

            if (!File.Exists(scriptPath))
                return;

            var rootScript = File.ReadAllText(scriptPath);

            pack.Script = new ModAudioScript(pack, rootScript);
            pack.ScriptFiles.Add(scriptPath);

            AudioDebugDisplay.LogPack(LogLevel.Info, $"Loaded Lua script from {AliasRootPath(scriptPath)}.");
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Failed to read audio pack scripts for {AliasRootPath(folderPath)}.");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
            pack.SetFlag(PackFlags.ForceDisableScripts | PackFlags.HasEncounteredErrors);
        }
    }
}
