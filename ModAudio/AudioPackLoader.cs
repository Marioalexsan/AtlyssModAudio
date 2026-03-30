using BepInEx;
using BepInEx.Logging;
using Marioalexsan.ModAudio.Scripting;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public static class AudioPackLoader
{
    private static readonly char[] BepInExCharsToSanitize = ['=', '\n', '\t', '\\', '"', '\'', '[', ']'];
    private static readonly char[] BannedChars = [.. BepInExCharsToSanitize];

    public const string RoutesScriptName = "__routes.lua";
    public const string RoutesConfigName = "__routes.txt";

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

    private static string ConvertPathToDisplayName(string path)
    {
        const string RootSep = "://";

        var cleanPath = Utils.AliasRootPath(path);

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
        
        BuiltinPacks.LoadBuiltinAudioPacks(audioPacks);

        foreach (var rootPath in loadPaths)
        {
            LoadAudioPacksFromRoot(audioPacks, rootPath);
        }

        foreach (var audioPack in audioPacks)
        {
            FinalizePack(audioPack);
            AudioDebugDisplay.LogPack(LogLevel.Info, audioPack, $"Loaded pack with {audioPack.Config.Routes.Count} routes and {audioPack.Config.CustomClips.Count} clips.");
        }

        return audioPacks;
    }

    public static void FinalizePack(AudioPack pack)
    {
        pack.Script?.Start();

        if (!pack.HasFlag(PackFlags.BuiltinPack))
        {
            bool hasInvalidWeights = false;
            
            foreach (var route in pack.Config.Routes)
            {
                static float ClampWeight(float weight, ref bool wasClamped)
                {
                    var clampedWeight = Mathf.Clamp(weight, ModAudio.MinWeight, ModAudio.MaxWeight);

                    // ReSharper disable once CompareOfFloatsByEqualityOperator
                    if (clampedWeight != weight)
                        wasClamped = true;
                    
                    return clampedWeight;
                }
                
                route.ReplacementWeight = ClampWeight(route.ReplacementWeight, ref hasInvalidWeights);

                foreach (var selection in route.ReplacementClips)
                    selection.Weight = ClampWeight(selection.Weight, ref hasInvalidWeights);

                foreach (var selection in route.OverlayClips)
                    selection.Weight = ClampWeight(selection.Weight, ref hasInvalidWeights);
            }

            if (hasInvalidWeights)
            {
                AudioDebugDisplay.LogPack(LogLevel.Warning, pack, $"This pack has invalid clip weights! They were clamped to the [{ModAudio.MinWeight}, {ModAudio.MaxWeight}] range!");
                pack.SetFlag(PackFlags.HasEncounteredErrors);
            }
        }
        
        foreach (var route in pack.Config.Routes)
        {
            if (!string.IsNullOrEmpty(route.TargetGroupScript) && (pack.Script == null || !pack.Script.HasExportedMethod(route.TargetGroupScript)))
            {
                AudioDebugDisplay.LogPack(LogLevel.Warning, pack, $"Couldn't find target group method {route.TargetGroupScript} in the Lua script!");
                pack.SetFlag(PackFlags.HasEncounteredErrors);
            }
        }

        if (!string.IsNullOrEmpty(pack.Config.PackScripts.Update) && (pack.Script == null || !pack.Script.HasExportedMethod(pack.Config.PackScripts.Update)))
        {
            AudioDebugDisplay.LogPack(LogLevel.Warning, pack, $"Couldn't find update method {pack.Config.PackScripts.Update} in the Lua script!");
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

        var routeFiles = Directory.EnumerateFiles(folderPath, "__routes.*.txt");

        if (File.Exists(routesFormatRootRouteFile))
            routeFiles = Enumerable.Prepend(routeFiles, routesFormatRootRouteFile);

        var routesToLoad = routeFiles.ToList();

        if (routesToLoad.Count == 0)
            return; // Need at least one

        var pack = LoadAudioPack(existingPacks, folderPath, routesToLoad, AudioPackConfig.AudioPackConfig.ReadRouteConfig);
                
        if (pack != null)
            existingPacks.Add(pack);
    }

    private static AudioPack? LoadAudioPack(List<AudioPack> existingPacks, string folderPath, List<string> routePaths, Func<List<string>, AudioPackConfig.AudioPackConfig> configReader)
    {
        if (routePaths.Count == 0)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, null, $"Couldn't find any pack configuration in {Utils.AliasRootPath(folderPath)}!");
            return null;
        }
        
        AudioDebugDisplay.LogPack(LogLevel.Info, null, $"Loading pack from {Utils.AliasRootPath(folderPath)}.");

        AudioPackConfig.AudioPackConfig config;
        try
        {
            config = configReader(routePaths);
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, null, $"Failed to read audio pack configuration from {Utils.AliasRootPath(folderPath)}!");
            AudioDebugDisplay.LogPack(LogLevel.Error, null, $"Exception data: {e}");
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
            pack.Config.Id = SanitizeId(Utils.AliasRootPath(virtualRoutesConfig));
        }
        else if (SanitizeId(pack.Config.Id) != pack.Config.Id)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, null, $"Couldn't load pack from {pack.PackPath}, the ID {pack.Config.Id} has invalid characters!");
            return null;
        }

        if (string.IsNullOrEmpty(pack.Config.DisplayName))
        {
            // Assign a display name based on folder
            pack.Config.DisplayName = ConvertPathToDisplayName(virtualRoutesConfig);
        }

        if (existingPacks.Any(x => x.Config.Id == pack.Config.Id))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, null, $"Couldn't load pack from {pack.PackPath}, there is already an audio pack loaded with the ID {pack.Config.Id}!");
            return null;
        }

        LoadScriptData(folderPath, pack);

        return pack;
    }

    public static string? ResolvePath(AudioPack pack, string clipName)
    {
        var clipData = pack.Config.CustomClips.FirstOrDefault(x => x.Name == clipName);

        if (clipData == null)
            return null;
        
        string clipPath = Utils.ResolvePathAlias(clipData.Path);

        if (!Path.IsPathFullyQualified(clipPath))
            clipPath = Path.GetFullPath(Path.Combine(pack.PackPath, clipData.Path));

        // Disallow loading stuff outside of mod folders *unless it's a builtin*
        if (!clipPath.StartsWith(Paths.PluginPath) && !clipPath.StartsWith(Paths.ConfigPath) && !pack.HasFlag(PackFlags.BuiltinPack))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, pack, $"Couldn't load clip {clipData.Name} from {clipData.Path}. Clips can only be loaded if they are in the BepInEx/plugins or BepInEx/config folders!");
            pack.SetFlag(PackFlags.HasEncounteredErrors);
            return null;
        }

        bool gotExtension = AudioClipLoader.SupportedExtensions.Any(clipPath.EndsWith);

        if (!gotExtension)
        {
            // Check if a related audio file exists
            foreach (var ext in AudioClipLoader.SupportedExtensions)
            {
                if (File.Exists(clipPath + ext))
                {
                    clipPath += ext;
                    gotExtension = true;
                    break;
                }
            }
        }

        if (!gotExtension || !File.Exists(clipPath))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, pack, $"Couldn't load clip {clipData.Name} from {clipData.Path}. Either it was not found, or it is not a supported audio format.");
            pack.SetFlag(PackFlags.HasEncounteredErrors);
            return null;
        }

        return clipPath;
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

            AudioDebugDisplay.LogPack(LogLevel.Debug, pack, $"Loaded Lua script from {Utils.AliasRootPath(scriptPath)}.");
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, pack, $"Failed to read audio pack scripts for {Utils.AliasRootPath(folderPath)}.");
            AudioDebugDisplay.LogPack(LogLevel.Error, pack, $"Exception data: {e}");
            pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
        }
    }
}
