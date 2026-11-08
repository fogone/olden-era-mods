using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using OldenEra.Modding.Common;
using UnityEngine;

namespace OldenEra.Modding.Research;

// Frame rate and the time our mods take, for finding what makes the game stutter.
//   fps            the last 10 seconds: frames per second, the slowest frames; with `perf on`, our code by time
//   perf on        time our mods' per-frame callbacks (Ticker) and every patch method of our plugins on the game's code
//   perf off       stop timing them
// Always on (cheap): the frame times; a frame over 200 ms goes to the log, with our code's share when timed.
internal static class Perf
{
    const int Frames = 1200;            // about 10-20 s of frames
    const float SpikeMs = 200f;
    static readonly float[] frameMs = new float[Frames];
    static readonly float[] ourMs = new float[Frames];
    static readonly float[] frameAt = new float[Frames];
    static int next, count;

    // Our code's time: per method, the ticks in this frame and in total since `perf on`.
    sealed class Spent
    {
        public long Ticks, ThisFrame, Max;
        public int Calls;
    }

    static readonly Dictionary<string, Spent> spent = new();
    static Harmony harmony;
    static bool timing;
    static long frameTicks;
    static float since;

    static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    // Every frame (the plugin's tick).
    public static void Frame()
    {
        var ms = Time.unscaledDeltaTime * 1000f;
        frameMs[next] = ms;
        ourMs[next] = (float)Ms(frameTicks);
        frameAt[next] = Time.realtimeSinceStartup;
        if (ms > SpikeMs)
        {
            var top = timing ? string.Join(", ", spent.Where(p => p.Value.ThisFrame > 0).OrderByDescending(p => p.Value.ThisFrame).Take(5)
                .Select(p => $"{p.Key} {Ms(p.Value.ThisFrame):0.0} ms")) : "not timed (perf on)";
            Log?.Invoke($"PERF slow frame {ms:0} ms, our code {Ms(frameTicks):0.0} ms: {top}");
        }
        next = (next + 1) % Frames;
        count = Math.Min(count + 1, Frames);
        frameTicks = 0;
        foreach (var s in spent.Values)
            s.ThisFrame = 0;
    }

    public static Action<string> Log;

    // Frames per second and the longest frame (ms) of the last seconds.
    public static (float fps, float worst) Recent(float seconds)
    {
        var now = Time.realtimeSinceStartup;
        float sum = 0f, worst = 0f;
        var frames = 0;
        for (var i = 0; i < count; i++)
        {
            if (now - frameAt[i] > seconds)
                continue;
            frames++;
            sum += frameMs[i];
            worst = Math.Max(worst, frameMs[i]);
        }
        return (sum > 0f ? frames * 1000f / sum : 0f, worst);
    }

    // The frames of the last seconds: rate, median and worst frame, slow ones; our code's share when timed, and the
    // most expensive of our methods since perf on (ms per second, calls per second, the longest call).
    public sealed class Stats
    {
        public float Span, Fps, Median, Worst, OursPerSecond, OursShare, OursWorst, TimedFor;
        public int Over33, Over100;
        public bool Timed;
        public List<(string name, double msPerSecond, double callsPerSecond, double max)> Top = new();
    }

    public static Stats Measure(float seconds, int top = 25)
    {
        var now = Time.realtimeSinceStartup;
        var frames = Enumerable.Range(0, count).Select(i => (ms: frameMs[i], our: ourMs[i], at: frameAt[i])).Where(f => now - f.at <= seconds).ToList();
        if (frames.Count == 0)
            return null;
        var span = Math.Max(frames.Max(f => f.at) - frames.Min(f => f.at), 0.001f);
        var sorted = frames.Select(f => f.ms).OrderBy(m => m).ToList();
        var stats = new Stats
        {
            Span = span, Fps = frames.Count / span, Median = sorted[sorted.Count / 2], Worst = sorted[^1],
            Over33 = sorted.Count(m => m > 33), Over100 = sorted.Count(m => m > 100), Timed = timing,
        };
        if (!timing)
            return stats;
        var ours = frames.Sum(f => f.our);
        stats.OursPerSecond = ours / span;
        stats.OursShare = ours / Math.Max(frames.Sum(f => f.ms), 1f);
        stats.OursWorst = frames.Max(f => f.our);
        var total = Math.Max(Time.realtimeSinceStartup - since, 0.001f);
        stats.TimedFor = total;
        stats.Top = spent.OrderByDescending(p => p.Value.Ticks).Take(top)
            .Select(p => (p.Key, Ms(p.Value.Ticks) / total, p.Value.Calls / (double)total, Ms(p.Value.Max))).ToList();
        return stats;
    }

    public static IEnumerable<string> Report(float seconds = 10f)
    {
        if (Measure(seconds) is not { } s)
        {
            yield return "no frames yet";
            yield break;
        }
        yield return $"last {s.Span:0.0} s: {s.Fps:0.0} fps, frame median {s.Median:0.0} ms, worst {s.Worst:0} ms, over 33 ms: {s.Over33}, over 100 ms: {s.Over100}";
        if (!s.Timed)
        {
            yield return "our code not timed: perf on";
            yield break;
        }
        yield return $"our code: {s.OursPerSecond:0.0} ms per second ({s.OursShare:0.0%} of the time), worst frame {s.OursWorst:0.0} ms";
        foreach (var (name, perSecond, calls, max) in s.Top)
            yield return $"  {perSecond,7:0.00} ms/s  {calls,7:0.0} calls/s  max {max,6:0.0} ms  {name}";
        yield return $"(totals since perf on, {s.TimedFor:0} s ago)";
    }

    // ---------- timing our code ----------

    public static string On(Harmony owner)
    {
        if (timing)
            return "already timing";
        harmony = owner;
        spent.Clear();
        since = Time.realtimeSinceStartup;
        timing = true;
        // The per-frame callbacks: Ticker.Tick runs them, here it runs them timed instead.
        var tick = typeof(Ticker).GetMethod("Tick", BindingFlags.NonPublic | BindingFlags.Static);
        harmony.Patch(tick, prefix: new HarmonyMethod(typeof(Perf), nameof(TickPrefix)));
        // Our plugins' patch methods on the game's code (prefixes, postfixes, finalizers): timed by a patch on each.
        var ours = Harmony.GetAllPatchedMethods().Select(Harmony.GetPatchInfo).Where(i => i != null)
            .SelectMany(i => i.Prefixes.Concat(i.Postfixes).Concat(i.Finalizers))
            .Where(p => p.owner.StartsWith("nobirds.") && p.owner != owner.Id)
            .Select(p => p.PatchMethod).Distinct().ToList();
        var timed = 0;
        foreach (var method in ours)
        {
            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(Perf), nameof(StartPrefix)), finalizer: new HarmonyMethod(typeof(Perf), nameof(StopFinalizer)));
                timed++;
            }
            catch (Exception e) { Log?.Invoke($"PERF not timed {method.DeclaringType?.Name}.{method.Name}: {e.GetType().Name}"); }
        }
        return $"timing the per-frame callbacks and {timed} of {ours.Count} patch methods of our plugins (after a plugin reloads: perf off, perf on)";
    }

    // Also time methods of our plugins whose Assembly.Type.method matches (with perf on): what a slow callback spends its time in.
    // Careful: Harmony's copy of a plugin method can break it (05.11: EntryPointNotFoundException in ToList() of the
    // visited markers' scan, until the plugin was reloaded). After timing, reload the plugins timed.
    public static string Add(string pattern)
    {
        if (!timing)
            return "perf on first";
        var regex = new System.Text.RegularExpressions.Regex(pattern);
        // Of each plugin the copy loaded last: the dev loader's reloads leave the older ones in the process.
        var methods = AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name.StartsWith("OldenEra") && a.GetName().Name != "OldenEraResearch")
            .GroupBy(a => a.GetName().Name).Select(g => g.Last())
            .SelectMany(a => { try { return a.GetTypes(); } catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); } })
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsAbstract && !m.IsGenericMethodDefinition && !m.ContainsGenericParameters && regex.IsMatch($"{m.DeclaringType?.Assembly.GetName().Name}.{m.DeclaringType?.Name}.{m.Name}"))
            .Take(100).ToList();
        var timed = new List<string>();
        foreach (var method in methods)
        {
            try
            {
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(Perf), nameof(StartPrefix)), finalizer: new HarmonyMethod(typeof(Perf), nameof(StopFinalizer)));
                timed.Add($"{method.DeclaringType?.Assembly.GetName().Name}.{method.DeclaringType?.Name}.{method.Name}");
            }
            catch (Exception e) { timed.Add($"{method.DeclaringType?.Name}.{method.Name} (not: {e.GetType().Name})"); }
        }
        return timed.Count > 0 ? $"timing {string.Join(", ", timed)}" : "no such method of our plugins";
    }

    public static string Off()
    {
        if (!timing)
            return "not timing";
        timing = false;
        harmony.UnpatchSelf();
        return "stopped timing";
    }

    static void Add(string name, long ticks)
    {
        if (!spent.TryGetValue(name, out var s))
            spent[name] = s = new Spent();
        s.Ticks += ticks;
        s.ThisFrame += ticks;
        s.Max = Math.Max(s.Max, ticks);
        s.Calls++;
        frameTicks += ticks;
    }

    static readonly FieldInfo actionsField = typeof(Ticker).GetField("actions", BindingFlags.NonPublic | BindingFlags.Static);

    static readonly HashSet<string> reportedTicks = new();

    static bool TickPrefix()
    {
        if (!timing || actionsField?.GetValue(null) is not List<(object owner, Action action)> actions)
            return true;
        foreach (var (owner, action) in actions.ToArray())
        {
            var start = Stopwatch.GetTimestamp();
            try { action(); }
            catch (Exception e)
            {
                // The plugin's own error, said once per callback as Ticker says it (05.11: a swallowed error hid why
                // the visited markers were gone).
                if (reportedTicks.Add($"{owner.GetType().FullName}.{action.Method.Name}"))
                    Log?.Invoke($"PERF {owner.GetType().FullName}.{action.Method.Name}: {e}");
            }
            Add($"tick {owner.GetType().Assembly.GetName().Name}.{action.Method.Name}", Stopwatch.GetTimestamp() - start);
        }
        return false;
    }

    static void StartPrefix(out long __state) => __state = Stopwatch.GetTimestamp();

    static Exception StopFinalizer(Exception __exception, long __state, MethodBase __originalMethod)
    {
        Add($"{__originalMethod.DeclaringType?.Assembly.GetName().Name}.{__originalMethod.DeclaringType?.Name}.{__originalMethod.Name}", Stopwatch.GetTimestamp() - __state);
        return __exception;
    }
}
