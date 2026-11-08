using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEra.Modding.Manager;

// The player's own mod sources (issue #6): BepInEx\oldenera-mods\sources.json, besides the built-in catalog.
//   { "schema": 1,
//     "builtIn": { "checkEvery": "daily", "lastChecked": "2026-11-08T10:00:00" },
//     "sources": [ { "url": "https://.../catalog.json", "name": "...", "added": "2026-11-08",
//                    "checkEvery": "weekly", "lastChecked": "2026-11-08T10:00:00" } ] }
// No game, no BepInEx (the tests use it). The built-in source is not in the list: its name and check settings are in
// "builtIn" (no url there). A file without checkEvery / lastChecked reads fine: checked at every start, never checked.
public sealed class SourceEntry
{
    [JsonPropertyName("url")] public string Url { get; set; }
    // The root meta file's name when it was last read (null: none yet).
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("added")] public string Added { get; set; }
    // How often the manager reads the source at the game's start: "start" (every start, the default), "daily",
    // "weekly" or "manual" (never by itself). Null reads as "start".
    [JsonPropertyName("checkEvery")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string CheckEvery { get; set; }
    // When it was last read from the network successfully (local time); null: never.
    [JsonPropertyName("lastChecked")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? LastChecked { get; set; }
}

// When a source is due for a check at the game's start.
public static class CheckSchedule
{
    public const string Start = "start", Daily = "daily", Weekly = "weekly", Manual = "manual";
    public static readonly string[] All = { Start, Daily, Weekly, Manual };

    // A known value in lower case; anything else (null, a typo, a newer manager's value) reads as "start".
    public static string Normalize(string every)
    {
        every = every?.Trim().ToLowerInvariant();
        return All.Contains(every) ? every : Start;
    }

    // start: always. daily: the last check was on an earlier day. weekly: seven days or more ago. manual: never.
    // A source never checked is due unless manual; a last check in the future (the clock moved back) counts as due.
    public static bool IsDue(string every, DateTime? lastChecked, DateTime now)
    {
        switch (Normalize(every))
        {
            case Manual: return false;
            case Daily: return lastChecked == null || lastChecked.Value > now || lastChecked.Value.Date < now.Date;
            case Weekly: return lastChecked == null || lastChecked.Value > now || (now.Date - lastChecked.Value.Date).Days >= 7;
            default: return true;
        }
    }
}

public sealed class SourceStore
{
    public const int Schema = 1;
    static readonly JsonSerializerOptions options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    sealed class FileShape
    {
        [JsonPropertyName("schema")] public int SchemaVersion { get; set; } = Schema;
        [JsonPropertyName("builtIn")] [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceEntry BuiltIn { get; set; }
        [JsonPropertyName("sources")] public List<SourceEntry> Sources { get; set; } = new();
    }

    readonly object gate = new();
    readonly string path;
    readonly List<SourceEntry> entries = new();
    // The built-in source's name and check settings (the url is not kept).
    SourceEntry builtIn = new();
    string builtInUrl;

    SourceStore(string modsFolder) => path = Path.Combine(modsFolder, "sources.json");

    // The file; when there is none yet, the old config value (root URLs separated by whitespace) once, saved at once.
    // A file it cannot read is kept aside (sources.json.bad) and the list starts empty. `builtInUrl`: the built-in
    // source's URL, whose settings (name, check frequency, last check) the store keeps too.
    public static SourceStore Load(string modsFolder, string legacyConfig, Action<string> warn = null, string builtInUrl = null)
    {
        var store = new SourceStore(modsFolder) { builtInUrl = builtInUrl };
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
            store.builtIn = shape.BuiltIn ?? new SourceEntry();
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
    public IReadOnlyList<SourceEntry> Entries { get { lock (gate) return entries.Select(Copy).ToList(); } }

    static SourceEntry Copy(SourceEntry e) =>
        new() { Url = e.Url, Name = e.Name, Added = e.Added, CheckEvery = e.CheckEvery, LastChecked = e.LastChecked };

    SourceEntry Find(string url) =>
        builtInUrl != null && string.Equals(url, builtInUrl, StringComparison.OrdinalIgnoreCase) ? builtIn
        : entries.FirstOrDefault(e => string.Equals(e.Url, url, StringComparison.OrdinalIgnoreCase));

    // A source's settings (the built-in one's too), or null for one that is not known.
    public SourceEntry Settings(string url) { lock (gate) return Find(url) is { } e ? Copy(e) : null; }

    // How often the source is checked at the start (normalized: "start" when not set).
    public string CheckEveryOf(string url) { lock (gate) return CheckSchedule.Normalize(Find(url)?.CheckEvery); }

    public DateTime? LastCheckedOf(string url) { lock (gate) return Find(url)?.LastChecked; }

    // False for an unknown source or a value that is not one of CheckSchedule.All.
    public bool SetCheckEvery(string url, string every)
    {
        if (every == null || !CheckSchedule.All.Contains(every.Trim().ToLowerInvariant()))
            return false;
        lock (gate)
        {
            if (Find(url) is not { } entry)
                return false;
            entry.CheckEvery = CheckSchedule.Normalize(every);
            Save();
            return true;
        }
    }

    // A successful read from the network just now.
    public void SetChecked(string url, DateTime when)
    {
        lock (gate)
        {
            if (Find(url) is not { } entry)
                return;
            entry.LastChecked = when;
            Save();
        }
    }

    // Whether the source is read from the network at this start.
    public bool IsDue(string url, DateTime now)
    {
        lock (gate)
            return CheckSchedule.IsDue(Find(url)?.CheckEvery, Find(url)?.LastChecked, now);
    }

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
            var entry = Find(url);
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
            File.WriteAllText(temporary, JsonSerializer.Serialize(new FileShape { BuiltIn = builtIn, Sources = entries }, options));
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
