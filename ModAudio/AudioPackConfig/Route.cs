using Newtonsoft.Json;
using Tomlet.Attributes;

namespace Marioalexsan.ModAudio;

public class Route
{
    /// <summary>
    /// If true, overlays can only play if the replacement from the same route has been selected.
    /// If false, overlays can play separately from the replacement.
    /// </summary>
    [JsonProperty("link_overlay_and_replacement", Required = Required.DisallowNull)]
    [TomlProperty("link_overlay_and_replacement")]
    public bool LinkOverlayAndReplacement { get; set; } = true;

    /// <summary>
    /// If true, overlays will stop if the source audio is stopped programatically.
    /// If false, overlays will continue to play even if the source audio is stopped.
    /// </summary>
    [JsonProperty("overlay_stops_if_source_stops", Required = Required.DisallowNull)]
    [TomlProperty("overlay_stops_if_source_stops")]
    public bool OverlayStopsIfSourceStops { get; set; } = true;

    /// <summary>
    /// If true, replacement effects (volume, pitch, etc.) are modifiers on top of the original source.
    /// If false, replacement effects override the original source's effects.
    /// </summary>
    [JsonProperty("relative_replacement_effects", Required = Required.DisallowNull)]
    [TomlProperty("relative_replacement_effects")]
    public bool RelativeReplacementEffects { get; set; } = true;

    /// <summary>
    /// If true, overlay effects (volume, pitch, etc.) are modifiers on top of the original source.
    /// If false, overlay effects override the original source's effects.
    /// </summary>
    [JsonProperty("relative_overlay_effects", Required = Required.DisallowNull)]
    [TomlProperty("relative_overlay_effects")]
    public bool RelativeOverlayEffects { get; set; } = false;

    /// <summary>
    /// If true, overlay effects (volume, pitch, etc.) will not be played again if the audio source has been restarted while playing.
    /// If false, overlay effects will play on every playthrough, including restarts.
    /// This may be helpful to deal with sources that are played multiple times in the same frame.
    /// </summary>
    [JsonProperty("overlays_ignore_restarts", Required = Required.DisallowNull)]
    [TomlProperty("overlays_ignore_restarts")]
    public bool OverlaysIgnoreRestarts { get; set; } = false;

    /// <summary>
    /// A list of clips that will be affected by this route.
    /// </summary>
    [JsonProperty("original_clips", Required = Required.Always)]
    [TomlProperty("original_clips")]
    public List<string> OriginalClips { get; set; } = [];

    /// <summary>
    /// A list of clips to be used as replacements for this route.
    /// </summary>
    [JsonProperty("replacement_clips", Required = Required.DisallowNull)]
    [TomlProperty("replacement_clips")]
    public List<ReplacementClipSelection> ReplacementClips { get; set; } = [];

    /// <summary>
    /// A list of clips to be used as overlays for this route.
    /// </summary>
    [JsonProperty("overlay_clips", Required = Required.DisallowNull)]
    [TomlProperty("overlay_clips")]
    public List<ClipSelection> OverlayClips { get; set; } = [];

    /// <summary>
    /// Determines how often the replacement from this route will be used relative to other replacements.
    /// </summary>
    [JsonProperty("replacement_weight", Required = Required.DisallowNull)]
    [TomlProperty("replacement_weight")]
    public float ReplacementWeight { get; set; } = 1f;

    /// <summary>
    /// Volume modifier for the audio source.
    /// </summary>
    [JsonProperty("volume", Required = Required.DisallowNull)]
    [TomlProperty("volume")]
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// Pitch modifier for the audio source.
    /// </summary>
    [JsonProperty("pitch", Required = Required.DisallowNull)]
    [TomlProperty("pitch")]
    public float Pitch { get; set; } = 1f;

    /// <summary>
    /// If set to true, the audio source will be forced to loop even if it normally shouldn't.
    /// </summary>
    [JsonProperty("force_loop", Required = Required.DisallowNull)]
    [TomlProperty("force_loop")]
    public bool ForceLoop { get; set; } = false;

    /// <summary>
    /// The name of an exported JavaScript method from the audio pack script that dynamically picks a target group to play.
    /// If this is specified, only the target clips that have the specified group will be chosen to play.
    /// Two special groups are available:
    /// "___skip___" skips this route from being selected at all, acting as a filter.
    /// "___all___" explicitly selects all of the tracks from this route.
    /// </summary>
    [JsonProperty("target_group_script", Required = Required.DisallowNull)]
    [TomlProperty("target_group_script")]
    public string TargetGroupScript { get; set; } = "";

    /// <summary>
    /// If set to true, then the target group script will dynamically track the group of this route and switch the playing audio based on it.
    /// This allows you to switch music dynamically based on 
    /// </summary>
    [JsonProperty("enable_dynamic_targeting", Required = Required.DisallowNull)]
    [TomlProperty("enable_dynamic_targeting")]
    public bool EnableDynamicTargeting { get; set; } = false;

    /// <summary>
    /// If set to true, and dynamic targeting is enabled, then the current audio will fade into the new group instead of instantly restarting.
    /// </summary>
    [JsonProperty("smooth_dynamic_targeting", Required = Required.DisallowNull)]
    [TomlProperty("smooth_dynamic_targeting")]
    public bool SmoothDynamicTargeting { get; set; } = false;
}
