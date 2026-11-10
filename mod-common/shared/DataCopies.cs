using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Nobirds.OldenEra.Modding.Common;

// A new faction's parts copied from a faction of the game (#56). A faction is many entries in many files (its units,
// heroes and their specializations, city, dwellings, laws, faction skill and its tables, sub-classes, about 650 map
// squads the map generator looks for by the city's faction), and their ids are not all made from the faction's id
// (skill_faction_humans, humans_might_skills_table). A data mod names the files to copy and the library writes the
// copies into an archive of its own data folder (CopiesFile), written with the additions (DataAdditions):
//
//   Copy/cove.json   {"like": "human", "words": {"humans": "cove"},
//                     "files": ["DB/units/units_logics/humans/", "DB/heroes/humans/", "DB/squads/squads_human/", ...],
//                     "names": {"human_name": "cove_name"}}
//
// From each file (a folder: every file under it), an id-keyed file {"array": [{"id": ...}, ...]}, the entries of the
// faction are copied: those whose "fraction" (or "faction") is `like` or whose id has a word of `words` (`like`
// itself always one, words being the parts between "_"), and the entries with the id of one of those (a unit's view
// has the unit's id). A copied entry's id gets the words replaced
// (human_hero_1 -> cove_hero_1); an id without such a word gets the faction's id in front (esquire -> cove_esquire).
// In the copies every string that is the id of a copied entry becomes the copy's id, `like` becomes the faction's id,
// and a string of `names` becomes its value; other strings (text keys, pictures, ids not copied) stay, so a copy shows
// the game's texts and pictures until the mod gives its own. An entry the mod's own data has (the same id) is not
// copied: the mod's wins. The file's path gets the words replaced too (DB/squads/squads_human/... ->
// DB/squads/squads_cove/...); a path with no such word gets the faction's id at the end of the name.
internal static class DataCopies
{
    sealed record Copy(string Root, string Id, string Like, IReadOnlyList<string> Files,
        IReadOnlyDictionary<string, string> Words, IReadOnlyDictionary<string, string> Names);

    static readonly List<Copy> copies = new();
    static readonly Regex word = new("[A-Za-z0-9]+", RegexOptions.Compiled);

    public static bool Any => copies.Count > 0;

    // The ids of the copies, old -> new, as last written (their texts: Texts.CopyIdTexts).
    public static IReadOnlyDictionary<string, string> CopiedIds => renamedAll;
    static readonly Dictionary<string, string> renamedAll = new(StringComparer.Ordinal);

    public static void Clear()
    {
        copies.Clear();
        renamedAll.Clear();
    }

    // A mod's Copy/<id>.json (ModContent).
    public static void Add(string root, string id, byte[] json)
    {
        var what = $"{root} Copy/{id}.json";
        if (DataAdditions.Parse(json, what) is not JsonObject spec || (string)spec["like"] is not { Length: > 0 } like)
        {
            Game.Log?.Invoke($"DataCopies: {what}: no \"like\" (the faction it copies)");
            return;
        }
        var files = (spec["files"] as JsonArray)?.Select(f => (string)f).Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f.Replace('\\', '/')).ToList();
        if (files == null || files.Count == 0)
        {
            Game.Log?.Invoke($"DataCopies: {what}: no \"files\" (the game's files and folders to copy from)");
            return;
        }
        var words = Map(spec["words"]);
        words[like] = id;
        copies.RemoveAll(c => c.Root == root && c.Id == id);
        copies.Add(new Copy(root, id, like, files, words, Map(spec["names"])));
    }

    static Dictionary<string, string> Map(JsonNode node)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (node is JsonObject obj)
            foreach (var (from, to) in obj)
                if ((string)to is { Length: > 0 } value)
                    map[from] = value;
        return map;
    }

    // The archive of a mod's copies, in its own data folder: the game takes some of a faction's entries only from the
    // data root that has the faction (a random hero is one of those), so the copies are of the mod's root. The library
    // reads a root's archives without it (Archives).
    public const string CopiesFile = "~oe-copies.zip";

    // A data root's archives as the library reads them: in name order, the copies made from it not among them.
    public static IEnumerable<string> Archives(string folder) =>
        Directory.GetFiles(folder, "*.zip").Where(f => !Path.GetFileName(f).Equals(CopiesFile, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);

    // Each mod's copies into its folder's CopiesFile; `sources` the roots in order (the game's first, named "game").
    public static void Write(List<(string name, string folder)> sources)
    {
        renamedAll.Clear();
        foreach (var (name, folder) in sources.Where(s => s.name != "game" && Directory.Exists(s.folder)))
        {
            var file = Path.Combine(folder, CopiesFile);
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
                var mine = copies.Where(c => c.Root == name).ToList();
                if (mine.Count == 0)
                    continue;
                using var archive = ZipFile.Open(file, ZipArchiveMode.Create);
                foreach (var copy in mine)
                    try { Write(archive, copy, sources); }
                    catch (Exception e) { Game.Log?.Invoke($"DataCopies: {copy.Root}: Copy/{copy.Id}.json: {e.GetType().Name}: {e.Message}"); }
            }
            catch (Exception e) { Game.Log?.Invoke($"DataCopies: {name}: {file}: {e.GetType().Name}: {e.Message}"); }
        }
    }

    static void Write(ZipArchive archive, Copy copy, List<(string name, string folder)> sources)
    {
        // The roots before the mod's own and the mod's: what is copied from, and what the mod has itself.
        var own = sources.FindIndex(s => s.name == copy.Root);
        var from = own < 0 ? sources : sources.Take(own).ToList();
        var files = Read(from, path => copy.Files.Any(f => f.EndsWith("/") ? path.StartsWith(f, StringComparison.OrdinalIgnoreCase) : path.Equals(f, StringComparison.OrdinalIgnoreCase)));
        foreach (var f in copy.Files.Where(f => !files.Keys.Any(p => f.EndsWith("/") ? p.StartsWith(f, StringComparison.OrdinalIgnoreCase) : p.Equals(f, StringComparison.OrdinalIgnoreCase))))
            Game.Log?.Invoke($"DataCopies: {copy.Root}: Copy/{copy.Id}.json: no game file {f}");
        var mine = own < 0 ? new HashSet<string>() : Ids(Read(new List<(string, string)> { sources[own] }, path => path.StartsWith("DB/", StringComparison.OrdinalIgnoreCase)).Values);

        // First those of the faction, then those with the id of one of them (a unit's view has the unit's id).
        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in files.Values.SelectMany(Entries))
            if ((string)entry["id"] is { Length: > 0 } id && (Of(entry, copy.Like) || Words(id, copy) != id))
                renamed[id] = renamedAll[id] = NewId(id, copy);
        var entries = new List<(string path, List<JsonObject> copied)>();
        foreach (var (path, file) in files)
        {
            // The mod's own entry of that id is referred to as a copy would be, and not copied.
            var copied = Entries(file).Where(e => (string)e["id"] is { } id && renamed.TryGetValue(id, out var newId) && !mine.Contains(newId)).ToList();
            if (copied.Count > 0)
                entries.Add((path, copied));
        }

        int written = 0;
        foreach (var (path, copied) in entries)
        {
            var array = new JsonArray(copied.Select(e => Renamed(DataAdditions.Clone(e), null, copy, renamed)).ToArray());
            var name = Words(path, copy);
            if (name == path)
                name = path.Substring(0, path.Length - ".json".Length) + "_" + copy.Id + ".json";
            using var stream = archive.CreateEntry(name).Open();
            var json = Encoding.UTF8.GetBytes(new JsonObject { ["array"] = array }.ToJsonString());
            stream.Write(json, 0, json.Length);
            written += copied.Count;
        }
        var kept = renamed.Values.Count(mine.Contains);
        Game.Log?.Invoke($"DataCopies: {copy.Root}: {written} entries of {copy.Id} copied from those of {copy.Like} in {entries.Count} files" +
            (kept > 0 ? $", {kept} of the mod's own kept" : ""));
    }

    // An entry of the faction: its "fraction" (or "faction") is it.
    static bool Of(JsonObject entry, string faction) =>
        (string)(entry["fraction"] as JsonValue) == faction || (string)(entry["faction"] as JsonValue) == faction;

    static string NewId(string id, Copy copy) =>
        copy.Names.TryGetValue(id, out var name) ? name : Words(id, copy) is var w && w != id ? w : copy.Id + "_" + id;

    // The words of `text` (the parts between "_", "/" and ".") replaced.
    static string Words(string text, Copy copy) =>
        word.Replace(text, m => copy.Words.TryGetValue(m.Value, out var to) ? to : m.Value);

    static JsonNode Renamed(JsonNode node, string key, Copy copy, Dictionary<string, string> renamed)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Select(p => p.Key).ToList())
                    if (Renamed(obj[name], name, copy, renamed) is var value && value != obj[name])
                        obj[name] = value;
                return obj;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                    if (Renamed(array[i], key, copy, renamed) is var value && value != array[i])
                        array[i] = value;
                return array;
            case JsonValue value when value.TryGetValue<string>(out var text):
                if (copy.Names.TryGetValue(text, out var given))
                    return JsonValue.Create(given);
                if (renamed.TryGetValue(text, out var id))
                    return JsonValue.Create(id);
                if (text == copy.Like)
                    return JsonValue.Create(copy.Id);
                return value;
            default:
                return node;
        }
    }

    // The entries of an id-keyed file.
    static IEnumerable<JsonObject> Entries(JsonObject file) => (file["array"] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

    // The ids of entries in the files.
    static HashSet<string> Ids(IEnumerable<JsonObject> files) =>
        files.SelectMany(Entries).Select(e => (string)(e["id"] as JsonValue)).Where(id => id != null).ToHashSet();

    // The JSON files of the roots that `take` takes, a later root's (or archive's) replacing an earlier one's.
    static SortedDictionary<string, JsonObject> Read(List<(string name, string folder)> roots, Func<string, bool> take)
    {
        var files = new SortedDictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, folder) in roots.Where(r => Directory.Exists(r.folder)))
            foreach (var zip in Archives(folder))
            {
                using var archive = ZipFile.OpenRead(zip);
                foreach (var entry in archive.Entries)
                {
                    var path = entry.FullName.Replace('\\', '/');
                    if (entry.Name.Length == 0 || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || !take(path))
                        continue;
                    using var stream = entry.Open();
                    using var bytes = new MemoryStream();
                    stream.CopyTo(bytes);
                    if (DataAdditions.Parse(bytes.ToArray(), $"{name} {path}") is JsonObject file)
                        files[path] = file;
                }
            }
        return files;
    }
}
