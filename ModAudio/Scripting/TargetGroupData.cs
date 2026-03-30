using Lua;

namespace Marioalexsan.ModAudio.Scripting;

[LuaObject]
public partial class TargetGroupData
{
    public ModAudioSource Source { get; set; } = null!;

    [LuaMember("targetGroup")]
    public string TargetGroup { get; set; } = AudioEngine.TargetGroupAll;

    [LuaMember("skipRoute")]
    public bool SkipRoute { get; set; }

    [LuaMember("originalClipName")]
    public string? OriginalClipName => Source.InitialState.Clip?.name;

    [LuaMember("clipName")]
    public string? ClipName => Source.Audio.clip?.name;

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
