using Marioalexsan.ModAudio.Scripting;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using Unity.Profiling;
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
        public DateTime LastUsed;
    }
    
    public string PackPath { get; set; } = "";
    
    // Display / debug stuff
    public List<string> ScriptFiles { get; set; } = [];
    public List<string> ConfigFiles { get; set; } = [];
    
    public AudioPackConfig Config { get; set; } = new();

    public PackFlags Flags;

    // These clips are loaded / streamed when needed
    public Dictionary<string, AudioData> ReadyAudio { get; } = [];
    private (string ClipName, Task<AudioClipLoader.LoadResult> LoadTask)? PendingClipLoad;

    private Queue<string> PreloadQueue { get; } = [];
    
    // Statistics
    public int CurrentStreamedClips { get; set; }
    public int CurrentInMemoryClips { get; set; }
    public int CurrentlyWaitingForLoad => PreloadQueue.Count;

    public IModAudioScript? Script { get; set; }

    public void QueuePreload(string clipName)
    {
        if (!PreloadQueue.Contains(clipName))
            PreloadQueue.Enqueue(clipName);
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

        while (PreloadQueue.Count > 0)
        {
            var nextClip = PreloadQueue.Dequeue();

            if (AudioEngine.IsSpecialClip(nextClip))
                continue; // Skip these

            if (!ReadyAudio.ContainsKey(nextClip))
            {
                clipToLoad = nextClip;
                break;
            }
        }

        if (clipToLoad == null)
            return; // Nothing to do

        var resolvedFilePath = AudioPackLoader.ResolvePath(this, clipToLoad);
        var clipData = Config.CustomClips.FirstOrDefault(x => x.Name == clipToLoad);
        
        if (clipData == null || resolvedFilePath == null)
        {
            Logging.LogWarning($"Failed to load clip {clipToLoad}: no such clip data in the pack!");
            return;
        }
        
        AudioDebugDisplay.LogEngine(LogLevel.Debug, $"Loading {clipToLoad} from pack {Config.Id}...");
        
        PendingClipLoad = (clipToLoad, Task.Run(() =>
        {
            return AudioClipLoader.CreateFromFile(clipData.Name, resolvedFilePath, clipData.Volume);
        }));
    }
    
    private static readonly ProfilerMarker _loadFinalizer = new ProfilerMarker("ModAudio time spent finalizing loads");

    public void FinalizeLoadIfAny()
    {
        if (!PendingClipLoad.HasValue)
            return;
     
        _loadFinalizer.Begin();
        
        var (clipName, loadTask) = PendingClipLoad.Value;

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
                Logging.LogError($"Tried to load a clip {clipName} that was already loaded!");
                Logging.LogError("This is likely a logic error, please notify the mod developer about this!");
                openStream?.Dispose();
                PendingClipLoad = null;
                _loadFinalizer.End();
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
            
            AudioDebugDisplay.LogEngine(LogLevel.Debug, $"Successfully loaded clip {clipName} in {(openStream != null ? "streamed" : "in-memory")} mode!");
        }
        catch (Exception e)
        {
            Logging.LogError("Failed to load an audio clip! It will be replaced with an empty audio file!");
            Logging.LogError($"Exception: {e}!");
            openStream?.Dispose();
            openStream = null;
            clip = AudioEngine.ErrorClip;
        }
        
        ReadyAudio[clipName] = new AudioData()
        {
            Clip = clip,
            Stream = openStream,
            LastUsed = DateTime.UtcNow
        };
        PendingClipLoad = null;
        _loadFinalizer.End();
    }

    public bool LoadClip(string name, out AudioClip? clip)
    {
        if (ReadyAudio.TryGetValue(name, out var audio))
        {
            clip = audio.Clip;
            ReadyAudio[name] = audio with { LastUsed = DateTime.UtcNow };
            return true;
        }

        // TODO: Remove Linq call and fix shitcode
        var clipData = Config.CustomClips.FirstOrDefault(x => x.Name == name);
        var resolvedFilePath = AudioPackLoader.ResolvePath(this, name);

        if (clipData == null || resolvedFilePath == null)
        {
            clip = null;
            return false;
        }

        if (PendingClipLoad.HasValue && PendingClipLoad.Value.ClipName != name)
        {
            // We have to clear this one out first!
            FinalizeLoadIfAny();
        }

        PendingClipLoad = (name, Task.Run(() =>
        {
            return AudioClipLoader.CreateFromFile(clipData.Name, resolvedFilePath, clipData.Volume);
        }));
        
        // Gotta wait for it right away and check ready clips again
        FinalizeLoadIfAny();

        if (ReadyAudio.TryGetValue(name, out audio))
        {
            clip = audio.Clip;
            ReadyAudio[name] = audio with { LastUsed = DateTime.UtcNow };
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
