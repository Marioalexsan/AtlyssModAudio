using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Marioalexsan.ModAudio;

public static class SoftDependencies
{
    public const MethodImplOptions MethodOpts = MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization;

    public const string EasySettings = "EasySettings";
    public const string Homebrewery = "Homebrewery";
    
    public static bool HasEasySettings() => HasDependency(EasySettings, new Version(1, 3, 0));
    public static bool HasHomebrewery() => HasDependency(Homebrewery);

    public static bool HasDependency(string modId, Version? minimumVersion = null)
    {
        return DependencyHandler != null && DependencyHandler(modId, out var version) && (minimumVersion == null || version >= minimumVersion);
    }
    
    public static bool HasDependency(string modId, [NotNullWhen(true)] out Version? currentVersion)
    {
        currentVersion = null;
        return DependencyHandler != null && DependencyHandler(modId, out currentVersion);
    }
    
    internal delegate bool DependencyHandlerDelegate(string modId, [NotNullWhen(true)] out Version? version);
    internal static DependencyHandlerDelegate? DependencyHandler = null;
}