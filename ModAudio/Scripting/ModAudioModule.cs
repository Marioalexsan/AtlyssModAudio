using Lua;

namespace Marioalexsan.ModAudio.Scripting;

[LuaObject]
public partial class ModAudioModule
{
    [LuaMember("context")]
    public static ILuaUserData? Context { get; internal set; }

    [LuaMember("engine")]
    public static EngineData Engine { get; internal set; } = new EngineData();
}