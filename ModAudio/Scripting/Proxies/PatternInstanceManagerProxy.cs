using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class PatternInstanceManagerProxy
{
    private PatternInstanceManagerProxy(PatternInstanceManager target)
    {
        Target = target;
    }

    public static PatternInstanceManagerProxy? Proxy(PatternInstanceManager? target) => target != null ? new(target) : null;

    public readonly PatternInstanceManager Target;

    [LuaMember]
    public byte _setDungeonDifficulty => (byte)Target._setDungeonDifficulty;

    [LuaMember]
    public bool _allArenasBeaten => Target._allArenasBeaten;

    [LuaMember]
    public bool _isBossEngaged => Target._isBossEngaged;

    [LuaMember]
    public bool _isBossDefeated => Target._isBossDefeated;

    [LuaMember]
    public int _dungeonKeysAquired => Target._dungeonKeysAquired;
}
