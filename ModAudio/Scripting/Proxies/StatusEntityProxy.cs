using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class StatusEntityProxy
{
    private StatusEntityProxy(StatusEntity target)
    {
        Target = target;
    }

    public static StatusEntityProxy? Proxy(StatusEntity? target) => target != null ? new(target) : null;

    public readonly StatusEntity Target;

    [LuaMember]
    public int _currentHealth => Target._currentHealth;

    [LuaMember]
    public int _currentMana => Target._currentMana;

    [LuaMember]
    public int _currentStamina => Target._currentStamina;

    [LuaMember]
    public float _damageAbsorbtion => Target._damageAbsorbtion;

    [LuaMember]
    public bool _immuneToKnockback => Target._immuneToKnockback;

    [LuaMember]
    public bool _autoParry => Target._autoParry;

    [LuaMember]
    public bool _reflectDamage => Target._reflectDamage;

    [LuaMember]
    public float _reflectPercent => Target._reflectPercent;
}
