using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class PlayerCombatProxy(PlayerCombat target)
{
    public readonly PlayerCombat Target = target;

    [LuaMember]
    public ScriptableWeaponProxy _equippedWeapon => new(Target._equippedWeapon);
}
