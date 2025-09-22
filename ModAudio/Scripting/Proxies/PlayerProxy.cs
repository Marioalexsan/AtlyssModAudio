using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class PlayerProxy(Player target)
{
    public readonly Player Target = target;

    [LuaMember]
    public string _nickname => Target._nickname;

    [LuaMember]
    public string _globalNickname => Target._globalNickname;

    [LuaMember]
    public byte _currentPlayerCondition => (byte)Target._currentPlayerCondition;

    [LuaMember]
    public PlayerStatsProxy _pStats => new(Target._pStats);

    [LuaMember]
    public PlayerCombatProxy _pCombat = new(target._pCombat);

    [LuaMember]
    public MapInstanceProxy _playerMapInstance => new(Target._playerMapInstance);
}
