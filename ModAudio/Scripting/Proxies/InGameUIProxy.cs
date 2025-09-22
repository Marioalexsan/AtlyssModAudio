using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class InGameUIProxy(InGameUI target)
{
    public readonly InGameUI Target = target;

    [LuaMember]
    public string _reigonTitle => Target._reigonTitle;
}
