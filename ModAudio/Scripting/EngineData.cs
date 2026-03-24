using Lua;
using Marioalexsan.ModAudio.HarmonyPatches;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class EngineData
{
    [LuaMember("forceCombatMusic")]
    public static void ForceCombatMusic(bool enabled) => AudioEngine.Game.Specialized_ForceCombatMusic(enabled);
}
