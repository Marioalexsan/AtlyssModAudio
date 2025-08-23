using BepInEx.Logging;
using Jint;
using Jint.Native;
using Jint.Native.Json;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting;
internal static class ScriptingEngine
{
    public static Engine SetupJint()
    {
        var engine = new Engine(options =>
        {
            options.Strict = true; // Best practice
            options.Interop.AllowWrite = false; // Readonly
            options.Interop.ObjectWrapperReportedMemberTypes = System.Reflection.MemberTypes.Field | System.Reflection.MemberTypes.Property; // Only properties and fields (i.e. data)
            options.Interop.ObjectWrapperReportedFieldBindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            options.Interop.ObjectWrapperReportedPropertyBindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            options.LimitMemory(4 * 1024 * 1024); // Max 4MB before script is murdered
            options.TimeoutInterval(TimeSpan.FromMilliseconds(100)); // Max 100ms before script is murdered (~6 frames, which is overkill)
        });

        AddConsoleAPI(engine);

        engine.Modules.Add("modaudio", module => ModAudioModule.Build(engine, module));
        engine.Modules.Add("atlyss", module => AtlyssModule.Build(engine, module));

        return engine;
    }

    private static void AddConsoleAPI(Engine jsEngine)
    {
        var serializer = new JsonSerializer(jsEngine);

        var console = new JsObject(jsEngine);

        console.DefineOwnProperty("log", new PropertyDescriptor(
            new ClrFunction(jsEngine, "log", (self, arguments) => Log(serializer, arguments, LogLevel.Info)),
            false, true, false
            ));


        console.DefineOwnProperty("error", new PropertyDescriptor(
            new ClrFunction(jsEngine, "error", (self, arguments) => Log(serializer, arguments, LogLevel.Error)),
            false, true, false
            ));

        console.PreventExtensions();

        jsEngine.Global.DefineOwnProperty("console", new PropertyDescriptor(console, false, true, false));
    }

    private static JsValue Log(JsonSerializer serializer, JsValue[] arguments, LogLevel level)
    {
        var repr = new StringBuilder("[JS]: ");

        for (int i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is JsString str)
            {
                repr.Append(str.AsString());
            }
            else if (arguments[i] is JsError error)
            {
                repr.Append(error["message"].AsString());
                repr.Append(' ');
                repr.Append(error["stack"].AsString());
            }
            else
            {
                repr.Append(serializer.Serialize(arguments[i]).ToString());
            }

            if (i != arguments.Length - 1)
            {
                repr.Append(' ');
            }
        }

        switch (level)
        {
            case LogLevel.Info:
                Logging.LogInfo(repr.ToString());
                break;
            case LogLevel.Error:
                Logging.LogError(repr.ToString());
                break;
        }

        return JsValue.Undefined;
    }
}
