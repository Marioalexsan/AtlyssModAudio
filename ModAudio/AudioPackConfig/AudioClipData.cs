namespace Marioalexsan.ModAudio;

public class AudioClipData
{
    /// <summary>
    /// An unique name for your clip.
    /// It would be a good idea to use something that wouldn't conflict with other pack clip names.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The relative path to your clip's audio file.
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>
    /// A volume modifier for your clip.
    /// This is the only place where you can amplify audio by using modifiers above 1.0.
    /// </summary>
    public float Volume { get; set; } = 1f;
}
