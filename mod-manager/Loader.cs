using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace Nobirds.OldenEra.Modding.Manager;

// Loads the installed mods (ManagerState) at run time, the way the dev loader does: each DLL from its bytes (the file
// stays free for an update), its BepInEx plugin made and loaded, after the mods it requires. A mod is switched off,
// on or to another version while the game runs when its plugins can unload themselves (ModBase: patches, menu,
// callbacks, objects); another author's plain BepInEx plugin cannot, and its change waits for the next start.
// Old builds stay in memory: .NET cannot unload assemblies Harmony patched from.
public static class ModLoader
{
    public enum Outcome { Done, NeedsRestart, Failed }

    sealed class Loaded
    {
        public string Version;
        public readonly List<BasePlugin> Plugins = new();
    }

    static ManualLogSource log;
    static ManagerState state;
    static readonly Dictionary<string, Loaded> loaded = new(StringComparer.OrdinalIgnoreCase);
    // Changes that wait for the next start (a mod that cannot unload), by id: what was asked.
    static readonly Dictionary<string, string> pending = new(StringComparer.OrdinalIgnoreCase);

    public static ManagerState State => state;
    public static bool IsLoaded(string id) => loaded.ContainsKey(id);
    public static string LoadedVersion(string id) => loaded.TryGetValue(id, out var l) ? l.Version : null;
    public static IReadOnlyDictionary<string, string> Pending => pending;

    public static void Init(ManagerState managerState, ManualLogSource logSource)
    {
        state = managerState;
        log = logSource;
    }

    // Every mod switched on, each after the ones it requires; one whose requirement is missing, off or failed is not
    // loaded, and the log says why.
    public static void LoadAll()
    {
        foreach (var mod in InOrder(state.Mods.Where(m => m.Enabled)))
            if (!loaded.ContainsKey(mod.Id))
                Load(mod);
    }

    // The mods in an order where each comes after those it requires (of the given ones); a cycle is broken and said.
    static List<InstalledMod> InOrder(IEnumerable<InstalledMod> mods)
    {
        var list = mods.ToList();
        var byId = list.ToDictionary(m => m.Id, StringComparer.OrdinalIgnoreCase);
        var ordered = new List<InstalledMod>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(InstalledMod mod)
        {
            if (done.Contains(mod.Id))
                return;
            if (!visiting.Add(mod.Id))
            {
                log.LogWarning($"{mod.Id}: its requirements go round in a circle");
                return;
            }
            foreach (var required in mod.Requires)
                if (byId.TryGetValue(required, out var other))
                    Visit(other);
            visiting.Remove(mod.Id);
            done.Add(mod.Id);
            ordered.Add(mod);
        }
        foreach (var mod in list)
            Visit(mod);
        return ordered;
    }

    // A requirement is met by a mod this manager loaded or a plugin BepInEx loaded itself (BepInEx\plugins).
    static string MissingRequirement(InstalledMod mod) =>
        mod.Requires.FirstOrDefault(r => !loaded.ContainsKey(r) && !IL2CPPChainloader.Instance.Plugins.ContainsKey(r));

    static bool Load(InstalledMod mod)
    {
        if (MissingRequirement(mod) is { } missing)
        {
            log.LogWarning($"{mod.Id} {mod.Version}: not loaded, it requires {missing}, which is not loaded");
            return false;
        }
        var folder = ManagerState.FolderOf(mod);
        var entry = new Loaded { Version = mod.Version };
        try
        {
            // The libraries a plugin uses are among the files: all are loaded first, then the plugins made.
            var assemblies = new List<Assembly>();
            foreach (var file in mod.Files)
            {
                var path = Path.Combine(folder, file);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"its file {file} is not in {folder}");
                assemblies.Add(Assembly.Load(File.ReadAllBytes(path)));
            }
            foreach (var type in assemblies.SelectMany(SafeTypes).Where(t => typeof(BasePlugin).IsAssignableFrom(t) && !t.IsAbstract))
            {
                if (type.GetCustomAttribute<BepInPlugin>() is not { } metadata)
                    continue;
                if (IL2CPPChainloader.Instance.Plugins.ContainsKey(metadata.GUID))
                {
                    log.LogWarning($"{mod.Id}: {metadata.GUID} is also in BepInEx\\plugins and loaded from there; remove one of them (with the game closed)");
                    continue;
                }
                if (!string.Equals(metadata.GUID, mod.Id, StringComparison.OrdinalIgnoreCase))
                    log.LogWarning($"{mod.Id}: its plugin's GUID is {metadata.GUID}");
                var plugin = (BasePlugin)Activator.CreateInstance(type);
                plugin.Load();
                entry.Plugins.Add(plugin);
                log.LogMessage($"loaded {metadata.Name} {metadata.Version} ({mod.Id} {mod.Version})");
            }
            if (entry.Plugins.Count == 0)
            {
                log.LogWarning($"{mod.Id} {mod.Version}: no BepInEx plugin in its files");
                return false;
            }
            loaded[mod.Id] = entry;
            return true;
        }
        catch (Exception e)
        {
            log.LogError($"{mod.Id} {mod.Version}: {e}");
            // What did load of it goes again, so it is not half there.
            foreach (var plugin in entry.Plugins)
                try { plugin.Unload(); } catch (Exception) { }
            return false;
        }
    }

    // Unloaded with the mods that require it (they go first); false when one of them cannot unload itself.
    static bool Unload(string id)
    {
        if (!loaded.TryGetValue(id, out var entry))
            return true;
        foreach (var dependent in loaded.Keys.ToList())
            if (state.Find(dependent) is { } d && d.Requires.Contains(id, StringComparer.OrdinalIgnoreCase) && !Unload(dependent))
                return false;
        foreach (var plugin in entry.Plugins)
        {
            bool unloaded;
            try { unloaded = plugin.Unload(); }
            catch (Exception e)
            {
                log.LogError($"{id}: unload: {e}");
                unloaded = false;
            }
            if (!unloaded)
            {
                log.LogWarning($"{id}: cannot unload itself; the change waits for the next start");
                return false;
            }
        }
        loaded.Remove(id);
        log.LogMessage($"unloaded {id} {entry.Version}");
        return true;
    }

    // Switches a mod on or off, saved in the state; at once when it can, else on the next start.
    public static Outcome SetEnabled(string id, bool enabled)
    {
        if (state.Find(id) is not { } mod)
            return Outcome.Failed;
        mod.Enabled = enabled;
        state.Save();
        if (!enabled)
        {
            if (Unload(id))
                return Outcome.Done;
            pending[id] = "off";
            return Outcome.NeedsRestart;
        }
        if (loaded.ContainsKey(id))
            return Outcome.Done;
        if (!Load(mod))
            return Outcome.Failed;
        // The mods that waited for it come too.
        foreach (var waiting in InOrder(state.Mods.Where(m => m.Enabled && !loaded.ContainsKey(m.Id))))
            if (MissingRequirement(waiting) == null)
                Load(waiting);
        return Outcome.Done;
    }

    // The installed mods that require this one (whether switched on or not).
    public static List<InstalledMod> RequiredBy(string id) =>
        state.Mods.Where(m => m.Requires.Contains(id, StringComparer.OrdinalIgnoreCase)).ToList();

    // Removes a mod: unloaded, out of the state, its folder (every version) deleted; its config stays, for a later
    // install. Not while other installed mods require it (the tab says which). When it cannot unload itself it is
    // still removed from the state and the disk (its DLLs were loaded from bytes, the files are free) and goes away
    // at the next start.
    public static Outcome Remove(string id)
    {
        if (state.Find(id) is not { } mod)
            return Outcome.Failed;
        if (RequiredBy(id).Count > 0)
        {
            log.LogWarning($"{id}: not removed, {string.Join(", ", RequiredBy(id).Select(m => m.Id))} require it");
            return Outcome.Failed;
        }
        var unloaded = Unload(id);
        state.Mods.Remove(mod);
        state.Save();
        var folder = Path.Combine(ManagerState.Folder, mod.Id);
        try
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, true);
        }
        catch (Exception e) { log.LogWarning($"{id}: its folder is not deleted ({e.Message}); delete {folder} with the game closed"); }
        log.LogMessage($"removed {id} {mod.Version}");
        if (unloaded)
        {
            pending.Remove(id);
            return Outcome.Done;
        }
        pending[id] = "removed";
        return Outcome.NeedsRestart;
    }

    // Puts another installed version of a mod in place of the loaded one (its folder must be there, from the catalog
    // client); the state says the new version. Back to the old one when the new one fails to load.
    public static Outcome SwitchVersion(string id, string version)
    {
        if (state.Find(id) is not { } mod)
            return Outcome.Failed;
        var old = mod.Version;
        mod.Version = version;
        state.Save();
        if (!mod.Enabled || !loaded.ContainsKey(id))
            return Outcome.Done;
        var dependents = loaded.Keys.Where(k => state.Find(k)?.Requires.Contains(id, StringComparer.OrdinalIgnoreCase) == true).ToList();
        if (!Unload(id))
        {
            pending[id] = version;
            return Outcome.NeedsRestart;
        }
        if (!Load(mod))
        {
            log.LogWarning($"{id}: {version} did not load, back to {old}");
            mod.Version = old;
            state.Save();
            Load(mod);
            foreach (var d in dependents)
                if (state.Find(d) is { } dependent)
                    Load(dependent);
            return Outcome.Failed;
        }
        foreach (var d in dependents)
            if (state.Find(d) is { } dependent)
                Load(dependent);
        return Outcome.Done;
    }

    static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
    }
}
