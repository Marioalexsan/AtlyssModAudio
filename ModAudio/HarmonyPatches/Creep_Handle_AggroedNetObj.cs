using HarmonyLib;
using System.Reflection;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch]
static class Creep_Handle_AggroedNetObj
{
    static MethodInfo TargetMethod() => AccessTools.GetDeclaredMethods(typeof(Creep)).First(x => x.Name.Contains("Handle_AggroedNetObj"));

    static void Prefix(Creep __instance)
    {
        if (__instance && __instance.Network_aggroedEntity)
            TrackedAggroCreeps.Creeps.Add(__instance);
    }
}

public static class TrackedAggroCreeps
{
    public static HashSet<Creep> Creeps { get; } = [];
}