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
