using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class PlayerStatsProxy(PlayerStats target)
{
    public readonly PlayerStats Target = target;

    [LuaMember]
    public int _currentLevel => Target._currentLevel;
}
