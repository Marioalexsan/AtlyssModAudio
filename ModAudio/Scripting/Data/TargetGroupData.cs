using Lua;
using System;
using System.Collections.Generic;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class TargetGroupData(ModAudioSource target)
{
    public ModAudioSource Source = target;

    [LuaMember("targetGroup")]
    public string TargetGroup { get; set; } = AudioEngine.TargetGroupAll;

    [LuaMember("skipRoute")]
    public bool SkipRoute { get; set; }

    [LuaMember("originalClipName")]
    public string? OriginalClipName => Source.InitialState.Clip?.name;

    [LuaMember("clipName")]
    public string? ClipName => Source.CurrentState.Clip?.name;

    [LuaMember("gameObjectName")]
    public string ObjectName => Source.Audio.gameObject.name;

    [LuaMember("getGameObjectHierarchy")]
    public LuaTable GetGameObjectHierarchy()
    {
        var table = new LuaTable();

        var transform = Source.Audio.gameObject.transform;

        int count = 0;

        while (transform != null)
        {
            table[++count] = transform.name;
            transform = transform.parent;
        }

        return table;
    }

    [LuaMember("hasGameObjectInHierarchy")]
    public bool HasGameObjectInHierarchy(string name)
    {
        var transform = Source.Audio.gameObject.transform;

        while (transform != null)
        {
            if (transform.name == name)
                return true;

            transform = transform.parent;
        }

        return false;
    }
}
