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
// to: the game reads it as it reads any root. Writing it also writes each mod's copies of the game's entries
// (Copy/<faction>.json) into that mod's own folder (DataCopies).
internal static class DataAdditions
{
    public const string RootName = "nobirds.oldenera.data-additions";

    static readonly List<(string root, string path, JsonNode add)> additions = new();
    static readonly JsonDocumentOptions options = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };

    public static bool Any => additions.Count > 0 || DataCopies.Any;

    // None of the mods' additions and copies (tools/manager-tests, between cases).
    public static void Clear()
    {
        additions.Clear();
        DataCopies.Clear();
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

    // The archive of the files added to, in `folder`, and each mod's copies (written again each time, since the roots or
    // the additions changed): the game's roots (`gameFolders`) then ours (`roots`) are what the files are read from.
    public static bool Write(string folder, IEnumerable<string> gameFolders, IEnumerable<(string name, string folder)> roots)
    {
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var old in Directory.GetFiles(folder, "*.zip"))
                File.Delete(old);
            var sources = gameFolders.Select(f => ("game", f)).Concat(roots).ToList();
            // Each mod's copies go into its own folder (DataCopies.CopiesFile).
            DataCopies.Write(sources);
            if (additions.Count == 0)
                return false;
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
            return true;
        }
        catch (Exception e)
        {
            Game.Log?.Invoke($"DataAdditions: {e.GetType().Name}: {e.Message}");
            return false;
        }
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
            foreach (var zip in DataCopies.Archives(folder))
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

    internal static JsonNode Clone(JsonNode node) => JsonNode.Parse(node.ToJsonString());

    internal static JsonNode Parse(byte[] json, string what)
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
