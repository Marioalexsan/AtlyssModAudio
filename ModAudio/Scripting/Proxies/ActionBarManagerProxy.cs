using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ActionBarManagerProxy(ActionBarManager target)
{
    public readonly ActionBarManager Target = target;
}
