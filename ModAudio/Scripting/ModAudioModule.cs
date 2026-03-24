using Lua;
using System;
using System.Collections.Generic;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class ModAudioModule
{
    [LuaMember("context")]
    public static ILuaUserData? Context { get; internal set; } = null;

    [LuaMember("engine")]
    public static EngineData Engine { get; internal set; } = new EngineData();
}