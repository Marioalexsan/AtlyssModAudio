namespace Marioalexsan.ModAudio;

public class ClipSelection
{
    /// <summary>
    /// The name of the clip.
    /// A special value of "___default___" will select the original clip as replacement.
    /// A special value of "___nothing___" replaces the audio clip with an empty one.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// How often it should be selected compared to other clips.
    /// </summary>
    public float Weight { get; set; } = 1f;

    /// <summary>
    /// Volume adjustment for this selection.
    /// </summary>
    public float Volume { get; set; } = 1f;

    /// <summary>
    /// Pitch adjustment for this selection.
    /// </summary>
    public float Pitch { get; set; } = 1f;
}

public class ReplacementClipSelection : ClipSelection
{
    /// <summary>
    /// Target group of this replacement.
    /// This is used only if a target group script is specified, otherwise it's ignored.
    /// </summary>
    public string Group { get; set; } = "";
}
