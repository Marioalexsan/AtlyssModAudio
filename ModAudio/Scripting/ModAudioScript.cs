using System.Diagnostics;
using BepInEx.Logging;
using Lua;
using Lua.Standard;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting;

/// <summary>
/// Note: this is mostly to avoid referencing Lua types as part of signatures in other classes
/// </summary>
public interface IModAudioScript : IDisposable
{
    public void ExecuteUpdate();
    public void Start();
    public bool HasExportedMethod(string name);
    public void ExecuteTargetGroup(Route route, TargetGroupData routeData);
}

public class ModAudioScript : IModAudioScript
{
    private string _rootScript;
    private LuaState _luaState;
    private LuaTable _rootModule;

    public AudioPack Pack { get; }

    public ModAudioScript(AudioPack pack, string rootScript)
    {
        Pack = pack;

        _luaState = LuaState.Create();
        _rootModule = new LuaTable();
        _rootScript = rootScript;

        _luaState.OpenBasicLibrary();
        _luaState.OpenBitwiseLibrary();
        _luaState.OpenTableLibrary();
        _luaState.OpenStringLibrary();
        _luaState.OpenMathLibrary();

        _luaState.Environment["print"] = new LuaFunction("print", Print);
        _luaState.Environment["accessPath"] = new LuaFunction("print", AccessPath);

        ModAudioModule.Context = AudioEngine.Game.Context as ILuaUserData;
        var gameData = AudioEngine.Game.GameData as ILuaUserData;
        
        if (gameData == null || ModAudioModule.Context == null)
            AudioDebugDisplay.LogScript(LogLevel.Error, null, "Either game data or context for Lua was null! Please report this to the mod developer!");
        
        _luaState.Environment["modaudio"] = LuaValue.FromUserData(new ModAudioModule());
        _luaState.Environment["game"] = LuaValue.FromUserData(gameData);
        
        // Atlyss used to grab its data from this property; keep it around for backwards compatibility
        _luaState.Environment["atlyss"] = LuaValue.FromUserData(gameData);
    }

    public void Dispose()
    {
        _luaState.Dispose();
    }
    
    private CancellationTokenSource GetTokenSourceWithExecutionLimitsForInitialization()
    {
        // Script initializers will be killed if they take more than 2500ms to execute
        // Note: script initialization is ass and the first script init can take significantly more time
        // than the rest
        return new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
    }

    private CancellationTokenSource GetTokenSourceWithExecutionLimits()
    {
        // Scripts will be killed if they take more than 100ms to execute
        // This is equivalent to eating 6-7 frames' worth of time at 60 FPS, which means something is likely going terribly wrong
        // Ideally, script calls should be up to 1-5ms at most
        return new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
    }

    // NOTE: Lua-CSharp doesn't have "..." / "arg" implemented for some reason, so we need to do this in script land
    private static async ValueTask<int> AccessPath(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.ArgumentCount == 0)
            return context.Return(LuaValue.Nil);

        LuaValue target = context.GetArgument(0);

        for (int i = 1; i < context.ArgumentCount; i++)
        {
            if (target.Type == LuaValueType.Nil)
                return context.Return(LuaValue.Nil);

            target = await context.State.GetTable(target, context.GetArgument(i));
        }

        return context.Return(target);
    }

    private ValueTask<int> Print(LuaFunctionExecutionContext context, CancellationToken cancellationToken)
    {
        var stringBuilder = new StringBuilder();

        for (int i = 0; i < context.ArgumentCount; i++)
        {
            var value = context.GetArgument(i);

            stringBuilder.Append(value.ToString());

            if (i != context.ArgumentCount - 1)
                stringBuilder.Append(' ');
        }

        AudioDebugDisplay.LogScript(LogLevel.Info, Pack, stringBuilder.ToString());

        return new(context.Return());
    }

    public void Start()
    {
        if (Pack.HasFlag(PackFlags.ForceDisableScripts))
            return;

        try
        {
            var tokenSource = GetTokenSourceWithExecutionLimitsForInitialization();

            var stopwatch = Stopwatch.StartNew();
            var result = _luaState.DoStringAsync(_rootScript, $"__routes.lua for {Pack.Config.Id}", tokenSource.Token).Result;
            stopwatch.Stop();
            _rootScript = "";

            if (result.Length == 0 || !result[0].TryRead<LuaTable>(out var table))
                throw new InvalidOperationException("Expected an exported table from the Lua module!");
            
            AudioDebugDisplay.LogScript(LogLevel.Debug, null, $"Loading __routes.lua for {Pack.Config.Id} took {stopwatch.Elapsed.TotalMilliseconds:F2}ms.");

            _rootModule = table;
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogScript(LogLevel.Error, null, $"The root module for pack {Pack.Config.Id} encountered an error!");
            AudioDebugDisplay.LogScript(LogLevel.Error, null, $"Exception data: {e}");
            Pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
        }
    }

    public bool HasExportedMethod(string name)
    {
        return _rootModule[name].TryRead<LuaFunction>(out _);
    }

    public void ExecuteUpdate()
    {
        if (Pack.HasFlag(PackFlags.ForceDisableScripts))
            return;

        if (!_rootModule[Pack.Config.PackScripts.Update].TryRead<LuaFunction>(out var update))
        {
            // Silently skip - pack loading should have notified about this during load
            Pack.SetFlag(PackFlags.HasEncounteredErrors);
            return;
        }

        PreScriptActions();
        try
        {
            var tokenSource = GetTokenSourceWithExecutionLimits();

            _ = _luaState.Call(update, [], tokenSource.Token).Result;
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogScript(LogLevel.Error, Pack, $"Update script call failed for pack {Pack.Config.Id}, script {Pack.Config.PackScripts.Update}!");
            AudioDebugDisplay.LogScript(LogLevel.Error, Pack, $"Exception data: {e}");
            Pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
        }
        PostScriptActions();
    }

    public void ExecuteTargetGroup(Route route, TargetGroupData routeData)
    {
        if (Pack.HasFlag(PackFlags.ForceDisableScripts))
        {
            routeData.SkipRoute = true;
            return;
        }

        if (string.IsNullOrEmpty(route.TargetGroupScript))
        {
            routeData.TargetGroup = AudioEngine.TargetGroupAll;
            return;
        }

        if (!_rootModule[route.TargetGroupScript].TryRead<LuaFunction>(out var targetGroup))
        {
            // Silently skip - pack loading should have notified about this during load
            Pack.SetFlag(PackFlags.HasEncounteredErrors);
            routeData.SkipRoute = true;
            return;
        }

        PreScriptActions();
        try
        {
            var tokenSource = GetTokenSourceWithExecutionLimits();

            // TODO: Check whenever scripts are allocating way too much total memory
            _ = _luaState.Call(targetGroup, [new LuaValue(routeData)], tokenSource.Token).Result;
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogScript(LogLevel.Error, Pack, $"Target group script call failed for pack {Pack.Config.Id}, script {route.TargetGroupScript}!");
            AudioDebugDisplay.LogScript(LogLevel.Error, Pack, $"Exception data: {e}");
            Pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
            routeData.SkipRoute = true;
        }
        PostScriptActions();
    }

    private void PreScriptActions()
    {
    }

    private void PostScriptActions()
    {
    }
}