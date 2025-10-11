using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class CreepProxy
{
    private CreepProxy(Creep target)
    {
        Target = target;
    }

    public static CreepProxy? Proxy(Creep? target) => target != null ? new(target) : null;

    public readonly Creep Target;

    [LuaMember]
    public string _creepDisplayName => Target._creepDisplayName;

    [LuaMember]
    public string _creepNameID => Target._creepNameID;

    [LuaMember]
    public bool _isTargetable => Target._isTargetable;

    [LuaMember]
    public ScriptableCreepProxy? _scriptCreep => ScriptableCreepProxy.Proxy(Target._scriptCreep);

    [LuaMember]
    public int _creepLevel => Target._creepLevel;

    // _combatElement?

    [LuaMember]
    public bool _ignoreAggroedTarget => Target._ignoreAggroedTarget;

    [LuaMember]
    public bool _disableAggro => Target._disableAggro;

    [LuaMember]
    public bool _isKnockedBack => Target._isKnockedBack;

    [LuaMember]
    public bool _isMoving => Target._isMoving;

    [LuaMember]
    public bool _isParryStaggered => Target._isParryStaggered;
}
