using BepInEx.Logging;

namespace Marioalexsan.ModAudio.AudioPackConfig;

public class AudioPackConfig
{
    /// <summary>
    /// An unique identifier for your audio pack.
    /// It would be a good idea to not change this once you publish your pack.
    /// For example, you could use "YourName_YourPackName" as the identifier.
    /// </summary>
    public string Id { get; set; } = "";

    /// <summary>
    /// A user-readable display name for your audio pack. Used in the UI.
    /// </summary>
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// Sets the initial state of this audio pack. If set to false, the audio pack will be disabled on first game launch.
    /// </summary>
    public bool EnabledByDefault { get; set; } = true;

    /// <summary>
    /// A user-readable display name for your audio pack. Used in the UI.
    /// </summary>
    public PackScripts PackScripts { get; set; } = new();

    /// <summary>
    /// A list of custom clips defined by your audio pack.
    /// </summary>
    public List<AudioClipData> CustomClips { get; set; } = [];

    /// <summary>
    /// A list of routes defined by your audio pack.
    /// </summary>
    public List<Route> Routes { get; set; } = [];

    // Validation has to be done separately due to feature disparity between Newtonsoft.Json and Tomlet
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            return false;

        return true;
    }

    public static AudioPackConfig ReadRouteConfig(List<string> routePaths)
    {
        RouteConfig routeConfig = new();
        
        foreach (var path in routePaths)
        {
            var aliasedPath = Utils.AliasRootPath(path);
            try
            {
                using var stream = File.OpenRead(path);
                RouteConfig.ReadTextFormat(stream, routeConfig, ((lineNumber, message) => AudioDebugDisplay.LogPack(LogLevel.Warning, null, $"File {aliasedPath}, line {lineNumber}: {message}")));
            }
            catch (Exception e)
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, null, $"Failed to read route file {aliasedPath}.");
                AudioDebugDisplay.LogPack(LogLevel.Error, null, $"Exception data: {e}");
            }
        }

        for (int i = 0; i < routeConfig.Routes.Count; i++)
        {
            var route = routeConfig.Routes[i];

            // "source ~ effect" routes act as if they have an implicit default replacement
            if (route.ReplacementClips.Count == 0 && route.OverlayClips.Count == 0)
                route.ReplacementClips.Add(new() { Name = "___default___" });
        }
        
        var config = new AudioPackConfig
        {
            CustomClips = routeConfig.Routes
                .SelectMany(x => x.ReplacementClips.Concat(x.OverlayClips))
                .Select(x => x.Name)
                .Where(x => !AudioEngine.IsSpecialClip(x) && !AudioEngine.IsVanillaClip(x, out _)) // TODO: Move these magic strings to a constant
                .Distinct()
                .Select(x => new AudioClipData()
                {
                    Name = x,
                    Path = routeConfig.ClipPaths.ContainsKey(x) ? routeConfig.ClipPaths[x] : x,
                    Volume = routeConfig.ClipVolumes.ContainsKey(x) ? routeConfig.ClipVolumes[x] : 1f
                })
                .ToList(),
            Routes = routeConfig.Routes,
            Id = routeConfig.Id,
            DisplayName = routeConfig.DisplayName,
            EnabledByDefault = routeConfig.EnabledByDefault,
            PackScripts =
            {
                Update = routeConfig.UpdateScript
            }
        };

        foreach (var clipVolume in routeConfig.ClipVolumes)
        {
            if (config.CustomClips.All(x => x.Name != clipVolume.Key))
                AudioDebugDisplay.LogPack(LogLevel.Warning, null, $"Pack config {config.Id}: couldn't find clip {clipVolume.Key} to set volume for.");
        }

        return config;
    }
}
