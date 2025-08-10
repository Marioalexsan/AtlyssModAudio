using Newtonsoft.Json;
using Tomlet.Attributes;

namespace Marioalexsan.ModAudio;

public class ClipSelection
{
    /// <summary>
    /// The name of the clip.
    /// A special value of "___default___" will select the original clip as replacement.
    /// A special value of "___nothing___" replaces the audio clip with an empty one.
    /// </summary>
    [JsonProperty("name", Required = Required.Always)]
    [TomlProperty("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// How often it should be selected compared to other clips.
    /// </summary>
    [JsonProperty("weight", Required = Required.DisallowNull)]
    [TomlProperty("weight")]
    public float Weight { get; set; } = 1f;

    /// <summary>
    /// Volume adjustment for this selection.
    /// </summary>
    [JsonProperty("volume", Required = Required.DisallowNull)]
    [TomlProperty("volume")]
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// Pitch adjustment for this selection.
    /// </summary>
    [JsonProperty("pitch", Required = Required.DisallowNull)]
    [TomlProperty("pitch")]
    public float Pitch { get; set; } = 1f;
}

public class ReplacementClipSelection : ClipSelection
{
    /// <summary>
    /// Target group of this replacement.
    /// This is used only if a target group script is specified, otherwise it's ignored.
    /// </summary>
    [JsonProperty("group", Required = Required.DisallowNull)]
    [TomlProperty("group")]
    public string Group { get; set; } = "";
}
