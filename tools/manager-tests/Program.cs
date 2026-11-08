using System.Text;
using OldenEra.Modding.Manager;

// Each case on a fresh temporary BepInEx folder; a failed check says which and the exit code is 1.
var failures = 0;
var cases = 0;

void Case(string name, Action<string> body)
{
    cases++;
    var root = Path.Combine(Path.GetTempPath(), "oe-manager-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "plugins"));
    Directory.CreateDirectory(Path.Combine(root, "patchers"));
    try
    {
        body(root);
        Console.WriteLine($"ok    {name}");
    }
    catch (Exception e)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}: {e.Message}");
    }
    finally
    {
        try { Directory.Delete(root, true); } catch (Exception) { }
    }
}

void Check(bool condition, string what)
{
    if (!condition)
        throw new Exception(what);
}

static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
static string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

var manager = new UpdateFile { Name = "OldenEraModManager.dll", Folder = "plugins" };
var library = new UpdateFile { Name = "OldenEraCommon.dll", Folder = "plugins" };
var patcher = new UpdateFile { Name = "OldenEraModManager.Patcher.dll", Folder = "patchers" };

void Install(string root, string version)
{
    File.WriteAllText(Path.Combine(root, "plugins", manager.Name), $"manager {version}");
    File.WriteAllText(Path.Combine(root, "plugins", library.Name), $"library {version}");
}

(UpdateFile, byte[])[] Release(string version) =>
    new[] { (manager, Bytes($"manager {version}")), (library, Bytes($"library {version}")) };

Updater U(string root) => new(root, _ => { });

Case("catalog meta: tags round-trip, a file without them reads as an empty list", root =>
{
    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var withTags = System.Text.Json.JsonSerializer.Deserialize<ModMeta>("""{ "id": "a", "tags": [ "map", "tooltip" ] }""", opts);
    Check(withTags.Tags.SequenceEqual(new[] { "map", "tooltip" }), "tags not read");
    var again = System.Text.Json.JsonSerializer.Deserialize<ModMeta>(System.Text.Json.JsonSerializer.Serialize(withTags), opts);
    Check(again.Tags.SequenceEqual(new[] { "map", "tooltip" }), "tags lost on a round trip");
    var without = System.Text.Json.JsonSerializer.Deserialize<ModMeta>("""{ "id": "b" }""", opts);
    Check(without.Tags != null && without.Tags.Count == 0, "no tags is not an empty list");
});

Case("nothing staged: the start changes nothing", root =>
{
    Install(root, "0.1");
    U(root).ApplyAtStart();
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.1", "the manager changed");
    Check(U(root).Status().State == UpdateStatus.None, "a state appeared");
});

Case("staged: the running files stay until the next start", root =>
{
    Install(root, "0.1");
    Check(U(root).Stage("0.2", "0.1", Release("0.2")), "not staged");
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.1", "the manager was replaced while running");
    Check(U(root).Status().State == UpdateStatus.Staged, "not staged in the status");
});

Case("applied at the start, kept once the new manager confirms", root =>
{
    Install(root, "0.1");
    U(root).Stage("0.2", "0.1", Release("0.2"));
    U(root).ApplyAtStart();
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.2", "the manager was not updated");
    Check(Read(Path.Combine(root, "plugins", library.Name)) == "library 0.2", "the library was not updated");
    Check(U(root).Status().State == UpdateStatus.Trying, "not trying");
    U(root).Confirm();
    var status = U(root).Status();
    Check(status.State == UpdateStatus.Ok && status.Message.Contains("0.2"), "not ok after the confirmation");
    U(root).ApplyAtStart();   // the next start: nothing to do
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.2", "a later start changed the manager");
});

Case("not confirmed: the next start rolls back and the version is not offered again", root =>
{
    Install(root, "0.1");
    U(root).Stage("0.2", "0.1", Release("0.2"));
    U(root).ApplyAtStart();
    // The new manager crashed: no Confirm. The next start:
    U(root).ApplyAtStart();
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.1", "the manager was not rolled back");
    Check(Read(Path.Combine(root, "plugins", library.Name)) == "library 0.1", "the library was not rolled back");
    var status = U(root).Status();
    Check(status.State == UpdateStatus.RolledBack && status.Version == "0.1", "the status does not say rolled back to 0.1");
    Check(status.Failed.Contains("0.2") && U(root).WasFailed("0.2"), "0.2 not marked failed");
    Check(!U(root).Stage("0.2", "0.1", Release("0.2")), "the failed version was staged again");
    Check(U(root).Stage("0.3", "0.1", Release("0.3")), "a newer version could not be staged");
});

Case("a file the update adds goes on a rollback", root =>
{
    Install(root, "0.1");
    U(root).Stage("0.2", "0.1", Release("0.2").Append((patcher, Bytes("patcher 0.2"))));
    U(root).ApplyAtStart();
    Check(Read(Path.Combine(root, "patchers", patcher.Name)) == "patcher 0.2", "the new patcher was not put in");
    U(root).ApplyAtStart();
    Check(!File.Exists(Path.Combine(root, "patchers", patcher.Name)), "the added patcher stayed after the rollback");
});

Case("a file in use: nothing is applied, the update stays staged", root =>
{
    Install(root, "0.1");
    U(root).Stage("0.2", "0.1", Release("0.2"));
    // The library is held open without sharing, as a running game may hold a DLL.
    using (new FileStream(Path.Combine(root, "plugins", library.Name), FileMode.Open, FileAccess.Read, FileShare.None))
        U(root).ApplyAtStart();
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.1", "the manager was replaced although the library could not be");
    Check(Read(Path.Combine(root, "plugins", library.Name)) == "library 0.1", "the library changed");
    Check(U(root).Status().State == UpdateStatus.Staged, "no longer staged");
    U(root).ApplyAtStart();   // the next start, the file free
    Check(Read(Path.Combine(root, "plugins", library.Name)) == "library 0.2", "not applied once the file was free");
});

Case("the player's rollback: the previous version at the next start, the left one not offered", root =>
{
    Install(root, "0.1");
    U(root).Stage("0.2", "0.1", Release("0.2"));
    U(root).ApplyAtStart();
    U(root).Confirm();
    Check(U(root).StageRollback(), "the rollback was not staged");
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.2", "rolled back while running");
    U(root).ApplyAtStart();
    U(root).Confirm();
    Check(Read(Path.Combine(root, "plugins", manager.Name)) == "manager 0.1", "not back to 0.1");
    Check(U(root).WasFailed("0.2"), "0.2 still offered");
});

Case("an unreadable status is taken as none", root =>
{
    Directory.CreateDirectory(Path.Combine(root, "oldenera-mods", ".update"));
    File.WriteAllText(Path.Combine(root, "oldenera-mods", ".update", "update.json"), "{ not json");
    Install(root, "0.1");
    U(root).ApplyAtStart();
    Check(U(root).Status().State == UpdateStatus.None, "not none");
});


// ---------- the catalog (fake network: URL -> bytes) ----------

static string Sha(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

(CatalogClient client, Dictionary<string, byte[]> net, List<string> asked) Net(string root, bool offline = false)
{
    var net = new Dictionary<string, byte[]>();
    var asked = new List<string>();
    var client = new CatalogClient(url =>
    {
        asked.Add(url);
        if (offline || !net.TryGetValue(url, out var bytes))
            throw new HttpRequestException($"{url}: 404");
        return Task.FromResult(bytes);
    }, Path.Combine(root, "oldenera-mods"), _ => { });
    return (client, net, asked);
}

const string Source = "https://example.test/catalog.json";
const string MetaUrl = "https://example.test/mods/counter.json";
var dll048 = Bytes("counter 0.48");
var dll049 = Bytes("counter 0.49");
var dll050 = Bytes("counter 0.50");

void Publish(Dictionary<string, byte[]> net, string sha049 = null)
{
    net[Source] = Bytes($$"""{ "schema": 1, "name": "test", "mods": [ { "id": "oe.counter", "meta": "{{MetaUrl}}" } ] }""");
    net[MetaUrl] = Bytes($$"""
    { "schema": 1, "id": "oe.counter", "name": { "english": "Battle counter", "russian": "Ответка в бою" },
      "releases": [
        { "version": "0.48.0", "games": [ "0.82.07" ], "requires": [ { "id": "oe.core" } ],
          "files": [ { "name": "Counter.dll", "url": "https://example.test/0.48/Counter.dll", "sha256": "{{Sha(dll048)}}" } ] },
        { "version": "0.49.0", "games": [ "0.82.07", "0.82.08" ], "requires": [ { "id": "oe.core" } ],
          "files": [ { "name": "Counter.dll", "url": "https://example.test/0.49/Counter.dll", "sha256": "{{sha049 ?? Sha(dll049)}}" } ] },
        { "version": "0.50.0", "games": [ "0.83.01" ],
          "files": [ { "name": "Counter.dll", "url": "https://example.test/0.50/Counter.dll", "sha256": "{{Sha(dll050)}}" } ] }
      ] }
    """);
    net["https://example.test/0.48/Counter.dll"] = dll048;
    net["https://example.test/0.49/Counter.dll"] = dll049;
    net["https://example.test/0.50/Counter.dll"] = dll050;
}

Case("catalog: the newest release for the game's version, its name in the language", root =>
{
    var (client, net, _) = Net(root);
    Publish(net);
    var mods = client.Read(new[] { Source }).Result;
    Check(mods.Count == 1 && mods[0].Url == MetaUrl, "the mod not read");
    Check(mods[0].NameIn("russian") == "Ответка в бою" && mods[0].NameIn("german") == "Battle counter", "the names by language");
    Check(CatalogClient.ReleaseFor(mods[0], "0.82.07").Version == "0.49.0", "not the newest for 0.82.07");
    Check(CatalogClient.ReleaseFor(mods[0], "0.83.01").Version == "0.50.0", "not the one for 0.83.01");
    Check(CatalogClient.ReleaseFor(mods[0], "0.81.04") == null, "a release for a game version nobody built for");
});

Case("catalog: download checked by sha256 into <id>\\<version>, the entry with its requirements", root =>
{
    var (client, net, _) = Net(root);
    Publish(net);
    var meta = client.Read(new[] { Source }).Result[0];
    var installed = client.Download(meta, CatalogClient.ReleaseFor(meta, "0.82.07")).Result;
    var file = Path.Combine(root, "oldenera-mods", "oe.counter", "0.49.0", "Counter.dll");
    Check(Read(file) == "counter 0.49", "the file is not there");
    Check(installed.Version == "0.49.0" && installed.Files.SequenceEqual(new[] { "Counter.dll" }) && installed.Requires.SequenceEqual(new[] { "oe.core" }) && installed.Source == MetaUrl, "the entry");
});

Case("catalog: a file whose sha256 does not match is refused and nothing is left", root =>
{
    var (client, net, _) = Net(root);
    Publish(net, sha049: Sha(Bytes("something else")));
    var meta = client.Read(new[] { Source }).Result[0];
    var refused = false;
    try { client.Download(meta, CatalogClient.ReleaseFor(meta, "0.82.07")).Wait(); }
    catch (AggregateException e) when (e.InnerException is InvalidOperationException) { refused = true; }
    Check(refused, "not refused");
    Check(!Directory.Exists(Path.Combine(root, "oldenera-mods", "oe.counter", "0.49.0")), "a folder was left");
});

Case("catalog: http (not https) and odd file names are refused", root =>
{
    Check(!CatalogClient.Allowed("http://example.test/x.json") && CatalogClient.Allowed("https://example.test/x.json"), "http allowed");
    var (client, net, _) = Net(root);
    var meta = new ModMeta { Id = "oe.bad", Releases = { new ModRelease { Version = "1", Games = { "g" }, Files = { new ReleaseFile { Name = "..\\evil.dll", Url = "https://example.test/e", Sha256 = "00" } } } } };
    var refused = false;
    try { client.Download(meta, meta.Releases[0]).Wait(); }
    catch (AggregateException e) when (e.InnerException is InvalidOperationException) { refused = true; }
    Check(refused, "a path in the file name was taken");
});

Case("catalog: without the network the cached meta files are used", root =>
{
    var (client, net, _) = Net(root);
    Publish(net);
    client.Read(new[] { Source }).Wait();
    var (offline, _, _) = Net(root, offline: true);
    var mods = offline.Read(new[] { Source }).Result;
    Check(mods.Count == 1 && mods[0].Releases.Count == 3, "the cache was not used");
});

Case("catalog: updates for the game's version, newer only", root =>
{
    var (client, net, _) = Net(root);
    Publish(net);
    var mods = client.Read(new[] { Source }).Result;
    var state = new ManagerState { Mods = { new InstalledMod { Id = "oe.counter", Version = "0.48.0" } } };
    var updates = CatalogClient.Updates(mods, state, "0.82.07");
    Check(updates.Count == 1 && updates[0].Release.Version == "0.49.0", "no update to 0.49.0");
    state.Mods[0].Version = "0.49.0";
    Check(CatalogClient.Updates(mods, state, "0.82.07").Count == 0, "an update offered to the same version");
    Check(CatalogClient.Updates(mods, state, "0.81.04").Count == 0, "an update for a game version nobody built for");
});

// ---------- the player's sources (issue #6) ----------

Case("sources: GitHub names -> the standard place of the root file", root =>
{
    const string Std = "https://raw.githubusercontent.com/{0}/{1}/HEAD/catalog/catalog.json";
    Check(SourceStore.GitHubUrl("alice") == string.Format(Std, "alice", "olden-era-mods"), "owner");
    Check(SourceStore.GitHubUrl(" alice/my_mods.v2 ") == string.Format(Std, "alice", "my_mods.v2"), "owner/repo");
    Check(SourceStore.GitHubUrl("https://github.com/alice") == string.Format(Std, "alice", "olden-era-mods"), "github.com/owner");
    Check(SourceStore.GitHubUrl("https://github.com/alice/mods") == string.Format(Std, "alice", "mods"), "github.com/owner/repo");
    Check(SourceStore.GitHubUrl("https://github.com/alice/mods.git") == string.Format(Std, "alice", "mods"), ".git");
    Check(SourceStore.GitHubUrl("https://github.com/alice/mods/tree/main/catalog") == string.Format(Std, "alice", "mods"), "a deeper page");
    foreach (var bad in new[] { "", "  ", null, "a b", "al!ce", "alice/", "/mods", "a/b/c", "alice/mo ds", "alice/..", "al_ice", "-x/", "https://github.com/", "https://example.com/alice", "alice/mods?x=1" })
        Check(SourceStore.GitHubUrl(bad) == null, $"accepted: {bad}");
});

Case("sources: sources.json round trip, no duplicates (any case), remove", root =>
{
    var folder = Path.Combine(root, "oldenera-mods");
    var store = SourceStore.Load(folder, "");
    Check(store.Entries.Count == 0, "not empty at first");
    Check(store.Add("https://a.test/catalog.json", "A"), "not added");
    Check(!store.Add("HTTPS://A.TEST/catalog.json", "again"), "a duplicate in another case was added");
    Check(store.Add("https://b.test/catalog.json", null), "second not added");
    var again = SourceStore.Load(folder, "https://ignored.test/x.json");
    Check(again.Entries.Select(e => e.Url).SequenceEqual(new[] { "https://a.test/catalog.json", "https://b.test/catalog.json" }), "the order or the content changed");
    Check(again.Entries[0].Name == "A" && again.Entries[0].Added.Length == 10, "name or date lost");
    again.SetName("https://b.test/catalog.json", "B");
    Check(SourceStore.Load(folder, "").Entries[1].Name == "B", "the name was not saved");
    Check(again.Remove("https://A.test/catalog.json") && !again.Remove("https://a.test/catalog.json"), "remove");
    Check(SourceStore.Load(folder, "").Entries.Count == 1, "the removal was not saved");
});

Case("sources: the old config value moves into sources.json once", root =>
{
    var folder = Path.Combine(root, "oldenera-mods");
    var store = SourceStore.Load(folder, "https://a.test/c.json  https://b.test/c.json\nhttps://A.test/c.json");
    Check(store.Entries.Count == 2, "the config's URLs: " + store.Entries.Count);
    Check(File.Exists(Path.Combine(folder, "sources.json")), "not saved");
    store.Remove("https://a.test/c.json");
    // The config still has it, but the file is there now: the config is not read again.
    Check(SourceStore.Load(folder, "https://a.test/c.json https://b.test/c.json").Entries.Select(e => e.Url).SequenceEqual(new[] { "https://b.test/c.json" }), "the config was taken again");
});

Case("sources: an unreadable sources.json is kept aside and the list starts empty", root =>
{
    var folder = Path.Combine(root, "oldenera-mods");
    Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "sources.json"), "{ not json");
    var warned = new List<string>();
    Check(SourceStore.Load(folder, "https://a.test/c.json", warned.Add).Entries.Count == 0 && warned.Count == 1 && File.Exists(Path.Combine(folder, "sources.json.bad")), "not kept aside");
});

// Two sources listing the same mod with different meta files.
const string OtherSource = "https://other.test/catalog.json";
const string OtherMeta = "https://other.test/counter.json";

void PublishOther(Dictionary<string, byte[]> net)
{
    net[OtherSource] = Bytes($$"""{ "schema": 1, "name": "Other", "mods": [ { "id": "oe.counter", "meta": "{{OtherMeta}}" }, { "id": "oe.extra", "meta": "https://other.test/extra.json" } ] }""");
    net[OtherMeta] = Bytes("""{ "schema": 1, "id": "oe.counter", "name": { "english": "Their counter" }, "releases": [ { "version": "9.0.0", "games": [ "0.82.07" ], "files": [] } ] }""");
    net["https://other.test/extra.json"] = Bytes("""{ "schema": 1, "id": "oe.extra", "name": { "english": "Extra" }, "releases": [] }""");
}

Case("sources: each source reports its name, mods and state; a failed one does not stop the others", root =>
{
    var (client, net, _) = Net(root);
    Publish(net);
    PublishOther(net);
    var reading = client.ReadAll(new[] { Source, "https://missing.test/catalog.json", OtherSource }).Result;
    Check(reading.Sources.Count == 3, "a result per source");
    Check(reading.Sources[0].Name == "test" && reading.Sources[0].Mods == 1 && reading.Sources[0].State == SourceState.Read, "the first source");
    Check(reading.Sources[1].State == SourceState.Failed && reading.Sources[1].Error != null, "the missing source");
    Check(reading.Sources[2].Name == "Other" && reading.Sources[2].Mods == 2, "the third source");
    Check(reading.All.Count == 3 && reading.Mods.Count == 2, "all and chosen");
    Check(reading.All.All(m => m.SourceUrl == (m.Url.StartsWith("https://other.test") ? OtherSource : Source)), "SourceUrl");
    // Offline: the cached copies, said so.
    var (offline, _, _) = Net(root, offline: true);
    var cached = offline.ReadAll(new[] { Source, "https://missing.test/catalog.json" }).Result;
    Check(cached.Sources[0].State == SourceState.Cached && cached.Sources[0].Mods == 1 && cached.Sources[1].State == SourceState.Failed, "the cached source");
});

Case("sources: one source read for adding does not use the cache", root =>
{
    var (client, net, _) = Net(root);
    Publish(net);
    Check(client.ReadSource(Source, false).Result.result.State == SourceState.Read, "not read");
    var (offline, _, _) = Net(root, offline: true);
    Check(offline.ReadSource(Source, false).Result.result.State == SourceState.Failed, "the cache stood in for a source being added");
});

Case("sources: an installed mod's own source wins, otherwise the first source", root =>
{
    var (client, net, _) = Net(root);
    Publish(net);
    PublishOther(net);
    var both = new[] { Source, OtherSource };
    Check(client.Read(both).Result.First(m => m.Id == "oe.counter").Url == MetaUrl, "not the first source");
    var fromOther = new[] { new InstalledMod { Id = "oe.counter", Version = "9.0.0", Source = OtherMeta } };
    Check(client.Read(both, fromOther).Result.First(m => m.Id == "oe.counter").Url == OtherMeta, "the installed mod's source did not win");
    var fromFirst = new[] { new InstalledMod { Id = "oe.counter", Version = "0.48.0", Source = MetaUrl } };
    Check(client.Read(new[] { OtherSource, Source }, fromFirst).Result.First(m => m.Id == "oe.counter").Url == MetaUrl, "the mod's own source did not win over the first");
    var byHand = new[] { new InstalledMod { Id = "oe.counter", Version = "0.48.0" } };
    Check(client.Read(new[] { OtherSource, Source }, byHand).Result.First(m => m.Id == "oe.counter").Url == OtherMeta, "a mod put by hand: the first source");
    // Its source gone from the list: the other source's meta is chosen but offers no update.
    var chosen = client.Read(new[] { OtherSource }, fromFirst).Result;
    var state = new ManagerState { Mods = { new InstalledMod { Id = "oe.counter", Version = "0.48.0", Source = MetaUrl } } };
    Check(CatalogClient.Updates(chosen, state, "0.82.07").Count == 0, "an update from another source than the mod's own");
    var home = client.Read(both, state.Mods).Result;
    Check(CatalogClient.Updates(home, state, "0.82.07").Count == 1, "no update from the mod's own source");
});

Case("versions compare by numbers", root =>
{
    var c = VersionComparer.Instance;
    Check(c.Compare("0.48.10", "0.48.9") > 0 && c.Compare("0.49", "0.48.10") > 0 && c.Compare("1.0", "1.0.0") == 0, "the order");
});
// ---------- online, only with OE_GITHUB_TOKEN: the real catalog in the private repository, the whole way ----------

if (Environment.GetEnvironmentVariable("OE_GITHUB_TOKEN") is { Length: > 0 } githubToken)
{
    Case("online: the catalog read from the private repository, every mod downloaded from its release and checked", root =>
    {
        var fetch = new GitHubFetch("OldenEraModManager-tests", githubToken);
        var client = new CatalogClient(fetch.Get, Path.Combine(root, "oldenera-mods"), line => Console.WriteLine("      " + line));
        var mods = client.Read(new[] { "https://raw.githubusercontent.com/fogone/olden-era-mods/main/catalog/catalog.json" }).Result;
        Check(mods.Count == 8, $"{mods.Count} mods in the catalog, 8 expected (the Mods tab comes with the manager)");
        foreach (var meta in mods)
        {
            var release = CatalogClient.ReleaseFor(meta, "0.82.07") ?? throw new Exception($"{meta.Id}: no release for 0.82.07");
            var installed = client.Download(meta, release).Result;   // each file's sha256 checked inside
            Check(installed.Files.All(f => File.Exists(Path.Combine(root, "oldenera-mods", meta.Id, release.Version, f))), $"{meta.Id}: files missing");
            Console.WriteLine($"      {meta.Id} {release.Version}: {installed.Files.Count} files, sha256 ok");
        }
    });
    Case("online: an installed mod's update is seen, downloaded and checked (battle counter 0.48.0 -> the newest)", root =>
    {
        var fetch = new GitHubFetch("OldenEraModManager-tests", githubToken);
        var client = new CatalogClient(fetch.Get, Path.Combine(root, "oldenera-mods"), _ => { });
        var mods = client.Read(new[] { "https://raw.githubusercontent.com/fogone/olden-era-mods/main/catalog/catalog.json" }).Result;
        var state = new ManagerState { Mods = { new InstalledMod { Id = "nobirds.oldenera.battlecounter", Version = "0.48.0", Files = { "OldenEraBattleCounter.dll" } } } };
        var updates = CatalogClient.Updates(mods, state, "0.82.07");
        Check(updates.Count == 1 && VersionComparer.Instance.Compare(updates[0].Release.Version, "0.48.0") > 0, "no update seen for 0.48.0");
        var installed = client.Download(updates[0].Meta, updates[0].Release).Result;
        Console.WriteLine($"      update {updates[0].Installed} -> {installed.Version}: downloaded, sha256 ok");
        state.Mods[0].Version = installed.Version;
        Check(CatalogClient.Updates(mods, state, "0.82.07").Count == 0, "still an update after updating");
    });
    Case("online: without the token the private repository gives nothing", root =>
    {
        var client = new CatalogClient(new GitHubFetch("OldenEraModManager-tests").Get, Path.Combine(root, "oldenera-mods"), _ => { });
        Check(client.Read(new[] { "https://raw.githubusercontent.com/fogone/olden-era-mods/main/catalog/catalog.json" }).Result.Count == 0, "read without a token");
    });
}
Console.WriteLine($"{cases - failures} of {cases} passed");
return failures == 0 ? 0 : 1;
