using UnityEngine;

namespace Marioalexsan.ModAudio;

// TODO: Not decided on the utility of this yet.
public static class CustomAudioEvents
{
    private const int CustomEventClipSizeInSamples = 16;
    private static string GenerateEventName(string name) => $"___event:{name.ToLower()}___";

    public static AudioClip PlayerHit => _playerHit ??= AudioClipLoader.GenerateEmptyClip(GenerateEventName("player_hit"), CustomEventClipSizeInSamples);
    private static AudioClip? _playerHit;
}
