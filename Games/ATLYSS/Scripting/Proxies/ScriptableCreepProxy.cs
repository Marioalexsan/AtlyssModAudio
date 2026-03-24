using Lua;
using System;
using System.Collections.Generic;
using System.Text;
using static UnityEngine.RemoteConfigSettingsHelper;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ScriptableCreepProxy
{
    private ScriptableCreepProxy(ScriptableCreep target)
    {
        Target = target;
    }

    public static ScriptableCreepProxy? Proxy(ScriptableCreep? target) => target != null ? new(target) : null;

    public readonly ScriptableCreep Target;

    [LuaMember]
    public string _creepName => Target._creepName;

    [LuaMember]
    public bool _unkillable => Target._unkillable;

    [LuaMember]
    public bool _isElite => Target._isElite;

    [LuaMember]
    public byte _creepAttribute => (byte)Target._creepAttribute;

    [LuaMember]
    public bool _immuneToKnockback => Target._immuneToKnockback;

    [LuaMember]
    public bool _immuneToHitstun => Target._immuneToHitstun;

    [LuaMember]
    public bool _canAggro => Target._canAggro;

    [LuaMember]
    public bool _ignoreHighLevelPlayer => Target._ignoreHighLevelPlayer;

    [LuaMember]
    public bool _dontFollowAggroTarget => Target._dontFollowAggroTarget;

    [LuaMember]
    public bool _playMapInstanceActionMusic => Target._playMapInstanceActionMusic;
}
