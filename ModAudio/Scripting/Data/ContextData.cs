using Lua;
using Marioalexsan.ModAudio.HarmonyPatches;
using Marioalexsan.ModAudio.Scripting.Proxies;
using UnityEngine;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class ContextData
{
    [LuaMember("mapName")]
    public static string MapName
    {
        get
        {
            if (Player._mainPlayer && Player._mainPlayer.Network_playerMapInstance)
                return Player._mainPlayer.Network_playerMapInstance._mapName ?? "";

            return "";
        }
    }

    [LuaMember("mapSubregion")]
    public static string MapSubregion
    {
        get
        {
            if (InGameUI._current)
                return InGameUI._current._reigonTitle ?? "";

            return "";
        }
    }

    [LuaMember("secondsSinceGameStart")]
    public static double SecondsSinceGameStart => Time.realtimeSinceStartupAsDouble;

    [LuaMember("deltaTime")]
    public static double DeltaTime => Time.deltaTime;

    [LuaMember("mainPlayerLastPvpEventAt")]
    public static double MainPlayerLastPvpEventAt { get; internal set; }

    [LuaMember("lastPlayerPvp")]
    public static PlayerProxy? LastPlayerPvpProxy => PlayerProxy.Proxy(LastPlayerPvp);

    public static Player? LastPlayerPvp { get; set; }

    [LuaMember("aggroedEnemies")]
    public static LuaTable AggroedEnemies { get; internal set; } = new LuaTable(32, 0);
}
