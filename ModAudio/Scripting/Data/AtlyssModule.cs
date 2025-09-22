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
    public static PlayerProxy? MainPlayer => Player._mainPlayer != null ? new PlayerProxy(Player._mainPlayer) : null;

    [LuaMember("actionBarManager")]
    public static ActionBarManagerProxy ActionBarManager => new ActionBarManagerProxy(global::ActionBarManager._current);

    [LuaMember("gameWorldManager")]
    public static GameWorldManagerProxy GameWorldManager => new GameWorldManagerProxy(global::GameWorldManager._current);

    [LuaMember("shopkeepManager")]
    public static ShopkeepManagerProxy ShopkeepManager => new ShopkeepManagerProxy(global::ShopkeepManager._current);

    [LuaMember("mainMenuManager")]
    public static MainMenuManagerProxy MainMenuManager => new MainMenuManagerProxy(global::MainMenuManager._current);

    [LuaMember("inGameUI")]
    public static InGameUIProxy InGameUI => new InGameUIProxy(global::InGameUI._current);
}
