using Jint;
using Jint.Native;
using Marioalexsan.ModAudio.HarmonyPatches;

namespace Marioalexsan.ModAudio.Scripting;

internal class TargetGroupRouteAPI(in AudioSourceState state)
{
    public string TargetGroup { get; set; } = state.RouteGroup ?? "";

    public JsObject Wrap(Engine engine)
    {
        var wrapper = new JsObject(engine);

        wrapper.AddGetSet("targetGroup", (self, args) => TargetGroup, (self, args) =>
        {
            TargetGroup = args[0].AsString();
            return JsValue.Undefined;
        });

        return wrapper;
    }
}
