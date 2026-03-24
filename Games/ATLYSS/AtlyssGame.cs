using Lua;
using Marioalexsan.ModAudio.Atlyss.HarmonyPatches;
using Marioalexsan.ModAudio.Scripting.Data;
using Marioalexsan.ModAudio.Scripting.Proxies;
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

    public override bool MatchesAlias(ModAudioSource audio, string alias)
    {
        // Second form is deprecated, but I'll keep support for it
        if (alias.StartsWith("modaudio_atlyss_map_") || alias.StartsWith("modaudio_map_"))
        {
            if (!Player._mainPlayer || !Player._mainPlayer._playerMapInstance)
                return false;
            
            var aliasData = alias.StartsWith("modaudio_atlyss_map_")
                ? alias.Substring("modaudio_atlyss_map_".Length)
                : alias.Substring("modaudio_map_".Length);

            var map = Player._mainPlayer._playerMapInstance;
            var mapName = GetCleanMapName(map);

            if (aliasData == $"{mapName}_day" && map._daytimeMusic == audio.Audio)
                return true;
            else if (aliasData == $"{mapName}_night" && map._nightMusic == audio.Audio)
                return true;
            else if (aliasData == $"{mapName}_action" && map._actionMusic == audio.Audio)
                return true;
            else if (aliasData == $"{mapName}_null" && map._nullMusic == audio.Audio)
                return true;
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
        return string.Concat(instance._mapName.ToLower().Where(x => 'a' <= x && x <= 'z'));
    }
}