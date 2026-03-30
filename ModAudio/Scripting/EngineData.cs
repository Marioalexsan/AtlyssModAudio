using Lua;

namespace Marioalexsan.ModAudio.Scripting;

[LuaObject]
public partial class EngineData
{
    [LuaMember("forceCombatMusic")]
    public static void ForceCombatMusic(bool enabled) => AudioEngine.Game.Specialized_ForceCombatMusic(enabled);
}
