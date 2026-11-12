using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Hex.UI;
using UnityEngine;

namespace Nobirds.OldenEra.Modding.Common;

// What of a data mod's archives the game does not read itself (#56), read from a data root (DataRoots.Add) the moment
// it is added:
//
//   Lang/<language>/**/*.json        the mod's texts, in the game's format ({"tokens":[{"sid","text"}]}); the game reads
//                                    its own Lang from a fixed place, these go into its table through Texts
//   Sprites/<folder>/<name>.png      a picture into the game's table of that Resources folder, under <name> (Sprites)
//   Lobby/<id>.json                  a faction in the lobby: {"like": "<faction>"}, its pictures those of that faction
//   Lobby/<id>/<picture>.png         ... except these (icon, slotFon, statisticFon, versusFon, bigIcon, card; Factions)
//   Add/<path>.json                  what it adds to the game's single-object file <path> (DB/data.json): DataAdditions
//   Copy/<id>.json                   the faction's parts copied from another's: {"like", "files", "words", "names"} (DataCopies)
//   Bundles/<bundle>                 an AssetBundle, loaded by the game's resource manager (ModAssets)
//   Models/<path>.json               a model of the game's kind at <path>, made from a bundle's: {"like", "model", "scale"} (ModAssets)
//
// The rest (DB/..., campaigns/...) the game reads itself. Paths are matched without regard to case; the archives in
// the order of their names, a later one's file of the same path replacing an earlier one's.
internal static class ModContent
{
    public static void Read(string root, string folder)
    {
        var files = new Dictionary<string, Func<byte[]>>(StringComparer.OrdinalIgnoreCase);
        var bundles = new Dictionary<string, (string zip, string entry, long length, DateTimeOffset written)>(StringComparer.OrdinalIgnoreCase);
        foreach (var zip in DataCopies.Archives(folder))
            try
            {
                using var archive = ZipFile.OpenRead(zip);
                foreach (var entry in archive.Entries.Where(e => e.Name.Length > 0))
                {
                    var path = entry.FullName.Replace('\\', '/');
                    if (path.StartsWith("Bundles/", StringComparison.OrdinalIgnoreCase) && path.Count(c => c == '/') == 1)
                        bundles[path.Substring(8)] = (zip, entry.FullName, entry.Length, entry.LastWriteTime);
                    else if (IsOurs(path))
                        files[path] = Bytes(zip, entry.FullName);
                }
            }
            catch (Exception e) { Game.Log?.Invoke($"ModContent: {root}: {Path.GetFileName(zip)}: {e.GetType().Name}: {e.Message}"); }
        foreach (var (name, (zip, entry, length, written)) in bundles)
            ModAssets.AddBundle(root, name, length, written, target =>
            {
                using var archive = ZipFile.OpenRead(zip);
                using var stream = archive.GetEntry(entry)!.Open();
                stream.CopyTo(target);
            });
        if (files.Count == 0 && bundles.Count == 0)
            return;
        int texts = 0, sprites = 0, lobbies = 0, added = 0, copies = 0, models = 0;
        foreach (var (path, bytes) in files.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
        {
            var parts = path.Split('/');
            var top = parts[0].ToLowerInvariant();
            if (top == "lang" && parts.Length >= 3 && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                if (!Enum.TryParse<ELang>(parts[1], true, out var language))
                {
                    if (!parts[1].Equals("args", StringComparison.OrdinalIgnoreCase))
                        Game.Log?.Invoke($"ModContent: {root}: {path}: no such language of the game");
                    continue;
                }
                using var stream = new MemoryStream(bytes());
                Texts.Load(language, stream, $"{root} {path}");
                texts++;
            }
            else if (top == "sprites" && parts.Length >= 3 && path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                var spriteFolder = string.Join("/", parts.Skip(1).Take(parts.Length - 2));
                Sprites.AddPng(spriteFolder, Path.GetFileNameWithoutExtension(parts[^1]), bytes());
                sprites++;
            }
            else if (top == "lobby" && parts.Length == 2 && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                var id = Path.GetFileNameWithoutExtension(parts[1]);
                if (Like(root, path, bytes()) is not { } like)
                    continue;
                var pictures = new Dictionary<string, Func<Sprite>>();
                foreach (var name in Factions.Pictures)
                    if (files.TryGetValue($"{parts[0]}/{id}/{name}.png", out var picture))
                    {
                        var png = picture();
                        var spriteName = $"{id}_lobby_{name}";
                        pictures[name] = () => Sprites.FromPng(png, spriteName);
                    }
                Factions.AddLobby(id, like, pictures);
                lobbies++;
            }
            else if (top == "add" && parts.Length >= 2 && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                DataAdditions.Add(root, path.Substring(parts[0].Length + 1), bytes());
                added++;
            }
            else if (top == "copy" && parts.Length == 2 && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                DataCopies.Add(root, Path.GetFileNameWithoutExtension(parts[1]), bytes());
                copies++;
            }
            else if (top == "models" && parts.Length >= 2 && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                ModAssets.AddModel(root, path.Substring(parts[0].Length + 1, path.Length - parts[0].Length - 1 - ".json".Length), bytes());
                models++;
            }
        }
        Game.Log?.Invoke($"ModContent: {root}: {texts} text files, {sprites} sprites, {lobbies} lobby factions, {added} additions, {copies} copies, {bundles.Count} bundles, {models} models");
    }

    static bool IsOurs(string path) =>
        new[] { "Lang/", "Sprites/", "Lobby/", "Add/", "Copy/", "Models/" }.Any(top => path.StartsWith(top, StringComparison.OrdinalIgnoreCase));

    // An entry's bytes, read when asked (the archive opened again then).
    static Func<byte[]> Bytes(string zip, string entryName) => () =>
    {
        using var archive = ZipFile.OpenRead(zip);
        using var stream = archive.GetEntry(entryName)!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    };

    static string Like(string root, string path, byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.TryGetProperty("like", out var like) && like.GetString() is { Length: > 0 } faction)
                return faction;
            Game.Log?.Invoke($"ModContent: {root}: {path}: no \"like\" (the faction whose pictures it starts from)");
        }
        catch (Exception e) { Game.Log?.Invoke($"ModContent: {root}: {path}: {e.Message}"); }
        return null;
    }
}
