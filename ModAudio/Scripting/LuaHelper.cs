using Lua;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Marioalexsan.ModAudio.Scripting;

internal static class LuaHelper
{
    public static void ThrowReadonly([CallerMemberName] string? name = null) => throw new InvalidOperationException($"Member {name ?? "<unknown>"} is readonly.");
}
