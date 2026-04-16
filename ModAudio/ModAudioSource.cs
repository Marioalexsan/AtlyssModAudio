using Marioalexsan.ModAudio;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public struct RouteStep
{
    public AudioPack AudioPack;
    public Route Route;
    public string TargetGroup;
    public AudioClip SelectedClip;
}

public struct AudioStepState
{
    public AudioClip? Clip;
    public float Volume;
    public float Pitch;
    public bool Loop;
}

[Flags]
public enum AudioFlags : uint
{
    None = uint.MinValue,
    All = uint.MaxValue,

    DisableRouting = 1 << 0,
    IsDedicatedOneShotSource = 1 << 1,
    IsOverlay = 1 << 2,
    OneShotStopsIfSourceStops = 1 << 3,
    ShouldUpdateDynamicTargeting = 1 << 4,
    IsSwappingTargets = 1 << 5,
    LoopWasForced = 1 << 6,
    VolumeLock = 1 << 7,
    PitchLock = 1 << 8,
    LoopLock = 1 << 9,
    WasStoppedOrDisabled = 1 << 10,
    HasEncounteredErrors = 1 << 11, // Only assign to sources that have had erroneous routes states applied to them
}

public class ModAudioSource
{
    private static long NextTrackedInstanceId = 0;
    public const int MaxChainedRoutes = 4;

    public ModAudioSource(AudioSource source)
    {
        Audio = source;
        TrackedInstanceId = NextTrackedInstanceId++;
    }

    public readonly AudioSource Audio;
    public AudioSource? OneShotOrigin;
    public readonly long TrackedInstanceId; // For stable sorting purposes

    // TODO: Not implemented!
    /// <summary>
    /// Used only as part of dynamic targeting.
    /// This saves the global play position so that dynamic clips flow into one another more smoothly.
    /// </summary>
    public TimeSpan DynamicTargetingPlayPosition;
    
    /// <summary>
    /// Multiplies all volume sets by this amount, and divides volume gets by this amount
    /// This allows amplifying audio that is dynamically faded in / out by the game or mods
    /// </summary>
    public float ProxyVolumeModifier
    {
        get => _proxyVolumeModifier;
        set
        {
            _proxyVolumeModifier = value;
            Audio.volume = Audio.volume; // Will trigger an update using the new proxy modifier
        }
    }

    private float _proxyVolumeModifier = 1f;
    
    /// <summary>
    /// Returns the true volume as set on the engine side
    /// </summary>
    public float ProxiedVolume => Audio.volume * ProxyVolumeModifier;

    /// <summary>
    /// Multiplies all pitch sets by this amount, and divides pitch gets by this amount
    /// This allows amplifying pitch that is dynamically modified by the game or mods
    /// </summary>
    public float ProxyPitchModifier
    {
        get => _proxyPitchModifier;
        set
        {
            _proxyPitchModifier = value;
            Audio.pitch = Audio.pitch; // Will trigger an update using the new proxy modifier
        }
    }

    private float _proxyPitchModifier = 1f;

    /// <summary>
    /// Last value that the volume setter was called with
    /// </summary>
    public float LastUnproxiedVolume = 1f;

    /// <summary>
    /// Last value that the pitch setter was called with
    /// </summary>
    public float LastUnproxiedPitch = 1f;
    
    /// <summary>
    /// Returns the true pitch as set on the engine side
    /// </summary>
    public float ProxiedPitch => Audio.pitch * ProxyPitchModifier; 

    /// <summary>
    /// Initial state is the original state of the audio before any modifications applied by ModAudio
    /// </summary>
    public AudioStepState InitialState;
    
    /// <summary>
    /// Applied state is the last state (or desired target state) applied by ModAudio; this may or may not be desynced from the actual state
    /// </summary>
    public AudioStepState AppliedState;

    private readonly RouteStep[] Routes = new RouteStep[MaxChainedRoutes];

    public RouteStep? LatestRoute => RouteCount > 0 ? Routes[RouteCount - 1] : null;

    public RouteStep GetRoute(int index)
    {
        if (index < 0 || index >= RouteCount)
            throw new ArgumentOutOfRangeException("Tried to access a route index that is invalid for this audio source! Please notify the mod developer about this!");

        return Routes[index];
    }

    public bool PushRoute(AudioPack pack, Route route, string targetGroup, AudioClip selectedClip)
    {
        if (RouteCount >= Routes.Length)
            return false;

        Routes[RouteCount++] = new RouteStep()
        {
            AudioPack = pack,
            Route = route,
            TargetGroup = targetGroup,
            SelectedClip = selectedClip,
        };
        return true;
    }

    public void ClearRoutes()
    {
        for (int i = 0; i < Routes.Length; i++)
            Routes[i] = default;

        RouteCount = 0;
    }

    public int RouteCount { get; private set; }

    public AudioFlags Flags;

    public void Clear()
    {
        InitialState = new();
        RouteCount = 0;

        for (int i = 0; i < MaxChainedRoutes; i++)
            Routes[i] = new();

        Flags = AudioFlags.None;
        OneShotOrigin = null;
    }
    
    public void RevertSource()
    {
        ProxyVolumeModifier = 1f;
        ProxyPitchModifier = 1f;
        
        if (Audio.clip != AppliedState.Clip)
        {
            // Likely changed externally
            InitialState.Clip = Audio.clip;
        }
        else
        {
            Audio.clip = InitialState.Clip;
        }

        if (Audio.volume != AppliedState.Volume)
        {
            // Likely changed externally
            InitialState.Volume = Audio.volume;
        }
        else
        {
            Audio.volume = InitialState.Volume;
        }

        if (Audio.pitch != AppliedState.Pitch)
        {
            // Likely changed externally
            InitialState.Pitch = Audio.pitch;
        }
        else
        {
            Audio.pitch = InitialState.Pitch;
        }

        if (Audio.loop != AppliedState.Loop)
        {
            // Likely changed externally
            InitialState.Loop = Audio.loop;
        }
        else
        {
            Audio.loop = InitialState.Loop;
        }

        AppliedState.Clip = Audio.clip;
        AppliedState.Volume = Audio.volume;
        AppliedState.Pitch = Audio.pitch;
        AppliedState.Loop = Audio.loop;

        ClearRoutes();

        // Only clear flags that are set as part of routing
        ClearFlag(AudioFlags.ShouldUpdateDynamicTargeting | AudioFlags.IsSwappingTargets);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetFlag(AudioFlags flag) => Flags |= flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ClearFlag(AudioFlags flag) => Flags &= ~flag;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasFlag(AudioFlags flag) => (Flags & flag) == flag; // Do not use Enum.HasFlag, it's a boxing operation and allocates junk

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AssignFlag(AudioFlags flag, bool shouldBeSet)
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

    public void PlayWithoutRouting()
    {
        bool lastDisableRouteState = HasFlag(AudioFlags.DisableRouting);

        SetFlag(AudioFlags.DisableRouting);
        Audio.Play();
        AssignFlag(AudioFlags.DisableRouting, lastDisableRouteState);
    }
}
