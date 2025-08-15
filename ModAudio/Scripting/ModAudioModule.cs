using Jint;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Runtime.Modules;

namespace Marioalexsan.ModAudio.Scripting;
internal static class ModAudioModule
{
    public static void Build(Engine engine, ModuleBuilder module)
    {
        module.ExportObject("context", WrapContextProvider(engine));
        module.ExportObject("engine", WrapAudioEngine(engine));
    }

    public static JsObject WrapContextProvider(Engine engine)
    {
        var wrapper = new JsObject(engine);

        wrapper.AddGet("mapName", (self, args) => ContextAPI.MapName);
        wrapper.AddGet("mapSubregion", (self, args) => ContextAPI.MapSubregion);
        wrapper.AddGet("enemiesTargetingPlayer", (self, args) => ContextAPI.EnemiesTargetingPlayer);
        wrapper.AddGet("secondsSinceGameStart", (self, args) => ContextAPI.SecondsSinceGameStart);

        wrapper.PreventExtensions();

        return wrapper;
    }

    public static JsObject WrapAudioEngine(Engine engine)
    {
        var wrapper = new JsObject(engine);

        wrapper.AddReadonlyData("forceCombatMusic", new ClrFunction(engine, "forceCombatMusic", (self, args) =>
        {
            if (args.Length < 1 || !args[0].IsBoolean())
                throw new InvalidOperationException("Expected a boolean parameter for first argument of forceCombatMusic.");

            AudioEngineAPI.ForceCombatMusic = args[0].AsBoolean();

            return JsValue.Undefined;
        }));

        wrapper.PreventExtensions();

        return wrapper;
    }
}
