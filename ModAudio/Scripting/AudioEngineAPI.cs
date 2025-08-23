using Marioalexsan.ModAudio.HarmonyPatches;

namespace Marioalexsan.ModAudio.Scripting;
internal static class AudioEngineAPI
{
    public static bool ForceCombatMusic { get; set; }

    public static void UpdateGameState()
    {
        ForceCombatMusic = MapInstance_Handle_AudioSettings.ForceCombatMusic;
    }
}
