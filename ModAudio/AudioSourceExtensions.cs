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
}
