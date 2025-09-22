using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ScriptableWeaponProxy(ScriptableWeapon target)
{
    public readonly ScriptableWeapon Target = target;

    [LuaMember]
    public string _itemName => Target._itemName;
}
