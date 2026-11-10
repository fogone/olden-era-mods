using System;
using System.Collections.Generic;
using System.Linq;
using Hex.Storage;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using SpriteTable = Il2CppSystem.Collections.Generic.Dictionary<string, UnityEngine.Sprite>;

namespace Nobirds.OldenEra.Modding.Common;

// A mod's own pictures among the game's, by name (#56). The game keeps its icons in tables name -> sprite, one table per
// Resources folder (icons/units/hex_portraits, icons/heroes, icons/cities_buildings/..., GameNames.SpriteRegistry), and
// the places that show them look a name up there: a unit's portrait by the unit's id, a hero's icon by the name in its
// JSON. A sprite added under a folder goes into that folder's table and is shown wherever the game looks its name up.
//
//   Sprites.Add("icons/units/hex_portraits", Sprites.FromPng(bytes, "cove_angel"));
//
// A data mod needs no code for it: its archive's Sprites/<folder>/<name>.png are added so (ModContent). The table of a
// folder is the one that holds the game's own sprites of that folder; a folder the game fills no table from is said in
// the log. The game may fill its tables again: ours are put back.
public static class Sprites
{
    // What is to be in the tables: folder -> name -> sprite (or the PNG it is made from, on first use).
    static readonly Dictionary<string, Dictionary<string, Func<Sprite>>> wanted = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, Sprite> made = new();
    static readonly Dictionary<string, SpriteTable> tables = new(StringComparer.OrdinalIgnoreCase);
    static readonly HashSet<string> unknownFolders = new(StringComparer.OrdinalIgnoreCase);
    static bool started;
    static int nextCheck;

    // A sprite into the game's table of a Resources folder, under its name.
    public static void Add(string folder, Sprite sprite)
    {
        if (sprite == null)
            return;
        Want(folder, sprite.name, () => sprite);
    }

    // A PNG into the game's table of a Resources folder, under `name`; the sprite is made when the game's tables are
    // there.
    public static void AddPng(string folder, string name, byte[] png) =>
        Want(folder, name, () => FromPng(png, name));

    // A sprite made from the bytes of a PNG, the game's way (IconData, as the pictures of its campaigns); kept when the
    // game unloads unused assets.
    public static Sprite FromPng(byte[] png, string name)
    {
        // The class has no managed constructor: a bare object of it, its bytes set.
        var data = new IconData(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<IconData>.NativeClassPtr)) { bytes_ = new Il2CppStructArray<byte>(png) };
        var sprite = data.ToSprite();
        if (sprite == null)
            return null;
        sprite.name = name;
        sprite.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        if (sprite.texture != null)
            sprite.texture.hideFlags |= HideFlags.DontUnloadUnusedAsset;
        return sprite;
    }

    static void Want(string folder, string name, Func<Sprite> make)
    {
        folder = folder.Trim('/').Replace('\\', '/');
        if (!wanted.TryGetValue(folder, out var names))
            wanted[folder] = names = new Dictionary<string, Func<Sprite>>();
        names[name] = make;
        made.Remove(folder + "/" + name);
        Start();
    }

    static void Start()
    {
        nextCheck = 0;
        if (!started)
        {
            started = true;
            Ticker.Add(typeof(Sprites), Fill);
        }
    }

    // A sprite of ours by folder and name (made now if it was not), or null.
    public static Sprite Get(string folder, string name)
    {
        folder = folder.Trim('/').Replace('\\', '/');
        return wanted.TryGetValue(folder, out var names) && names.TryGetValue(name, out var make) ? Made(folder, name, make) : null;
    }

    static Sprite Made(string folder, string name, Func<Sprite> make)
    {
        var key = folder + "/" + name;
        if (made.TryGetValue(key, out var sprite) && sprite != null)
            return sprite;
        try { sprite = make(); }
        catch (Exception e) { Game.Log?.Invoke($"Sprites: {key}: {e.GetType().Name}: {e.Message}"); }
        if (sprite == null)
            Game.Log?.Invoke($"Sprites: {key}: not made");
        return made[key] = sprite;
    }

    // Every 60 frames: ours into the tables that miss them (the first time the tables are there, and when the game
    // fills one again).
    static void Fill()
    {
        if (Time.frameCount < nextCheck)
            return;
        nextCheck = Time.frameCount + 60;
        var registry = GameNames.SpriteRegistry;
        if (registry == null || !registry.Tables.Any(t => t.Count > 0))
            return;
        var put = 0;
        foreach (var (folder, names) in wanted)
        {
            if (TableOf(registry, folder) is not { } table)
                continue;
            foreach (var (name, make) in names)
                if (!table.ContainsKey(name) && Made(folder, name, make) is { } sprite)
                {
                    table[name] = sprite;
                    put++;
                }
        }
        put += FillCopies(registry);
        if (put > 0)
            Game.Log?.Invoke($"Sprites: {put} put into the game's tables");
    }

    // The pictures of copied entries (DataCopies): the game looks some pictures up by an entry's id (a unit's portrait
    // by the unit's id), so a copy shows the original's picture under its own id until the mod gives one: in every table
    // that has a picture named by an old id, the same picture under the new id. A table is looked at again when its
    // count changes (the game filled it, or we did).
    static readonly Dictionary<string, string> copiedIds = new(StringComparer.Ordinal);
    static readonly Dictionary<IntPtr, int> copiesFilled = new();

    public static void CopyIdSprites(IReadOnlyDictionary<string, string> renamed)
    {
        foreach (var (from, to) in renamed)
            copiedIds[from] = to;
        copiesFilled.Clear();
        Start();
    }

    static int FillCopies(GameSpriteRegistry registry)
    {
        if (copiedIds.Count == 0)
            return 0;
        var put = 0;
        foreach (var table in registry.Tables)
        {
            if (table == null || table.Count == 0 || copiesFilled.TryGetValue(table.Pointer, out var count) && count == table.Count)
                continue;
            foreach (var (from, to) in copiedIds)
                if (!table.ContainsKey(to) && table.TryGetValue(from, out var sprite) && sprite != null)
                {
                    table[to] = sprite;
                    put++;
                }
            copiesFilled[table.Pointer] = table.Count;
        }
        return put;
    }

    // The table the game fills from a folder: the one with the most of the folder's own sprites.
    static SpriteTable TableOf(GameSpriteRegistry registry, string folder)
    {
        if (tables.TryGetValue(folder, out var known) && known != null && registry.Tables.Any(t => t.Pointer == known.Pointer))
            return known;
        if (unknownFolders.Contains(folder))
            return null;
        var names = new HashSet<string>(Resources.LoadAll<Sprite>(folder).Select(s => s.name));
        var best = names.Count == 0 ? null : registry.Tables.Select(t => ((SpriteTable table, int count)?)(t, names.Count(t.ContainsKey))).OrderByDescending(t => t.Value.count).First();
        if (best is not { count: > 0 })
        {
            if (unknownFolders.Add(folder))
                Game.Log?.Invoke($"Sprites: the game fills no table from {folder}: its sprites are not shown");
            return null;
        }
        Game.Log?.Invoke($"Sprites: {folder}: the table with {best.Value.table.Count} sprites");
        return tables[folder] = best.Value.table;
    }
}
