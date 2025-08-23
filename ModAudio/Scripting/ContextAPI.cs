using Marioalexsan.ModAudio.HarmonyPatches;
using UnityEngine;

namespace Marioalexsan.ModAudio.Scripting;

public static class ContextAPI
{
    public static string MapName { get; private set; } = "";
    public static string MapSubregion { get; private set; } = "";

    public static List<Creep> AggroedEnemies { get; private set; } = [];
    public static double SecondsSinceGameStart => Time.realtimeSinceStartupAsDouble;
    public static double DeltaTime => Time.deltaTime;
    public static double MainPlayerLastPvpEventAt { get; set; } = 0;
    public static Player? LastPlayerPvp { get; set; }

    public static void UpdateGameState()
    {
        if (Player._mainPlayer && Player._mainPlayer.Network_playerMapInstance)
            MapName = Player._mainPlayer.Network_playerMapInstance._mapName ?? "";

        if (InGameUI._current)
            MapSubregion = InGameUI._current._reigonTitle ?? "";

        TrackedAggroCreeps.Creeps.RemoveWhere(x => x.IsNullOrDestroyed() || x.Network_aggroedEntity.IsNullOrDestroyed());
        AggroedEnemies = [..TrackedAggroCreeps.Creeps.Where(x => x.Network_aggroedEntity != Player._mainPlayer)];
    }
}