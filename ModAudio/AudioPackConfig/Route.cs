namespace Marioalexsan.ModAudio;

public class Route
{
    /// <summary>
    /// If true, overlays can only play if the replacement from the same route has been selected.
    /// If false, overlays can play separately from the replacement.
    /// </summary>
    public bool LinkOverlayAndReplacement { get; set; } = true;

    /// <summary>
    /// If true, overlays will stop if the source audio is stopped programatically.
    /// If false, overlays will continue to play even if the source audio is stopped.
    /// </summary>
    public bool OverlayStopsIfSourceStops { get; set; } = true;

    /// <summary>
    /// If true, replacement effects (volume, pitch, etc.) are modifiers on top of the original source.
    /// If false, replacement effects override the original source's effects.
    /// </summary>
    public bool RelativeReplacementEffects { get; set; } = true;

    /// <summary>
    /// If true, overlay effects (volume, pitch, etc.) are modifiers on top of the original source.
    /// If false, overlay effects override the original source's effects.
    /// </summary>
    public bool RelativeOverlayEffects { get; set; } = false;

    /// <summary>
    /// If true, overlay effects (volume, pitch, etc.) will not be played again if the audio source has been restarted while playing.
    /// If false, overlay effects will play on every playthrough, including restarts.
    /// This may be helpful to deal with sources that are played multiple times in the same frame.
    /// </summary>
    public bool OverlaysIgnoreRestarts { get; set; } = false;

    /// <summary>
    /// A list of clips that will be affected by this route.
    /// </summary>
    public List<string> OriginalClips { get; set; } = [];

    /// <summary>
    /// A list of clips to be used as replacements for this route.
    /// </summary>
    public List<ReplacementClipSelection> ReplacementClips { get; set; } = [];

    /// <summary>
    /// A list of clips to be used as overlays for this route.
    /// </summary>
    public List<ClipSelection> OverlayClips { get; set; } = [];

    /// <summary>
    /// Determines how often the replacement from this route will be used relative to other replacements.
    /// </summary>
    public float ReplacementWeight { get; set; } = 1f;

    /// <summary>
    /// Volume modifier for the audio source.
    /// </summary>
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// Pitch modifier for the audio source.
    /// </summary>
    public float Pitch { get; set; } = 1f;

    /// <summary>
    /// If set to true, the audio source will be forced to loop even if it normally shouldn't.
    /// </summary>
    public bool ForceLoop { get; set; } = false;

    /// <summary>
    /// The name of an exported JavaScript method from the audio pack script that dynamically picks a target group to play.
    /// If this is specified, only the target clips that have the specified group will be chosen to play.
    /// One special group is available:
    /// - "all" - explicitly selects all of the tracks from this route.
    /// </summary>
    public string TargetGroupScript { get; set; } = "";

    /// <summary>
    /// If set to true, then the target group script will dynamically track the group of this route and switch the playing audio based on it.
    /// This allows you to implement dynamic music based on game conditions.
    /// </summary>
    public bool EnableDynamicTargeting { get; set; } = false;

    /// <summary>
    /// If set to true, and dynamic targeting is enabled, then the current audio will fade into the new group instead of instantly restarting.
    /// Note: this can be a bit buggy since the implementation tries to wrestle volume control from the game.
    /// </summary>
    public bool SmoothDynamicTargeting { get; set; } = false;

    /// <summary>
    /// Important: This is an experimental option and can be buggy.
    /// If set to true, the engine will try to chain the resulting replacements through the routing system again.
    /// This can be handy if you try to reroute to a vanilla clip - in this case, the vanilla clip will then be rerouted to other routes' custom music.
    /// </summary>
    public bool UseChainRouting { get; set; } = false;
}
