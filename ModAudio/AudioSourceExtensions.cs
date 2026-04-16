using System.Text;
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
        var message = new StringBuilder(512);

        message
            .Append('[')
            .Append(state.InitialState.Clip?.name ?? "(null)");

        for (int i = 0; i < state.RouteCount; i++)
        {
            message
                .Append(" > ")
                .Append(state.GetRoute(i).SelectedClip?.name ?? "(null)");
        }
        message
            .Append("] S(")
            .Append(state.Audio.name)
            .Append(") V(")
            .Append($"{state.InitialState.Volume:F2}");
        
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (state.InitialState.Volume != state.AppliedState.Volume)
        {
            message
                .Append(" > ")
                .Append($"{state.AppliedState.Volume:F2}");
        }

        message
            .Append(") P(")
            .Append($"{state.InitialState.Pitch:F2}");
        
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (state.InitialState.Pitch != state.AppliedState.Pitch)
        {
            message
                .Append(" > ")
                .Append($"{state.AppliedState.Pitch:F2}");
        }

        var audioGroup = state.Audio.outputAudioMixerGroup?.name;
        
        message
            .Append(") G(")
            .Append(audioGroup ?? "(null)")
            .Append(") R(");

        bool dynamicRoute = false;

        for (int i = 0; i < state.RouteCount; i++)
        {
            if (i != state.RouteCount - 1)
                message.Append(" > ");
            
            message.Append(state.GetRoute(i).TargetGroup);

            dynamicRoute = dynamicRoute || state.GetRoute(i).Route.EnableDynamicTargeting;
        }

        message.Append(") ");

        if (state.HasFlag(AudioFlags.IsOverlay))
            message.Append("overlay ");

        if (state.HasFlag(AudioFlags.IsDedicatedOneShotSource))
            message.Append("oneshot ");

        message
            .Append("loop_")
            .Append(state.AppliedState.Loop ? "on" : "off");

        if (state.HasFlag(AudioFlags.LoopWasForced))
            message.Append("_force");
        
        message.Append(' ');

        if (dynamicRoute)
            message.Append("dynamic ");

        if (state.RouteCount >= 1 && state.GetRoute(0).Route.UseChainRouting)
            message.Append("chainrouted ");

        if (state.Audio.playOnAwake)
            message.Append("onAwake ");

        float distance = 0f;

        var tags = AudioDebugDisplay.AudioLogFlags.None;

        if (state.Audio.spatialBlend <= 0.01f)
        {
            // Assume it's a 2D sound
            message.Append("(2D) ");
            tags = tags | AudioDebugDisplay.AudioLogFlags.Is2DSound;
        }
        else
        {
            if (AudioEngine.Game.TryGetDistanceFromPlayer(state.Audio, out distance))
            {
                message
                    .Append("(3D / ")
                    .Append($"{distance:F2}")
                    .Append(") ");
            }
            else
            {
                message.Append("(3D) ");
            }
        }

        if (state.RouteCount > 0)
            tags = tags | AudioDebugDisplay.AudioLogFlags.Routed;

        if (state.HasFlag(AudioFlags.IsOverlay))
            tags = tags | AudioDebugDisplay.AudioLogFlags.Overlay;

        AudioDebugDisplay.LogAudio(LogLevel.Info, message.ToString(), tags, audioGroup, distance);
    }
}
