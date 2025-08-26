using BepInEx.Logging;
using Jint;
using Jint.Native.Function;
using Marioalexsan.ModAudio.HarmonyPatches;
using Marioalexsan.ModAudio.Scripting;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Audio;

namespace Marioalexsan.ModAudio;

internal static class AudioEngine
{
    private static readonly ProfilerMarker _routing = new ProfilerMarker("ModAudio Route()");
    private static readonly ProfilerMarker _playRouting = new ProfilerMarker("ModAudio Route() from AudioPlayed()");
    private static readonly ProfilerMarker _fetchSources = new ProfilerMarker("ModAudio Update() fetch active sources");
    private static readonly ProfilerMarker _innerLoop = new ProfilerMarker("ModAudio Update() inner loop");
    private static readonly ProfilerMarker _executeUpdate = new ProfilerMarker("ModAudio Update() execute script updates");
    private static readonly ProfilerMarker _executeTargetGroups = new ProfilerMarker("ModAudio Update() execute script target groups");
    private static readonly ProfilerMarker _cleanupSources = new ProfilerMarker("ModAudio Update() cleanup sources");
    private static readonly ProfilerMarker _updateTargeting = new ProfilerMarker("ModAudio Update() update targeting");

    public const string DefaultClipKeyword = "___default___";
    public const string EmptyClipKeyword = "___nothing___";
    public const string ErrorClipKeyword = "!!! error !!!";

    public const string TargetGroupAll = "all";

    internal static Jint.Engine ScriptEngine = ScriptingEngine.SetupJint();

    private const int MaxChainRoutes = 4;

    private static readonly System.Random RNG = new();
    private static readonly System.Diagnostics.Stopwatch Watch = new();

    public static readonly Dictionary<AudioSource, ModAudioSource> TrackedSources = new(8192);
    public static readonly HashSet<AudioSource> TrackedPlayOnAwakeSources = [];
    public static readonly HashSet<ModAudioSource> TrackedOneShots = [];

    public static List<AudioPack> AudioPacks { get; } = [];
    public static IEnumerable<AudioPack> EnabledPacks => AudioPacks.Where(x => x.HasFlag(PackFlags.Enabled));

    public static Dictionary<string, AudioClip> LoadedVanillaClips = [];
    public static Dictionary<string, AudioMixerGroup> LoadedMixerGroups = [];

    public static AudioPack? CurrentlyCalledScriptPack { get; private set; }

    // Temporary data for routing
    private static readonly Dictionary<Route, TargetGroupRouteAPI> CachedRoutingTargetGroupData = [];

    private static AudioClip EmptyClip
    {
        get
        {
            // Setting this too low might cause it to fail for playOnAwake activeSources
            // This is due to the detection method in Update(), which relies on scanning audio activeSources every frame
            // This is why we need to use a minimum size (a few game frames at least).

            const int EmptyClipSizeInSamples = 16384; // 0.37 seconds
            return _emptyClip ??= AudioClipLoader.GenerateEmptyClip(EmptyClipKeyword, EmptyClipSizeInSamples);
        }
    }
    private static AudioClip? _emptyClip;

    // This is not an "actual" clip, but should be used to
    // 1. Avoid null references, and
    // 2. Indicate that an error state was encountered (clip failed to load, etc.)
    private static AudioClip ErrorClip
    {
        get
        {
            const int EmptyClipSizeInSamples = 16384; // 0.37 seconds
            return _errorClip ??= AudioClipLoader.GenerateEmptyClip(EmptyClipKeyword, EmptyClipSizeInSamples);
        }
    }
    private static AudioClip? _errorClip;

    public static bool IsVolumeLocked(AudioSource source) => GetModAudioSourceIfExists(source)?.HasFlag(AudioFlags.VolumeLock) ?? false;
    public static bool IsPitchLocked(AudioSource source) => GetModAudioSourceIfExists(source)?.HasFlag(AudioFlags.PitchLock) ?? false;
    public static bool IsLoopLocked(AudioSource source) => GetModAudioSourceIfExists(source)?.HasFlag(AudioFlags.LoopLock) ?? false;

    public static void HardReload() => Reload(hardReload: true);

    public static void SoftReload() => Reload(hardReload: false);

    private static void Reload(bool hardReload)
    {
        Watch.Restart();

        try
        {
            Logging.LogInfo("Reloading engine! this might take a while...");

            // Reset internal state
            MapInstance_Handle_AudioSettings.ForceCombatMusic = false;

            if (hardReload)
            {
                Logging.LogInfo("Reloading scripting engine...");
                ScriptEngine?.Dispose();
                ScriptEngine = ScriptingEngine.SetupJint();

                Logging.LogInfo("Clearing loaded vanilla clips...");
                LoadedVanillaClips.Clear();

                LoadedMixerGroups.Clear();

                LoadedMixerGroups = SettingsManager._current._masterMixer.FindMatchingGroups("").ToDictionary(x => x.name.ToLower());
            }

            // I like cleaning audio sources
            CleanupSources();

            if (hardReload)
            {
                // Get rid of one-shots forcefully

                Utils.CachedForeach(
                    TrackedSources,
                    static (in KeyValuePair<AudioSource, ModAudioSource> source) =>
                    {
                        if (source.Value.HasFlag(AudioFlags.IsDedicatedOneShotSource))
                        {
                            TrackedSources.Remove(source.Key);
                            UnityEngine.Object.Destroy(source.Key);
                        }
                    }
                );
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

            if (hardReload)
            {
                Logging.LogInfo("Reloading audio packs...");

                // Clean up handles from streams
                foreach (var pack in AudioPacks)
                {
                    foreach (var handle in pack.OpenStreams)
                        handle.Dispose();
                }

                AudioPacks.Clear();
                AudioPacks.AddRange(AudioPackLoader.LoadAudioPacks());
                ModAudio.Plugin.InitializePackConfiguration(); // TODO I wish ModAudio plugin ref wouldn't be here
            }

            Logging.LogInfo("Preloading audio data...");
            foreach (var pack in AudioPacks)
            {
                if (pack.HasFlag(PackFlags.Enabled) && pack.PendingClipsToLoad.Count > 0)
                {
                    // If a selectedPack is enabled, we should preload all of the in-memory clips
                    // Opening a ton of streams at the start is not great though, so those remain on-demand

                    var clipsToPreload = pack.PendingClipsToLoad.Keys.ToArray();

                    foreach (var clip in clipsToPreload)
                    {
                        _ = pack.TryGetReadyClip(clip, out _);
                    }
                }
            }

            Logging.LogInfo("Restarting audio sources...");

            // Restart audio
            // Note: I'm fine with FindObjectsOfType here since it's a one time thing per reload
            foreach (var audio in UnityEngine.Object.FindObjectsOfType<AudioSource>(true))
            {
                if (wasPlayingPreviously[audio])
                    audio.Play();
            }
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(Reload)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
        }

        Watch.Stop();

        Logging.LogInfo($"Done with reload! Reload took {Watch.ElapsedMilliseconds} milliseconds.");
    }

    public static void Update()
    {
        try
        {
            // Run update scripts first

            foreach (var pack in EnabledPacks)
            {
                if (!string.IsNullOrEmpty(pack.Config.PackScripts.Update))
                {
                    _executeUpdate.Begin();
                    ExecuteUpdate(pack);
                    _executeUpdate.End();
                }
            }

            // Check play on awake activeSources

            // This is to detect playOnAwake audio activeSources that have been played
            // directly by the engine and not via the script API

            _innerLoop.Begin();

            Utils.CachedForeach(
                TrackedPlayOnAwakeSources,
                (in AudioSource source) =>
                {
                    if (!source.IsNullOrDestroyed() && !source.isActiveAndEnabled && !source.isPlaying)
                        AudioStopped(source, false);
                }
            );

            _fetchSources.Begin();
            var activeSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _fetchSources.End();

            foreach (var audio in activeSources)
            {
                if (!audio.playOnAwake)
                    continue;

                if (!TrackedPlayOnAwakeSources.Contains(audio) && audio.isActiveAndEnabled && audio.isPlaying)
                    AudioPlayed(audio);
            }

            _innerLoop.End();

            _cleanupSources.Begin();
            CleanupSources();
            _cleanupSources.End();

            _updateTargeting.Begin();
            UpdateDynamicTargeting();
            _updateTargeting.End();
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(Update)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
        }
    }

    private static void UpdateDynamicTargeting()
    {
        CachedRoutingTargetGroupData.Clear();
        Utils.CachedForeach(
            TrackedSources,
            static (in KeyValuePair<AudioSource, ModAudioSource> pair) =>
            {
                var source = pair.Value;

                if (source.Audio != null && source.HasFlag(AudioFlags.ShouldUpdateDynamicTargeting))
                {
                    // Disable dynamic targeting in case something wrong happened
                    if (!UpdateDynamicTargeting(source))
                        source.ClearFlag(AudioFlags.ShouldUpdateDynamicTargeting);
                }
            }
        );
        CachedRoutingTargetGroupData.Clear();
    }

    private static bool UpdateDynamicTargeting(ModAudioSource source)
    {
        bool groupsMismatched = false;
        bool useSmoothing = false;

        for (int i = 0; i < source.RouteCount; i++)
        {
            var routeData = source.GetRoute(i);

            // Use smoothing if any routes demand it
            useSmoothing = useSmoothing || routeData.Route.SmoothDynamicTargeting;

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

                if (useSmoothing)
                {
                    source.Audio.volume = 0f;
                    source.SetFlag(AudioFlags.IsSwappingTargets);
                    source.AssignFlag(AudioFlags.VolumeLock, UseVolumeLock);
                }

                if (wasPlaying)
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
        Utils.CachedForeach(
            TrackedPlayOnAwakeSources,
            static (in AudioSource source) =>
            {
                if (source.IsNullOrDestroyed())
                {
                    TrackedPlayOnAwakeSources.Remove(source);
                }
            }
        );

        // Cleanup dead one shots
        Utils.CachedForeach(
            TrackedOneShots,
            static (in ModAudioSource source) =>
            {
                if (source.Audio.IsNullOrDestroyed())
                {
                    TrackedOneShots.Remove(source);
                }
            }
        );

        // Cleanup stale stuff
        Utils.CachedForeach(
            TrackedSources,
            static (in KeyValuePair<AudioSource, ModAudioSource> source) =>
            {
                if (source.Key.IsNullOrDestroyed())
                {
                    TrackedSources.Remove(source.Key);
                }
                else if (source.Value.HasFlag(AudioFlags.IsDedicatedOneShotSource) && !source.Key.isPlaying)
                {
                    AudioStopped(source.Key, false);
                    TrackedSources.Remove(source.Key);
                    UnityEngine.Object.Destroy(source.Key);
                }
            }
        );
    }

    private static ModAudioSource? GetModAudioSourceIfExists(AudioSource source)
    {
        if (TrackedSources.TryGetValue(source, out var state))
            return state;

        return null;
    }

    private static ModAudioSource GetOrCreateModAudioSource(AudioSource source)
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
            }
        });

        state.AppliedState = state.InitialState;
        return state;
    }

    private static ModAudioSource CreateOneShotFromSource(ModAudioSource state)
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

        var createdState = GetOrCreateModAudioSource(oneShotSource);
        createdState.SetFlag(AudioFlags.IsDedicatedOneShotSource);
        createdState.OneShotOrigin = state.Audio;

        TrackedOneShots.Add(createdState); // Also track these separately for performance reasons

        return createdState;
    }

    public static bool OneShotClipPlayed(AudioClip clip, AudioSource source, float volumeScale)
    {
        try
        {
            // Move to a dedicated audio source for better control. Note: This is likely overkill and might mess with other mods?

            var oneShot = CreateOneShotFromSource(GetOrCreateModAudioSource(source));
            oneShot.Audio.volume *= volumeScale;
            oneShot.Audio.clip = clip;

            oneShot.InitialState.Clip = oneShot.Audio.clip;
            oneShot.InitialState.Volume = oneShot.Audio.volume;

            oneShot.Audio.Play();

            return false;
        }
        catch (Exception e)
        {
            Logging.LogError($"ModAudio crashed in {nameof(OneShotClipPlayed)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
            Logging.LogError($"AudioSource that caused the crash:");
            Logging.LogError($"  name = {source?.name ?? "(null)"}");
            Logging.LogError($"  clip = {source?.clip?.name ?? "(null)"}");
            Logging.LogError($"AudioClip that caused the crash:");
            Logging.LogError($"  name = {clip?.name ?? "(null)"}");
            Logging.LogError($"Parameter {nameof(volumeScale)} was: {volumeScale}");
            return true;
        }
    }

    public static bool AudioPlayed(AudioSource source)
    {
        try
        {
            var state = GetOrCreateModAudioSource(source);

            if (state.Audio.playOnAwake)
                TrackedPlayOnAwakeSources.Add(state.Audio);

            var wasPlaying = state.Audio.isPlaying;

            _playRouting.Begin();
            Route(state, false);
            _playRouting.End();

            state.LogDebugDisplay();

            state.ClearFlag(AudioFlags.WasStoppedOrDisabled);

            bool requiresRestart = wasPlaying && !state.Audio.isPlaying;

            if (requiresRestart)
                state.PlayWithoutRouting();

            // If a restart was required, then we already played the sound
            // so let's skip the original if this is called as part of the AudioSource.Play() hooks
            return !requiresRestart;
        }
        catch (Exception e)
        {
            _playRouting.End();
            Logging.LogError($"ModAudio crashed in {nameof(AudioPlayed)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
            Logging.LogError($"AudioSource that caused the crash:");
            Logging.LogError($"  name = {source?.name ?? "(null)"}");
            Logging.LogError($"  clip = {source?.clip?.name ?? "(null)"}");
            return true;
        }
    }

    public static bool AudioStopped(AudioSource source, bool stopOneShots)
    {
        try
        {
            // Do not track sources here
            var state = GetModAudioSourceIfExists(source);

            if (stopOneShots)
            {
                Utils.CachedForeach(
                    TrackedOneShots,
                    source,
                    static (in ModAudioSource trackedSource, in AudioSource stoppedSource) =>
                    {
                        if (!trackedSource.HasFlag(AudioFlags.IsDedicatedOneShotSource | AudioFlags.OneShotStopsIfSourceStops))
                            return;

                        if (trackedSource.OneShotOrigin != stoppedSource || trackedSource.Audio.IsNullOrDestroyed())
                            return;

                        if (trackedSource.Audio.isPlaying)
                            trackedSource.Audio.Stop();
                    }
                );
            }

            if (source.playOnAwake)
                TrackedPlayOnAwakeSources.Remove(source);

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
            Logging.LogError($"ModAudio crashed in {nameof(AudioStopped)}! Please report this error to the mod developer:");
            Logging.LogError(e.ToString());
            Logging.LogError($"AudioSource that caused the crash:");
            Logging.LogError($"  name = {source?.name ?? "(null)"}");
            Logging.LogError($"  clip = {source?.clip?.name ?? "(null)"}");
            Logging.LogError($"Parameter {nameof(stopOneShots)} was: {stopOneShots}");
            return true;
        }
    }

    private static void PreScriptActions(AudioPack callingPack)
    {
        AudioEngineAPI.UpdateGameState();
        ContextAPI.UpdateGameState();
        CurrentlyCalledScriptPack = callingPack;
    }

    private static void PostScriptActions()
    {
        MapInstance_Handle_AudioSettings.ForceCombatMusic = AudioEngineAPI.ForceCombatMusic;
        CurrentlyCalledScriptPack = null;
    }

    private static TargetGroupRouteAPI GetCachedTargetGroup(ModAudioSource source, AudioPack pack, Route route)
    {
        if (!CachedRoutingTargetGroupData.TryGetValue(route, out var routeApi))
        {
            _executeTargetGroups.Begin();
            ExecuteTargetGroup(pack, route, routeApi = new TargetGroupRouteAPI(source));
            _executeTargetGroups.End();
            CachedRoutingTargetGroupData[route] = routeApi;
        }

        return routeApi;
    }

    private static void ExecuteTargetGroup(AudioPack pack, Route route, TargetGroupRouteAPI routeApi)
    {
        if (pack.HasFlag(PackFlags.ForceDisableScripts))
        {
            routeApi.SkipRoute = true;
            return;
        }

        if (string.IsNullOrEmpty(route.TargetGroupScript))
        {
            routeApi.TargetGroup = TargetGroupAll;
            return;
        }

        if (!pack.ScriptMethods.TryGetValue(route.TargetGroupScript, out Function script))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"A script method for {pack.Config.Id} is missing for some reason!");
            pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
            routeApi.SkipRoute = true;
            return;
        }

        PreScriptActions(pack);
        try
        {
            // TODO: Check whenever scripts are allocating way too much total memory
            script.Call(routeApi.Wrap(ScriptEngine));
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Target group script call failed for pack {pack.Config.Id}, script {route.TargetGroupScript}!");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
            pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
            routeApi.SkipRoute = true;
        }
        PostScriptActions();
    }

    private static void ExecuteUpdate(AudioPack pack)
    {
        if (pack.HasFlag(PackFlags.ForceDisableScripts))
        {
            return;
        }

        if (!pack.ScriptMethods.TryGetValue(pack.Config.PackScripts.Update, out Function script))
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"A script method for {pack.Config.Id} is missing for some reason!");
            pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
            return;
        }

        PreScriptActions(pack);
        try
        {
            // TODO: Check whenever scripts are allocating way too much total memory
            script.Call();
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Update script call failed for pack {pack.Config.Id}, script {pack.Config.PackScripts.Update}!");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
            pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
        }
        PostScriptActions();
    }

    private static bool MatchesSource(AudioStepState state, Route route)
    {
        var originalClipName = state.Clip?.name;

        if (originalClipName == null)
            return false;

        for (int i = 0; i < route.OriginalClips.Count; i++)
            if (route.OriginalClips[i] == originalClipName)
                return true;

        return false;
    }

    /// <summary>
    /// Applies full routing to an audio source.
    /// If an audio source was routed previously, it is restored to its original state before routing, effectively applying a new selectedRoute.
    /// </summary>
    /// <param name="source">The audio source to selectedRoute</param>
    /// <returns>True if the source was rerouted or modified, false otherwise (for example when there are no routes defined)</returns>
    private static bool Route(ModAudioSource source, bool reuseOldRoutesIfPossible)
    {
        if (source.HasFlag(AudioFlags.DisableRouting))
            return false; // Do not apply changes

        _routing.Begin();

        CachedRoutingTargetGroupData.Clear();

        int currentChainRoutes = 0;

        var previousRoutes = source.RouteCount > 0 ? new Route[source.RouteCount] : [];

        for (int i = 0; i < source.RouteCount; i++)
            previousRoutes[i] = source.GetRoute(i).Route;

        source.RevertSource();

        var wasRouted = false;

        while (currentChainRoutes++ < MaxChainRoutes)
        {
            var preferredRoute = currentChainRoutes <= previousRoutes.Length ? previousRoutes[currentChainRoutes - 1] : null;

            var stateBeforeRouting = source.AppliedState;

            if (!ExecuteRouteStep(source, preferredRoute))
                break;

            var routeData = source.GetRoute(currentChainRoutes - 1);

            if (source.HasFlag(AudioFlags.HasEncounteredErrors))
                break; // Stop further routing or overlays

            PlayOverlays(source, stateBeforeRouting, routeData.Route);

            if (routeData.SelectedClip == EmptyClip)
                break; // Can't route 

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

        _routing.End();
        return wasRouted;
    }

    private static void PlayOverlay(ModAudioSource source, AudioPack pack, Route route)
    {
        var randomSelection = Utils.SelectRandomWeighted(RNG, route.OverlayClips);

        if (randomSelection.Name == EmptyClipKeyword)
            return;

        if (pack.TryGetReadyClip(randomSelection.Name, out var selectedClip))
        {
            var oneShot = CreateOneShotFromSource(source);
            oneShot.Audio.clip = selectedClip;

            oneShot.Audio.volume = randomSelection.Volume;
            oneShot.Audio.pitch = randomSelection.Pitch;

            if (route.RelativeOverlayEffects)
            {
                oneShot.Audio.volume *= oneShot.InitialState.Volume;
                oneShot.Audio.pitch *= oneShot.InitialState.Pitch;
            }

            if (route.ForceLoop)
                oneShot.Audio.loop = true;

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
            AudioDebugDisplay.LogAudio(LogLevel.Warning, Texts.AudioClipNotFound(randomSelection.Name));
            pack.SetFlag(PackFlags.HasEncounteredErrors);
            source.SetFlag(AudioFlags.HasEncounteredErrors);
        }
    }

    private static void PlayOverlays(ModAudioSource source, AudioStepState stateBeforeRouting, Route? replacementRoute)
    {
        // Note: Overlays should not be able to trigger other overlays
        // Otherwise you can easily create infinite loops

        List<(AudioPack Pack, Route Route)> overlays = [];

        foreach (var pack in EnabledPacks)
        {
            foreach (var route in pack.Config.Routes)
            {
                if (route.OverlaysIgnoreRestarts && !(source.HasFlag(AudioFlags.WasStoppedOrDisabled) || !source.Audio.isPlaying))
                    continue;

                if (!(route.OverlayClips.Count > 0 && MatchesSource(stateBeforeRouting, route) && (!route.LinkOverlayAndReplacement || replacementRoute == route)))
                    continue;

                var routeApi = GetCachedTargetGroup(source, pack, route);

                if (!routeApi.SkipRoute)
                    overlays.Add((pack, route));
            }
        }

        foreach (var (Pack, Route) in overlays)
        {
            PlayOverlay(source, Pack, Route);
        }
    }

    private static bool ExecuteRouteStep(ModAudioSource source, Route? preferredRoute)
    {
        ContextAPI.UpdateGameState();

        if (source.HasFlag(AudioFlags.DisableRouting))
            return false;

        // Get a replacement from routes

        var replacements = new List<(AudioPack Pack, Route Route)>();

        foreach (var pack in EnabledPacks)
        {
            foreach (var route in pack.Config.Routes)
            {
                if (!MatchesSource(source.AppliedState, route))
                    continue;

                // Disallow selecting routes that are already present in the chain
                for (int i = 0; i < source.RouteCount; i++)
                {
                    if (source.GetRoute(i).Route == route)
                        continue;
                }

                var thisRouteApi = GetCachedTargetGroup(source, pack, route);

                if (!thisRouteApi.SkipRoute)
                    replacements.Add((pack, route));
            }
        }

        if (replacements.Count == 0)
            return false;

        if (source.RouteCount >= ModAudioSource.MaxChainedRoutes)
        {
            // This is a sanity check, normally this should be prevented earlier in the call stack
            Logging.LogWarning("Tried to route an audio source that has reached max chained routes! Aborting routing operation. Please notify the mod developer about this!");
            return false;
        }

        var (selectedPack, selectedRoute) = Utils.SelectRandomWeighted(RNG, replacements);

        // Apply overall effects

        source.Audio.volume = selectedRoute.Volume;
        source.Audio.pitch = selectedRoute.Pitch;

        if (selectedRoute.RelativeReplacementEffects)
        {
            source.Audio.volume *= source.InitialState.Volume;
            source.Audio.pitch *= source.InitialState.Pitch;
        }

        if (selectedRoute.ForceLoop)
            source.Audio.loop = true;

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

        AudioClip? destinationClip = null;

        if (groupReplacements.Count == 0)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, Texts.NoAudioClipsInGroup(routeApi.TargetGroup));
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
            else if (randomSelection.Name.StartsWith("<atlyss>"))
            {
                // Try to load a vanilla clip
                var possibleName = randomSelection.Name["<atlyss>".Length..];

                if (!LoadedVanillaClips.TryGetValue(possibleName, out destinationClip) || destinationClip.IsNullOrDestroyed())
                {
                    if (VanillaClips.NameToResourcePath.TryGetValue(possibleName, out var path))
                    {
                        destinationClip = Resources.Load<AudioClip>(path);

                        if (destinationClip)
                        {
                            LoadedVanillaClips[possibleName] = destinationClip;
                        }
                    }
                }
            }
            else
            {
                selectedPack.TryGetReadyClip(randomSelection.Name, out destinationClip);
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
                AudioDebugDisplay.LogPack(LogLevel.Error, Texts.AudioClipNotFound(randomSelection.Name));
                selectedPack.SetFlag(PackFlags.HasEncounteredErrors);
                source.SetFlag(AudioFlags.HasEncounteredErrors);
                destinationClip = ErrorClip;
            }
        }

        source.PushRoute(selectedPack, selectedRoute, routeApi.TargetGroup, destinationClip ?? ErrorClip);

        return true;
    }
}
