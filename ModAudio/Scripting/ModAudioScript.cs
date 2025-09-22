using BepInEx.Logging;
using Lua;
using Lua.Standard;
using Marioalexsan.ModAudio.HarmonyPatches;
using Marioalexsan.ModAudio.Scripting.Data;
using Marioalexsan.ModAudio.Scripting.Proxies;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting;

public class ModAudioScript : IDisposable
{
    private string _rootScript;
    private LuaState _luaState;
    private LuaTable _rootModule;

    public AudioPack Pack { get; }

    private readonly CancellationTokenSource _tokenSource = new();

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

        _luaState.Environment["modaudio"] = LuaValue.FromUserData(new ModAudioModule());
        _luaState.Environment["atlyss"] = LuaValue.FromUserData(new AtlyssModule());
    }

    public void Dispose()
    {
        _luaState.Dispose();
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

        AudioDebugDisplay.LogScript(LogLevel.Info, $"[{Pack.Config.DisplayName}] {stringBuilder}");

        return new(context.Return());
    }

    public void Start()
    {
        if (Pack.HasFlag(PackFlags.ForceDisableScripts))
            return;

        try
        {
            var tokenSource = GetTokenSourceWithExecutionLimits();

            var result = _luaState.DoStringAsync(_rootScript, $"__routes.lua for {Pack.Config.Id}", tokenSource.Token).Result;
            _rootScript = "";

            if (result.Length == 0 || !result[0].TryRead<LuaTable>(out var table))
                throw new InvalidOperationException("Expected an exported table from the Lua module!");

            _rootModule = table;
        }
        catch (Exception e)
        {
            AudioDebugDisplay.LogPack(LogLevel.Error, $"The root module for pack {Pack.Config.Id} encountered an error!");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
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
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Cannot execute update method for {Pack.Config.Id}: an update script is missing!");
            Pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
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
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Update script call failed for pack {Pack.Config.Id}, script {Pack.Config.PackScripts.Update}!");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
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
            AudioDebugDisplay.LogPack(LogLevel.Error, $"A script method for {Pack.Config.Id} is missing for some reason!");
            Pack.SetFlag(PackFlags.HasEncounteredErrors | PackFlags.ForceDisableScripts);
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
            AudioDebugDisplay.LogPack(LogLevel.Error, $"Target group script call failed for pack {Pack.Config.Id}, script {route.TargetGroupScript}!");
            AudioDebugDisplay.LogPack(LogLevel.Error, e.ToString());
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

    public static void TriggerNewFrame()
    {
        TrackedAggroCreeps.Creeps.RemoveWhere(x => x == null || x.Network_aggroedEntity == null);

        ContextData.AggroedEnemies.Clear();

        int index = 1;

        foreach (var creep in TrackedAggroCreeps.Creeps)
            ContextData.AggroedEnemies[index++] = LuaValue.FromUserData(new CreepProxy(creep));
    }
}