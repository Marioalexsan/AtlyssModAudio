using Marioalexsan.ModAudio;
using System.Runtime.CompilerServices;
using UnityEngine;

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
    public const int MaxChainedRoutes = 4;

    public ModAudioSource(AudioSource source)
    {
        Audio = source;
    }

    public readonly AudioSource Audio;
    public AudioSource? OneShotOrigin;

    public AudioStepState InitialState;
    public AudioStepState AppliedState;
    public AudioStepState CurrentState => new()
    {
        Clip = Audio.clip,
        Volume = Audio.volume,
        Pitch = Audio.pitch,
        Loop = Audio.loop
    };

    private readonly RouteStep[] Routes = new RouteStep[MaxChainedRoutes];

    public RouteStep GetRoute(int index)
    {
        if (index < 0 || index >= Routes.Length)
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
