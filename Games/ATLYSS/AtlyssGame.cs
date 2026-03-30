using System.Diagnostics.CodeAnalysis;
using Lua;
using Marioalexsan.ModAudio.Atlyss.HarmonyPatches;
using Marioalexsan.ModAudio.Atlyss.Scripting.Data;
using Marioalexsan.ModAudio.Atlyss.Scripting.Proxies;
using UnityEngine;
using UnityEngine.Audio;

namespace Marioalexsan.ModAudio.Atlyss;

public class AtlyssGame : ModAudioGame
{
    public AtlyssGame()
    {
        GameData = new AtlyssModule();
        Context = new ContextData();
    }
    
    public static Dictionary<string, AudioMixerGroup> LoadedMixerGroups = [];

    public override void OnReload()
    {
        MapInstance_Handle_AudioSettings.ForceCombatMusic = false;
        
        LoadedMixerGroups.Clear();
        LoadedMixerGroups = SettingsManager._current._masterMixer.FindMatchingGroups("").ToDictionary(x => x.name.ToLower());
        
        var mapInstances = UnityEngine.Object.FindObjectsByType<MapInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var map in mapInstances)
        {
            map._actionMusic.Stop();
            map._daytimeMusic.Stop();
            map._nightMusic.Stop();
            map._nullMusic.Stop();
            map._actionMusic.volume = 0;
            map._daytimeMusic.volume = 0;
            map._nightMusic.volume = 0;
            map._nullMusic.volume = 0;
        }
    }

    public override void PostReload()
    {
        // Reset MapInstance play state and recheck whenever to use null music or not
        var mapInstances = UnityEngine.Object.FindObjectsByType<MapInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var map in mapInstances)
        {
            map._musicBeginBuffer = map._timeBeforeMusicStart - 1f;
            map._musicStarted = false;
        }
    }

    public override void OnUpdate()
    {
        TrackedAggroCreeps.Creeps.RemoveWhere(x => x == null || x.Network_aggroedEntity == null);

        ContextData.AggroedEnemies.Clear();

        int index = 1;

        foreach (var creep in TrackedAggroCreeps.Creeps)
            ContextData.AggroedEnemies[index++] = LuaValue.FromUserData(CreepProxy.Proxy(creep));
    }

    public override void Specialized_ForceCombatMusic(bool enabled)
    {
        MapInstance_Handle_AudioSettings.ForceCombatMusic = enabled;
    }

    public override bool MatchesAlias(ModAudioSource audio, ReadOnlySpan<char> alias)
    {
        // PS: This method is in a hot path, make it efficient!
        bool isMapAlias = false;

        if (alias.StartsWith("atlyss_map_"))
        {
            alias = alias.Slice("atlyss_map_".Length);
            isMapAlias = true;
        }
        else if (alias.StartsWith("map_"))
        {
            alias = alias.Slice("map_".Length);
            isMapAlias = true;
        }
        
        // Second form is deprecated, but I'll keep support for it
        if (isMapAlias)
        {
            if (!Player._mainPlayer || !Player._mainPlayer._playerMapInstance)
                return false;

            var map = Player._mainPlayer._playerMapInstance;
            var mapName = GetCleanMapName(map);

            if (!alias.StartsWith(mapName))
                return false;

            alias = alias.Slice(mapName.Length);
            
            if (alias is "_day")
                return map._daytimeMusic == audio.Audio;
            else if (alias is "_night")
                return map._nightMusic == audio.Audio;
            else if (alias is "_action")
                return map._actionMusic == audio.Audio;
            else if (alias is "_null")
                return map._nullMusic == audio.Audio;
            else
                return false;
        }

        return false;
    }

    public override bool TryGetDistanceFromPlayer(AudioSource source, out float distance)
    {
        if (!Player._mainPlayer)
        {
            distance = 0;
            return false;
        }
        
        distance = Vector3.Distance(Player._mainPlayer.transform.position, source.transform.position);
        return true;
    }

    public override string? Specialized_GetMapName()
    {
        if (Player._mainPlayer && Player._mainPlayer.Network_playerMapInstance)
            return Player._mainPlayer.Network_playerMapInstance._mapName;

        return null;
    }

    public static string GetCleanMapName(MapInstance instance)
    {
        // Small optimization, as a treat
        if (instance == _cachedMap)
            return _cachedMapCleanedName;

        _cachedMap = instance;
        return _cachedMapCleanedName = string.Concat(instance._mapName.ToLower().Where(x => 'a' <= x && x <= 'z'));
    }

    private static MapInstance? _cachedMap;
    private static string _cachedMapCleanedName = "";

    public override bool TryLoadVanillaClip(string identifier, [NotNullWhen(true)] out AudioClip? clip)
    {
        if (VanillaClips.NameToResourcePath.TryGetValue(identifier, out var path))
        {
            clip = Resources.Load<AudioClip>(path);
            return true;
        }

        clip = null;
        return false;
    }
}