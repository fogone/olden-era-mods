using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace Nobirds.OldenEra.Modding.Common;

// Base of our plugins: owns the plugin's Harmony patches, its menu in the "Моды" tab, its per-frame callbacks and
// the scene objects it creates (by name prefix), and removes them all on Unload, so the dev loader can swap in a
// new build while the game runs (mod-devloader).
//
//   public class Plugin : ModBase
//   {
//       static ModLog log;
//       protected override string[] ObjectPrefixes => new[] { "OE_Counter" };
//       protected override void OnLoad()
//       {
//           log = Logs;
//           On(ModEvents.SquadTooltipFilled, fill => log.Guard("tooltip", () => Show(fill)));   // the game's events (ModEvents)
//           OnTick(Watch);
//       }
//   }
public abstract class ModBase : BasePlugin
{
    public Harmony Harmony { get; private set; }
    public ModMenu Menu { get; private set; }

    // The mod's log, with the usual ways: Once, Report (an error once per place), Guard (run, an error reported).
    public ModLog Logs { get; private set; }

    // A developer's flag kept only in the config file, section [Debug], off by default and not in the "Моды" tab:
    // extra logging, research. What the player chooses goes into the menu (Menu.Toggle, Menu.Choose).
    //   logObjects = DebugFlag("LogObjects", "Write the visit data of every hovered map object to the log.");
    //   if (logObjects.Value) ...
    protected ConfigEntry<bool> DebugFlag(string key, string description) => Config.Bind("Debug", key, false, description);

    // Whether the mod has an entry in the "Моды" tab (its name: oe.<id>.title in its lang files). Not mod-settings.
    protected virtual bool HasMenu => true;

    // Names the mod gives the scene objects it creates; those objects are destroyed on unload.
    protected virtual string[] ObjectPrefixes => Array.Empty<string>();

    // Whether the mod works on the game's version only: its generated names (GameNames.g.cs, compiled into it, made
    // for one version) must be that version's. Not the research plugin, which makes them.
    protected virtual bool NeedsGameNames => true;
    bool loaded;

    public sealed override void Load()
    {
        Harmony = new Harmony(MetadataHelper.GetMetadata(this).GUID);
        Logs = new ModLog(Log);
        if (NeedsGameNames && WrongGameVersion() is { } wrong)
        {
            Log.LogError(wrong.message);
            VersionNotice.Add(MetadataHelper.GetMetadata(this).Name, wrong.built, wrong.game);
            // In the Mods tab too, its title saying it is off (its texts for the title).
            Texts.Load(GetType().Assembly);
            if (HasMenu)
                ModMenu.Off(this, wrong.built);
            return;
        }
        VersionNotice.Remove(MetadataHelper.GetMetadata(this).Name);
        Texts.Load(GetType().Assembly);
        Menu = HasMenu ? ModMenu.For(this) : null;
        loaded = true;
        try { OnLoad(); }
        catch (Exception e)
        {
            // Half loaded is worse than off: what it set up so far (patches, callbacks, handlers, objects) goes.
            Log.LogError($"load failed, the mod is off: {e}");
            Unload();
        }
    }

    // The game's names are for one version of the game: the library's (GameNames, made for the version it is released
    // for), and those of a mod that works on the game's code itself (it compiles GameTypes.g.cs, which stamps it with
    // the version: AssemblyMetadata "Nobirds.OldenEra.GameVersion"). On another version the obfuscated names are other ones: the
    // mod stays off and says so on screen (VersionNotice). Null when it fits. The debug flag PretendOtherGameVersion
    // takes the game for another version, to see the notice without a patch of the game.
    (string built, string game, string message)? WrongGameVersion()
    {
        var own = GetType().Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == "Nobirds.OldenEra.GameVersion")?.Value;
        var game = UnityEngine.Application.version;
        if (Config.Bind("Debug", "PretendOtherGameVersion", false, "Take the game for another version: the mod stays off and the notice shows.").Value)
            game += "-pretend";
        if (GameNames.GameVersion != game)
            return (GameNames.GameVersion, game, $"The mod library (OldenEraCommon) is for the game {GameNames.GameVersion}, the game is {game}: the mod is " +
                                                 "off until the library for this version of the game is installed.");
        return own == null || own == game
            ? null
            : (own, game, $"Built for the game {own}, the game is {game}: the mod is off. Rebuild it on this version of the game " +
                          "(the build makes the game's names for it, docs/game-names.md) and install it.");
    }

    protected abstract void OnLoad();

    // Put back what the mod changed in the game's own objects (texts hidden, values replaced); the mod's own
    // objects (ObjectPrefixes), patches, menu and callbacks are removed after it.
    protected virtual void OnUnload() { }

    // A callback every frame, on the main thread, for as long as the mod is loaded.
    protected void OnTick(Action action) => Ticker.Add(this, action);

    // A handler of one of the game's events (ModEvents), for as long as the mod is loaded.
    // Its errors go to the mod's log; a switched off mod gets none; without the core (mod-core) none come, said once.
    protected void On<T>(GameEvent<T> gameEvent, Action<T> handler)
    {
        if (!ModEvents.SourceReady && !sourceWarned)
        {
            sourceWarned = true;
            Logs.Warn("The game's events are not there yet (mod-core not loaded): the mod waits for them");
        }
        gameEvent.Add(this, handler);
    }

    bool sourceWarned;

    public override bool Unload()
    {
        if (loaded)
            try { OnUnload(); }
            catch (Exception e) { Log.LogError($"unload: {e}"); }
        Harmony.UnpatchSelf();
        Ticker.RemoveOwner(this);
        ModEvents.RemoveOwner(this);
        ModMenu.Remove(this);
        var prefixes = ObjectPrefixes;
        if (prefixes.Length > 0)
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                if (t != null && t.gameObject.scene.IsValid() && prefixes.Any(p => t.name.StartsWith(p, StringComparison.Ordinal)))
                    UnityEngine.Object.Destroy(t.gameObject);
        Logger.Sources.Remove(Log);
        return true;
    }
}

// A mod's log with the ways every mod needs: a line once, an error once per place (with its kind), a piece of work
// run with its error reported instead of thrown.
public sealed class ModLog
{
    readonly ManualLogSource source;
    readonly HashSet<string> said = new();

    public ModLog(ManualLogSource source) => this.source = source;

    public void Info(string line) => source.LogInfo(line);
    public void Warn(string line) => source.LogWarning(line);
    public void Error(string line) => source.LogError(line);

    // The line, the first time only.
    public void Once(string line)
    {
        if (said.Add(line))
            source.LogInfo(line);
    }

    // An error at `where`, once per place and kind of error.
    public void Report(string where, Exception e)
    {
        if (said.Add($"{where}: {e.GetType().FullName}"))
            source.LogError($"{where}: {e}");
    }

    public void Guard(string where, Action work)
    {
        try { work(); }
        catch (Exception e) { Report(where, e); }
    }

    public T Guard<T>(string where, Func<T> work, T otherwise = default)
    {
        try { return work(); }
        catch (Exception e)
        {
            Report(where, e);
            return otherwise;
        }
    }
}

// Per-frame callbacks of our plugins through one Unity component, registered once by this library: the plugins'
// own MonoBehaviour classes could not be registered again by a reloaded build.
public static class Ticker
{
    static readonly List<(object owner, Action action)> actions = new();
    static readonly HashSet<string> reported = new();
    static ManualLogSource log;
    static bool started;

    public static void Add(object owner, Action action)
    {
        if (!started)
        {
            started = true;
            log = Logger.CreateLogSource("Olden Era Common");
            IL2CPPChainloader.AddUnityComponent<TickBehaviour>();
        }
        actions.Add((owner, action));
    }

    // The owner's callbacks go, and what was said about them: a reloaded build (the same names) reports its own errors.
    public static void RemoveOwner(object owner)
    {
        actions.RemoveAll(a => a.owner == owner);
        reported.RemoveWhere(key => key.StartsWith(owner.GetType().FullName + ".", StringComparison.Ordinal));
    }

    internal static void Tick()
    {
        foreach (var (owner, action) in actions.ToArray())
        {
            try { action(); }
            catch (Exception e)
            {
                if (owner is ModBase mod)
                    mod.Logs.Report($"tick ({action.Method.Name})", e);   // the mod's own log, once per callback
                else if (reported.Add($"{owner.GetType().FullName}.{action.Method.Name}"))
                    log.LogError($"{owner.GetType().FullName}.{action.Method.Name}: {e}");
            }
        }
    }
}

public class TickBehaviour : MonoBehaviour
{
    public TickBehaviour(IntPtr pointer) : base(pointer) { }

    public void Update() => Ticker.Tick();
}
