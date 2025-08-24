using Jint;
using Jint.Native;

namespace Marioalexsan.ModAudio.Scripting;

internal class TargetGroupRouteAPI(in ModAudioSource state)
{
    public string TargetGroup { get; set; } = AudioEngine.TargetGroupAll;
    public bool SkipRoute { get; set; }

    public JsObject Wrap(Engine engine)
    {
        var wrapper = new JsObject(engine);

        wrapper.AddGetSet("targetGroup", (self, args) => TargetGroup, (self, args) =>
        {
            TargetGroup = args[0].AsString();
            return JsValue.Undefined;
        });

        wrapper.AddGetSet("skipRoute", (self, args) => SkipRoute, (self, args) =>
        {
            SkipRoute = args[0].AsBoolean();
            return JsValue.Undefined;
        });

        return wrapper;
    }
}
