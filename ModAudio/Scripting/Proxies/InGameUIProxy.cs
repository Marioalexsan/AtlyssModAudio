using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class InGameUIProxy
{
    private InGameUIProxy(InGameUI target)
    {
        Target = target;
    }

    public static InGameUIProxy? Proxy(InGameUI? target) => target != null ? new(target) : null;

    public readonly InGameUI Target;

    [LuaMember]
    public bool _displayUI => Target._displayUI;

    [LuaMember]
    public string _reigonTitle => Target._reigonTitle;
}
