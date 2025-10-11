using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class GameWorldManagerProxy
{
    private GameWorldManagerProxy(GameWorldManager target)
    {
        Target = target;
    }

    public static GameWorldManagerProxy? Proxy(GameWorldManager? target) => target != null ? new(target) : null;

    public readonly GameWorldManager Target;

    [LuaMember]
    public byte _worldTime => (byte)Target._worldTime;

    [LuaMember]
    public byte _clockSetting => (byte)Target._clockSetting;

    [LuaMember]
    public int _timeDisplay => Target._timeDisplay;
}
