using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class MapInstanceProxy
{
    private MapInstanceProxy(MapInstance target)
    {
        Target = target;
    }

    public static MapInstanceProxy? Proxy(MapInstance? target) => target != null ? new(target) : null;

    public readonly MapInstance Target;

    [LuaMember]
    public string _mapName => Target._mapName;

    [LuaMember]
    public byte _zoneType => (byte)Target._zoneType;

    [LuaMember]
    public bool _isWeatherEnabled => Target._isWeatherEnabled;

    [LuaMember]
    public byte _instanceWorldTime => (byte)Target._instanceWorldTime;

    [LuaMember]
    public byte _instanceClockSetting => (byte)Target._instanceClockSetting;

    [LuaMember]
    public int _instanceTime => Target._instanceTime;

    [LuaMember]
    public PatternInstanceManagerProxy? _patternInstance => PatternInstanceManagerProxy.Proxy(Target._patternInstance);

    // TODO: _peersInInstance ?

    [LuaMember]
    public bool _playActionMusic => Target._playActionMusic;
}
