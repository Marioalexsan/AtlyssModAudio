using UnityEngine;

public struct AudioSourceState
{
    // Previous state
    public AudioClip? Clip;
    public float Volume;
    public float Pitch;
    public bool Loop;

    // (supposedly) Current state
    public AudioClip? AppliedClip;
    public float AppliedVolume;
    public float AppliedPitch;
    public bool AppliedLoop;

    // Route data
    public string? RouteAudioPackId;
    public int RouteAudioPackRouteIndex;
    public string? RouteGroup;

    // Flags
    public bool DisableRouting;
    public bool IsOneShotSource;
    public bool IsOverlay;
    public bool IsCustomEvent;
    public bool OneShotStopsIfSourceStops;
    public bool UsesDynamicTargeting;
    public bool IsSwappingTargets;

    // Dangerous flags
    public bool VolumeLock; // Prevents changing volume on this audio source

    // Temporary Flags
    public bool JustRouted;
    public bool JustUsedDefaultClip;
    public bool WasStoppedOrDisabled;

    // Misc
    public AudioSource? OneShotOrigin;
}
