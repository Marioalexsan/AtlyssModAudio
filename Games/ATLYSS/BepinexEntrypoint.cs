using BepInEx;
using HarmonyLib;

namespace Marioalexsan.ModAudio.Atlyss;

[BepInPlugin("Marioalexsan.ModAudio.Atlyss", "ModAudio (ATLYSS Features)", ModInfo.VERSION)]
[BepInDependency("Marioalexsan.ModAudio")]
[BepInProcess("ATLYSS.exe")]
public class BepinexEntrypoint : BaseUnityPlugin
{
    private Harmony Harmony = new Harmony("Marioalexsan.ModAudio.Atlyss");
    
    public void Awake()
    {
        var game = new AtlyssGame();
        ModAudio.RegisterGameImplementation(game);
        Harmony.PatchAll();
    }
}