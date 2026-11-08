using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace OldenEra.Modding.Manager;

// The catalog (issue #10): a root meta file listing mods by their own meta files, each mod's meta file listing its
// releases, each release the game versions it works on and its files with their sha256. Read over HTTPS (a local file
// for a development source); every file checked by its hash before it is kept. Only files and HTTP through `fetch`:
// no BepInEx, no game (the manager gives the fetching and the game's version; the tests a fake).
//
//   catalog.json:   { "schema": 1, "name": "...", "mods": [ { "id": "...", "meta": "<URL of the mod's meta file>" } ] }
//   <mod>.json:     { "schema": 1, "id": "...", "name": { "english": "...", "russian": "..." }, "author": "...",
//                     "source": "<repository>", "description": { ... },
//                     "tags": [ "map", "tooltip" ],
//                     "releases": [ { "version": "0.48.0", "games": [ "0.82.07" ], "bepinex": "6.0.0-be.788",
//                                     "requires": [ { "id": "nobirds.oldenera.core", "version": "0.7.0" } ],
//                                     "files": [ { "name": "X.dll", "url": "https://...", "sha256": "..." } ] } ] }
public sealed class CatalogRoot
{
    [JsonPropertyName("schema")] public int Schema { get; set; } = 1;
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("mods")] public List<CatalogEntry> Mods { get; set; } = new();
}

public sealed class CatalogEntry
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("meta")] public string Meta { get; set; }
}

public sealed class ModMeta
{
    [JsonPropertyName("schema")] public int Schema { get; set; } = 1;
    [JsonPropertyName("id")] public string Id { get; set; }
    // By the game's language name (english, russian, ...): what the Mods tab shows, english when its language is missing.
    [JsonPropertyName("name")] public Dictionary<string, string> Name { get; set; } = new();
    [JsonPropertyName("description")] public Dictionary<string, string> Description { get; set; } = new();
    [JsonPropertyName("author")] public string Author { get; set; }
    [JsonPropertyName("source")] public string Source { get; set; }
    // Tag ids (map, battle, tooltip, ...): the manager names the known ones per language (Tags.TagName), an unknown one
    // is shown by its id. Meta files without it read fine (an empty list).
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("releases")] public List<ModRelease> Releases { get; set; } = new();

    // The URL it was read from (not in the file).
    [JsonIgnore] public string Url { get; set; }
    // The root file (the source) that listed it (not in the file).
    [JsonIgnore] public string SourceUrl { get; set; }

    public string NameIn(string language) =>
        Name.TryGetValue(language, out var name) || Name.TryGetValue("english", out name) ? name : Id;
}

public sealed class ModRelease
{
    [JsonPropertyName("version")] public string Version { get; set; }
    // The game versions it works on (its game's names unchanged across them).
    [JsonPropertyName("games")] public List<string> Games { get; set; } = new();
    [JsonPropertyName("bepinex")] public string BepInEx { get; set; }
    [JsonPropertyName("requires")] public List<Requirement> Requires { get; set; } = new();
    [JsonPropertyName("files")] public List<ReleaseFile> Files { get; set; } = new();
}

public sealed class Requirement
{
    [JsonPropertyName("id")] public string Id { get; set; }
    // The lowest version it needs, or null for any.
    [JsonPropertyName("version")] public string Version { get; set; }
}

public sealed class ReleaseFile
{
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("url")] public string Url { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; }
}

public enum SourceState { Read, Failed, Cached }

// How one source's root file read: its name, how many mods it lists, its state (Failed: nothing came and there is no
// cached copy; Cached: a cached copy was used) and why when it failed.
public sealed record SourceResult(string Url, string Name, int Mods, SourceState State, string Error);

// What a reading of the sources gave: every meta of every source (All, in the sources' order), the one chosen per id
// (Mods) and how each source read.
public sealed class CatalogReading
{
    public List<ModMeta> Mods { get; set; } = new();
    public List<ModMeta> All { get; set; } = new();
    public List<SourceResult> Sources { get; set; } = new();
}

// What the catalog has for one installed mod: a newer release for this game version.
public sealed record Update(string Id, string Installed, ModRelease Release, ModMeta Meta);

public sealed class CatalogClient
{
    public const long MaxFileSize = 32L * 1024 * 1024;   // a mod's DLL is far smaller; more is refused

    readonly Func<string, Task<byte[]>> fetch;
    readonly string cacheFolder;
    readonly Action<string> log;
    static readonly JsonSerializerOptions options = new() { PropertyNameCaseInsensitive = true };

    // `fetch`: the bytes at a URL (HTTPS; the manager's HttpClient). `modsFolder`: BepInEx\oldenera-mods (the cache in
    // .cache\, the mods' files in <id>\<version>\).
    public CatalogClient(Func<string, Task<byte[]>> fetch, string modsFolder, Action<string> log)
    {
        this.fetch = fetch;
        ModsFolder = modsFolder;
        cacheFolder = Path.Combine(modsFolder, ".cache");
        this.log = log ?? (_ => { });
    }

    public string ModsFolder { get; }

    // Only HTTPS, or a local file (a development source on this machine).
    public static bool Allowed(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.IsFile);

    async Task<byte[]> Get(string url)
    {
        if (!Allowed(url))
            throw new InvalidOperationException($"{url}: only https (or a local file) is allowed");
        var bytes = Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.IsFile ? File.ReadAllBytes(uri.LocalPath) : await fetch(url);
        if (bytes == null)
            throw new IOException($"{url}: nothing came");
        if (bytes.LongLength > MaxFileSize)
            throw new IOException($"{url}: {bytes.LongLength} bytes, more than the {MaxFileSize} allowed");
        return bytes;
    }

    // A meta file read from its URL, kept in the cache; the cached one when it cannot be read (no network) and
    // `useCache` (a source being added is not taken from the cache). Cached: whether the cached copy was used.
    async Task<(T value, bool cached)> GetJson<T>(string url, bool useCache = true) where T : class
    {
        var cached = Path.Combine(cacheFolder, Hex(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".json");
        try
        {
            var bytes = await Get(url);
            var value = JsonSerializer.Deserialize<T>(bytes, options) ?? throw new IOException($"{url}: empty");
            Directory.CreateDirectory(cacheFolder);
            File.WriteAllBytes(cached, bytes);
            return (value, false);
        }
        catch (Exception e) when (useCache && File.Exists(cached))
        {
            log($"{url}: {e.Message}; the cached copy is used");
            return (JsonSerializer.Deserialize<T>(File.ReadAllBytes(cached), options), true);
        }
    }

    // Every mod of the sources (their root files). A mod in several sources: the one from the source it was installed
    // from (an installed mod's Source is its meta file's URL), else from the first source.
    public async Task<List<ModMeta>> Read(IEnumerable<string> sources, IEnumerable<InstalledMod> installed = null) =>
        (await ReadAll(sources, installed)).Mods;

    public async Task<CatalogReading> ReadAll(IEnumerable<string> sources, IEnumerable<InstalledMod> installed = null)
    {
        var reading = new CatalogReading();
        foreach (var source in sources)
        {
            var (result, metas) = await ReadSource(source, true);
            reading.Sources.Add(result);
            reading.All.AddRange(metas);
        }
        reading.Mods = Choose(reading.All, installed);
        return reading;
    }

    // One source's root file and the mods' meta files it lists. `useCache`: the cached copies stand in for what cannot
    // be read; off for a source being added, which must read for real.
    public async Task<(SourceResult result, List<ModMeta> metas)> ReadSource(string source, bool useCache)
    {
        var metas = new List<ModMeta>();
        CatalogRoot root;
        bool cached;
        try { (root, cached) = await GetJson<CatalogRoot>(source, useCache); }
        catch (Exception e)
        {
            log($"source {source}: {e.Message}");
            return (new SourceResult(source, null, 0, SourceState.Failed, e.Message), metas);
        }
        var listed = root.Mods?.Where(m => !string.IsNullOrWhiteSpace(m?.Meta)).ToList() ?? new List<CatalogEntry>();
        foreach (var entry in listed)
        {
            if (metas.Any(m => string.Equals(m.Id, entry.Id, StringComparison.OrdinalIgnoreCase)))
                continue;
            try
            {
                var (meta, metaCached) = await GetJson<ModMeta>(entry.Meta, useCache);
                cached |= metaCached;
                if (!string.Equals(meta.Id, entry.Id, StringComparison.OrdinalIgnoreCase))
                {
                    log($"{entry.Meta}: says it is {meta.Id}, the catalog lists it as {entry.Id}; left out");
                    continue;
                }
                meta.Url = entry.Meta;
                meta.SourceUrl = source;
                metas.Add(meta);
            }
            catch (Exception e) { log($"{entry.Id} ({entry.Meta}): {e.Message}"); }
        }
        return (new SourceResult(source, root.Name, listed.Count, cached ? SourceState.Cached : SourceState.Read, null), metas);
    }

    // One meta per id, in the order the ids first appear: the installed mod's own source's (its Source is the meta
    // file's URL) when that source listed it, else the first source's.
    public static List<ModMeta> Choose(IEnumerable<ModMeta> all, IEnumerable<InstalledMod> installed)
    {
        var own = (installed ?? Array.Empty<InstalledMod>()).Where(m => !string.IsNullOrEmpty(m.Source)).ToList();
        var chosen = new List<ModMeta>();
        foreach (var group in all.GroupBy(m => m.Id, StringComparer.OrdinalIgnoreCase))
        {
            var from = own.FirstOrDefault(m => string.Equals(m.Id, group.Key, StringComparison.OrdinalIgnoreCase))?.Source;
            chosen.Add(from != null ? group.FirstOrDefault(m => string.Equals(m.Url, from, StringComparison.OrdinalIgnoreCase)) ?? group.First() : group.First());
        }
        return chosen;
    }

    // The newest release made for this game version, or null.
    public static ModRelease ReleaseFor(ModMeta meta, string gameVersion) =>
        meta.Releases.Where(r => r.Games.Contains(gameVersion)).OrderByDescending(r => r.Version, VersionComparer.Instance).FirstOrDefault();

    // The installed mods the catalog has a newer release of for this game version (not one that failed before).
    public static List<Update> Updates(IEnumerable<ModMeta> catalog, ManagerState state, string gameVersion) =>
        state.Mods.Select(installed => (installed, meta: catalog.FirstOrDefault(m => string.Equals(m.Id, installed.Id, StringComparison.OrdinalIgnoreCase))))
            // A mod updates only from the source it was installed from (one put by hand has none).
            .Where(x => x.meta != null && (string.IsNullOrEmpty(x.installed.Source) || string.Equals(x.installed.Source, x.meta.Url, StringComparison.OrdinalIgnoreCase)))
            .Select(x => (x.installed, x.meta, release: ReleaseFor(x.meta, gameVersion)))
            .Where(x => x.release != null && VersionComparer.Instance.Compare(x.release.Version, x.installed.Version) > 0)
            .Select(x => new Update(x.installed.Id, x.installed.Version, x.release, x.meta))
            .ToList();

    // A release's files into <id>\<version>\, each checked by its sha256 first; all or none (a mismatch or a failed
    // download leaves nothing). The installed entry for the state (switched on, its requirements).
    public async Task<InstalledMod> Download(ModMeta meta, ModRelease release)
    {
        var folder = Path.Combine(ModsFolder, meta.Id, release.Version);
        var files = new List<(string name, byte[] bytes)>();
        foreach (var file in release.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Name) || file.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || file.Name.Contains(".."))
                throw new InvalidOperationException($"{meta.Id} {release.Version}: a file name that is not a plain name: {file.Name}");
            if (string.IsNullOrWhiteSpace(file.Sha256))
                throw new InvalidOperationException($"{meta.Id} {release.Version}: {file.Name} has no sha256; refused");
            var bytes = await Get(file.Url);
            var hash = Hex(SHA256.HashData(bytes));
            if (!string.Equals(hash, file.Sha256.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"{meta.Id} {release.Version}: {file.Name} is not the file the catalog names (sha256 {hash}, expected {file.Sha256}); refused");
            files.Add((file.Name, bytes));
        }
        // Written into a temporary folder, then put in place whole.
        var temporary = folder + ".download";
        if (Directory.Exists(temporary))
            Directory.Delete(temporary, true);
        Directory.CreateDirectory(temporary);
        foreach (var (name, bytes) in files)
            File.WriteAllBytes(Path.Combine(temporary, name), bytes);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.CreateDirectory(Path.GetDirectoryName(folder)!);
        Directory.Move(temporary, folder);
        log($"{meta.Id} {release.Version}: {files.Count} files downloaded and checked");
        return new InstalledMod
        {
            Id = meta.Id,
            Version = release.Version,
            Enabled = true,
            Files = release.Files.Select(f => f.Name).ToList(),
            Requires = release.Requires.Select(r => r.Id).ToList(),
            Source = meta.Url,
        };
    }

    static string Hex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();
}

// "0.48.0" < "0.48.10" < "0.49": by numbers, part by part; a part that is not a number by its text.
public sealed class VersionComparer : IComparer<string>
{
    public static readonly VersionComparer Instance = new();

    public int Compare(string a, string b)
    {
        var x = (a ?? "").Split('.', '-', '+');
        var y = (b ?? "").Split('.', '-', '+');
        for (var i = 0; i < Math.Max(x.Length, y.Length); i++)
        {
            var p = i < x.Length ? x[i] : "0";
            var q = i < y.Length ? y[i] : "0";
            var c = long.TryParse(p, out var m) && long.TryParse(q, out var n) ? m.CompareTo(n) : string.CompareOrdinal(p, q);
            if (c != 0)
                return c;
        }
        return 0;
    }
}
