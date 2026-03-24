using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;

namespace Marioalexsan.ModAudio.Atlyss;

[BepInPlugin("Marioalexsan.ModAudio.Atlyss", "ModAudio (ATLYSS Features)", Marioalexsan.ModAudio.ModInfo.VERSION)]
[BepInDependency("Marioalexsan.ModAudio")]
[BepInProcess("ATLYSS.exe")]
public class BepinexEntrypoint : BaseUnityPlugin
{
    private Harmony Harmony = new Harmony("Marioalexsan.ModAudio.Atlyss");
    
    public void Awake()
    {
        ModAudio.RegisterGameImplementation(new AtlyssGame());
        Harmony.PatchAll();
    }
}