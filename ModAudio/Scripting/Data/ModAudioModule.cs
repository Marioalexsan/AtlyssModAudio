using Lua;
using System;
using System.Collections.Generic;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class ModAudioModule
{
    [LuaMember("context")]
    public static ContextData Context { get; } = new ContextData();

    [LuaMember("engine")]
    public static EngineData Engine { get; } = new EngineData();
}
