using System.Diagnostics.CodeAnalysis;
using BepInEx;
using BepInEx.Logging;
using Marioalexsan.ModAudio.Scripting;
using Newtonsoft.Json;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public enum SourceDetectionRate
{
    Realtime,
    Fast,
    Medium,
    Slow,
}

internal static class AudioEngine
{
    public const string DefaultClipKeyword = "___default___";
    public const string EmptyClipKeyword = "___nothing___";
    public const string DisableClipKeyword = "___disable___";
    public const string ErrorClipKeyword = "!!! error !!!";

    public const string TargetGroupAll = "all";

    private const int MaxChainRoutes = 4;

    private static readonly System.Random RNG = new();
    private static readonly System.Diagnostics.Stopwatch Watch = new();

    public static readonly Dictionary<AudioSource, ModAudioSource> TrackedSources = new(8192);
    public static readonly Dictionary<AudioSource, bool> TrackedPlayOnAwakeSourceStates = [];
    public static readonly HashSet<ModAudioSource> TrackedOneShots = [];

    public static List<AudioPack> AudioPacks { get; } = [];
    public static List<ModpackOverride> ModpackOverrides { get; } = [];

    public static Dictionary<string, AudioClip> LoadedVanillaClips = [];

    private static DateTime LastSourceFetch = DateTime.Now;

    internal static ModAudioGame Game = null!;

    // Temporary data for routing
    private static readonly Dictionary<Route, TargetGroupData> CachedRoutingTargetGroupData = [];
    
    // Temporary data for selecting routes without allocations
    private static readonly List<(AudioPack Pack, Route Route)> CachedReplacementSelectionList = new List<(AudioPack Pack, Route Route)>(128);
    
    // Setting this too low might cause it to fail for playOnAwake activeSources
    // This is due to the detection method in Update(), which relies on scanning audio activeSources every frame
    // This is why we need to use a minimum size (a few game frames at least).

    private const int EmptyClipSizeInSamples = 16384; // 0.37 seconds
    
    public static bool IsSpecialClip(string name)
    {
        return name == DefaultClipKeyword || name == EmptyClipKeyword || name == ErrorClipKeyword || name == DisableClipKeyword;
    }

    public static bool IsVanillaClip(string name, [NotNullWhen(true)] out string? actualClip)
    {
        if (name.StartsWith("<atlyss>"))
        {
            actualClip = name.Substring("<atlyss>".Length);
            return true;
        }
        
        if (name.StartsWith("<game>"))
        {
            actualClip = name.Substring("<game>".Length);
            return true;
        }

        actualClip = name;
        return false;
    }

    internal static AudioClip EmptyClip => _emptyClip ??= AudioClipLoader.GenerateEmptyClip(EmptyClipKeyword, EmptyClipSizeInSamples);
    private static AudioClip? _emptyClip;

    // Special clip similar to ___nothing___ that has special meaning in some contexts
    internal static AudioClip DisableClip => _disableClip ??= AudioClipLoader.GenerateEmptyClip(DisableClipKeyword, EmptyClipSizeInSamples);
    private static AudioClip? _disableClip;

    // This is not an "actual" clip, but should be used to
    // 1. Avoid null references, and
    // 2. Indicate that an error state was encountered (clip failed to load, etc.)
    internal static AudioClip ErrorClip => _errorClip ??= AudioClipLoader.GenerateEmptyClip(ErrorClipKeyword, EmptyClipSizeInSamples);
    private static AudioClip? _errorClip;

    internal static bool SetVolumeCallback(AudioSource source, ref float value)
    {
        var modSource = GetModAudioSourceIfExists(source);

        if (modSource == null)
            return true;

        if (modSource.HasFlag(AudioFlags.VolumeLock))
            return false;

        modSource.LastUnproxiedVolume = value;
        value *= modSource.ProxyVolumeModifier;
        
        return true;
    }
    
    internal static bool SetPitchCallback(AudioSource source, ref float value)
    {
        var modSource = GetModAudioSourceIfExists(source);

        if (modSource == null)
            return true;

        if (modSource.HasFlag(AudioFlags.PitchLock))
            return false;

        modSource.LastUnproxiedPitch = value;
        value *= modSource.ProxyPitchModifier;
        
        return true;
    }

    internal static void GetVolumeCallback(AudioSource source, ref float value)
    {
        var modSource = GetModAudioSourceIfExists(source);

        if (modSource == null)
            return;

        // Dividing by the multiplier to get the original value is not possible in the case where it's 0
        // So let's return the last known value instead
        value = modSource.LastUnproxiedVolume;
    }

    internal static void GetPitchCallback(AudioSource source, ref float value)
    {
        var modSource = GetModAudioSourceIfExists(source);

        if (modSource == null)
            return;

        // Dividing by the multiplier to get the original value is not possible in the case where it's 0
        // So let's return the last known value instead
        value = modSource.LastUnproxiedPitch;
    }

    public static void HardReload() => Reload(hardReload: true);

    public static void SoftReload() => Reload(hardReload: false);

    public static void SoftReloadScripts()
    {
        try
        {
            foreach (var pack in AudioPacks)
            {
                pack.Script?.Dispose();
                pack.Script = null;
            }

            foreach (var pack in AudioPacks)
            {
                // TODO: This stinks
                pack.ScriptFiles.Clear();
                AudioPackLoader.LoadScriptData(pack.PackPath, pack);
                AudioPackLoader.FinalizePack(pack);
            }

            // Just to reset state properly
            SoftReload();
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in {nameof(SoftReloadScripts)}! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
        }
    }

    internal static void TriggerGarbageCollection()
    {
        // Hacky, but will do
        LastGarbageCollection = DateTime.Now - TimeSpan.FromDays(1);
    }

    private static void RunGarbageCollection()
    {
        AudioDebugDisplay.LogEngine(LogLevel.Debug, "Running garbage collection for custom audio");
        
        // Referenced audio (i.e. the audio source that preloaded it is still alive) will be kept loaded for longer
        var canCollectUnreferencedAudioBefore = DateTime.UtcNow - TimeSpan.FromSeconds(ModAudio.AudioCacheTimeInSeconds.Value);
        var canCollectReferencedAudioBefore = DateTime.UtcNow - 4 * TimeSpan.FromSeconds(ModAudio.AudioCacheTimeInSeconds.Value);
        
        foreach (var pack in AudioPacks)
        {
            var readyAudio = ForeachCache<KeyValuePair<string, AudioPack.AudioData>>.CacheFrom(pack.ReadyAudio);

            for (int i = 0; i < readyAudio.Length; i++)
            {
                var audioData = readyAudio[i];
                
                if (!audioData.Value.Clip || IsSpecialClip(audioData.Value.Clip.name))
                    continue; // These shouldn't be collected

                var requestingSource = audioData.Value.RequestedBy;
                var canCollectAudioBefore = requestingSource != null
                    ? canCollectReferencedAudioBefore
                    : canCollectUnreferencedAudioBefore;
                
                if (audioData.Value.LastUsed < canCollectAudioBefore)
                {
                    RunGarbageCollection(pack, audioData.Key);
                }
            }
        }
    }

    private static void RunGarbageCollection(AudioPack pack, string clipName)
    {
        // This logic relies on ModAudio explicitly tracking one shot audio
        // Without said tracking, it would be hard to tell whenever audio is actually in use
        var audioData = pack.ReadyAudio[clipName];
        var clip = audioData.Clip;

        bool inUse = false;

        foreach (var source in TrackedSources)
        {
            if (!source.Key)
                continue;

            if (source.Key.clip == clip)
            {
                inUse = true;
                break;
            }
        }

        if (inUse)
        {
            // Reset its last used time so that we avoid checking it for a while
            pack.ReadyAudio[clipName] = audioData with { LastUsed = DateTime.UtcNow };
            return;
        }
        
        // No uses found, clean it up
        
        AudioDebugDisplay.LogEngine(LogLevel.Debug, $"Garbage collecting clip {clipName} from pack {pack.Config.Id}");

        UnityEngine.Object.Destroy(clip);
        
        if (audioData.Stream != null)
        {
            audioData.Stream.Dispose();
            pack.CurrentStreamedClips--;
        }
        else
        {
            pack.CurrentInMemoryClips--;
        }

        pack.ReadyAudio.Remove(clipName);
    }

    /// <summary>
    /// Soft reload resets game / mod audio state, hard reload also reloads packs.
    /// </summary>
    private static void Reload(bool hardReload)
    {
        Watch.Restart();
        
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (AudioEngine.Game == null)
        {
            AudioEngine.Game = new ModAudioGame();

            const string IssuesLink = "https://github.com/Marioalexsan/AtlyssModAudio/issues/";
            
            AudioDebugDisplay.LogEngine(LogLevel.Warning, $"Hey there! It seems like you're running ModAudio on \"{Application.productName}\".");
            AudioDebugDisplay.LogEngine(LogLevel.Warning, $"This game has experimental support at best. Lua scripting might be limited, and no game specific features will be available.");
            AudioDebugDisplay.LogEngine(LogLevel.Warning, $"If you find any issues, bugs, or have constructive feedback, please report them at {IssuesLink}.");
        }
        
        AudioDebugDisplay.LogEngine(LogLevel.Info, "UseSystemAcmCodecs is set to " + ModAudio.UseSystemAcmCodecs.Value);

        try
        {
            AudioDebugDisplay.LogEngine(LogLevel.Info, "Reloading engine! This might take a while...");

            // Reset internal state
            Game.OnReload();

            if (hardReload)
            {
                AudioDebugDisplay.LogEngine(LogLevel.Info, "Clearing loaded vanilla clips...");
                LoadedVanillaClips.Clear();
            }

            // I like cleaning audio sources
            CleanupSources();

            if (hardReload)
            {
                // Get rid of one-shots forcefully

                var trackedSources = ForeachCache<KeyValuePair<AudioSource, ModAudioSource>>.CacheFrom(TrackedSources);
                for (int i = 0; i < trackedSources.Length; i++)
                {
                    var source = trackedSources[i];

                    if (source.Value.HasFlag(AudioFlags.IsDedicatedOneShotSource))
                    {
                        TrackedSources.Remove(source.Key);
                        UnityEngine.Object.Destroy(source.Key);
                    }
                }
            }

            // Restore previous state and unlock if needed
            Dictionary<AudioSource, bool> wasPlayingPreviously = [];

            // Note: I'm fine with FindObjectsOfType here since it's a one time thing per reload
            foreach (var audio in UnityEngine.Object.FindObjectsOfType<AudioSource>(true))
            {
                wasPlayingPreviously[audio] = audio.isPlaying;

                if (wasPlayingPreviously[audio])
                {
                    audio.Stop();
                }

                GetModAudioSourceIfExists(audio)?.ClearFlag(AudioFlags.VolumeLock);
            }

            // Restore original state
            foreach (var source in TrackedSources)
            {
                source.Value.RevertSource();
            }

            TrackedSources.Clear();
            TrackedPlayOnAwakeSourceStates.Clear();
            TrackedOneShots.Clear();

            if (hardReload)
            {
                AudioDebugDisplay.LogEngine(LogLevel.Info, "Reloading mod pack overrides...");
                
                ModpackOverrides.Clear();

                var searchPaths = new Queue<string>(Directory.GetDirectories(Paths.PluginPath));

                while (searchPaths.Count > 0)
                {
                    var folder = searchPaths.Dequeue();

                    foreach (var innerFolder in Directory.GetDirectories(Path.Combine(Paths.PluginPath, folder)))
                        searchPaths.Enqueue(Path.Combine(folder, innerFolder));

                    var modpackOverridePath = Path.Combine(Paths.PluginPath, folder, "modaudio.modpack_overrides.json");

                    if (File.Exists(modpackOverridePath))
                    {
                        try
                        {
                            var overrides = JsonConvert.DeserializeObject<ModpackOverride[]>(File.ReadAllText(modpackOverridePath)) ?? throw new NullReferenceException("Modpack override deserialized to null!");
                            ModpackOverrides.AddRange(overrides);
                            AudioDebugDisplay.LogEngine(LogLevel.Info, $"Loaded modpack overrides from {modpackOverridePath}!");
                        }
                        catch (Exception e)
                        {
                            AudioDebugDisplay.LogEngine(LogLevel.Warning, $"Couldn't load modpack overrides from {modpackOverridePath}!");
                            AudioDebugDisplay.LogEngine(LogLevel.Warning, $"Exception data: {e}");
                        }
                    }
                }
                
                AudioDebugDisplay.LogEngine(LogLevel.Info, "Reloading audio packs...");

                // Clean up packs
                foreach (var pack in AudioPacks)
                    pack.Dispose();

                AudioPacks.Clear();
                AudioPacks.AddRange(AudioPackLoader.LoadAudioPacks());
                ModAudio.InitializePackConfiguration(); // TODO I wish ModAudio plugin ref wouldn't be here
            }

            AudioDebugDisplay.LogEngine(LogLevel.Info, "Restarting audio sources...");

            // Restart audio
            // Note: I'm fine with FindObjectsOfType here since it's a one time thing per reload
            foreach (var audio in UnityEngine.Object.FindObjectsOfType<AudioSource>(true))
            {
                if (wasPlayingPreviously[audio])
                    audio.Play();
            }
        
            // Reset internal state after full reload
            Game.PostReload();
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in {nameof(Reload)}! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
        }

        Watch.Stop();

        AudioDebugDisplay.LogEngine(LogLevel.Info, $"Reloaded engine in {Watch.ElapsedMilliseconds} milliseconds!");
    }

    private static DateTime LastGarbageCollection = DateTime.UtcNow;

    public static void Update()
    {
        if (!ModAudio.CurrentlyEnabled)
            return;

        try
        {
            AudioEngine.Game.OnUpdate();

            if (LastGarbageCollection + TimeSpan.FromSeconds(35) < DateTime.UtcNow)
            {
                LastGarbageCollection = DateTime.UtcNow;
                RunGarbageCollection();
            }
            
            // Handle audio preloading
            for (int i = 0; i < AudioPacks.Count; i++)
            {
                var pack = AudioPacks[i];

                if (!pack.HasFlag(PackFlags.Enabled))
                    continue;
                
                pack.TryHandleNextPreload();
            }

            // Run update scripts first
            for (int i = 0; i < AudioPacks.Count; i++)
            {
                var pack = AudioPacks[i];

                if (!pack.HasFlag(PackFlags.Enabled))
                    continue;

                if (!string.IsNullOrEmpty(pack.Config.PackScripts.Update) && pack.Script != null)
                {
                    using (Profiling.ExecuteUpdate.Auto())
                        pack.Script.ExecuteUpdate();
                }
            }

            DetectNewSources();

            using (Profiling.PlayOnAwakeHandling.Auto())
            {
                var playOnAwakeSources = ForeachCache<KeyValuePair<AudioSource, bool>>.CacheFrom(TrackedPlayOnAwakeSourceStates);
                for (int i = 0; i < playOnAwakeSources.Length; i++)
                {
                    var source = playOnAwakeSources[i].Key;

                    if (source == null)
                        continue;

                    var wasPlaying = playOnAwakeSources[i].Value;

                    if (wasPlaying && !source.isPlaying)
                    {
                        TrackedPlayOnAwakeSourceStates[source] = false;
                        AudioStopped(source, false);
                    }
                    else if (!wasPlaying && source.isPlaying)
                    {
                        TrackedPlayOnAwakeSourceStates[source] = true;
                        AudioPlayed(source);
                    }
                }
            }

            using (Profiling.CleanupSources.Auto())
                CleanupSources();

            using (Profiling.UpdateTargeting.Auto())
                UpdateDynamicTargeting();
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in {nameof(Update)}! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
        }
    }

    internal static void DetectNewSources()
    {
        // Note: This is mainly to detect playOnAwake audio sources that have been played directly by the engine and not via the script API
        // It's not really possible to detect them otherwise; you don't get any notifications via hooks for those.
        // Note that audio that's played via Play() and PlayOneShot() is automatically tracked when played, so this doesn't affect those sources

        var detectionSpeed = ModAudio.SourceDetectionRate.Value switch
        {
            SourceDetectionRate.Realtime => TimeSpan.Zero,
            SourceDetectionRate.Fast => TimeSpan.FromMilliseconds(100),
            SourceDetectionRate.Medium => TimeSpan.FromMilliseconds(500),
            _ => TimeSpan.FromMilliseconds(2500),
        };

        if (DateTime.Now - LastSourceFetch < detectionSpeed)
            return;

        LastSourceFetch = DateTime.Now;

        using (Profiling.DetectNewSources.Auto())
        {
            var activeSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < activeSources.Length; i++)
                _ = GetOrCreateModAudioSource(activeSources[i]);
        }
    }

    private static void UpdateDynamicTargeting()
    {
        CachedRoutingTargetGroupData.Clear();
        var trackedSources = ForeachCache<KeyValuePair<AudioSource, ModAudioSource>>.CacheFrom(TrackedSources);
        for (int i = 0; i < trackedSources.Length; i++)
        {
            var source = trackedSources[i].Value;

            if (source.Audio != null && source.HasFlag(AudioFlags.ShouldUpdateDynamicTargeting))
            {
                // Disable dynamic targeting in case something wrong happened
                if (!UpdateDynamicTargeting(source))
                    source.ClearFlag(AudioFlags.ShouldUpdateDynamicTargeting);
            }
        }
        CachedRoutingTargetGroupData.Clear();
    }

    private static bool UpdateDynamicTargeting(ModAudioSource source)
    {
        if (source.Audio.isPlaying)
        {
            if (!Mathf.Approximately(source.ProxiedPitch, 0))
                source.DynamicTargetingPlayPosition += TimeSpan.FromSeconds(Time.deltaTime / source.ProxiedPitch);
            else if (source.Audio.clip != null)
                source.DynamicTargetingPlayPosition = TimeSpan.FromSeconds((float)source.Audio.timeSamples / source.Audio.clip.frequency);
            else
                source.DynamicTargetingPlayPosition = TimeSpan.FromSeconds(source.Audio.time);
        }
        
        bool groupsMismatched = false;
        bool useSmoothing = false;
        bool useContinuousPlaying = false;
        bool forcePlay = false;

        for (int i = 0; i < source.RouteCount; i++)
        {
            var routeData = source.GetRoute(i);

            // Use smoothing if any routes demand it
            useSmoothing = useSmoothing || routeData.Route.SmoothDynamicTargeting;
            useContinuousPlaying = useContinuousPlaying || routeData.Route.ContinuousDynamicTargeting;
            forcePlay = routeData.Route.ForcePlay; // should take the value of the last route in the chain

            if (routeData.Route.EnableDynamicTargeting)
            {
                var routeApi = GetCachedTargetGroup(source, routeData.AudioPack, routeData.Route);

                if (routeApi.SkipRoute || routeApi.TargetGroup != routeData.TargetGroup)
                {
                    groupsMismatched = true;
                }
            }
        }

        const bool UseVolumeLock = true;

        if (groupsMismatched)
        {
            bool shouldSwitch = false;

            if (useSmoothing)
            {
                source.SetFlag(AudioFlags.IsSwappingTargets);
                source.ClearFlag(AudioFlags.VolumeLock);

                var newVolume = Mathf.Lerp(source.Audio.volume, 0f, Time.deltaTime * 4f);
                source.Audio.volume = newVolume;

                source.AssignFlag(AudioFlags.VolumeLock, UseVolumeLock);

                if (newVolume <= 0.05f)
                {
                    shouldSwitch = true;
                }
            }
            else
            {
                shouldSwitch = true;
            }

            if (shouldSwitch)
            {
                bool wasPlaying = source.Audio.isPlaying;
                source.Audio.Stop();

                Route(source, true);
                source.LogDebugDisplay();
                
                if (useContinuousPlaying && source.Audio.clip != null)
                    source.Audio.time = (float)(source.DynamicTargetingPlayPosition.TotalSeconds % source.Audio.clip.length);

                // This can happen if a script uses skipRoute and kills the route as a result!
                bool newRouteIsStillDynamic = source.HasFlag(AudioFlags.ShouldUpdateDynamicTargeting);

                if (newRouteIsStillDynamic)
                {
                    if (useSmoothing)
                    {
                        // TODO: Clearing these flags manually causes user error. Write methods on ModAudioSource that can
                        // TODO: bypass the volume / pitch / etc. locks
                        source.ClearFlag(AudioFlags.VolumeLock);
                        source.Audio.volume = 0f;
                        source.SetFlag(AudioFlags.IsSwappingTargets);
                        source.AssignFlag(AudioFlags.VolumeLock, UseVolumeLock);
                    }
                }
                else
                {
                    // Clear the volume lock to avoid breaking the new route since it won't be checked anymore
                    source.ClearFlag(AudioFlags.VolumeLock);
                }
                
                if (wasPlaying || forcePlay)
                    source.PlayWithoutRouting();
            }
        }
        else if (source.HasFlag(AudioFlags.IsSwappingTargets))
        {
            source.ClearFlag(AudioFlags.VolumeLock);

            var newVolume = Mathf.Lerp(source.Audio.volume, source.AppliedState.Volume, Time.deltaTime * 4f);
            source.Audio.volume = newVolume;

            if (Math.Abs(source.Audio.volume - source.AppliedState.Volume) <= 0.05f)
            {
                source.Audio.volume = source.AppliedState.Volume;
                source.ClearFlag(AudioFlags.IsSwappingTargets);
            }
            else
            {
                source.AssignFlag(AudioFlags.VolumeLock, UseVolumeLock);
            }
        }

        return true;
    }

    private static void CleanupSources()
    {
        // Cleanup dead play on awake sounds
        var trackedPlayOnAwake = ForeachCache<KeyValuePair<AudioSource, bool>>.CacheFrom(TrackedPlayOnAwakeSourceStates);
        for (int i = 0; i < trackedPlayOnAwake.Length; i++)
        {
            var source = trackedPlayOnAwake[i].Key;

            if (source == null)
                TrackedPlayOnAwakeSourceStates.Remove(source!);
        }

        // Cleanup dead one shots
        var trackedOneShots = ForeachCache<ModAudioSource>.CacheFrom(TrackedOneShots);
        for (int i = 0; i < trackedOneShots.Length; i++)
        {
            var source = trackedOneShots[i];

            if (source == null)
                TrackedOneShots.Remove(source!);
        }

        // Cleanup stale stuff
        var trackedSources = ForeachCache<KeyValuePair<AudioSource, ModAudioSource>>.CacheFrom(TrackedSources);
        for (int i = 0; i < trackedSources.Length; i++)
        {
            var source = trackedSources[i];

            if (source.Key == null)
            {
                TrackedSources.Remove(source.Key!);
            }
            else if (source.Value.HasFlag(AudioFlags.IsDedicatedOneShotSource) && !source.Key.isPlaying)
            {
                AudioStopped(source.Key, false);
                TrackedSources.Remove(source.Key);
                UnityEngine.Object.Destroy(source.Key);
            }
        }
    }

    private static ModAudioSource? GetModAudioSourceIfExists(AudioSource source)
    {
        if (TrackedSources.TryGetValue(source, out var state))
            return state;

        return null;
    }

    internal static ModAudioSource GetOrCreateModAudioSource(AudioSource source)
    {
        if (TrackedSources.TryGetValue(source, out var state))
            return state;

        TrackedSources.Add(source, state = new(source)
        {
            InitialState =
            {
                Clip = source.clip,
                Pitch = source.pitch,
                Loop = source.loop,
                Volume = source.volume
            },
            LastUnproxiedVolume = source.volume,
            LastUnproxiedPitch = source.pitch
        });

        if (state.Audio.playOnAwake)
            TrackedPlayOnAwakeSourceStates.Add(source, false); // Assume we haven't ran AudioPlayed() for this

        state.AppliedState = state.InitialState;
        
        return state;
    }

    private static ModAudioSource CreateOneShotFromSource(ModAudioSource state, AudioClip oneShotClip)
    {
        // Casualties: Unknown already has its own one shot system
        // Its sound tracking system will break if we try to mess with it
        // TODO: Move this to a dedicates Games/CasualtiesUnknown project
        bool originalSourceIsAlreadyDedicated = Application.productName == "CasualtiesUnknown";

        ModAudioSource createdState;
        
        if (originalSourceIsAlreadyDedicated)
        {
            createdState = state;
        }
        else
        {
            GameObject targetObject = state.Audio.gameObject;

            // Note: some sound effects are played on particle systems that disable themselves after they're played
            // We need to check if that is the case, and move the target object somewhere higher in the hierarchy
            // Unfortunately there's no API to check if the particle system actually has stop behaviour set to disable

            int parentsToGoThrough = 3;

            do
            {
                var particleSystem = targetObject.GetComponent<ParticleSystem>();

                if (particleSystem == null)
                    break;

                if (targetObject.transform.parent == null)
                    break;

                targetObject = targetObject.transform.parent.gameObject;
            }
            while (parentsToGoThrough-- > 0);

            var oneShotSource = state.Audio.CreateCloneOnTarget(targetObject);

            oneShotSource.playOnAwake = false; // This should be false for one shot activeSources, but whatever
            oneShotSource.loop = false; // Otherwise this won't play one-shot
            
            createdState = GetOrCreateModAudioSource(oneShotSource);
        }

        createdState.SetFlag(AudioFlags.IsDedicatedOneShotSource | AudioFlags.OneShotStopsIfSourceStops);
        createdState.OneShotOrigin = state.Audio;

        TrackedOneShots.Add(createdState); // Also track these separately for performance reasons

        createdState.Audio.clip = oneShotClip;
        createdState.InitialState.Clip = oneShotClip;

        return createdState;
    }

    public static bool OneShotClipPlayed(AudioClip clip, AudioSource source, float volumeScale)
    {
        if (!ModAudio.CurrentlyEnabled)
            return true;

        try
        {
            // Move to a dedicated audio source for better control. Note: This is likely overkill and might mess with other mods?

            var oneShot = CreateOneShotFromSource(GetOrCreateModAudioSource(source), clip);
            oneShot.Audio.volume *= volumeScale;
            oneShot.InitialState.Volume = oneShot.Audio.volume;

            oneShot.Audio.Play();

            return false;
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in {nameof(OneShotClipPlayed)}! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"AudioSource that caused the crash:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"  name = {source?.name ?? "(null)"}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"  clip = {source?.clip?.name ?? "(null)"}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"AudioClip that caused the crash:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"  name = {clip?.name ?? "(null)"}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Parameter {nameof(volumeScale)} was: {volumeScale}");
            return true;
        }
    }

    public static bool AudioPlayed(AudioSource source)
    {
        if (!ModAudio.CurrentlyEnabled)
            return true;

        try
        {
            var state = GetOrCreateModAudioSource(source);

            var wasPlaying = state.Audio.isPlaying;

            using (Profiling.Routing.Auto())
                Route(state, false);
            
            bool requiresRestart = wasPlaying && !state.Audio.isPlaying;
            
            if (!requiresRestart) // If we were to restart, then logging here would cause an additional log line
                state.LogDebugDisplay();

            state.ClearFlag(AudioFlags.WasStoppedOrDisabled);

            if (requiresRestart)
                state.PlayWithoutRouting();

            // If a restart was required, then we already played the sound
            // so let's skip the original if this is called as part of the AudioSource.Play() hooks
            return !requiresRestart;
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in {nameof(AudioPlayed)}! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"AudioSource that caused the crash:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"  name = {source?.name ?? "(null)"}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"  clip = {source?.clip?.name ?? "(null)"}");
            return true;
        }
    }

    public static bool AudioStopped(AudioSource source, bool stopOneShots)
    {
        if (!ModAudio.CurrentlyEnabled)
            return true;

        try
        {
            // Do not track sources here
            var state = GetModAudioSourceIfExists(source);

            if (stopOneShots)
            {
                var stoppedSource = source;

                var trackedOneShots = ForeachCache<ModAudioSource>.CacheFrom(TrackedOneShots);
                bool cacheCloned = false;

                for (int i = 0; i < trackedOneShots.Length; i++)
                {
                    var trackedSource = trackedOneShots[i];

                    if (!trackedSource.HasFlag(AudioFlags.IsDedicatedOneShotSource | AudioFlags.OneShotStopsIfSourceStops))
                        continue;

                    if (trackedSource.OneShotOrigin != stoppedSource || trackedSource.Audio == null)
                        continue;

                    if (trackedSource.Audio.isPlaying)
                    {
                        // Calling Stop() on an audio source has a chance to enter this code block again, triggering CacheFrom again
                        // This means that we need to clone the one shots we're iterating over in here, otherwise we risk destroying
                        // the references that were used earlier in the call stack (CacheFrom uses a static cache, so you need to be
                        // "done" with it before attempting to use it again)
                        // TODO: This is smelly and bad code. Is it possible to do this copy in a simpler, more intuitive way?
                        // NOTE: This generates GC allocs, not much I can do about it though!
                        if (!cacheCloned)
                        {
                            cacheCloned = true;
                            trackedOneShots = trackedOneShots.ToArray();
                        }

                        trackedSource.Audio.Stop();
                    }
                }
            }

            if (source.playOnAwake)
                TrackedPlayOnAwakeSourceStates.Remove(source);

            if (state != null)
            {
                if (state.HasFlag(AudioFlags.IsDedicatedOneShotSource))
                    TrackedOneShots.Remove(state);

                state.SetFlag(AudioFlags.WasStoppedOrDisabled);
            }

            return true;
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"ModAudio crashed in {nameof(AudioStopped)}! Please report this error to the mod developer:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Exception data: {e}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"AudioSource that caused the crash:");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"  name = {source?.name ?? "(null)"}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"  clip = {source?.clip?.name ?? "(null)"}");
            AudioDebugDisplay.LogEngine(LogLevel.Error, $"Parameter {nameof(stopOneShots)} was: {stopOneShots}");
            return true;
        }
    }

    private static TargetGroupData GetCachedTargetGroup(ModAudioSource source, AudioPack pack, Route route)
    {
        if (!CachedRoutingTargetGroupData.TryGetValue(route, out var routeData))
        {
            routeData = new TargetGroupData()
            {
                Source = source
            };

            if (pack.Script != null)
            {
                using (Profiling.ExecuteTargetGroups.Auto())
                    pack.Script.ExecuteTargetGroup(route, routeData);
            }
            
            CachedRoutingTargetGroupData[route] = routeData;
        }

        return routeData;
    }

    private static bool MatchesSource(ModAudioSource source, AudioStepState state, Route route)
    {
        var clipNameToMatch = state.Clip?.name;

        if (clipNameToMatch == null)
            return false;

        if (route.MapNameCondition.Count > 0)
        {
            bool matchesMap = false;
            string? currentMap = AudioEngine.Game.Specialized_GetMapName();
            
            for (int i = 0; i < route.MapNameCondition.Count; i++)
            {
                var mapName = route.MapNameCondition[i];

                if (string.IsNullOrWhiteSpace(mapName))
                    continue;
                
                if (currentMap == null && mapName == "___nomap___")
                {
                    matchesMap = true;
                    break;
                }
                
                if (currentMap != null && mapName == currentMap)
                {
                    matchesMap = true;
                    break;
                }
            }
            
            if (!matchesMap)
                return false;
        }
        

        for (int i = 0; i < route.OriginalClips.Count; i++)
        {
            if (route.OriginalClips[i] == clipNameToMatch)
            {
                return true;
            }
        }

        for (int i = 0; i < route.OriginalClipAliases.Count; i++)
        {
            if (AudioEngine.Game.MatchesAlias(source, route.OriginalClipAliases[i]))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Applies full routing to an audio source.
    /// If an audio source was routed previously, it is restored to its original state before routing, effectively applying a new selectedRoute.
    /// </summary>
    /// <param name="source">The audio source to selectedRoute</param>
    /// <param name="reuseOldRoutesIfPossible">Reuse previously exiting routes if they still apply somewhat?</param>
    /// <param name="skipOverlays">True to skip overlays, false (default) to play them.</param>
    /// <returns>True if the source was rerouted or modified, false otherwise (for example when there are no routes defined)</returns>
    internal static bool Route(ModAudioSource source, bool reuseOldRoutesIfPossible, bool skipOverlays = false)
    {
        if (source.HasFlag(AudioFlags.DisableRouting))
            return false; // Do not apply changes

        using var routingScope = Profiling.Routing.Auto();

        CachedRoutingTargetGroupData.Clear();

        int currentChainRoutes = 0;

        var previousSteps = source.RouteCount > 0 ? new RouteStep[source.RouteCount] : [];

        for (int i = 0; i < source.RouteCount; i++)
            previousSteps[i] = source.GetRoute(i);

        source.RevertSource();

        var wasRouted = false;

        while (currentChainRoutes++ < MaxChainRoutes)
        {
            RouteStep? preferredStep = reuseOldRoutesIfPossible && currentChainRoutes <= previousSteps.Length ? previousSteps[currentChainRoutes - 1] : null;

            var stateBeforeRouting = source.AppliedState;

            bool replacementApplied;

            using (Profiling.RoutingReplacements.Auto())
                replacementApplied = ExecuteRouteStep(source, preferredStep.HasValue ? (preferredStep.Value.AudioPack, preferredStep.Value.Route) : null);

            if (source.HasFlag(AudioFlags.HasEncounteredErrors))
                break; // Stop further routing or overlays

            if (!skipOverlays)
            {
                using (Profiling.RoutingOverlays.Auto())
                    PlayOverlays(source, stateBeforeRouting, source.LatestRoute?.Route);
            }

            if (!replacementApplied)
                break;

            if (source.LatestRoute?.SelectedClip == EmptyClip)
                break; // Can't route this any further

            wasRouted = true;

            if (source.RouteCount != currentChainRoutes || !source.GetRoute(currentChainRoutes - 1).Route.UseChainRouting)
                break;

            if (currentChainRoutes >= MaxChainRoutes)
            {
                AudioDebugDisplay.LogEngine(LogLevel.Warning, $"An audio source route ran into the max chain routing limit ({MaxChainRoutes})! Stopping routing...");
                break;
            }
        }

        CachedRoutingTargetGroupData.Clear();

        return wasRouted;
    }

    private static void PlayOverlay(ModAudioSource source, AudioPack pack, Route route)
    {
        List<ClipSelection> groupOverlays = [];
        
        var routeApi = GetCachedTargetGroup(source, pack, route);

        for (int i = 0; i < route.OverlayClips.Count; i++)
        {
            var overlay = route.OverlayClips[i];

            if (routeApi.TargetGroup == TargetGroupAll || overlay.Group == routeApi.TargetGroup)
                groupOverlays.Add(overlay);
        }

        if (groupOverlays.Count == 0)
            return;
        
        var randomSelection = Utils.SelectRandomWeighted(RNG, groupOverlays);

        if (randomSelection.Name == EmptyClipKeyword || randomSelection.Name == DisableClipKeyword)
            return;

        AudioClip? destinationClip;
        
        if (IsVanillaClip(randomSelection.Name, out var vanillaClipName)) // For backwards compatibility
        {
            destinationClip = LoadVanillaClip(vanillaClipName);
        }
        else
        {
            destinationClip = pack.LoadClip(randomSelection.Name, source.Audio);
        }

        if (destinationClip != null)
        {
            var oneShot = CreateOneShotFromSource(source, destinationClip);

            oneShot.Audio.volume = randomSelection.Volume;
            oneShot.Audio.pitch = randomSelection.Pitch;

            if (route.RelativeOverlayEffects)
            {
                oneShot.Audio.volume *= oneShot.InitialState.Volume;
                oneShot.Audio.pitch *= oneShot.InitialState.Pitch;
            }

            // Use an override if specified
            if (route.ForceLoop.HasValue)
            {
                oneShot.SetFlag(AudioFlags.LoopWasForced);
                oneShot.Audio.loop = route.ForceLoop.Value;
            }
            else
            {
                source.ClearFlag(AudioFlags.LoopWasForced);
                source.Audio.loop = source.InitialState.Loop;
            }

            oneShot.AppliedState.Clip = oneShot.Audio.clip;
            oneShot.AppliedState.Volume = oneShot.Audio.volume;
            oneShot.AppliedState.Pitch = oneShot.Audio.pitch;
            oneShot.AppliedState.Loop = oneShot.Audio.loop;
            oneShot.SetFlag(AudioFlags.IsOverlay | AudioFlags.DisableRouting);
            oneShot.AssignFlag(AudioFlags.OneShotStopsIfSourceStops, route.OverlayStopsIfSourceStops);

            oneShot.Audio.Play();
        }
        else
        {
            AudioDebugDisplay.LogPack(LogLevel.Warning, pack, $"Couldn't get clip {randomSelection.Name} to play for overlay!");
            pack.SetFlag(PackFlags.HasEncounteredErrors);
            source.SetFlag(AudioFlags.HasEncounteredErrors);
        }
    }

    private static void PlayOverlays(ModAudioSource source, AudioStepState stateBeforeRouting, Route? replacementRoute)
    {
        // Note: Overlays should not be able to trigger other overlays
        // Otherwise you can easily create infinite loops

        List<(AudioPack Pack, Route Route)> overlays = [];

        using (Profiling.SourceMatching.Auto())
        {
            for (int i = 0; i < AudioPacks.Count; i++)
            {
                var pack = AudioPacks[i];

                if (!pack.HasFlag(PackFlags.Enabled))
                    continue;

                var routes = pack.Config.Routes;

                for (int k = 0; k < routes.Count; k++)
                {
                    var route = routes[k];

                    if (route.OverlaysIgnoreRestarts && !(source.HasFlag(AudioFlags.WasStoppedOrDisabled) || !source.Audio.isPlaying))
                        continue;
                
                    if (!(route.OverlayClips.Count > 0 && MatchesSource(source, stateBeforeRouting, route) && (!route.LinkOverlayAndReplacement || route.ReplacementClips.Count == 0 || replacementRoute == route)))
                        continue;

                    var routeApi = GetCachedTargetGroup(source, pack, route);

                    if (!routeApi.SkipRoute)
                        overlays.Add((pack, route));
                }
            }
        }

        for (int i = 0; i < overlays.Count; i++)
        {
            var (pack, route) = overlays[i];

            PlayOverlay(source, pack, route);
        }
    }

    // Extremely basic method, all things considered
    // This should only be used to preload audio detected after a scene load due to performance considerations
    // TODO: rewrite preloading! this sucks!!!!
    internal static void TryPreloadSceneClips()
    {
        var trackedSources = ForeachCache<KeyValuePair<AudioSource, ModAudioSource>>.CacheFrom(TrackedSources);
        for (int i = 0; i < trackedSources.Length; i++)
            TryPreloadClips(trackedSources[i].Value);
    }
    
    private static void TryPreloadClips(ModAudioSource source)
    {
        var initialClip = source.InitialState.Clip?.name;

        if (initialClip == null)
            return;
        
        // Get a replacement from routes
        for (int packIndex = 0; packIndex < AudioPacks.Count; packIndex++)
        {
            var pack = AudioPacks[packIndex];

            if (!pack.HasFlag(PackFlags.Enabled))
                continue;

            var routes = pack.Config.Routes;

            for (int k = 0; k < routes.Count; k++)
            {
                var route = routes[k];

                bool matches = false;

                if (route.OriginalClips.Contains(initialClip))
                {
                    matches = true;
                }
                else
                {
                    for (int aliasIndex = 0; aliasIndex < route.OriginalClipAliases.Count; aliasIndex++)
                    {
                        if (AudioEngine.Game.MatchesAlias(source, route.OriginalClipAliases[aliasIndex]))
                        {
                            matches = true;
                            break;
                        }
                    }
                }

                if (matches)
                {
                    for (int clipIndex = 0; clipIndex < route.ReplacementClips.Count; clipIndex++)
                        pack.QueuePreload(route.ReplacementClips[clipIndex].Name, source.Audio);
                    
                    for (int clipIndex = 0; clipIndex < route.OverlayClips.Count; clipIndex++)
                        pack.QueuePreload(route.OverlayClips[clipIndex].Name, source.Audio);
                }
            }
        }
    }

    private static bool ExecuteRouteStep(ModAudioSource source, (AudioPack Pack, Route Route)? preferredPackRoute)
    {
        if (source.HasFlag(AudioFlags.DisableRouting))
            return false;
        
        static void AddReplacementIfEligible(ModAudioSource source, AudioPack pack, Route route, List<(AudioPack Pack, Route Route)> replacements)
        {
            if (route.ReplacementClips.Count == 0)
                return; // Nothing to do here!
            
            if (!MatchesSource(source, source.AppliedState, route))
                return;
            
            bool isAlreadyPresentInChain = false;

            // Disallow selecting routes that are already present in the chain
            for (int i = 0; i < source.RouteCount; i++)
            {
                if (source.GetRoute(i).Route == route)
                {
                    isAlreadyPresentInChain = true;
                    break;
                }
            }

            if (isAlreadyPresentInChain)
                return;

            var thisRouteApi = GetCachedTargetGroup(source, pack, route);

            if (!thisRouteApi.SkipRoute)
                replacements.Add((pack, route));
        }

        // Get a replacement from routes
        CachedReplacementSelectionList.Clear();

        using (Profiling.SourceMatching.Auto())
        {
            if (preferredPackRoute != null)
                AddReplacementIfEligible(source, preferredPackRoute.Value.Pack, preferredPackRoute.Value.Route, CachedReplacementSelectionList);

            // Check replacements if there is no preferred route - or if it was skipped for some reason
            if (CachedReplacementSelectionList.Count == 0)
            {
                for (int i = 0; i < AudioPacks.Count; i++)
                {
                    var pack = AudioPacks[i];

                    if (!pack.HasFlag(PackFlags.Enabled))
                        continue;

                    var routes = pack.Config.Routes;

                    for (int k = 0; k < routes.Count; k++)
                    {
                        var route = routes[k];

                        if (preferredPackRoute != null && route == preferredPackRoute.Value.Route)
                            continue; // Checked earlier

                        AddReplacementIfEligible(source, pack, route, CachedReplacementSelectionList);
                    }
                }
            }
        }
        
        if (CachedReplacementSelectionList.Count == 0)
            return false;

        if (source.RouteCount >= ModAudioSource.MaxChainedRoutes)
        {
            // This is a sanity check, normally this should be prevented earlier in the call stack
            // TODO: Is this really needed? It's already checked higher up the call stack
            AudioDebugDisplay.LogEngine(LogLevel.Warning, "Tried to route an audio source that has reached max chained routes! Aborting routing operation. Please notify the mod developer about this!");
            CachedReplacementSelectionList.Clear();
            return false;
        }

        var (selectedPack, selectedRoute) = Utils.SelectRandomWeighted(RNG, CachedReplacementSelectionList);
        CachedReplacementSelectionList.Clear();

        // Apply overall effects

        source.Audio.volume = selectedRoute.Volume;
        source.Audio.pitch = selectedRoute.Pitch;

        if (selectedRoute.RelativeReplacementEffects)
        {
            source.Audio.volume *= source.InitialState.Volume;
            source.Audio.pitch *= source.InitialState.Pitch;
        }

        // Use an override if specified
        if (selectedRoute.ForceLoop.HasValue)
        {
            source.SetFlag(AudioFlags.LoopWasForced);
            source.Audio.loop = selectedRoute.ForceLoop.Value;
        }
        else
        {
            source.ClearFlag(AudioFlags.LoopWasForced);
            source.Audio.loop = source.InitialState.Loop;
        }

        source.AppliedState.Volume = source.Audio.volume;
        source.AppliedState.Pitch = source.Audio.pitch;
        source.AppliedState.Loop = source.Audio.loop;

        var routeApi = GetCachedTargetGroup(source, selectedPack, selectedRoute);

        // Apply replacement if needed

        List<ClipSelection> groupReplacements = [];

        for (int i = 0; i < selectedRoute.ReplacementClips.Count; i++)
        {
            var replacement = selectedRoute.ReplacementClips[i];

            if (routeApi.TargetGroup == TargetGroupAll || replacement.Group == routeApi.TargetGroup)
                groupReplacements.Add(replacement);
        }

        AudioClip? destinationClip;

        if (groupReplacements.Count == 0)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, selectedPack, $"Couldn't get any replacement clips to use for group {routeApi.TargetGroup}!");
            selectedPack.SetFlag(PackFlags.HasEncounteredErrors);
            source.SetFlag(AudioFlags.HasEncounteredErrors);
            destinationClip = ErrorClip;
        }
        else
        {
            var randomSelection = Utils.SelectRandomWeighted(RNG, groupReplacements);

            if (randomSelection.Name == DefaultClipKeyword)
            {
                destinationClip = source.AppliedState.Clip;
            }
            else if (randomSelection.Name == EmptyClipKeyword)
            {
                destinationClip = EmptyClip;
            }
            else if (randomSelection.Name == DisableClipKeyword)
            {
                destinationClip = DisableClip;
            }
            else if (IsVanillaClip(randomSelection.Name, out var vanillaClipName))
            {
                destinationClip = LoadVanillaClip(vanillaClipName);
            }
            else
            {
                destinationClip = selectedPack.LoadClip(randomSelection.Name, source.Audio);
            }

            if (destinationClip != null)
            {
                source.Audio.volume *= randomSelection.Volume;
                source.Audio.pitch *= randomSelection.Pitch;

                source.AppliedState.Clip = destinationClip;
                source.AppliedState.Volume = source.Audio.volume;
                source.AppliedState.Pitch = source.Audio.pitch;
                source.AppliedState.Loop = source.Audio.loop;

                if (source.Audio.clip != destinationClip && source.Audio.isPlaying)
                    source.Audio.Stop();

                source.Audio.clip = destinationClip;

                if (selectedRoute.EnableDynamicTargeting)
                    source.SetFlag(AudioFlags.ShouldUpdateDynamicTargeting);
            }
            else
            {
                AudioDebugDisplay.LogPack(LogLevel.Error, selectedPack, $"Couldn't get clip {randomSelection.Name} to play for replacement!");
                selectedPack.SetFlag(PackFlags.HasEncounteredErrors);
                source.SetFlag(AudioFlags.HasEncounteredErrors);
                destinationClip = ErrorClip;
            }
        }

        source.PushRoute(selectedPack, selectedRoute, routeApi.TargetGroup, destinationClip);

        return true;
    }

    private static AudioClip? LoadVanillaClip(string clipName)
    {
        // Try to load a vanilla clip
        if (!LoadedVanillaClips.TryGetValue(clipName, out var destinationClip) || destinationClip == null)
        {
            if (Game.TryLoadVanillaClip(clipName, out destinationClip))
                LoadedVanillaClips[clipName] = destinationClip;
        }

        return destinationClip;
    }
}
