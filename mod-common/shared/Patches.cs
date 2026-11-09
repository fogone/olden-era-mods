using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;

namespace Nobirds.OldenEra.Modding.Common;

// Patching the game's methods safely, and watching its calls when looking for how it does something.
//
//   harmony.PatchEach(methods, postfix: new HarmonyMethod(typeof(Plugin), nameof(Postfix)))
//       // each native code once (clones fold into one); not one whose code other methods share (the patch would run
//       // for all of them: a hot shared stub slows the game down, see cxg.wdq in docs/game-internals.md)
//   Probe.Calls(harmony, methods, () => filling, line => logger.LogMessage(line))
//       // debug: every call of the methods while the condition holds, with the object, the arguments, the result
public static class Patches
{
    // Patches the methods, each native code once; leaves out (and says so) those sharing code with methods outside
    // the given ones. Returns the methods patched.
    public static List<MethodInfo> PatchEach(this Harmony harmony, IEnumerable<MethodInfo> methods,
                                             HarmonyMethod prefix = null, HarmonyMethod postfix = null, bool allowShared = false)
    {
        var list = methods.Where(m => m != null).ToList();
        var patched = new List<MethodInfo>();
        var seen = new HashSet<IntPtr>();
        foreach (var method in list)
        {
            if (!seen.Add(Game.NativeAddress(method)))
                continue;
            var others = allowShared ? new List<MethodInfo>() : GameCode.SharedWith(method).Except(list).ToList();
            if (others.Count > 0)
            {
                Game.Log?.Invoke($"Patch: {Members.Name(method)} not patched, its code is shared with {others.Count}: {string.Join(", ", others.Take(6).Select(Members.Name))}");
                continue;
            }
            try
            {
                harmony.Patch(method, prefix: prefix, postfix: postfix);
                patched.Add(method);
            }
            catch (Exception e) { Game.Log?.Invoke($"Patch: {Members.Name(method)} not patched: {e.GetType().Name}: {e.Message}"); }
        }
        return patched;
    }

    // One method of the game, `what` it is for: true when patched; when it was not found (null) or could not be
    // patched, false, and the log says what is off then. Every patch of our plugins goes through this or PatchEach.
    //   if (!Harmony.PatchOne(Game.SquadTooltipFill, "the squad tooltip's fill", postfix: ...)) return;
    public static bool PatchOne(this Harmony harmony, MethodInfo method, string what, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
    {
        if (method == null)
        {
            Game.Log?.Invoke($"Patch: {what}: the game's method not found, so off");
            return false;
        }
        if (harmony.PatchEach(new[] { method }, prefix, postfix).Count > 0)
            return true;
        Game.Log?.Invoke($"Patch: {what} ({Members.Name(method)}) not patched, so off");
        return false;
    }
}

// Debug: watching the game's calls of some methods (a postfix that only writes to the log).
internal static class Probe
{
    static Func<bool> when = () => true;
    static Action<string> log;
    static int left;

    // Every call of the methods while `condition` holds, at most `limit` lines: "CALL Type.method on <class> (args) =
    // result". Returns the methods patched (not those sharing code with others).
    public static List<MethodInfo> Calls(Harmony harmony, IEnumerable<MethodInfo> methods, Func<bool> condition, Action<string> output, int limit = 200)
    {
        when = condition ?? (() => true);
        log = output;
        left = limit;
        var patched = new List<MethodInfo>();
        foreach (var method in methods)
        {
            var name = (method.IsStatic ? "Static" : "Instance") + (method.ReturnType == typeof(void) ? "Void" : "Value");
            patched.AddRange(harmony.PatchEach(new[] { method }, postfix: new HarmonyMethod(typeof(Probe), name)));
        }
        return patched;
    }

    static void StaticVoid(MethodBase __originalMethod, object[] __args) => Write(__originalMethod, null, __args, null, false);
    static void InstanceVoid(MethodBase __originalMethod, object __instance, object[] __args) => Write(__originalMethod, __instance, __args, null, false);
    static void StaticValue(MethodBase __originalMethod, object[] __args, object __result) => Write(__originalMethod, null, __args, __result, true);
    static void InstanceValue(MethodBase __originalMethod, object __instance, object[] __args, object __result) => Write(__originalMethod, __instance, __args, __result, true);

    static void Write(MethodBase method, object instance, object[] args, object result, bool returns)
    {
        try
        {
            if (left <= 0 || !when())
                return;
            left--;
            log?.Invoke($"CALL {method.DeclaringType?.Name}.{method.Name}{(instance != null ? $" on {Describe(instance)}" : "")} " +
                        $"({string.Join(", ", (args ?? Array.Empty<object>()).Select(Describe))}){(returns ? $" = {Describe(result)}" : "")}");
        }
        catch (Exception e)
        {
            log?.Invoke($"CALL {method.Name}: {e.GetType().Name}");
        }
    }

    // A value for the log: an IL2CPP object by its class and address, the rest as it prints.
    public static string Describe(object value) => value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        Il2CppObjectBase o => $"{Game.ClassName(o.Pointer)}@{o.Pointer.ToInt64():X}",
        _ => value.ToString(),
    };
}
