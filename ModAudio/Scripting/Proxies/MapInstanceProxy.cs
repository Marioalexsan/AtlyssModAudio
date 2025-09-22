using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class MapInstanceProxy(MapInstance target)
{
    public readonly MapInstance Target = target;

    [LuaMember(nameof(MapInstance._mapName))]
    public string _mapName => Target._mapName;
}
