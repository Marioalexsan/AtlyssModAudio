using Lua;
using System;
using System.Collections.Generic;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class TargetGroupData
{
    [LuaMember("targetGroup")]
    public string TargetGroup { get; set; } = AudioEngine.TargetGroupAll;

    [LuaMember("skipRoute")]
    public bool SkipRoute { get; set; }
}
