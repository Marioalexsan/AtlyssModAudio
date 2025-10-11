using Lua;
using Marioalexsan.ModAudio.Scripting.Proxies;
using System;
using System.Collections.Generic;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class AtlyssModule
{
    [LuaMember("mainPlayer")]
    public static PlayerProxy? MainPlayer => PlayerProxy.Proxy(Player._mainPlayer);

    [LuaMember("actionBarManager")]
    public static ActionBarManagerProxy? ActionBarManager => ActionBarManagerProxy.Proxy(global::ActionBarManager._current);

    [LuaMember("gameWorldManager")]
    public static GameWorldManagerProxy? GameWorldManager => GameWorldManagerProxy.Proxy(global::GameWorldManager._current);

    [LuaMember("shopkeepManager")]
    public static ShopkeepManagerProxy? ShopkeepManager => ShopkeepManagerProxy.Proxy(global::ShopkeepManager._current);

    [LuaMember("mainMenuManager")]
    public static MainMenuManagerProxy? MainMenuManager => MainMenuManagerProxy.Proxy(global::MainMenuManager._current);

    [LuaMember("inGameUI")]
    public static InGameUIProxy? InGameUI => InGameUIProxy.Proxy(global::InGameUI._current);
}
