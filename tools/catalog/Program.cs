using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using OldenEra.Modding.Manager;

// Adds a release to a mod's meta file in the catalog and the mod to the root file (issue #10):
//   catalog add --catalog <dir> --root-url <URL the catalog folder is served at> --id <id> --version <v> --game <game version>
//               --bepinex <v> [--requires id[@version],...] [--lang <the mod's lang folder>] [--author a] [--source url]
//               --file <path>=<download URL> [--file ...]
// The same version again with the same files: the game version is added to it (a build that works on another game
// version too); with other files: refused (a version is never published twice with other content).
var options = new Dictionary<string, List<string>>();
for (var i = 1; i + 1 < args.Length; i += 2)
{
    if (!args[i].StartsWith("--"))
        return Fail($"unexpected {args[i]}");
    var key = args[i][2..];
    if (!options.TryGetValue(key, out var list))
        options[key] = list = new List<string>();
    list.Add(args[i + 1]);
}
if (args.Length == 0 || args[0] != "add")
    return Fail("usage: catalog add --catalog <dir> --root-url <url> --id <id> --version <v> --game <g> --bepinex <v> --file <path>=<url> ...");

string One(string key) => options.TryGetValue(key, out var v) ? v[^1] : null;
string Need(string key) => One(key) ?? throw new ArgumentException($"--{key} is needed");

var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNameCaseInsensitive = true };
var catalogDir = Need("catalog");
var rootUrl = Need("root-url").TrimEnd('/');
var id = Need("id");
var version = Need("version");
var game = Need("game");

var metaPath = Path.Combine(catalogDir, "mods", id + ".json");
var meta = File.Exists(metaPath) ? JsonSerializer.Deserialize<ModMeta>(File.ReadAllText(metaPath), json) : new ModMeta { Id = id };
meta.Author = One("author") ?? meta.Author;
meta.Source = One("source") ?? meta.Source;

// The mod's name in every language it has (its lang/<language>.json: oe.<short id>.title).
if (One("lang") is { } langDir && Directory.Exists(langDir))
{
    var shortId = id.Split('.').Last();
    foreach (var file in Directory.GetFiles(langDir, "*.json"))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var token in doc.RootElement.GetProperty("tokens").EnumerateArray())
            if (token.GetProperty("sid").GetString() == $"oe.{shortId}.title")
                meta.Name[Path.GetFileNameWithoutExtension(file)] = token.GetProperty("text").GetString();
    }
}

var files = (options.TryGetValue("file", out var fileArgs) ? fileArgs : new List<string>()).Select(f =>
{
    var at = f.IndexOf('=');
    if (at <= 0)
        throw new ArgumentException($"--file {f}: <path>=<url>");
    var path = f[..at];
    return new ReleaseFile
    {
        Name = Path.GetFileName(path),
        Url = f[(at + 1)..],
        Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant(),
    };
}).ToList();
if (files.Count == 0)
    return Fail("no --file");

var release = meta.Releases.FirstOrDefault(r => r.Version == version);
if (release != null)
{
    var same = release.Files.Count == files.Count && release.Files.All(r => files.Any(f => f.Name == r.Name && f.Sha256 == r.Sha256));
    if (!same)
        return Fail($"{id} {version} is published already with other files: raise the version");
    if (!release.Games.Contains(game))
        release.Games.Add(game);
    Console.WriteLine($"{id} {version}: the same files, now also for the game {game}");
}
else
{
    release = new ModRelease
    {
        Version = version,
        Games = { game },
        BepInEx = One("bepinex"),
        Files = files,
        Requires = (One("requires") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Split('@')).Select(p => new Requirement { Id = p[0].Trim(), Version = p.Length > 1 ? p[1].Trim() : null }).ToList(),
    };
    meta.Releases.Add(release);
    meta.Releases.Sort((a, b) => VersionComparer.Instance.Compare(b.Version, a.Version));
    Console.WriteLine($"{id} {version}: added for the game {game}");
}
Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, json) + "\n");

var rootPath = Path.Combine(catalogDir, "catalog.json");
var root = File.Exists(rootPath) ? JsonSerializer.Deserialize<CatalogRoot>(File.ReadAllText(rootPath), json) : new CatalogRoot { Name = "Olden Era mods" };
if (!root.Mods.Any(m => m.Id == id))
{
    root.Mods.Add(new CatalogEntry { Id = id, Meta = $"{rootUrl}/mods/{id}.json" });
    root.Mods.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
}
File.WriteAllText(rootPath, JsonSerializer.Serialize(root, json) + "\n");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine("error: " + message);
    return 1;
}
