using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class MainMenuManagerProxy(MainMenuManager target)
{
    public readonly MainMenuManager Target = target;
}
