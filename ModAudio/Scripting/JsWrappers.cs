using Jint;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Marioalexsan.ModAudio.Scripting;

internal static class JsWrappers
{
    public static void AddReadonlyData(this JsObject obj, string name, JsValue value)
    {
        obj.DefineOwnProperty(name, new PropertyDescriptor(value, false, true, false));
    }

    public static void AddData(this JsObject obj, string name, JsValue value)
    {
        obj.DefineOwnProperty(name, new PropertyDescriptor(value, true, true, false));
    }

    public static void AddGet(this JsObject obj, string name, Func<JsValue, JsValue[], JsValue> get)
    {
        var jsGet = new ClrFunction(obj.Engine, $"get {name}", get);
        obj.DefineOwnProperty(name, new GetSetPropertyDescriptor(jsGet, null, true, false));
    }

    public static void AddGetSet(this JsObject obj, string name, Func<JsValue, JsValue[], JsValue> get, Func<JsValue, JsValue[], JsValue> set)
    {
        var jsGet = new ClrFunction(obj.Engine, $"get {name}", get);
        var jsSet = new ClrFunction(obj.Engine, $"set {name}", set);
        obj.DefineOwnProperty(name, new GetSetPropertyDescriptor(jsGet, jsSet, true, false));
    }
}
