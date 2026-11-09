using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.Manager;

// The manager's work with the catalog: reading the sources, the updates for this game version, installing and updating
// mods. The network on background threads; the results and the loading on the main thread (Pump, every frame).
// Nothing is installed or updated by itself: the Mods window's buttons call these when the player asks.
public static class ManagerService
{
    // Our own catalog, built in (the repository's catalog/); the player's own sources are in oldenera-mods\sources.json (SourceStore).
    public const string BuiltInSource = "https://raw.githubusercontent.com/fogone/olden-era-mods/main/catalog/catalog.json";
    // The manager and the library: not reloadable, updated through the patcher at the next start (Updater).
    public const string ManagerId = "nobirds.oldenera.manager";

    static ManualLogSource log;
    static CatalogClient client;
    static SourceStore store;
    static string gameVersion;
    static string managerVersion;
    static GitHubFetch fetcher;
    static readonly ConcurrentQueue<Action> mainThread = new();

    // What the last reading of the catalog found; null until it has been read.
    public static IReadOnlyList<ModMeta> Catalog { get; private set; }
    // Every source's meta files of that reading (a mod listed by two sources is here twice), each with its source.
    static IReadOnlyList<ModMeta> allMetas = Array.Empty<ModMeta>();
    public static IReadOnlyList<Update> Updates { get; private set; } = Array.Empty<Update>();
    public static bool Busy { get; private set; }
    // The last thing done or failed, for the log.
    public static string LastMessage { get; private set; }
    // Whether the last thing asked failed (the window says so; the log says why).
    public static bool LastFailed { get; private set; }

    public static void Init(ConfigFile config, ManualLogSource logSource, string game, string manager)
    {
        log = logSource;
        Texts.Load(typeof(ManagerService).Assembly);
        gameVersion = game;
        managerVersion = manager;
        // The config line is only read for the migration into sources.json (once, when that file is not there yet).
        var legacy = config.Bind("Catalog", "Sources", "",
            "Old place of the player's sources, moved to oldenera-mods/sources.json in the BepInEx folder (the Mods window adds and removes them).");
        store = SourceStore.Load(ManagerState.Folder, legacy.Value, line => log.LogWarning(line), BuiltInSource);
        // An optional GitHub token (a private repository, the API's rate limit): the player's own, in a local file next
        // to the state (oldenera-mods\github-token.txt), never in the catalog, git or a build.
        var tokenFile = System.IO.Path.Combine(ManagerState.Folder, "github-token.txt");
        var token = System.IO.File.Exists(tokenFile) ? System.IO.File.ReadAllText(tokenFile) : null;
        fetcher = new GitHubFetch("OldenEraModManager/" + manager, token);
        if (fetcher.HasToken)
            log.LogInfo("a GitHub token is used for the catalog (oldenera-mods\\github-token.txt)");
        client = new CatalogClient(fetcher.Get, ManagerState.Folder, line => log.LogInfo(line));
    }

    // The built-in source first, then the player's in the order added.
    public static IEnumerable<string> Sources => new[] { BuiltInSource }.Concat(store.Entries.Select(e => e.Url)).Distinct(StringComparer.OrdinalIgnoreCase);
    // How each source read the last time (the built-in one first); empty until read.
    public static IReadOnlyList<SourceResult> SourceResults { get; private set; } = Array.Empty<SourceResult>();
    // What the last add or remove of a source said, for the status line: a localization key and its arguments
    // (null: nothing to say; cleared by the next thing asked).
    public static (string Key, object[] Args)? SourceNote { get; private set; }

    // A source's settings (the built-in one's too): its remembered name, check frequency, last check.
    public static SourceEntry SourceSettings(string url) => store.Settings(url);
    public static void SetCheckEvery(string url, string every)
    {
        if (store.SetCheckEvery(url, every))
            ModMenu.Changed();
    }

    // Counts up when a screenshot came into the cache: the rows are made again (their paths).
    public static int ScreensRevision { get; private set; }
    public static string ScreenshotPath(ScreenshotFile shot) => client.ScreenshotPath(shot);
    static readonly HashSet<string> screenshotsLoading = new(StringComparer.OrdinalIgnoreCase);
    public static string IconPath(IconFile icon) => client.IconPath(icon);
    static readonly HashSet<string> iconsLoading = new(StringComparer.OrdinalIgnoreCase);

    // The sources' icons not cached yet, loaded in the background; when one came the rows are made again.
    static void LoadIcons(IEnumerable<SourceResult> results)
    {
        foreach (var result in results)
        {
            var icon = result.Icon;
            if (icon == null || client.IconPath(icon) != null)
                continue;
            var key = icon.Sha256 ?? icon.Url ?? "";
            lock (iconsLoading)
                if (!iconsLoading.Add(key))
                    continue;
            var url = result.Url;
            Task.Run(async () =>
            {
                try
                {
                    await client.LoadIcon(icon);
                    mainThread.Enqueue(() => ScreensRevision++);
                }
                catch (Exception e) { log.LogWarning($"icon of {url}: {e.Message}"); }
                finally { lock (iconsLoading) iconsLoading.Remove(key); }
            });
        }
    }

    // Every frame on the main thread: what the background work left to do here (loading mods, the results).
    public static void Pump()
    {
        if (UnityEngine.Time.frameCount % 15 == 0)
            try { CatalogRows.Refresh(managerVersion, gameVersion); }
            catch (Exception e) { log.LogError($"catalog rows: {e}"); }
        while (mainThread.TryDequeue(out var action))
            try { action(); }
            catch (Exception e) { log.LogError($"manager: {e}"); }
    }

    static void Run(string what, Func<Task> work)
    {
        if (Busy)
            return;
        Busy = true;
        LastFailed = false;
        SourceNote = null;
        Task.Run(async () =>
        {
            try { await work(); }
            catch (Exception e)
            {
                log.LogError($"{what}: {e.Message}");
                mainThread.Enqueue(() => { LastMessage = $"{what}: {e.Message}"; LastFailed = true; });
            }
            finally { mainThread.Enqueue(() => Busy = false); }
        });
    }

    // At the game's start: the sources that are due are read from the network (CheckSchedule: every start, daily,
    // weekly; never the manual ones), the others come from the saved copies. Works out the updates; the manager's own
    // update among them.
    public static void Refresh() => Run("catalog", () => ReadCatalog(url => store.IsDue(url, DateTime.Now)));

    // "Check all": every source read from the network now.
    public static void CheckAll() => Run("check all", () => ReadCatalog(_ => true));

    // "Check" of one source: it is read now, the others come from the saved copies.
    public static void Check(string url) => Run("check source", () => ReadCatalog(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase)));

    // `online`: which sources are read from the network (the rest: the saved copies). A source read from the network
    // successfully gets its last check time.
    static async Task ReadCatalog(Func<string, bool> online)
    {
        var sources = Sources.ToList();
        var installed = ModLoader.State.Mods.ToList();
        var reading = await client.ReadAll(sources, installed, online, gameVersion);
        var catalog = reading.Mods;
        mainThread.Enqueue(() =>
        {
            Catalog = catalog;
            allMetas = reading.All;
            SourceResults = reading.Sources;
            LoadIcons(reading.Sources);
            var now = DateTime.Now;
            foreach (var result in reading.Sources)
            {
                store.SetName(result.Url, result.Name);
                if (result.State == SourceState.Read)
                    store.SetChecked(result.Url, now);
            }
            var updates = CatalogClient.Updates(catalog, ModLoader.State, gameVersion);
            // The manager is not in the state: compared with its own version.
            if (catalog.FirstOrDefault(m => m.Id == ManagerId) is { } self && CatalogClient.ReleaseFor(self, gameVersion) is { } release &&
                VersionComparer.Instance.Compare(release.Version, managerVersion) > 0 &&
                !new Updater(Paths.BepInExRootPath, null).WasFailed(release.Version))
                updates.Add(new Update(ManagerId, managerVersion, release, self));
            Updates = updates;
            LastMessage = $"catalog read: {catalog.Count} mods, {updates.Count} updates";
            log.LogInfo(LastMessage);
            // The sources asked for from the network that did not read: told once (the window's message box).
            var failed = reading.Sources.Where(r => r.State == SourceState.Failed && online(r.Url))
                .Select(r => string.IsNullOrWhiteSpace(r.Name) ? store.Settings(r.Url)?.Name ?? r.Url : r.Name).ToList();
            if (failed.Count > 0)
                Note("oe.manager.src.checkFailed", string.Join(", ", failed));
        });
    }

    // ---------- the player's sources ----------

    static void Note(string key, params object[] args)
    {
        SourceNote = (key, args);
        log.LogInfo($"sources: {key} {string.Join(", ", args)}");
    }

    // A source by its root meta file's URL (https only; one already there is refused). Read at once: one that does not
    // read is not added. The outcome is in SourceNote.
    public static void AddSource(string url)
    {
        url = url?.Trim();
        if (Busy)
            return;
        if (string.IsNullOrEmpty(url) || !CatalogClient.Allowed(url) || new Uri(url).IsFile)
        {
            Note("oe.manager.src.notHttps");
            return;
        }
        if (Sources.Contains(url, StringComparer.OrdinalIgnoreCase))
        {
            Note("oe.manager.src.duplicate");
            return;
        }
        Run("add source", async () =>
        {
            var (result, _) = await client.ReadSource(url, false);
            if (result.State == SourceState.Failed)
            {
                mainThread.Enqueue(() => Note("oe.manager.src.readFailed", result.Error));
                return;
            }
            store.Add(url, result.Name);
            store.SetChecked(url, DateTime.Now);
            // The catalog read again with the new source in it (the mods it lists show in the window); the others
            // as they are due.
            await ReadCatalog(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase) || store.IsDue(u, DateTime.Now));
            mainThread.Enqueue(() => Note("oe.manager.src.added", string.IsNullOrWhiteSpace(result.Name) ? url : result.Name, result.Mods));
        });
    }

    // "owner" or "owner/repo" of GitHub, or a pasted github.com address, at the standard place (SourceStore.GitHubUrl).
    public static void AddGitHubSource(string input)
    {
        if (Busy)
            return;
        var url = SourceStore.GitHubUrl(input);
        if (url == null)
            Note("oe.manager.src.badGitHub");
        else
            AddSource(url);
    }

    // The installed mods that came from the source (an installed mod's Source is the meta file it was installed from).
    public static List<string> InstalledFrom(string url) =>
        ModLoader.State.Mods.Where(m => !string.IsNullOrEmpty(m.Source) && allMetas.Any(meta =>
                string.Equals(meta.Url, m.Source, StringComparison.OrdinalIgnoreCase) && string.Equals(meta.SourceUrl, url, StringComparison.OrdinalIgnoreCase)))
            .Select(m => m.Id).ToList();

    // `withMods`: its installed mods are removed first (as Remove: one another mod needs stays). Otherwise they stay
    // installed, without updates (a mod updates only from its own source).
    public static void RemoveSource(string url, bool withMods)
    {
        if (Busy)
            return;
        if (string.Equals(url, BuiltInSource, StringComparison.OrdinalIgnoreCase))
            return;
        if (withMods)
            Remove(InstalledFrom(url));
        var name = store.Entries.FirstOrDefault(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase))?.Name;
        if (!store.Remove(url))
            return;
        Run("remove source", async () =>
        {
            await ReadCatalog(_ => false);
            mainThread.Enqueue(() => Note("oe.manager.src.removed", string.IsNullOrWhiteSpace(name) ? url : name));
        });
    }

    // Installs a mod of the catalog (its release for this game version) with the mods it requires that are not there
    // yet (the core), each before the ones needing it, and loads them.
    public static void Install(string id) => Run($"install {id}", async () =>
    {
        var order = new List<(ModMeta meta, ModRelease release)>();
        void Add(string modId, int depth)
        {
            if (depth > 10 || order.Any(o => string.Equals(o.meta.Id, modId, StringComparison.OrdinalIgnoreCase)) ||
                ModLoader.State.Find(modId) != null || BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance.Plugins.ContainsKey(modId))
                return;
            var meta = Catalog?.FirstOrDefault(m => string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException($"{modId} is not in the catalog");
            var release = CatalogClient.ReleaseFor(meta, gameVersion) ?? throw new InvalidOperationException($"{modId}: no release for the game {gameVersion}");
            foreach (var required in release.Requires)
                Add(required.Id, depth + 1);
            order.Add((meta, release));
        }
        Add(id, 0);
        if (order.Count == 0)
            return;   // installed already (a second click before the window caught up)
        var downloaded = new List<InstalledMod>();
        foreach (var (meta, release) in order)
            downloaded.Add(await client.Download(meta, release));
        mainThread.Enqueue(() =>
        {
            var state = ModLoader.State;
            foreach (var installed in downloaded)
            {
                state.Mods.RemoveAll(m => string.Equals(m.Id, installed.Id, StringComparison.OrdinalIgnoreCase));
                state.Mods.Add(installed);
            }
            state.Save();
            foreach (var installed in downloaded)
                LastMessage = $"{installed.Id} {installed.Version}: {ModLoader.SetEnabled(installed.Id, true)}";
            log.LogInfo($"installed {string.Join(", ", downloaded.Select(d => d.Id + " " + d.Version))}");
        });
    });

    // A mod with a menu that the manager did not install (BepInEx loaded it from plugins or devplugins).
    public static bool IsByHand(string id) => id != ManagerId && ModLoader.State.Find(id) == null && ModMenu.All.Any(m => m.Guid == id);

    // Removes installed mods (ModLoader.Remove), dependents first; one that a mod staying installed requires is left
    // out. On the main thread, nothing to download.
    public static void Remove(IReadOnlyList<string> ids)
    {
        if (Busy || ids == null)
            return;
        // Mods BepInEx loaded by itself (installed by hand) are not the manager's to remove: their files are the player's.
        var byHand = ids.Count(IsByHand);
        if (byHand > 0)
            log.LogInfo($"remove: {byHand} mods installed by hand are left, remove their files yourself");
        ids = ids.Where(id => !IsByHand(id)).ToList();
        var order = Batch.Removable(ids, ModLoader.State.Mods);
        var failed = 0;
        foreach (var id in order)
        {
            var outcome = ModLoader.Remove(id);
            if (outcome == ModLoader.Outcome.Failed)
                failed++;
            log.LogInfo($"remove {id}: {outcome}");
        }
        Updates = Updates.Where(u => !order.Contains(u.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        LastFailed = failed > 0 || order.Count < ids.Count;
        LastMessage = $"removed {order.Count} of {ids.Count} mods";
        log.LogInfo(LastMessage);
        ModMenu.Changed();
    }

    // Switches installed mods off or on (ModLoader.SetEnabled: at once when it can unload, else at the next start), what
    // a mod needs first when switching on, dependents first when switching off.
    public static void SetEnabled(IReadOnlyList<string> ids, bool enabled)
    {
        if (Busy || ids == null)
            return;
        var failed = 0;
        // A mod installed by hand: its own switch in its menu (the next start, or at once for a mod that can reload).
        foreach (var id in ids.Where(IsByHand))
            if (ModMenu.All.FirstOrDefault(m => m.Guid == id) is { } menu)
            {
                menu.Enabled.Value = enabled;
                log.LogInfo($"{(enabled ? "on" : "off")} {id}: its menu switch");
            }
        ids = ids.Where(id => !IsByHand(id)).ToList();
        var order = Batch.SwitchOrder(ids, ModLoader.State.Mods, enabled);
        foreach (var id in order)
        {
            var outcome = ModLoader.SetEnabled(id, enabled);
            if (outcome == ModLoader.Outcome.Failed)
                failed++;
            log.LogInfo($"{(enabled ? "on" : "off")} {id}: {outcome}");
        }
        LastFailed = failed > 0;
        LastMessage = $"{(enabled ? "on" : "off")}: {order.Count} mods";
        ModMenu.Changed();
    }

    // Updates mods one after another in one run (or the manager, at the next start); one that fails does not stop the
    // others, the run then says it failed. An id with no update is skipped.
    public static void Update(IReadOnlyList<string> ids) => Run("update", async () =>
    {
        var failures = new List<string>();
        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            if (Updates.All(u => !string.Equals(u.Id, id, StringComparison.OrdinalIgnoreCase)))
                continue;
            try { await UpdateOne(id); }
            catch (Exception e)
            {
                log.LogError($"update {id}: {e.Message}");
                failures.Add($"{id}: {e.Message}");
            }
        }
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join("; ", failures));
    });

    // The player's "Go back" after the manager's update: the previous version at the next start (Updater).
    public static void RollBackManager()
    {
        var staged = new Updater(Paths.BepInExRootPath, line => log.LogInfo(line)).StageRollback();
        LastFailed = !staged;
        ModMenu.Changed();
    }

    public static void UpdateAll() => Update(Updates.Select(u => u.Id).ToList());

    // The mod's screenshots into the cache in the background (the window asks when it shows the details; it does not
    // wait for the manager's other work). When some came, the rows are made again: their Path is set.
    public static void LoadScreenshots(string id)
    {
        var meta = Catalog?.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        if (meta == null || meta.Screenshots.Count == 0 || meta.Screenshots.All(s => client.ScreenshotPath(s) != null))
            return;
        lock (screenshotsLoading)
            if (!screenshotsLoading.Add(id))
                return;
        Task.Run(async () =>
        {
            try
            {
                if (await client.LoadScreenshots(meta) > 0)
                    mainThread.Enqueue(() => ScreensRevision++);
            }
            catch (Exception e) { log.LogWarning($"{id} screenshots: {e.Message}"); }
            finally { lock (screenshotsLoading) screenshotsLoading.Remove(id); }
        });
    }

    static async Task UpdateOne(string id)
    {
        var update = Updates.FirstOrDefault(u => u.Id == id) ?? throw new InvalidOperationException("no update");
        if (id == ManagerId)
        {
            // Downloaded and checked like a mod, then staged for the patcher (the manager and the library go together).
            var downloaded = await client.Download(update.Meta, update.Release);
            var folder = System.IO.Path.Combine(ManagerState.Folder, id, update.Release.Version);
            var files = downloaded.Files.Select(name => (new UpdateFile { Name = name, Folder = name.EndsWith(".Patcher.dll") ? "patchers" : "plugins" },
                                                          System.IO.File.ReadAllBytes(System.IO.Path.Combine(folder, name))));
            var staged = new Updater(Paths.BepInExRootPath, line => log.LogInfo(line)).Stage(update.Release.Version, managerVersion, files);
            mainThread.Enqueue(() => LastMessage = staged ? $"the manager {update.Release.Version}: at the next start" : $"the manager {update.Release.Version}: not staged");
            return;
        }
        var installed = await client.Download(update.Meta, update.Release);
        mainThread.Enqueue(() =>
        {
            var mod = ModLoader.State.Find(id);
            mod.Files = installed.Files;
            mod.Requires = installed.Requires;
            mod.Source = installed.Source;
            var outcome = ModLoader.SwitchVersion(id, installed.Version);
            Updates = Updates.Where(u => u.Id != id).ToList();
            LastMessage = $"{id} {installed.Version}: {outcome}";
            log.LogInfo(LastMessage);
        });
    }
}
