using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class ActionBarManagerProxy
{
    private ActionBarManagerProxy(ActionBarManager target)
    {
        Target = target;
    }

    public static ActionBarManagerProxy? Proxy(ActionBarManager? target) => target != null ? new(target) : null;

    public readonly ActionBarManager Target;

    [LuaMember]
    public int _currentHighlightedSlotIndex => Target._currentHighlightedSlotIndex;

    [LuaMember]
    public bool _isHighlightingSlot => Target._isHighlightingSlot;
}
