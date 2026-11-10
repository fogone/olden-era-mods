using System.Text;
using Nobirds.OldenEra.Modding.Manager;

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

// A package: a zip with manifest.json and plugins\ (what the SDK's ModPackage makes).
static byte[] Package(string id, string version, params (string path, string text)[] files)
{
    using var memory = new MemoryStream();
    using (var zip = new System.IO.Compression.ZipArchive(memory, System.IO.Compression.ZipArchiveMode.Create, true))
    {
        void Add(string path, string text)
        {
            using var writer = new StreamWriter(zip.CreateEntry(path).Open());
            writer.Write(text);
        }
        Add("manifest.json", $$"""{ "schema": 1, "id": "{{id}}", "version": "{{version}}", "files": [] }""");
        foreach (var (path, text) in files)
            Add(path, text);
    }
    return memory.ToArray();
}

void PublishPackage(Dictionary<string, byte[]> net, byte[] zip)
{
    net[Source] = Bytes($$"""{ "schema": 1, "name": "test", "mods": [ { "id": "oe.counter", "meta": "{{MetaUrl}}" } ] }""");
    net[MetaUrl] = Bytes($$"""
    { "schema": 1, "id": "oe.counter",
      "releases": [ { "version": "0.49.0", "games": [ "0.82.08" ],
          "files": [ { "name": "Counter.dll", "url": "https://example.test/0.49/Counter.dll", "sha256": "{{Sha(dll049)}}" } ] } ],
      "packages": [ { "version": "0.51.0", "games": [ "0.82.08" ], "requires": [ { "id": "oe.core" } ],
          "archive": { "name": "oe.counter-0.51.0.zip", "url": "https://example.test/0.51/oe.counter-0.51.0.zip", "sha256": "{{Sha(zip)}}" } } ] }
    """);
    net["https://example.test/0.51/oe.counter-0.51.0.zip"] = zip;
}

Case("catalog: a package is newer than the release of files, installed from its archive's plugins", root =>
{
    var (client, net, _) = Net(root);
    PublishPackage(net, Package("oe.counter", "0.51.0", ("plugins/Counter.dll", "counter 0.51"), ("README.md", "readme")));
    var meta = client.Read(new[] { Source }).Result[0];
    var release = CatalogClient.ReleaseFor(meta, "0.82.08");
    Check(release.Version == "0.51.0" && release.Archive != null, "the package is not the one for 0.82.08");
    var installed = client.Download(meta, release).Result;
    var folder = Path.Combine(root, "oldenera-mods", "oe.counter", "0.51.0");
    Check(Read(Path.Combine(folder, "Counter.dll")) == "counter 0.51", "the plugin is not there");
    Check(!File.Exists(Path.Combine(folder, "README.md")) && !File.Exists(Path.Combine(folder, "manifest.json")), "more than the plugins installed");
    Check(installed.Files.SequenceEqual(new[] { "Counter.dll" }) && installed.Requires.SequenceEqual(new[] { "oe.core" }), "the entry");
});

Case("catalog: a package's data archives go into its data folder; a package of data only installs", root =>
{
    var (client, net, _) = Net(root);
    PublishPackage(net, Package("oe.counter", "0.51.0", ("data/faction.zip", "zip"), ("data/notes.txt", "x")));
    var meta = client.Read(new[] { Source }).Result[0];
    var installed = client.Download(meta, CatalogClient.ReleaseFor(meta, "0.82.08")).Result;
    var folder = Path.Combine(root, "oldenera-mods", "oe.counter", "0.51.0");
    Check(Read(Path.Combine(folder, "data", "faction.zip")) == "zip", "the data archive is not there");
    Check(!File.Exists(Path.Combine(folder, "data", "notes.txt")), "more than the data archives installed");
    Check(installed.Files.Count == 0, "the entry");
});

Case("catalog: a package of another mod, a path in plugins or data, or nothing to install, is refused", root =>
{
    foreach (var zip in new[] { Package("oe.other", "0.51.0", ("plugins/Counter.dll", "x")), Package("oe.counter", "0.51.0", ("plugins/../evil.dll", "x")),
                 Package("oe.counter", "0.51.0", ("data/a/b.zip", "x")), Package("oe.counter", "0.51.0", ("README.md", "x")) })
    {
        var (client, net, _) = Net(root);
        PublishPackage(net, zip);
        var meta = client.Read(new[] { Source }).Result[0];
        var refused = false;
        try { client.Download(meta, CatalogClient.ReleaseFor(meta, "0.82.08")).Wait(); }
        catch (AggregateException e) when (e.InnerException is InvalidOperationException) { refused = true; }
        Check(refused, "taken");
        Check(!Directory.Exists(Path.Combine(root, "oldenera-mods", "oe.counter", "0.51.0")), "a folder was left");
    }
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

Case("sources: an installed mod's own source wins, otherwise the first with a release for the game, otherwise the first", root =>
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
    // Not installed from either: the first source with a release for the game (Other has 0.82.07 only).
    var reversed = new[] { OtherSource, Source };
    Check(client.Read(reversed, null, "0.82.08").Result.First(m => m.Id == "oe.counter").Url == MetaUrl, "a source without a release for the game chosen");
    Check(client.Read(reversed, null, "0.82.07").Result.First(m => m.Id == "oe.counter").Url == OtherMeta, "both have a release: not the first");
    Check(client.Read(reversed, null, "0.99.01").Result.First(m => m.Id == "oe.counter").Url == OtherMeta, "none has a release: not the first");
    Check(client.Read(reversed, fromOther, "0.82.08").Result.First(m => m.Id == "oe.counter").Url == OtherMeta, "the installed mod's source did not win over a release elsewhere");
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
    Check(c.Compare("0.4.1-rc.1", "0.4.1") < 0 && c.Compare("0.4.1", "0.4.1-rc.9") > 0 && c.Compare("0.4.1-rc.1", "0.4.0") > 0, "a pre-release is below its release, above the one before");
    Check(c.Compare("0.4.1-rc.10", "0.4.1-rc.2") > 0 && c.Compare("1.0-alpha", "1.0-rc") < 0 && c.Compare("1.0-rc", "1.0-rc.1") < 0 && c.Compare("1.0-1", "1.0-a") < 0, "pre-release identifiers: numbers as numbers, below words, a shorter list lower");
    Check(c.Compare("1.0+abc", "1.0") == 0, "build metadata ignored");
});
// ---------- check frequency, names, screenshots, batches (issues #6, #14) ----------

Case("check schedule: start always, daily by the day, weekly by seven days, manual never", root =>
{
    var now = new DateTime(2026, 11, 8, 10, 0, 0);
    Check(CheckSchedule.IsDue("start", now, now) && CheckSchedule.IsDue(null, null, now) && CheckSchedule.IsDue("nonsense", now, now), "start (or unknown) is always due");
    Check(!CheckSchedule.IsDue("manual", null, now) && !CheckSchedule.IsDue("manual", now.AddDays(-100), now), "manual is never due");
    Check(CheckSchedule.IsDue("daily", null, now), "daily, never checked");
    Check(!CheckSchedule.IsDue("daily", new DateTime(2026, 11, 8, 0, 5, 0), now), "daily, checked today");
    Check(CheckSchedule.IsDue("daily", new DateTime(2026, 11, 7, 23, 59, 0), now), "daily, checked yesterday");
    Check(CheckSchedule.IsDue("daily", now.AddHours(3), now), "daily, last check in the future");
    Check(CheckSchedule.IsDue("weekly", null, now), "weekly, never checked");
    Check(!CheckSchedule.IsDue("weekly", now.AddDays(-6), now), "weekly, six days ago");
    Check(CheckSchedule.IsDue("weekly", now.AddDays(-7), now), "weekly, seven days ago");
    Check(CheckSchedule.IsDue("WEEKLY", now.AddDays(-30), now), "weekly in capitals");
    Check(CheckSchedule.Normalize(" Daily ") == "daily" && CheckSchedule.Normalize("x") == "start", "normalize");
});

Case("sources.json: checkEvery and lastChecked round-trip, the built-in source's too; an old file reads as start / never", root =>
{
    var folder = Path.Combine(root, "oldenera-mods");
    Directory.CreateDirectory(folder);
    const string Built = "https://built.test/catalog.json";
    var store = SourceStore.Load(folder, "", null, Built);
    Check(store.CheckEveryOf(Built) == "start" && store.LastCheckedOf(Built) == null, "built-in defaults");
    store.Add("https://a.test/c.json", "A");
    Check(store.CheckEveryOf("https://a.test/c.json") == "start", "default start");
    Check(store.SetCheckEvery("https://a.test/c.json", "Weekly") && store.SetCheckEvery(Built, "manual"), "not set");
    Check(!store.SetCheckEvery("https://a.test/c.json", "hourly") && !store.SetCheckEvery("https://unknown.test/c.json", "daily"), "a bad value or source was accepted");
    var when = new DateTime(2026, 11, 8, 9, 30, 0);
    store.SetChecked("https://a.test/c.json", when);
    store.SetChecked(Built, when.AddHours(1));
    store.SetName(Built, "Built-in");
    var again = SourceStore.Load(folder, "", null, Built);
    Check(again.CheckEveryOf("https://a.test/c.json") == "weekly" && again.LastCheckedOf("https://a.test/c.json") == when, "own source lost its settings");
    Check(again.CheckEveryOf(Built) == "manual" && again.LastCheckedOf(Built) == when.AddHours(1) && again.Settings(Built).Name == "Built-in", "built-in settings lost");
    Check(again.Entries.Count == 1 && again.Entries[0].CheckEvery == "weekly", "the built-in source is in the list");
    Check(!again.IsDue(Built, when.AddDays(30)) && again.IsDue("https://a.test/c.json", when.AddDays(7)) && !again.IsDue("https://a.test/c.json", when.AddDays(6)), "IsDue");
    // An old file: no checkEvery, no lastChecked, no builtIn.
    File.WriteAllText(Path.Combine(folder, "sources.json"), """{ "schema": 1, "sources": [ { "url": "https://old.test/c.json", "name": "Old", "added": "2026-11-01" } ] }""");
    var old = SourceStore.Load(folder, "", null, Built);
    Check(old.Entries.Count == 1 && old.CheckEveryOf("https://old.test/c.json") == "start" && old.LastCheckedOf("https://old.test/c.json") == null, "old file");
    Check(old.IsDue("https://old.test/c.json", DateTime.Now) && old.IsDue(Built, DateTime.Now), "an old file's sources are due at every start");
    old.SetCheckEvery("https://old.test/c.json", "daily");
    Check(!File.ReadAllText(Path.Combine(folder, "sources.json")).Contains("\"lastChecked\""), "a null last check was written");
});

Case("sources: due ones read from the network, the others from the saved copies; a never-read one is NotChecked", root =>
{
    var (client, net, asked) = Net(root);
    Publish(net);
    PublishOther(net);
    var both = new[] { Source, OtherSource };
    Check(client.ReadAll(both).Result.Sources.All(s => s.State == SourceState.Read), "the first reading");
    asked.Clear();
    var reading = client.ReadAll(both, null, url => url == OtherSource).Result;
    Check(reading.Sources[0].State == SourceState.Cached && reading.Sources[0].Mods == 1 && reading.Sources[0].Name == "test", "the source not due is not from the saved copy");
    Check(reading.Sources[1].State == SourceState.Read, "the due source was not read");
    Check(!asked.Any(a => a == Source || a == MetaUrl), "the network was asked for a source not due: " + string.Join(",", asked));
    Check(reading.Mods.Count == 2, "mods of the cached source missing");
    var never = client.ReadAll(new[] { "https://never.test/catalog.json" }, null, _ => false).Result;
    Check(never.Sources[0].State == SourceState.NotChecked && never.Sources[0].Error == null && never.Mods.Count == 0 && !asked.Contains("https://never.test/catalog.json"), "a never-read source not due");
    var forced = client.ReadAll(new[] { "https://never.test/catalog.json" }, null, _ => true).Result;
    Check(forced.Sources[0].State == SourceState.Failed, "a due source that does not read");
});

Case("sources: a root file without a name is flagged, one with a name is not", root =>
{
    var (client, net, _) = Net(root);
    net[Source] = Bytes("""{ "schema": 1, "mods": [] }""");
    net[OtherSource] = Bytes("""{ "schema": 1, "name": "  ", "mods": [] }""");
    net["https://named.test/c.json"] = Bytes("""{ "schema": 1, "name": "Named", "mods": [] }""");
    var reading = client.ReadAll(new[] { Source, OtherSource, "https://named.test/c.json", "https://missing.test/c.json" }).Result;
    Check(reading.Sources[0].NoName && reading.Sources[1].NoName, "a missing or blank name was not flagged");
    Check(!reading.Sources[2].NoName && !reading.Sources[3].NoName, "a named or failed source was flagged");
    Check(reading.Sources[0].State == SourceState.Read, "a nameless source is still read");
});

// A small PNG (the signature and some bytes) and a JPG.
byte[] Png(int length = 64) { var b = new byte[length]; new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(b, 0); b[^1] = 7; return b; }
byte[] Jpg() => new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 };

Case("meta: screenshots round-trip with captions by language; a file without them reads as an empty list", root =>
{
    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var meta = System.Text.Json.JsonSerializer.Deserialize<ModMeta>("""{ "id": "a", "screenshots": [ { "url": "https://x.test/a.png", "sha256": "ab", "caption": { "english": "One", "russian": "Раз" } }, { "url": "https://x.test/b.jpg", "sha256": "cd" } ] }""", opts);
    Check(meta.Screenshots.Count == 2 && meta.Screenshots[0].CaptionIn("russian") == "Раз" && meta.Screenshots[0].CaptionIn("german") == "One", "captions");
    Check(meta.Screenshots[1].CaptionIn("english") == null, "a missing caption is not null");
    var again = System.Text.Json.JsonSerializer.Deserialize<ModMeta>(System.Text.Json.JsonSerializer.Serialize(meta), opts);
    Check(again.Screenshots.Count == 2 && again.Screenshots[1].Url == "https://x.test/b.jpg", "lost on a round trip");
    Check(System.Text.Json.JsonSerializer.Deserialize<ModMeta>("""{ "id": "a" }""", opts).Screenshots.Count == 0, "no screenshots is not an empty list");
});

Case("screenshots: downloaded, checked by sha256 and signature, cached; a bad one is refused and leaves nothing", root =>
{
    var (client, net, asked) = Net(root);
    var png = Png();
    var jpg = Jpg();
    net["https://x.test/a.png"] = png;
    net["https://x.test/b.jpg"] = jpg;
    var good = new ScreenshotFile { Url = "https://x.test/a.png", Sha256 = Sha(png) };
    Check(client.ScreenshotPath(good) == null, "cached before downloading");
    var path = client.LoadScreenshot(good).Result;
    Check(path.EndsWith(Sha(png) + ".png") && path.Contains(Path.Combine(".cache", "screens")) && File.ReadAllBytes(path).SequenceEqual(png), "the cached file");
    Check(client.ScreenshotPath(good) == path, "ScreenshotPath does not find it");
    var jpgPath = client.LoadScreenshot(new ScreenshotFile { Url = "https://x.test/b.jpg", Sha256 = Sha(jpg).ToUpperInvariant() }).Result;
    Check(jpgPath.EndsWith(".jpg"), "a jpg kept as a jpg");
    // Cache hit: no network.
    asked.Clear();
    Check(client.LoadScreenshot(good).Result == path && asked.Count == 0, "the cached screenshot was downloaded again");

    void Refused(ScreenshotFile shot, string what)
    {
        var refused = false;
        try { client.LoadScreenshot(shot).Wait(); }
        catch (AggregateException) { refused = true; }
        Check(refused, what + " was not refused");
        Check(client.ScreenshotPath(shot) == null, what + " left a file");
    }
    net["https://x.test/other.png"] = Png(80);
    Refused(new ScreenshotFile { Url = "https://x.test/other.png", Sha256 = Sha(Png(33)) }, "a sha256 mismatch");
    var big = Png(2 * 1024 * 1024 + 1);
    net["https://x.test/big.png"] = big;
    Refused(new ScreenshotFile { Url = "https://x.test/big.png", Sha256 = Sha(big) }, "a file over 2 MB");
    var exactly = Png(2 * 1024 * 1024);
    net["https://x.test/max.png"] = exactly;
    Check(client.LoadScreenshot(new ScreenshotFile { Url = "https://x.test/max.png", Sha256 = Sha(exactly) }).Result.EndsWith(".png"), "a file of exactly 2 MB was refused");
    var text = Bytes("not a picture");
    net["https://x.test/t.png"] = text;
    Refused(new ScreenshotFile { Url = "https://x.test/t.png", Sha256 = Sha(text) }, "a file that is not a picture");
    net["http://x.test/h.png"] = Png(41);
    Refused(new ScreenshotFile { Url = "http://x.test/h.png", Sha256 = Sha(Png(41)) }, "an http address");
    Refused(new ScreenshotFile { Url = "https://x.test/a.png", Sha256 = "../../evil" }, "a sha256 that is a path");
    Refused(new ScreenshotFile { Url = "https://x.test/a.png" }, "no sha256");
    Refused(new ScreenshotFile { Url = "https://x.test/missing.png", Sha256 = Sha(Png(9)) }, "a missing file");
    // A whole mod: the good ones come, the bad one is left out, the count says how many came now.
    var meta = new ModMeta { Id = "m", Screenshots = { new ScreenshotFile { Url = "https://x.test/other.png", Sha256 = Sha(png) }, good, new ScreenshotFile { Url = "https://x.test/b.jpg", Sha256 = Sha(jpg) } } };
    Check(client.LoadScreenshots(meta).Result == 0, "nothing new was expected (the good ones are cached, the bad refused)");
    var (client2, net2, _) = Net(Path.Combine(root, "second"));
    net2["https://x.test/a.png"] = png;
    net2["https://x.test/b.jpg"] = jpg;
    net2["https://x.test/other.png"] = Png(80);
    Check(client2.LoadScreenshots(meta).Result == 2 && client2.ScreenshotPath(meta.Screenshots[1]) != null && client2.ScreenshotPath(meta.Screenshots[2]) != null, "LoadScreenshots");
});

Case("icon: root file round-trips it, an old root without one reads, the source result carries it", root =>
{
    var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var rootFile = System.Text.Json.JsonSerializer.Deserialize<CatalogRoot>("""{ "schema": 1, "name": "N", "icon": { "url": "https://x.test/i.png", "sha256": "ab" }, "mods": [] }""", opts);
    Check(rootFile.Icon?.Url == "https://x.test/i.png" && rootFile.Icon.Sha256 == "ab", "icon read");
    var again = System.Text.Json.JsonSerializer.Deserialize<CatalogRoot>(System.Text.Json.JsonSerializer.Serialize(rootFile), opts);
    Check(again.Icon?.Sha256 == "ab" && again.Icon.Url == "https://x.test/i.png", "icon lost on a round trip");
    Check(System.Text.Json.JsonSerializer.Deserialize<CatalogRoot>("""{ "schema": 1, "name": "N", "mods": [] }""", opts).Icon == null, "an old root has no icon");
    var png = Png();
    var (client, net, _) = Net(root);
    net[Source] = Bytes("{ \"schema\": 1, \"name\": \"N\", \"icon\": { \"url\": \"https://x.test/i.png\", \"sha256\": \"" + Sha(png) + "\" }, \"mods\": [] }");
    net[OtherSource] = Bytes("""{ "schema": 1, "name": "Old", "mods": [] }""");
    var reading = client.ReadAll(new[] { Source, OtherSource }).Result;
    Check(reading.Sources[0].Icon?.Sha256 == Sha(png) && reading.Sources[1].Icon == null, "SourceResult.Icon");
});

Case("icon: downloaded into .cache\\icons, checked by sha256, signature, https and 512 KB; a bad one leaves nothing", root =>
{
    var (client, net, asked) = Net(root);
    var png = Png();
    net["https://x.test/i.png"] = png;
    var icon = new IconFile { Url = "https://x.test/i.png", Sha256 = Sha(png) };
    Check(client.IconPath(icon) == null, "cached before downloading");
    var path = client.LoadIcon(icon).Result;
    Check(path.EndsWith(Sha(png) + ".png") && path.Contains(Path.Combine(".cache", "icons")) && File.ReadAllBytes(path).SequenceEqual(png), "the cached file");
    Check(client.IconPath(icon) == path, "IconPath does not find it");
    asked.Clear();
    Check(client.LoadIcon(icon).Result == path && asked.Count == 0, "downloaded again");
    void Refused(IconFile bad, string what)
    {
        var refused = false;
        try { client.LoadIcon(bad).Wait(); }
        catch (AggregateException) { refused = true; }
        Check(refused, what + " was not refused");
        Check(client.IconPath(bad) == null, what + " left a file");
    }
    net["https://x.test/other.png"] = Png(80);
    Refused(new IconFile { Url = "https://x.test/other.png", Sha256 = Sha(Png(33)) }, "a sha256 mismatch");
    var big = Png(512 * 1024 + 1);
    net["https://x.test/big.png"] = big;
    Refused(new IconFile { Url = "https://x.test/big.png", Sha256 = Sha(big) }, "an icon over 512 KB");
    var exactly = Png(512 * 1024);
    net["https://x.test/max.png"] = exactly;
    Check(client.LoadIcon(new IconFile { Url = "https://x.test/max.png", Sha256 = Sha(exactly) }).Result.EndsWith(".png"), "an icon of exactly 512 KB was refused");
    var text = Bytes("not a picture");
    net["https://x.test/t.png"] = text;
    Refused(new IconFile { Url = "https://x.test/t.png", Sha256 = Sha(text) }, "not a picture");
    net["http://x.test/h.png"] = Png(41);
    Refused(new IconFile { Url = "http://x.test/h.png", Sha256 = Sha(Png(41)) }, "an http address");
    Refused(new IconFile { Url = "https://x.test/i.png" }, "no sha256");
});

Case("batches: dependents first when removing or switching off, what a mod needs first when switching on; a needed mod stays", root =>
{
    InstalledMod M(string id, params string[] requires) => new() { Id = id, Version = "1", Requires = requires.ToList() };
    var mods = new[] { M("core"), M("a", "core"), M("b", "core", "a"), M("lone") };
    Check(Batch.DependentsFirst(new[] { "core", "a", "b" }, mods).SequenceEqual(new[] { "b", "a", "core" }), "dependents first: " + string.Join(",", Batch.DependentsFirst(new[] { "core", "a", "b" }, mods)));
    Check(Batch.SwitchOrder(new[] { "b", "core", "a" }, mods, true).SequenceEqual(new[] { "core", "a", "b" }), "on: what a mod needs first");
    Check(Batch.SwitchOrder(new[] { "core", "a" }, mods, false).SequenceEqual(new[] { "a", "core" }), "off: dependents first");
    Check(Batch.Removable(new[] { "core", "a", "b" }, mods).SequenceEqual(new[] { "b", "a", "core" }), "everything selected goes, dependents first");
    Check(Batch.Removable(new[] { "core", "a" }, mods).Count == 0, "core and a are both needed by b, which stays");
    Check(Batch.Removable(new[] { "b", "lone", "core" }, mods).SequenceEqual(new[] { "b", "lone" }), "core stays while a needs it");
    Check(Batch.Removable(new[] { "nobody", "lone", "lone" }, mods).SequenceEqual(new[] { "lone" }), "unknown and repeated ids");
    Check(Batch.DependentsFirst(new[] { "x", "y" }, new[] { M("x", "y"), M("y", "x") }).Count == 2, "a cycle lost mods");
});

Case("the mod manager's lang files all parse and have the same keys as english", root =>
{
    var dir = AppContext.BaseDirectory;
    while (dir != null && !Directory.Exists(Path.Combine(dir, "mod-manager", "lang")))
        dir = Path.GetDirectoryName(dir);
    Check(dir != null, "the repository's mod-manager\\lang not found");
    var docs = new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true };
    HashSet<string> Keys(string file) => System.Text.Json.JsonDocument.Parse(File.ReadAllText(file), docs).RootElement.GetProperty("tokens").EnumerateArray()
        .Select(t => t.GetProperty("sid").GetString()).ToHashSet();
    var files = Directory.GetFiles(Path.Combine(dir, "mod-manager", "lang"), "*.json");
    Check(files.Length == 16, $"{files.Length} language files, 16 expected");
    var english = Keys(Path.Combine(dir, "mod-manager", "lang", "english.json"));
    foreach (var file in files)
    {
        var keys = Keys(file);
        Check(keys.SetEquals(english), $"{Path.GetFileName(file)}: keys differ from english ({string.Join(",", keys.Except(english).Concat(english.Except(keys)))})");
        Check(!File.ReadAllText(file).Contains('≈'), $"{Path.GetFileName(file)} has the sign the game's font lacks");
    }
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
