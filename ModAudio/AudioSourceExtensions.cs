using BepInEx.Logging;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public static class AudioSourceExtensions
{
    public static AudioSource CreateCloneOnTarget(this AudioSource source, GameObject targetObject)
    {
        var clone = targetObject.AddComponent<AudioSource>();

        clone.volume = source.volume;
        clone.pitch = source.pitch;
        clone.clip = source.clip;
        clone.outputAudioMixerGroup = source.outputAudioMixerGroup;
        clone.loop = source.loop;
        clone.ignoreListenerVolume = source.ignoreListenerVolume;
        clone.ignoreListenerPause = source.ignoreListenerPause;
        clone.velocityUpdateMode = source.velocityUpdateMode;
        clone.panStereo = source.panStereo;
        clone.spatialBlend = source.spatialBlend;
        clone.spatialize = source.spatialize;
        clone.spatializePostEffects = source.spatializePostEffects;
        clone.reverbZoneMix = source.reverbZoneMix;
        clone.bypassEffects = source.bypassEffects;
        clone.bypassListenerEffects = source.bypassListenerEffects;
        clone.bypassReverbZones = source.bypassReverbZones;
        clone.dopplerLevel = source.dopplerLevel;
        clone.spread = source.spread;
        clone.priority = source.priority;
        clone.mute = source.mute;
        clone.minDistance = source.minDistance;
        clone.maxDistance = source.maxDistance;
        clone.rolloffMode = source.rolloffMode;
        clone.playOnAwake = source.playOnAwake;

        clone.SetCustomCurve(AudioSourceCurveType.CustomRolloff, source.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
        clone.SetCustomCurve(AudioSourceCurveType.ReverbZoneMix, source.GetCustomCurve(AudioSourceCurveType.ReverbZoneMix));
        clone.SetCustomCurve(AudioSourceCurveType.SpatialBlend, source.GetCustomCurve(AudioSourceCurveType.SpatialBlend));
        clone.SetCustomCurve(AudioSourceCurveType.Spread, source.GetCustomCurve(AudioSourceCurveType.Spread));

        return clone;
    }

    public static void LogDebugDisplay(this ModAudioSource state)
    {
        var originalClipName = state.InitialState.Clip?.name ?? "(null)";

        var clipDisplay = originalClipName;

        for (int i = 0; i < state.RouteCount; i++)
        {
            clipDisplay += " > " + state.GetRoute(i).SelectedClip?.name ?? "(null)";
        }

        var volumeDisplay = state.InitialState.Volume != state.AppliedState.Volume ? $"{state.InitialState.Volume:F2} > {state.AppliedState.Volume:F2}" : $"{state.AppliedState.Volume:F2}";
        var pitchDisplay = state.InitialState.Pitch != state.AppliedState.Pitch ? $"{state.InitialState.Pitch:F2} > {state.AppliedState.Pitch:F2}" : $"{state.AppliedState.Pitch:F2}";

        var messageDisplay = $"Clip {clipDisplay} Src {state.Audio.name} Vol {volumeDisplay} Pit {pitchDisplay} AudGrp {state.Audio.outputAudioMixerGroup?.name ?? "(null)"}";

        messageDisplay += $" Routing (";

        bool dynamicRoute = false;

        for (int i = 0; i < state.RouteCount; i++)
        {
            messageDisplay += state.GetRoute(i).TargetGroup;

            if (i != state.RouteCount - 1)
                messageDisplay += " > ";

            dynamicRoute = dynamicRoute || state.GetRoute(i).Route.EnableDynamicTargeting;
        }

        messageDisplay += $")";

        if (state.HasFlag(AudioFlags.IsOverlay))
            messageDisplay += " overlay";

        if (state.HasFlag(AudioFlags.IsDedicatedOneShotSource))
            messageDisplay += " oneshot";

        messageDisplay += " loop_" + (state.AppliedState.Loop ? "on" : "off");

        if (state.HasFlag(AudioFlags.LoopWasForced))
            messageDisplay += "(forced)";

        if (dynamicRoute)
            messageDisplay += " dynamic";

        if (state.RouteCount >= 1 && state.GetRoute(0).Route.UseChainRouting)
            messageDisplay += " chainrouted";

        if (state.Audio.playOnAwake)
            messageDisplay += " onAwake";

        // AudioGroup is lowercased since it needs to be in an exact, expected format for efficient filtering
        string tags = $"AudGrp {(state.Audio.outputAudioMixerGroup?.name ?? "(null)").ToLower()}";

        if (state.RouteCount > 0)
            tags += ",Routed";

        if (state.HasFlag(AudioFlags.IsOverlay))
            tags += ",Overlay";

        float distance = float.MinValue;

        if (Player._mainPlayer)
            distance = Vector3.Distance(Player._mainPlayer.transform.position, state.Audio.transform.position);

        if (distance != float.MinValue)
            messageDisplay += $" Dst {distance:F2}";

        AudioDebugDisplay.LogAudio(LogLevel.Info, messageDisplay, tags, extraParam1: distance);
    }
}
