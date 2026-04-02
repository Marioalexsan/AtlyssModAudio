using Marioalexsan.ModAudio.Scripting;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
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
    public struct AudioData
    {
        public AudioClip Clip;
        public IAudioStream? Stream;
        public AudioSource? RequestedBy;
        public DateTime LastUsed;
    }
    
    public string PackPath { get; set; } = "";
    
    // Display / debug stuff
    public List<string> ScriptFiles { get; set; } = [];
    public List<string> ConfigFiles { get; set; } = [];
    
    public AudioPackConfig.AudioPackConfig Config { get; set; } = new();

    public PackFlags Flags;

    // These clips are loaded / streamed when needed
    public Dictionary<string, AudioData> ReadyAudio { get; } = [];
    private (string ClipName, AudioSource? RequestingSource, Task<AudioClipLoader.LoadResult> LoadTask)? PendingClipLoad;

    private Queue<(string ClipName, AudioSource? RequestingSource)> PreloadQueue { get; } = [];
    
    // Statistics
    public int CurrentStreamedClips { get; set; }
    public int CurrentInMemoryClips { get; set; }
    public int CurrentQueuedClips => PreloadQueue.Count;

    public IModAudioScript? Script { get; set; }

    public void QueuePreload(string clipName, AudioSource? requestingSource)
    {
        PreloadQueue.Enqueue((clipName, requestingSource));
    }

    public void Dispose()
    {
        PreloadQueue.Clear();
        FinalizeLoadIfAny();
        
        foreach (var handle in ReadyAudio)
        {
            handle.Value.Stream?.Dispose();
            
            // TODO: Is this really needed? ReadyAudio shouldn't even have special clips in it!
            if (!AudioEngine.IsSpecialClip(handle.Value.Clip.name))
                UnityEngine.Object.Destroy(handle.Value.Clip);
        }
        
        ReadyAudio.Clear();
        Script?.Dispose();
    }

    public bool IsUserPack()
    {
        return PackPath.StartsWith(ModAudio.ConfigFolder) ||
            PackPath.StartsWith(ModAudio.PluginFolder);
    }

    public void TryHandleNextPreload()
    {
        if (PreloadQueue.Count == 0)
            return;

        // Check if whatever we have right now is done
        if (PendingClipLoad.HasValue && PendingClipLoad.Value.LoadTask.IsCompleted)
            FinalizeLoadIfAny();

        // Do we have anything else to preload right now?
        if (PendingClipLoad.HasValue)
            return;

        string? clipToLoad = null;
        AudioSource? preloadSource = null;

        while (PreloadQueue.Count > 0)
        {
            var (nextClip, requestingSource) = PreloadQueue.Dequeue();

            if (AudioEngine.IsSpecialClip(nextClip) || AudioEngine.IsVanillaClip(nextClip, out _))
                continue; // Skip these

            if (!ReadyAudio.ContainsKey(nextClip))
            {
                clipToLoad = nextClip;
                preloadSource = requestingSource;
                break;
            }
        }

        if (clipToLoad == null)
            return; // Nothing to do

        var resolvedFilePath = AudioPackLoader.ResolvePath(this, clipToLoad);
        var clipData = Config.CustomClips.FirstOrDefault(x => x.Name == clipToLoad);
        
        if (clipData == null || resolvedFilePath == null)
        {
            AudioDebugDisplay.LogPack(LogLevel.Warning, this, $"Failed to load clip {clipToLoad}: no such clip data in the pack!");
            SetFlag(PackFlags.HasEncounteredErrors);
            return;
        }
        
        AudioDebugDisplay.LogPack(LogLevel.Debug, this, $"Loading {clipToLoad}...");
        
        PendingClipLoad = (clipToLoad, preloadSource, Task.Run(() =>
        {
            return AudioClipLoader.CreateFromFile(clipData.Name, resolvedFilePath, clipData.Volume);
        }));
    }

    public AudioClip? LoadClip(string name, AudioSource? requestingSource)
    {
        if (ReadyAudio.TryGetValue(name, out var audio))
        {
            var clip = audio.Clip;
            ReadyAudio[name] = audio with
            {
                RequestedBy = requestingSource,
                LastUsed = DateTime.UtcNow
            };
            return clip;
        }

        // TODO: Remove Linq call and fix shitcode
        var clipData = Config.CustomClips.FirstOrDefault(x => x.Name == name);
        var resolvedFilePath = AudioPackLoader.ResolvePath(this, name);

        if (clipData == null || resolvedFilePath == null)
        {
            return null;
        }

        if (PendingClipLoad.HasValue && PendingClipLoad.Value.ClipName != name)
        {
            // We have to clear this one out first!
            FinalizeLoadIfAny();
        }

        PendingClipLoad = (name, requestingSource, Task.Run(() =>
        {
            return AudioClipLoader.CreateFromFile(clipData.Name, resolvedFilePath, clipData.Volume);
        }));
        
        // Gotta wait for it right away and check ready clips again
        FinalizeLoadIfAny();

        if (ReadyAudio.TryGetValue(name, out audio))
        {
            var clip = audio.Clip;
            ReadyAudio[name] = audio with
            {
                RequestedBy = requestingSource,
                LastUsed = DateTime.UtcNow
            };
            return clip;
        }
        
        return null;
    }
    
    private void FinalizeLoadIfAny()
    {
        if (!PendingClipLoad.HasValue)
            return;
     
        using var loadFinalizer = Profiling.LoadFinalizer.Auto();
        
        var (clipName, requestingSource, loadTask) = PendingClipLoad.Value;

        IAudioStream? openStream = null;
        AudioClip clip;

        try
        {
            // "What are the odds of a deadlock, anyway?"
            //    - some wanker who deadlocks the app, 2026
            var cancellationSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            loadTask.Wait(cancellationSource.Token);
            
            var result = loadTask.Result;
            openStream = result.OpenStream;

            // Deal with a potential edge case, just in case
            if (ReadyAudio.ContainsKey(clipName))
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, this, $"Tried to load a clip {clipName} that was already loaded!");
                AudioDebugDisplay.LogPack(LogLevel.Error, this, "This is likely a logic error, please notify the mod developer about this!");
                SetFlag(PackFlags.HasEncounteredErrors);
                openStream?.Dispose();
                PendingClipLoad = null;
                return;
            }

            clip = result.CreateFromResult(clipName);

            if (openStream != null)
            {
                CurrentStreamedClips++;
            }
            else
            {
                CurrentInMemoryClips++;
            }
            
            AudioDebugDisplay.LogPack(LogLevel.Debug, this, $"Loaded {(openStream != null ? "streamed" : "in-memory")} clip {clipName}!");
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, this, $"Failed to load clip {clipName}! It will be replaced with an empty audio file!");
            AudioDebugDisplay.LogPack(LogLevel.Error, this, $"Exception: {e}!");
            SetFlag(PackFlags.HasEncounteredErrors);
            openStream?.Dispose();
            openStream = null;
            clip = AudioEngine.ErrorClip;
        }
        
        ReadyAudio[clipName] = new AudioData()
        {
            Clip = clip,
            Stream = openStream,
            RequestedBy = requestingSource,
            LastUsed = DateTime.UtcNow
        };
        PendingClipLoad = null;
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
