using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class PlayerProxy
{
    private PlayerProxy(Player target)
    {
        Target = target;
    }

    public static PlayerProxy? Proxy(Player? target) => target != null ? new(target) : null;

    public readonly Player Target;

    [LuaMember]
    public string _nickname => Target._nickname;

    [LuaMember]
    public string _globalNickname => Target._globalNickname;

    [LuaMember]
    public string _mapName => Target._mapName;

    [LuaMember]
    public string _recalledMapInstance => Target._recalledMapInstance;

    [LuaMember]
    public string _recalledMapInstanceName => Target._recalledMapInstanceName;

    [LuaMember]
    public bool _isHostPlayer => Target._isHostPlayer;

    [LuaMember]
    public bool _isAfk => Target._isAfk;

    [LuaMember]
    public bool _isHurt => Target._isHurt;

    [LuaMember]
    public bool _inIFrame => Target._inIFrame;

    [LuaMember]
    public bool _bufferingStatus => Target._bufferingStatus;

    [LuaMember]
    public bool _inUI => Target._inUI;

    [LuaMember]
    public bool _inChat => Target._inChat;

    [LuaMember]
    public bool _isSitting => Target._isSitting;

    [LuaMember]
    public bool _isHidden => Target._isHidden;

    [LuaMember]
    public byte _currentPlayerCondition => (byte)Target._currentPlayerCondition;

    [LuaMember]
    public PlayerStatsProxy? _pStats => PlayerStatsProxy.Proxy(Target._pStats);

    [LuaMember]
    public PlayerCombatProxy? _pCombat => PlayerCombatProxy.Proxy(Target._pCombat);

    [LuaMember]
    public StatusEntityProxy? _statusEntity => StatusEntityProxy.Proxy(Target._statusEntity);

    [LuaMember]
    public MapInstanceProxy? _playerMapInstance => MapInstanceProxy.Proxy(Target._playerMapInstance);
}
