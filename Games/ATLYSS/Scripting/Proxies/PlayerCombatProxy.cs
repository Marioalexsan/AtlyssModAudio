using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class PlayerCombatProxy
{
    private PlayerCombatProxy(PlayerCombat target)
    {
        Target = target;
    }

    public static PlayerCombatProxy? Proxy(PlayerCombat? target) => target != null ? new(target) : null;

    public readonly PlayerCombat Target;

    [LuaMember]
    public byte _currentSheathCondition => (byte)Target._currentSheathCondition;

    [LuaMember]
    public ScriptableWeaponProxy? _equippedWeapon => ScriptableWeaponProxy.Proxy(Target._equippedWeapon);

    [LuaMember]
    public bool _isUsingAltWeapon => Target._isUsingAltWeapon;

    [LuaMember]
    public bool _isDisarmed => Target._isDisarmed;

    [LuaMember]
    public bool _isUnarmed => Target._isUnarmed;

    [LuaMember]
    public bool _isChargingWeapon => Target._isChargingWeapon;

    [LuaMember]
    public bool _isChargedWeapon => Target._isChargedWeapon;

    [LuaMember]
    public bool _inParryWindow => Target._inParryWindow;
}
