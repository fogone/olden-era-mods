using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Nobirds.OldenEra.Modding.Common;

// Additions to the game's single-object files (#56). The game merges a single-object file (DB/data.json, the
// key/value lists such as DB/city_music_sounds.json) root after root field by field, but an array is replaced whole:
// a mod that writes DB/data.json's "fractions" to add its faction drops another mod's. So a data mod writes only what
// it adds, under Add/ with the file's path:
//
//   Add/DB/data.json                {"fractions": ["cove"], "availableFractions": ["cove"]}
//   Add/DB/city_music_sounds.json   {"data": [{"key": "cove", "val": {"soundSetName": "sounds/city/music/human"}}]}
//
// An object is added to field by field; an array gets the elements added at its end, an element already there (the
// same value, or an object with the same "key", "sid" or "id") replaced in place. The file as the game would have
// it (the game's own, then each data root's, merged as the game merges them) with every mod's additions, in the
// order of the roots, goes into one archive of a data root of ours after all the others, holding the fields added
// to: the game reads it as it reads any root. The same archive holds the data made for a mod from the game's: a new
// faction's map squads (Squads/<id>.json, AddSquads).
internal static class DataAdditions
{
    public const string RootName = "nobirds.oldenera.data-additions";

    static readonly List<(string root, string path, JsonNode add)> additions = new();
    static readonly JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    // A new faction's map squads made from those of a faction of the game: the faction, what it is like, its units for
    // that faction's.
    sealed record Squads(string Root, string Id, string Like, IReadOnlyDictionary<string, string> Units);
    static readonly List<Squads> squads = new();

    public static bool Any => additions.Count > 0 || squads.Count > 0;

    // None of the mods' additions (tools/manager-tests, between cases).
    public static void Clear()
    {
        additions.Clear();
        squads.Clear();
    }

    // A mod's Squads/<id>.json (ModContent): {"like": "human", "units": {"esquire": "myfaction_esquire", ...}}. The
    // map's squads of a faction are files of their own (DB/squads/squads_human/..., about 650: the guards of its
    // dwellings and the squads near its cities, by tier and value; the map generator looks for those of the city's
    // faction); the new faction gets a copy of each: its id squad_<id>_... for squad_<like>_..., its "fraction" the
    // new one, each unit of `units` replaced.
    public static void AddSquads(string root, string id, byte[] json)
    {
        if (Parse(json, $"{root} Squads/{id}.json") is not JsonObject spec || (string)spec["like"] is not { Length: > 0 } like)
        {
            Game.Log?.Invoke($"DataAdditions: {root}: Squads/{id}.json: no \"like\" (the faction whose squads it copies)");
            return;
        }
        var units = new Dictionary<string, string>();
        if (spec["units"] is JsonObject map)
            foreach (var (from, to) in map)
                if ((string)to is { Length: > 0 } unit)
                    units[from] = unit;
        squads.RemoveAll(s => s.Id == id);
        squads.Add(new Squads(root, id, like, units));
    }

    // A mod's Add/<path> file (ModContent): what it adds to the game's <path>.
    public static void Add(string root, string path, byte[] json)
    {
        if (Parse(json, $"{root} Add/{path}") is not JsonObject add)
        {
            Game.Log?.Invoke($"DataAdditions: {root}: Add/{path}: not a JSON object");
            return;
        }
        additions.Add((root, path, add));
    }

    // The archive of the files added to, in `folder` (written again each time, since the roots or the additions
    // changed): the game's roots (`gameFolders`) then ours (`roots`) are what the files are read from.
    public static bool Write(string folder, IEnumerable<string> gameFolders, IEnumerable<(string name, string folder)> roots)
    {
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "*.zip"))
                File.Delete(old);
            if (!Any)
                return false;
            var sources = gameFolders.Select(f => ("game", f)).Concat(roots).ToList();
            var zip = Path.Combine(folder, "additions.zip");
            using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
            foreach (var group in additions.GroupBy(a => a.path, StringComparer.OrdinalIgnoreCase))
            {
                var file = Merged(group.Key, sources);
                var written = new JsonObject();
                foreach (var (root, _, add) in group)
                    foreach (var (key, value) in (JsonObject)add)
                    {
                        file[key] = Added(file[key] is { } now ? Clone(now) : null, value);
                        written[key] = Clone(file[key]);
                    }
                using var stream = archive.CreateEntry(group.Key).Open();
                var bytes = Encoding.UTF8.GetBytes(written.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                stream.Write(bytes, 0, bytes.Length);
                Game.Log?.Invoke($"DataAdditions: {group.Key}: {string.Join(", ", written.Select(f => f.Key))} from {string.Join(", ", group.Select(a => a.root).Distinct())}");
            }
            foreach (var faction in squads)
                CopySquads(archive, faction, sources);
            return true;
        }
        catch (Exception e)
        {
            Game.Log?.Invoke($"DataAdditions: {e.GetType().Name}: {e.Message}");
            return false;
        }
    }

    static void CopySquads(ZipArchive archive, Squads faction, List<(string name, string folder)> sources)
    {
        var from = $"DB/squads/squads_{faction.Like}/";
        var files = new SortedDictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, folder) in sources.Where(s => Directory.Exists(s.folder)))
            foreach (var zip in Directory.GetFiles(folder, "*.zip").OrderBy(f => f, StringComparer.Ordinal))
            {
                using var source = ZipFile.OpenRead(zip);
                foreach (var entry in source.Entries.Where(e => e.Name.Length > 0 && e.FullName.Replace('\\', '/').StartsWith(from, StringComparison.OrdinalIgnoreCase)))
                {
                    using var stream = entry.Open();
                    using var copy = new MemoryStream();
                    stream.CopyTo(copy);
                    files[entry.FullName.Replace('\\', '/')] = copy.ToArray();
                }
            }
        if (files.Count == 0)
        {
            Game.Log?.Invoke($"DataAdditions: {faction.Root}: Squads/{faction.Id}.json: no squads of '{faction.Like}' ({from})");
            return;
        }
        string oldPrefix = $"squad_{faction.Like}_", newPrefix = $"squad_{faction.Id}_";
        int written = 0, units = 0;
        foreach (var (path, bytes) in files)
        {
            if (Parse(bytes, path) is not { } squad)
                continue;
            JsonNode Renamed(JsonNode node, string key)
            {
                switch (node)
                {
                    case JsonObject obj:
                        foreach (var name in obj.Select(p => p.Key).ToList())
                            if (Renamed(obj[name], name) is var renamed && renamed != obj[name])
                                obj[name] = renamed;
                        return obj;
                    case JsonArray array:
                        for (var i = 0; i < array.Count; i++)
                            if (Renamed(array[i], key) is var renamed && renamed != array[i])
                                array[i] = renamed;
                        return array;
                    case JsonValue value when value.TryGetValue<string>(out var text):
                        if (text.StartsWith(oldPrefix, StringComparison.Ordinal))
                            return JsonValue.Create(newPrefix + text.Substring(oldPrefix.Length));
                        if (key == "fraction" && text == faction.Like)
                            return JsonValue.Create(faction.Id);
                        if (faction.Units.TryGetValue(text, out var unit))
                        {
                            units++;
                            return JsonValue.Create(unit);
                        }
                        return value;
                    default:
                        return node;
                }
            }
            var copy = Renamed(Clone(squad), null);
            var name = $"DB/squads/squads_{faction.Id}/" + path.Substring(from.Length).Replace(oldPrefix, newPrefix);
            using var stream = archive.CreateEntry(name).Open();
            var json = Encoding.UTF8.GetBytes(copy.ToJsonString());
            stream.Write(json, 0, json.Length);
            written++;
        }
        Game.Log?.Invoke($"DataAdditions: {faction.Root}: {written} squads of {faction.Id} made from those of {faction.Like} ({units} units replaced)");
    }

    // The file as the roots give it: each root's own (its archives in name order, a later one's replacing) merged
    // over the ones before.
    static JsonObject Merged(string path, IEnumerable<(string name, string folder)> sources)
    {
        var file = new JsonObject();
        foreach (var (name, folder) in sources)
        {
            if (!Directory.Exists(folder))
                continue;
            JsonObject own = null;
            foreach (var zip in Directory.GetFiles(folder, "*.zip").OrderBy(f => f, StringComparer.Ordinal))
            {
                using var archive = ZipFile.OpenRead(zip);
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').Equals(path, StringComparison.OrdinalIgnoreCase));
                if (entry == null)
                    continue;
                using var stream = entry.Open();
                using var copy = new MemoryStream();
                stream.CopyTo(copy);
                if (Parse(copy.ToArray(), $"{name} {path}") is JsonObject read)
                    own = read;
            }
            if (own != null)
                Overwrite(file, own);
        }
        return file;
    }

    // The game's merge of a root's file: a field given replaces the field, an object field merged into.
    static void Overwrite(JsonObject into, JsonObject from)
    {
        foreach (var (key, value) in from.ToList())
            if (value is JsonObject obj && into[key] is JsonObject there)
                Overwrite(there, obj);
            else
                into[key] = value == null ? null : Clone(value);
    }

    // `now` with `add` added: objects field by field, arrays by element.
    static JsonNode Added(JsonNode now, JsonNode add)
    {
        switch (add)
        {
            case JsonObject obj when now is JsonObject there:
                foreach (var (key, value) in obj)
                    there[key] = Added(there[key] is { } v ? Clone(v) : null, value);
                return there;
            case JsonArray array when now is JsonArray there:
                foreach (var element in array)
                {
                    var index = there.Select((e, i) => (e, i)).FirstOrDefault(p => Same(p.e, element)) is { e: not null } found ? found.i : -1;
                    var copy = element == null ? null : Clone(element);
                    if (index >= 0)
                        there[index] = copy;
                    else
                        there.Add(copy);
                }
                return there;
            default:
                return add == null ? null : Clone(add);
        }
    }

    // The same element: the same value, or objects with the same "key", "sid" or "id".
    static bool Same(JsonNode a, JsonNode b)
    {
        if (a is JsonObject x && b is JsonObject y)
            return new[] { "key", "sid", "id" }.Any(k => x[k] is JsonValue p && y[k] is JsonValue q && p.ToJsonString() == q.ToJsonString());
        return a is JsonValue && b is JsonValue && a.ToJsonString() == b.ToJsonString();
    }

    static JsonNode Clone(JsonNode node) => JsonNode.Parse(node.ToJsonString());

    static JsonNode Parse(byte[] json, string what)
    {
        try
        {
            var span = json.AsSpan();
            if (span.Length >= 3 && span[0] == 0xEF && span[1] == 0xBB && span[2] == 0xBF)
                span = span[3..];
            return JsonNode.Parse(span, documentOptions: options);
        }
        catch (Exception e)
        {
            Game.Log?.Invoke($"DataAdditions: {what}: {e.Message}");
            return null;
        }
    }
}
