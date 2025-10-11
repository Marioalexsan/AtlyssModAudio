using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class PlayerStatsProxy
{
    private PlayerStatsProxy(PlayerStats target)
    {
        Target = target;
    }

    public static PlayerStatsProxy? Proxy(PlayerStats? target) => target != null ? new(target) : null;

    public readonly PlayerStats Target;

    [LuaMember]
    public int _currentLevel => Target._currentLevel;

    [LuaMember]
    public int _currentExp => Target._currentExp;

    [LuaMember]
    public string _syncClass => Target._syncClass;

    [LuaMember]
    public int _syncClassTier => Target._syncClassTier;

    [LuaMember]
    public int _currentSkillPoints => Target._currentSkillPoints;

    [LuaMember]
    public int _currentAttributePoints => Target._currentAttributePoints;
}
