using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ScriptableShopkeepProxy
{
    private ScriptableShopkeepProxy(ScriptableShopkeep target)
    {
        Target = target;
    }

    public static ScriptableShopkeepProxy? Proxy(ScriptableShopkeep? target) => target != null ? new(target) : null;

    public readonly ScriptableShopkeep Target;

    [LuaMember]
    public string _shopName => Target._shopName;

    [LuaMember]
    public bool _canBuyFromPlayer => Target._canBuyFromPlayer;

    [LuaMember]
    public bool _isGambler => Target._isGambler;
}
