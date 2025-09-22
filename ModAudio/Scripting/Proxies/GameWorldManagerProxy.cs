using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class GameWorldManagerProxy(GameWorldManager target)
{
    public readonly GameWorldManager Target = target;
}
