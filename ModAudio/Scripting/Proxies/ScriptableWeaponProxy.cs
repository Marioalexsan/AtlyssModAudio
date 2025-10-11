using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ScriptableWeaponProxy
{
    private ScriptableWeaponProxy(ScriptableWeapon target)
    {
        Target = target;
    }

    public static ScriptableWeaponProxy? Proxy(ScriptableWeapon? target) => target != null ? new(target) : null;

    public readonly ScriptableWeapon Target;

    [LuaMember]
    public int _equipmentLevel => Target._equipmentLevel;

    [LuaMember]
    public byte _equipType => (byte)Target._equipType;

    [LuaMember]
    public byte _itemType => (byte)Target._itemType;

    [LuaMember]
    public string _itemName => Target._itemName;

    [LuaMember]
    public byte _itemRarity => (byte)Target._itemRarity;

    [LuaMember]
    public bool _destroyOnDrop => Target._destroyOnDrop;

    [LuaMember]
    public string _itemDescription => Target._itemDescription;
}
