using Lua;

namespace Marioalexsan.ModAudio.Scripting.Proxies;

[LuaObject]
public partial class MainMenuManagerProxy
{
    private MainMenuManagerProxy(MainMenuManager target)
    {
        Target = target;
    }

    public static MainMenuManagerProxy? Proxy(MainMenuManager? target) => target != null ? new(target) : null;

    public readonly MainMenuManager Target;

    [LuaMember]
    public int _mainMenuSelection => Target._mainMenuSelection;

    [LuaMember]
    public int _multiplayerMenuSelection => Target._multiplayerMenuSelection;

    [LuaMember]
    public bool _inMultiplayerMenu => Target._inMultiplayerMenu;
}
