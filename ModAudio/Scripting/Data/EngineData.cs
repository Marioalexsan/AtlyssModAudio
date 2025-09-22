using Lua;
using Marioalexsan.ModAudio.HarmonyPatches;

namespace Marioalexsan.ModAudio.Scripting.Data;

[LuaObject]
public partial class EngineData
{
    [LuaMember("forceCombatMusic")]
    public static void ForceCombatMusic(bool enabled) => MapInstance_Handle_AudioSettings.ForceCombatMusic = enabled;
}
