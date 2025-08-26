using HarmonyLib;
using System.Reflection;

namespace Marioalexsan.ModAudio.HarmonyPatches;

[HarmonyPatch]
static class Creep_Handle_AggroedNetObj
{
    static MethodInfo TargetMethod() => AccessTools.GetDeclaredMethods(typeof(Creep)).First(x => x.Name.Contains("Handle_AggroedNetObj"));

    static void Prefix(Creep __instance)
    {
        // This should handle the server
        if (__instance && __instance.Network_aggroedEntity)
            TrackedAggroCreeps.Creeps.Add(__instance);
    }
}

[HarmonyPatch(typeof(Creep), nameof(Creep.OnChangeAggroTarget))]
static class Creep_OnChangeAggroTarget
{
    static void Prefix(Creep __instance, StatusEntity _new)
    {
        // This should handle clients
        if (__instance && _new)
            TrackedAggroCreeps.Creeps.Add(__instance);
    }
}

public static class TrackedAggroCreeps
{
    public static HashSet<Creep> Creeps { get; } = [];
}