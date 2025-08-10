using Newtonsoft.Json;
using Tomlet.Attributes;

namespace Marioalexsan.ModAudio;

public class AudioClipData
{
    /// <summary>
    /// An unique name for your clip.
    /// It would be a good idea to use something that wouldn't conflict with other pack clip names.
    /// </summary>
    [JsonProperty("name", Required = Required.Always)]
    [TomlProperty("name")]
    public string Name { get; set; } = "";

    /// <summary>
    /// The relative path to your clip's audio file.
    /// </summary>
    [JsonProperty("path", Required = Required.DisallowNull)]
    [TomlProperty("path")]
    public string Path { get; set; } = "";

    /// <summary>
    /// A volume modifier for your clip.
    /// This is the only place where you can amplify audio by using modifiers above 1.0.
    /// </summary>
    [JsonProperty("volume", Required = Required.DisallowNull)]
    [TomlProperty("volume")]
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// If true, extension is ignored (i.e. the first audio file matching the name is loaded).
    /// If false, extension is taken into account (i.e. the exact audio file is loaded).
    /// </summary>
    [JsonProperty("ignore_clip_extension", Required = Required.DisallowNull)]
    [TomlProperty("ignore_clip_extension")]
    public bool IgnoreClipExtension { get; set; } = false;
}
