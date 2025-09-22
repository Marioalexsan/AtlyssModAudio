using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class CreepProxy(Creep target)
{
    public readonly Creep Target = target;

    [LuaMember]
    public int _creepLevel => Target._creepLevel;

    [LuaMember]
    public ScriptableCreepProxy _scriptCreep => new(Target._scriptCreep);
}
