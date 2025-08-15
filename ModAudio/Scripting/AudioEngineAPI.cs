using Marioalexsan.ModAudio.HarmonyPatches;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Marioalexsan.ModAudio.Scripting;
internal static class AudioEngineAPI
{
    public static bool ForceCombatMusic { get; set; }

    public static void UpdateGameState()
    {
        ForceCombatMusic = MapInstance_Handle_AudioSettings.ForceCombatMusic;
    }
}
