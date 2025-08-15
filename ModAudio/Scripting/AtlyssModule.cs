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

        wrapper.AddGet("mainPlayer", (self, arguments) => ObjectWrapper.Create(engine, Player._mainPlayer));
        wrapper.AddGet("actionBarManager", (self, arguments) => ObjectWrapper.Create(engine, MainMenuManager._current));
        wrapper.AddGet("gameWorldManager", (self, arguments) => ObjectWrapper.Create(engine, GameWorldManager._current));
        wrapper.AddGet("shopkeepManager", (self, arguments) => ObjectWrapper.Create(engine, ShopkeepManager._current));

        wrapper.PreventExtensions();

        return wrapper;
    }
}
