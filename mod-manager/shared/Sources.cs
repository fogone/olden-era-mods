using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEra.Modding.Manager;

// The player's own mod sources (issue #6): BepInEx\oldenera-mods\sources.json, besides the built-in catalog.
//   { "schema": 1, "sources": [ { "url": "https://.../catalog.json", "name": "...", "added": "2026-11-08" } ] }
// No game, no BepInEx (the tests use it). The built-in source is not in the file.
public sealed class SourceEntry
{
    [JsonPropertyName("url")] public string Url { get; set; }
    // The root meta file's name when it was last read (null: none yet).
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("added")] public string Added { get; set; }
}

public sealed class SourceStore
{
    public const int Schema = 1;
    static readonly JsonSerializerOptions options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    sealed class FileShape
    {
        [JsonPropertyName("schema")] public int SchemaVersion { get; set; } = Schema;
        [JsonPropertyName("sources")] public List<SourceEntry> Sources { get; set; } = new();
    }

    readonly object gate = new();
    readonly string path;
    readonly List<SourceEntry> entries = new();

    SourceStore(string modsFolder) => path = Path.Combine(modsFolder, "sources.json");

    // The file; when there is none yet, the old config value (root URLs separated by whitespace) once, saved at once.
    // A file it cannot read is kept aside (sources.json.bad) and the list starts empty.
    public static SourceStore Load(string modsFolder, string legacyConfig, Action<string> warn = null)
    {
        var store = new SourceStore(modsFolder);
        if (!File.Exists(store.path))
        {
            foreach (var url in (legacyConfig ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
                store.entries.Add(new SourceEntry { Url = url, Added = Today() });
            store.Dedupe();
            try { store.Save(); } catch (Exception e) { warn?.Invoke($"{store.path}: {e.Message}"); }
            return store;
        }
        try
        {
            var shape = JsonSerializer.Deserialize<FileShape>(File.ReadAllText(store.path), options) ?? new FileShape();
            if (shape.SchemaVersion > Schema)
                warn?.Invoke($"{store.path} is of a newer manager (schema {shape.SchemaVersion}); read as far as this one understands it");
            store.entries.AddRange(shape.Sources.Where(s => !string.IsNullOrWhiteSpace(s?.Url)));
            store.Dedupe();
        }
        catch (Exception e)
        {
            warn?.Invoke($"{store.path} cannot be read ({e.Message}): kept as sources.json.bad, starting with no sources");
            try { File.Copy(store.path, store.path + ".bad", true); } catch (Exception) { }
        }
        return store;
    }

    static string Today() => DateTime.Now.ToString("yyyy-MM-dd");

    void Dedupe()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        entries.RemoveAll(e => !seen.Add(e.Url));
    }

    // A snapshot, in the order added.
    public IReadOnlyList<SourceEntry> Entries { get { lock (gate) return entries.Select(e => new SourceEntry { Url = e.Url, Name = e.Name, Added = e.Added }).ToList(); } }

    public bool Contains(string url) { lock (gate) return entries.Any(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase)); }

    // False when it is there already (case-insensitive) or empty.
    public bool Add(string url, string name)
    {
        url = url?.Trim();
        if (string.IsNullOrEmpty(url))
            return false;
        lock (gate)
        {
            if (entries.Any(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase)))
                return false;
            entries.Add(new SourceEntry { Url = url, Name = name, Added = Today() });
            Save();
            return true;
        }
    }

    public bool Remove(string url)
    {
        lock (gate)
        {
            var removed = entries.RemoveAll(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed)
                Save();
            return removed;
        }
    }

    // Remembers the name a source's root file gave (saved when it changed).
    public void SetName(string url, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        lock (gate)
        {
            var entry = entries.FirstOrDefault(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase));
            if (entry == null || entry.Name == name)
                return;
            entry.Name = name;
            Save();
        }
    }

    // Written through a temporary file, like the state.
    public void Save()
    {
        lock (gate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new FileShape { Sources = entries }, options));
            File.Move(temporary, path, true);
        }
    }

    // ---------- GitHub shorthand ----------

    public const string DefaultRepository = "olden-era-mods";

    // "owner" or "owner/repo" (or a pasted https://github.com/owner[/repo]) -> the standard place of the root file:
    // https://raw.githubusercontent.com/<owner>/<repo>/HEAD/catalog/catalog.json (HEAD: the default branch).
    // Null when the names are not GitHub's.
    public static string GitHubUrl(string input)
    {
        input = input?.Trim();
        if (string.IsNullOrEmpty(input))
            return null;
        if (input.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) || input.StartsWith("https://www.github.com/", StringComparison.OrdinalIgnoreCase))
        {
            input = input.Substring(input.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + "github.com/".Length);
            var cut = input.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0)
                input = input.Substring(0, cut);
            var parts = input.Split('/', StringSplitOptions.RemoveEmptyEntries);
            // Only the owner and the repository count; a deeper address (tree/..., blob/...) is the repository's page.
            input = parts.Length == 0 ? "" : parts.Length == 1 ? parts[0] : parts[0] + "/" + parts[1];
            if (input.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                input = input.Substring(0, input.Length - 4);
        }
        var names = input.Split('/');
        if (names.Length > 2)
            return null;
        var owner = names[0];
        var repo = names.Length == 2 ? names[1] : DefaultRepository;
        if (!ValidOwner(owner) || !ValidRepo(repo))
            return null;
        return $"https://raw.githubusercontent.com/{owner}/{repo}/HEAD/catalog/catalog.json";
    }

    static bool AsciiLetterOrDigit(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

    // GitHub user names: letters, digits and '-', up to 39 characters.
    static bool ValidOwner(string name) =>
        name.Length is > 0 and <= 39 && name.All(c => AsciiLetterOrDigit(c) || c == '-');

    // Repository names: letters, digits, '-', '_' and '.', not "." or "..", up to 100 characters.
    static bool ValidRepo(string name) =>
        name.Length is > 0 and <= 100 && name != "." && name != ".." && name.All(c => AsciiLetterOrDigit(c) || c == '-' || c == '_' || c == '.');
}
