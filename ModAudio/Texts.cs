namespace Marioalexsan.ModAudio;

public static class Texts
{
    public const string LogAudioLoadingTitle = "Log audio pack loading";
    public const string LogAudioLoadingDescription = "True to enable logging when loading audio packs,  false to disable it.";

    public const string LogAudioPlayedTitle = "Log audio played";
    public const string LogAudioPlayedDescription = "True to enable console logging for audio played, false to disable it.";

    public const string AlwaysLogCustomEventsTitle = "Always log custom events played";
    public const string AlwaysLogCustomEventsPlayedDescription = "True to enable console logging for custom events played even if they are unused, false to disable it.";

    public const string UseMaxDistanceForLoggingTitle = "Limit audio logged by distance";
    public const string UseMaxDistanceForLoggingDescription = "True to log audio played only if it's within a certain range of the player, false to log all sounds.";

    public const string MaxDistanceForLoggingTitle = "Max log distance";
    public const string MaxDistanceForLoggingDescription = "The max distance in units from the player where audio will be logged (for reference, 12 units ~ Angela Flux's height).";

    public const string LogAmbienceTitle = "Log ambience audio";
    public const string LogAmbienceDescription = "True to log audio that's part of the ambience group, false to skip it.";

    public const string LogGameTitle = "Log game audio";
    public const string LogGameDescription = "True to log audio that's part of the game group, false to skip it.";

    public const string LogGUITitle = "Log GUI audio";
    public const string LogGUIDescription = "True to log audio that's part of the GUI group, false to skip it.";

    public const string LogMusicTitle = "Log music audio";
    public const string LogMusicDescription = "True to log audio that's part of the music group, false to skip it.";

    public const string LogVoiceTitle = "Log voice audio";
    public const string LogVoiceDescription = "True to log audio that's part of the voice group, false to skip it.";

    public const string ReloadTitle = "Reload audio packs";

    public const string OpenCustomAudioPackTitle = "Open custom audio pack folder";

    public static string WeightClamped(float weight, AudioPack pack)
        => $"Pack {pack.Config.Id} has a weight {weight} that is outside the [{ModAudio.MinWeight}, {ModAudio.MaxWeight}] range and will be clamped.";

    public static string EnablePackDescription(string displayName)
        => $"Set to true to enable {displayName}, false to disable it.";

    public static string InvalidPackId(string path, string id)
        => $"Refusing to load pack {id} from {path}, its ID contains invalid characters.";

    public static string DuplicatePackId(string path, string id)
        => $"Refusing to load pack {id} from {path}, an audio pack with that ID already exists.";

    public static string DuplicateClipId(string path, string id)
        => $"Refusing to load clip {id} from {path}, a clip with that name was already loaded.";

    public static string InvalidPackPath(string path, string id)
        => $"Refusing to load clip {id} from {path}, its path was outside of audio pack.";

    public static string AudioFileNotFound(string file, string id)
        => $"File {file} from clip {id} was not found.";

    public static string UnsupportedAudioFile(string file, string id)
        => $"File {file} from clip {id} is not a supported audio format.";

    public static string DuplicateClipSkipped(string file, string id)
        => $"Audio file {file} was not loaded, clip {id} was already loaded previously.";

    public static string AutoloadingClips(string path)
        => $"Autoloading replacement clips from {AudioPackLoader.ReplaceRootPath(path)}";

    public static string LoadingPack(string path)
        => $"Loading pack from {AudioPackLoader.ReplaceRootPath(path)}";

    public static string PackLoaded(AudioPack pack)
        => $"Loaded pack {pack.Config.Id} with {pack.Config.Routes.Count} routes, {pack.PendingClipsToStream.Count} clips to streams and {pack.PendingClipsToLoad.Count} clips to load";

    public static string LoadingClip(string path, string name, bool useStreaming)
        => $"{(useStreaming ? "Streaming" : "Loading")} clip {name} from {AudioPackLoader.ReplaceRootPath(path)}";

    public static string AudioCannotBeStreamed(string path, long fileSize)
        => $"Audio file {path} is above the size threshold for streaming" +
           $" ({fileSize / AudioPackLoader.OneMB}MB >= {AudioPackLoader.FileSizeLimitForLoading / AudioPackLoader.OneMB}MB)," +
           $" but it cannot be streamed using the current audio format." +
           $" Please try using one of the following supported formats: {string.Join(", ", AudioClipLoader.SupportedStreamExtensions)}.";

    public static string VanillaClipNotFound(string name)
        => $"Couldn't get vanilla clip {name} to play for audio event!";

    public static string AudioClipNotFound(string name)
        => $"Couldn't get clip {name} to play for audio event!";

    public static string NoAudioClipsInGroup(string group)
        => $"Couldn't get any replacement clips for group {group} to play for audio event!";

    public static string MissingTargetGroupScript(string targetGroupScript, AudioPack pack)
        => $"Couldn't get target group script {targetGroupScript} for a route in {pack.Config.Id}! Check whenever your script code is correct!";
}
