using Lua;
using System;
using System.Collections.Generic;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ScriptableCreepProxy(ScriptableCreep target)
{
    public readonly ScriptableCreep Target = target;

    [LuaMember]
    public bool _playMapInstanceActionMusic => Target._playMapInstanceActionMusic;

    [LuaMember]
    public bool _isElite => Target._isElite;
}
