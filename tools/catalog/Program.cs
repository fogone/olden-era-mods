using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Nobirds.OldenEra.Modding.Manager;

// Adds a release to a mod's meta file in the catalog and the mod to the root file (issue #10):
//   catalog add --catalog <dir> --root-url <URL the catalog folder is served at> --id <id> --version <v> --game <game version>
//               --bepinex <v> [--requires id[@version],...] [--lang <the mod's lang folder>] [--author a] [--source url]
//               [--tags a,b,c]   (replaces the mod's tags: trimmed, lowercased, distinct, empty ones dropped)
//               [--name <catalog name>]   (the root file's name, shown everywhere; required while the root file has none)
//               [--screenshot <path>=<url>[|<english caption>] ...]   (replaces the mod's screenshots; PNG or JPG, up to
//                                                                      2 MB each; the sha256 is computed)
//               [--icon <path>=<url>]   (the catalog's icon in the root file; PNG or JPG, up to 512 KB; https; sha256 computed)
//               --file <path>=<download URL> [--file ...]
// The same version again with the same files: the game version is added to it (a build that works on another game
// version too); with other files: refused (a version is never published twice with other content).
//   catalog meta --catalog <dir> --root-url <url> --id <id> [the options above but --version, --game, --bepinex,
//                --requires and --file]   (only the mod's description: name, texts, tags, screenshots, author, source,
//                and the root file's name and icon; its releases stay as they are)
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
if (args.Length == 0 || args[0] is not ("add" or "meta"))
    return Fail("usage: catalog add --catalog <dir> --root-url <url> --id <id> --version <v> --game <g> --bepinex <v> --file <path>=<url> ...\n" +
                "       catalog meta --catalog <dir> --root-url <url> --id <id> [--tags ...] [--lang ...] [--screenshot ...]");
var metaOnly = args[0] == "meta";

string One(string key) => options.TryGetValue(key, out var v) ? v[^1] : null;
string Need(string key) => One(key) ?? throw new ArgumentException($"--{key} is needed");

var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNameCaseInsensitive = true };
var catalogDir = Need("catalog");
var rootUrl = Need("root-url").TrimEnd('/');
var id = Need("id");

// The root file's name is required (it is shown everywhere): given now, or already in the file.
var rootPath = Path.Combine(catalogDir, "catalog.json");
var root = File.Exists(rootPath) ? JsonSerializer.Deserialize<CatalogRoot>(File.ReadAllText(rootPath), json) : new CatalogRoot();
if (One("name") is { } rootName && !string.IsNullOrWhiteSpace(rootName))
    root.Name = rootName.Trim();
if (string.IsNullOrWhiteSpace(root.Name))
    return Fail("the catalog's root file has no name: give one with --name <catalog name> (it is shown everywhere)");
if (One("icon") is { } iconArg)
{
    var at = iconArg.IndexOf('=');
    if (at <= 0)
        return Fail($"--icon {iconArg}: <path>=<url>");
    var path = iconArg[..at];
    var url = iconArg[(at + 1)..];
    if (!File.Exists(path))
        return Fail($"--icon {path}: no such file");
    var bytes = File.ReadAllBytes(path);
    if (bytes.LongLength > CatalogClient.MaxIconSize)
        return Fail($"--icon {path}: {bytes.LongLength} bytes, more than the {CatalogClient.MaxIconSize} allowed");
    if (CatalogClient.PictureType(bytes) == null)
        return Fail($"--icon {path}: neither a PNG nor a JPG");
    if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        return Fail($"--icon {path}: the URL must be https");
    root.Icon = new IconFile { Url = url, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
}

var metaPath = Path.Combine(catalogDir, "mods", id + ".json");
var meta = File.Exists(metaPath) ? JsonSerializer.Deserialize<ModMeta>(File.ReadAllText(metaPath), json) : new ModMeta { Id = id };
meta.Author = One("author") ?? meta.Author;
meta.Source = One("source") ?? meta.Source;
if (One("tags") is { } tagsArg)
    meta.Tags = tagsArg.Split(',').Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList();

// Screenshots: <path>=<url> with an optional |<english caption>; given: they replace the mod's list.
if (options.TryGetValue("screenshot", out var shotArgs))
{
    var shots = new List<ScreenshotFile>();
    foreach (var arg in shotArgs)
    {
        var at = arg.IndexOf('=');
        if (at <= 0)
            return Fail($"--screenshot {arg}: <path>=<url>[|<caption>]");
        var path = arg[..at];
        var rest = arg[(at + 1)..];
        var bar = rest.LastIndexOf('|');
        var url = bar >= 0 ? rest[..bar] : rest;
        var caption = bar >= 0 ? rest[(bar + 1)..].Trim() : "";
        if (!File.Exists(path))
            return Fail($"--screenshot {path}: no such file");
        var bytes = File.ReadAllBytes(path);
        if (bytes.LongLength > CatalogClient.MaxScreenshotSize)
            return Fail($"--screenshot {path}: {bytes.LongLength} bytes, more than the {CatalogClient.MaxScreenshotSize} allowed");
        if (CatalogClient.PictureType(bytes) == null)
            return Fail($"--screenshot {path}: neither a PNG nor a JPG");
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return Fail($"--screenshot {path}: the URL must be https");
        shots.Add(new ScreenshotFile
        {
            Url = url,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
            Caption = caption.Length > 0 ? new Dictionary<string, string> { ["english"] = caption } : new(),
        });
    }
    meta.Screenshots = shots;
}

// The mod's name and description in every language it has (its lang/<language>.json: oe.<short id>.title, or
// oe.<short id>.name where a mod has no title, and oe.<short id>.description).
if (One("lang") is { } langDir && Directory.Exists(langDir))
{
    var shortId = id.Split('.').Last();
    foreach (var file in Directory.GetFiles(langDir, "*.json"))
    {
        var language = Path.GetFileNameWithoutExtension(file);
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        foreach (var token in doc.RootElement.GetProperty("tokens").EnumerateArray())
        {
            var sid = token.GetProperty("sid").GetString();
            if (sid == $"oe.{shortId}.title")
                meta.Name[language] = token.GetProperty("text").GetString();
            else if (sid == $"oe.{shortId}.name" && !meta.Name.ContainsKey(language))
                meta.Name[language] = token.GetProperty("text").GetString();
            else if (sid == $"oe.{shortId}.description")
                meta.Description[language] = token.GetProperty("text").GetString();
        }
    }
}

if (metaOnly)
{
    if (!meta.Releases.Any())
        return Fail($"{id}: no release in the catalog yet; add one first");
    Console.WriteLine($"{id}: the description only, the releases kept");
}
else if (AddRelease() is var failed && failed != 0)
    return failed;
Directory.CreateDirectory(Path.GetDirectoryName(metaPath)!);
File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, json) + "\n");

if (!root.Mods.Any(m => m.Id == id))
{
    root.Mods.Add(new CatalogEntry { Id = id, Meta = $"{rootUrl}/mods/{id}.json" });
    root.Mods.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
}
File.WriteAllText(rootPath, JsonSerializer.Serialize(root, json) + "\n");
return 0;

// A release added to the meta file, or the game version added to the same release; non-zero: refused.
int AddRelease()
{
    var version = Need("version");
    var game = Need("game");
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
    return 0;
}

static int Fail(string message)
{
    Console.Error.WriteLine("error: " + message);
    return 1;
}
