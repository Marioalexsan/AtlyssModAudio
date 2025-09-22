using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ShopkeepManagerProxy(ShopkeepManager target)
{
    public readonly ShopkeepManager Target = target;
}
