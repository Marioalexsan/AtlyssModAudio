using Unity.Profiling;

namespace Marioalexsan.ModAudio;

static class Profiling
{
    public static readonly ProfilerMarker Routing = new ProfilerMarker("ModAudio Route()");
    public static readonly ProfilerMarker RoutingReplacements = new ProfilerMarker("ModAudio Route() replacements");
    public static readonly ProfilerMarker RoutingOverlays = new ProfilerMarker("ModAudio Route() overlays");
    public static readonly ProfilerMarker SourceMatching = new ProfilerMarker("ModAudio Route() source matching mapname");
    public static readonly ProfilerMarker PlayRouting = new ProfilerMarker("ModAudio Route() from AudioPlayed()");
    public static readonly ProfilerMarker DetectNewSources = new ProfilerMarker("ModAudio Update() fetch audio sources");
    public static readonly ProfilerMarker PlayOnAwakeHandling = new ProfilerMarker("ModAudio Update() playOnAwake handling");
    public static readonly ProfilerMarker ExecuteUpdate = new ProfilerMarker("ModAudio Update() execute script updates");
    public static readonly ProfilerMarker ExecuteTargetGroups = new ProfilerMarker("ModAudio Update() execute script target groups");
    public static readonly ProfilerMarker CleanupSources = new ProfilerMarker("ModAudio Update() cleanup sources");
    public static readonly ProfilerMarker UpdateTargeting = new ProfilerMarker("ModAudio Update() update targeting");
    
    public static readonly ProfilerMarker UpdateLogsMarker = new ProfilerMarker("ModAudio debug menu update");
    public static readonly ProfilerMarker RenderLogsMarker = new ProfilerMarker("ModAudio debug menu render");
    
    public static readonly ProfilerMarker LoadFinalizer = new ProfilerMarker("ModAudio time spent finalizing loads");
    
    public static readonly ProfilerMarker AudioSourcePlayOneShotHelper = new ProfilerMarker("ModAudio hook AudioSource.PlayOneShotHelper()");
    public static readonly ProfilerMarker AudioSourceStop = new ProfilerMarker("ModAudio hook AudioSource.Stop()");
    public static readonly ProfilerMarker AudioSourcePlay = new ProfilerMarker("ModAudio hook AudioSource.Play()");
    public static readonly ProfilerMarker AudioSourcePlayHelper = new ProfilerMarker("ModAudio hook AudioSource.PlayHelper()");
}