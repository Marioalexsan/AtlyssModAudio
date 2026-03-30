using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Marioalexsan.ModAudio;

public static class SoftDependencies
{
    public const MethodImplOptions MethodOpts = MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization;

    public static bool HasEasySettings(Version? minimumVersion = null) => HasDependency("EasySettings", minimumVersion);
    public static bool HasHomebrewery(Version? minimumVersion = null) => HasDependency("Homebrewery", minimumVersion);
    
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