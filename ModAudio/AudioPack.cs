using Marioalexsan.ModAudio.Scripting;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Marioalexsan.ModAudio;

[Flags]
public enum PackFlags : uint
{
    None = uint.MinValue,
    All  = uint.MaxValue,

    Enabled = 1 << 0,
    ForceDisableScripts = 1 << 1,
    HasEncounteredErrors = 1 << 2,
    NotConfigurable = 1 << 3,
    BuiltinPack = 1 << 4,
}

public class AudioPack : IDisposable
{
    public string PackPath { get; set; } = "???";
    public AudioPackConfig Config { get; set; } = new();

    public PackFlags Flags;

    public List<IAudioStream> OpenStreams { get; } = []; // Only touch this if you plan on cleaning up the pack
    public Dictionary<string, AudioClip> ReadyClips { get; } = [];

    // These clips are loaded / streamed when needed
    public Dictionary<string, Func<AudioClip>> PendingClipsToLoad { get; } = [];
    public Dictionary<string, Func<AudioClip>> PendingClipsToStream { get; } = [];

    public ModAudioScript? Script { get; set; }

    public void Dispose()
    {
        foreach (var handle in OpenStreams)
            handle.Dispose();

        Script?.Dispose();
    }

    public bool IsUserPack()
    {
        return PackPath.StartsWith(ModAudio.Plugin.ModAudioConfigFolder) ||
            PackPath.StartsWith(ModAudio.Plugin.ModAudioPluginFolder);
    }

    public bool TryGetReadyClip(string name, out AudioClip? clip)
    {
        if (ReadyClips.TryGetValue(name, out clip))
            return true;

        if (PendingClipsToStream.TryGetValue(name, out var streamer))
        {
            PendingClipsToStream.Remove(name);
            clip = ReadyClips[name] = streamer();
            return true;
        }

        if (PendingClipsToLoad.TryGetValue(name, out var loader))
        {
            PendingClipsToLoad.Remove(name);
            clip = ReadyClips[name] = loader();
            return true;
        }

        clip = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFlag(PackFlags flag) => Flags |= flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearFlag(PackFlags flag) => Flags &= ~flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasFlag(PackFlags flag) => (Flags & flag) == flag; // Do not use Enum.HasFlag, it's a boxing operation and allocates junk

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AssignFlag(PackFlags flag, bool shouldBeSet)
    {
        if (shouldBeSet)
        {
            SetFlag(flag);
        }
        else
        {
            ClearFlag(flag);
        }
    }
}
