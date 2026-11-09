using System;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using Nobirds.OldenEra.Modding.Common;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Research;

// Development only (BepInEx\devplugins): research of the game's code and live state without rebuilding a plugin.
// The commands (Commands.cs) run on the game's main thread, from two places:
//   - the console on this computer only, http://127.0.0.1:8766/?c=<command> (Console.cs): the answer is the output;
//   - BepInEx\research.txt, when it is saved: the results replace BepInEx\research-out.txt and go to the log too.
[BepInPlugin(ModInfo.Id, ModInfo.Name, ModInfo.Version)]
public partial class Plugin : ModBase
{
    static ManualLogSource logger;
    static ModLog log;
    static string path, outPath;
    static DateTime seen;
    static int nextCheck;
    static Commands commands;

    static Switch showFps;

    protected override string[] ObjectPrefixes => new[] { FpsOverlay.ObjectName };

    // It makes the game's names (`aliases`), so it runs on a new game version too.
    protected override bool NeedsGameNames => false;

    const string FileHelp =
@"# Save this file: every line not starting with # runs, the results replace BepInEx\research-out.txt (and go to
# BepInEx\LogOutput.log as lines ""RESEARCH ..."").
";

    protected override void OnLoad()
    {
        logger = Log;
        log = Logs;
        showFps = Menu.Toggle("ShowFps", false);
        // Timing from the game's start (the first showing of something after a cold start is measured too); the
        // per-frame callbacks of every mod are timed through Ticker, also of the mods loaded after this one.
        var timeFromStart = DebugFlag("TimeFromStart", "Time our mods from the game's start, as 'perf on' does (the console's fps shows it, slow frames go to the log).").Value;
        commands = new Commands(Harmony, line => logger.LogMessage($"RESEARCH {line}"));
        if (timeFromStart)
            logger.LogInfo($"Timing from the start: {commands.StartTiming()}");
        path = Path.Combine(Paths.BepInExRootPath, "research.txt");
        outPath = Path.Combine(Paths.BepInExRootPath, "research-out.txt");
        if (!File.Exists(path) || !File.ReadAllText(path).StartsWith(FileHelp[..40]))
            File.WriteAllText(path, FileHelp + Commands.Help);
        seen = File.GetLastWriteTimeUtc(path);
        OnTick(Watch);
        logger.LogInfo($"Watching {path}, results in {outPath}");
        StartConsole();
    }

    protected override void OnUnload()
    {
        commands.Unload();
        StopConsole();
    }

    // Every frame the console's commands; every few frames, when the file was saved, the file's (their results replace
    // the out file).
    internal static void Note(string line) => logger.LogMessage($"RESEARCH {line}");

    static void Watch()
    {
        Perf.Frame();
        FpsOverlay.Update(showFps.On);
        RunConsoleJobs();
        if (Time.frameCount < nextCheck)
            return;
        nextCheck = Time.frameCount + 10;
        var changed = File.GetLastWriteTimeUtc(path);
        if (changed == seen)
            return;
        seen = changed;
        var output = new StringBuilder();
        foreach (var line in File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")))
            commands.Run(line, text =>
            {
                output.Append(text).Append('\n');
                logger.LogMessage($"RESEARCH {text}");
            });
        File.WriteAllText(outPath, output.ToString());
    }
}
