using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Marioalexsan.ModAudio;

/// <summary>
/// Inteface for implementing game specific logic
/// </summary>
public class ModAudioGame
{
    /// <summary>
    /// The starting prefix of audio aliases.
    /// Aliases *must* start with this prefix, otherwise they will be treated as regular clips for performance reasons!
    /// </summary>
    public const string AliasNameStart = "modaudio_";
    
    /// <summary>
    /// The true type should be ILuaUserData!
    /// </summary>
    public object? GameData { get; set; }
    
    /// <summary>
    /// The true type should be ILuaUserData!
    /// </summary>
    public object? Context { get; set; }
    
    public virtual void OnReload()
    {
        // Do nothing
    }
    
    public virtual void PostReload()
    {
        // Do nothing
    }

    public virtual void OnUpdate()
    {
        // Do nothing
    }

    public virtual bool TryGetDistanceFromPlayer(AudioSource source, out float distance)
    {
        // Distance unavailable
        distance = 0;
        return false;
    }

    /// <summary>
    /// Checks if an audio source matches an alias defined by the game.
    /// The aliases themselves follow the format modaudio_{gameIdentifier}_{aliasIdentifier}.
    /// This method will receive the alias without the associated prefix!
    /// </summary>
    /// <param name="audio">Audio source to check</param>
    /// <param name="alias">A clip name to check for aliases</param>
    /// <returns>Whenever the audio source matches or not</returns>
    public virtual bool MatchesAlias(ModAudioSource audio, ReadOnlySpan<char> alias)
    {
        // Return nothing
        return false;
    }

    public virtual bool TryLoadVanillaClip(string identifier, [NotNullWhen(true)] out AudioClip? clip)
    {
        clip = null;
        return false;
    }

    // Atlyss specific; not guaranteed to be usable in other games
    public virtual void Specialized_ForceCombatMusic(bool enabled)
    {
        // Do nothing
    }

    // Atlyss specific; not guaranteed to be usable in other games
    public virtual string? Specialized_GetMapName()
    {
        // No map available
        return null;
    }
}