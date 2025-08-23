using Jint;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Marioalexsan.ModAudio.Scripting;
internal static class AtlyssModule
{
    public static void Build(Engine engine, ModuleBuilder module)
    {
        module.ExportObject("game", Instances(engine));
    }

    public static JsObject Instances(Engine engine)
    {
        var wrapper = new JsObject(engine);

        wrapper.AddGet("mainPlayer", (self, arguments) => Player._mainPlayer ? ObjectWrapper.Create(engine, Player._mainPlayer) : JsValue.Null);
        wrapper.AddGet("actionBarManager", (self, arguments) => ActionBarManager._current ? ObjectWrapper.Create(engine, ActionBarManager._current) : JsValue.Null);
        wrapper.AddGet("gameWorldManager", (self, arguments) => GameWorldManager._current ? ObjectWrapper.Create(engine, GameWorldManager._current) : JsValue.Null);
        wrapper.AddGet("shopkeepManager", (self, arguments) => ShopkeepManager._current ? ObjectWrapper.Create(engine, ShopkeepManager._current) : JsValue.Null);
        wrapper.AddGet("mainMenuManager", (self, arguments) => MainMenuManager._current ? ObjectWrapper.Create(engine, MainMenuManager._current) : JsValue.Null);
        wrapper.AddGet("inGameUI", (self, arguments) => InGameUI._current ? ObjectWrapper.Create(engine, InGameUI._current) : JsValue.Null);

        wrapper.PreventExtensions();

        return wrapper;
    }
}
