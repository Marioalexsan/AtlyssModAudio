using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ShopkeepManagerProxy
{
    private ShopkeepManagerProxy(ShopkeepManager target)
    {
        Target = target;
    }

    public static ShopkeepManagerProxy? Proxy(ShopkeepManager? target) => target != null ? new(target) : null;

    public readonly ShopkeepManager Target;

    [LuaMember]
    public bool _isOpen => Target._isOpen;

    [LuaMember]
    public ScriptableShopkeepProxy? _scriptShopkeep => ScriptableShopkeepProxy.Proxy(Target._scriptShopkeep);
}
