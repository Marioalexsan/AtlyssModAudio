using Newtonsoft.Json;
using Tomlet;
using Tomlet.Attributes;

namespace Marioalexsan.ModAudio;

public class AudioPackConfig
{
    /// <summary>
    /// An unique identifier for your audio pack.
    /// It would be a good idea to not change this once you publish your pack.
    /// For example, you could use "YourName_YourPackName" as the identifier.
    /// </summary>
    [JsonProperty("id", Required = Required.DisallowNull)]
    [TomlProperty("id")]
    public string Id { get; set; } = "";

    /// <summary>
    /// A user-readable display name for your audio pack. Used in the UI.
    /// </summary>
    [JsonProperty("display_name", Required = Required.DisallowNull)]
    [TomlProperty("display_name")]
    public string DisplayName { get; set; } = "";

    /// <summary>
    /// A list of custom clips defined by your audio pack.
    /// </summary>
    [JsonProperty("custom_clips", Required = Required.DisallowNull)]
    [TomlProperty("custom_clips")]
    public List<AudioClipData> CustomClips { get; set; } = [];

    /// <summary>
    /// A list of routes defined by your audio pack.
    /// </summary>
    [JsonProperty("routes", Required = Required.DisallowNull)]
    [TomlProperty("routes")]
    public List<Route> Routes { get; set; } = [];

    // Validation has to be done separately due to feature disparity between Newtonsoft.Json and Tomlet
    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            return false;

        return true;
    }

    public static AudioPackConfig ReadTOML(Stream stream)
    {
        var reader = new StreamReader(stream);
        var text = reader.ReadToEnd();

        var data = TomletMain.To<AudioPackConfig>(text);

        return data;
    }

    public static AudioPackConfig ReadJSON(Stream stream)
    {
        List<string> warnings = [];

        var reader = new StreamReader(stream);
        var data = JsonConvert.DeserializeObject<AudioPackConfig>(reader.ReadToEnd(), new JsonSerializerSettings()
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            Error = (obj, e) =>
            {
                if (e.ErrorContext.Error is JsonException exception)
                {
                    if (exception.Message.Contains("Could not find member"))
                    {
                        warnings.Add(exception.Message);
                        e.ErrorContext.Handled = true;
                    }
                }
            }
        }) ?? throw new InvalidOperationException("JSON deserialization returned a null object.");

        foreach (var warning in warnings)
        {
            Logging.LogWarning(warning);
        }

        return data;
    }

    public static AudioPackConfig ReadRouteConfig(Stream stream)
    {
        var routeConfig = RouteConfig.ReadTextFormat(stream);

        var config = new AudioPackConfig
        {
            CustomClips = routeConfig.Routes
                .SelectMany(x => x.ReplacementClips.Concat(x.OverlayClips))
                .Select(x => x.Name)
                .Where(x => x != "___default___" && x != "___nothing___") // TODO: Move these magic strings to a constant
                .Distinct()
                .Select(x => new AudioClipData()
                {
                    Name = x,
                    Path = x,
                    IgnoreClipExtension = true,
                    Volume = routeConfig.ClipVolumes.ContainsKey(x) ? routeConfig.ClipVolumes[x] : 1f
                })
                .ToList(),
            Routes = routeConfig.Routes,
            Id = routeConfig.Id,
            DisplayName = routeConfig.DisplayName,
        };

        foreach (var clipVolume in routeConfig.ClipVolumes)
        {
            if (!config.CustomClips.Any(x => x.Name == clipVolume.Key))
                Logging.LogWarning($"Couldn't find clip {clipVolume.Key} to set volume for.");
        }

        return config;
    }
}
