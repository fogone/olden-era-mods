using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.Manager;

// The manager's work with the catalog (issue #10, step 3): reading the sources, the updates for this game version,
// installing and updating mods. The network on background threads; the results and the loading on the main thread
// (Pump, every frame). Nothing is installed or updated by itself: the player asks (fogone 07.11: "Update" per mod and
// "Update all", no automatic updates); the Mods window's buttons (issue #13, from the main menu) call these.
public static class ManagerService
{
    // Our own catalog, built in (the repository's catalog/, issue #10); more sources in the config, one URL per line.
    public const string BuiltInSource = "https://raw.githubusercontent.com/fogone/olden-era-mods/main/catalog/catalog.json";
    // The manager and the library: not reloadable, updated through the patcher at the next start (Updater).
    public const string ManagerId = "nobirds.oldenera.manager";

    static ManualLogSource log;
    static CatalogClient client;
    static ConfigEntry<string> extraSources;
    static string gameVersion;
    static string managerVersion;
    static GitHubFetch fetcher;
    static readonly ConcurrentQueue<Action> mainThread = new();

    // What the last reading of the catalog found; null until it has been read.
    public static IReadOnlyList<ModMeta> Catalog { get; private set; }
    public static IReadOnlyList<Update> Updates { get; private set; } = Array.Empty<Update>();
    public static bool Busy { get; private set; }
    // The last thing done or failed, for the tab.
    public static string LastMessage { get; private set; }
    // Whether the last thing asked failed (the tab says so; the log says why).
    public static bool LastFailed { get; private set; }

    public static void Init(ConfigFile config, ManualLogSource logSource, string game, string manager)
    {
        log = logSource;
        Texts.Load(typeof(ManagerService).Assembly);
        gameVersion = game;
        managerVersion = manager;
        extraSources = config.Bind("Catalog", "Sources", "",
            "More catalog sources: the URLs of their root meta files (https), separated by spaces. Nobody reviews them: add only sources you trust.");
        // A token for a private GitHub repository (debugging before the repository is public): the player's own, in a
        // local file next to the state (oldenera-mods\github-token.txt), never in the catalog, git or a build.
        var tokenFile = System.IO.Path.Combine(ManagerState.Folder, "github-token.txt");
        var token = System.IO.File.Exists(tokenFile) ? System.IO.File.ReadAllText(tokenFile) : null;
        fetcher = new GitHubFetch("OldenEraModManager/" + manager, token);
        if (fetcher.HasToken)
            log.LogInfo("a GitHub token is used for the catalog (oldenera-mods\\github-token.txt)");
        client = new CatalogClient(fetcher.Get, ManagerState.Folder, line => log.LogInfo(line));
    }

    public static IEnumerable<string> Sources =>
        new[] { BuiltInSource }.Concat((extraSources?.Value ?? "").Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Distinct();

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

    // Reads the sources and works out the updates; the manager's own update among them.
    public static void Refresh() => Run("catalog", async () =>
    {
        var catalog = await client.Read(Sources);
        mainThread.Enqueue(() =>
        {
            Catalog = catalog;
            var updates = CatalogClient.Updates(catalog, ModLoader.State, gameVersion);
            // The manager is not in the state: compared with its own version.
            if (catalog.FirstOrDefault(m => m.Id == ManagerId) is { } self && CatalogClient.ReleaseFor(self, gameVersion) is { } release &&
                VersionComparer.Instance.Compare(release.Version, managerVersion) > 0 &&
                !new Updater(Paths.BepInExRootPath, null).WasFailed(release.Version))
                updates.Add(new Update(ManagerId, managerVersion, release, self));
            Updates = updates;
            LastMessage = $"catalog read: {catalog.Count} mods, {updates.Count} updates";
            log.LogInfo(LastMessage);
        });
    });

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
    // Removes an installed mod (ModLoader.Remove); on the main thread, nothing to download.
    public static void Remove(string id)
    {
        if (Busy)
            return;
        var outcome = ModLoader.Remove(id);
        Updates = Updates.Where(u => u.Id != id).ToList();
        LastFailed = outcome == ModLoader.Outcome.Failed;
        LastMessage = $"remove {id}: {outcome}";
        log.LogInfo(LastMessage);
        ModMenu.Changed();
    }

    // Switches an installed mod off or on (ModLoader.SetEnabled: at once when it can unload, else at the next start).
    public static void SetEnabled(string id, bool enabled)
    {
        if (Busy)
            return;
        var outcome = ModLoader.SetEnabled(id, enabled);
        LastFailed = outcome == ModLoader.Outcome.Failed;
        LastMessage = $"{(enabled ? "on" : "off")} {id}: {outcome}";
        log.LogInfo(LastMessage);
        ModMenu.Changed();
    }

    // Updates one mod (or the manager, at the next start).
    public static void Update(string id) => Run($"update {id}", () => UpdateOne(id));

    // The player's "Go back" after the manager's update: the previous version at the next start (Updater).
    public static void RollBackManager()
    {
        var staged = new Updater(Paths.BepInExRootPath, line => log.LogInfo(line)).StageRollback();
        LastFailed = !staged;
        ModMenu.Changed();
    }

    public static void UpdateAll() => Run("update all", async () =>
    {
        foreach (var update in Updates.ToList())
            await UpdateOne(update.Id);
    });

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
