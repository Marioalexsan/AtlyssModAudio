namespace Marioalexsan.ModAudio;

public static partial class VanillaClips
{
    static VanillaClips()
    {
        NameToResourcePath = Paths.ToDictionary(Path.GetFileNameWithoutExtension, path => path[..^Path.GetExtension(path).Length]);
    }

    public static readonly Dictionary<string, string> NameToResourcePath;
}
