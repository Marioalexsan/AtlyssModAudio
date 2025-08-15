using Jint.Native.Object;
using Jint.Runtime.Interop;
using Marioalexsan.ModAudio.HarmonyPatches;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;

namespace Marioalexsan.ModAudio.Scripting;

public static class ContextAPI
{
    public static string MapName { get; private set; } = "";
    public static string MapSubregion { get; private set; } = "";

    public static int EnemiesTargetingPlayer { get; private set; } = 0;
    public static double SecondsSinceGameStart => Time.realtimeSinceStartupAsDouble;

    public static void UpdateGameState()
    {
        if (Player._mainPlayer && Player._mainPlayer.Network_playerMapInstance)
            MapName = Player._mainPlayer.Network_playerMapInstance._mapName ?? "";

        if (InGameUI._current)
            MapSubregion = InGameUI._current._reigonTitle ?? "";

        TrackedAggroCreeps.Creeps.RemoveWhere(x => x == null || x.Network_aggroedEntity == null);
        EnemiesTargetingPlayer = TrackedAggroCreeps.Creeps.Where(x => x.Network_aggroedEntity != Player._mainPlayer).Count();
    }
}