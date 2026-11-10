using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Nobirds.OldenEra.Modding.Common;

// A mod's data as one of the game's own data roots (#56). The game keeps a list of mod folders (GameNames.DataRoots,
// only "core" = StreamingAssets of its own): it mounts each folder's *.zip, laid out as Core.zip (DB/..., campaigns/...),
// and loads the database root after root. An entry with a new id is added; one with an id already there replaces it
// whole; a single-object file (DB/data.json) is merged field by field. What the game does not read from a root (texts,
// pictures, lobby entries) is read by ModContent.
//
//   DataRoots.Add("me.oldenera.myfaction", @"...\data")   // before the game loads its database: at the chainloader
//
// The game reads the roots once, when it starts, so a root added later (or removed) counts from the next start.
public static class DataRoots
{
    static readonly List<(string name, string folder)> added = new();
    static Harmony harmony;

    // The roots added, in order.
    public static IReadOnlyList<(string name, string folder)> Added => added;

    // Adds a folder as a root after the game's and those added before; false when the game's list is not found or the
    // folder is not there. The same name twice is added once.
    public static bool Add(string name, string folder)
    {
        if (string.IsNullOrWhiteSpace(name) || added.Any(r => r.name == name))
            return false;
        if (!Directory.Exists(folder))
        {
            Game.Log?.Invoke($"DataRoots: {name}: no folder {folder}");
            return false;
        }
        if (GameTargets.DataRootsBuilders.Length == 0)
        {
            Game.Log?.Invoke($"DataRoots: the game's data roots not found, {name} not added");
            return false;
        }
        added.Add((name, Path.GetFullPath(folder).Replace('\\', '/')));
        // The game's list is usually built before the plugins load; it is built again (a new list) by its builders.
        if (harmony == null)
        {
            harmony = new Harmony("nobirds.oldenera.common.data-roots");
            harmony.PatchEach(GameTargets.DataRootsBuilders, postfix: new HarmonyMethod(typeof(DataRoots), nameof(BuiltPostfix)));
        }
        // What of its archives the game does not read itself: texts, pictures, lobby entries, additions.
        ModContent.Read(name, folder);
        // Always: it also removes the archives of copies a mod no longer has.
        additions = DataAdditions.Write(AdditionsFolder, GameFolders(), added);
        // A copied entry shows the original's texts under its id until the mod has its own.
        if (DataCopies.CopiedIds.Count > 0)
            Texts.CopyIdTexts(DataCopies.CopiedIds);
        Put();
        return true;
    }

    // The root of the additions to the game's single-object files (DataAdditions), after all the others.
    static bool additions;
    static string AdditionsFolder => Path.Combine(BepInEx.Paths.CachePath, "oldenera-data-additions").Replace('\\', '/');

    // The game's own roots' folders (its list, before ours; StreamingAssets while the list is not there).
    static IEnumerable<string> GameFolders()
    {
        var names = added.Select(r => r.name).Append(DataAdditions.RootName).ToHashSet();
        var own = GameNames.DataRoots?.Where(r => !names.Contains(r.Name)).Select(r => r.Folder).ToList();
        return own is { Count: > 0 } ? own : new List<string> { UnityEngine.Application.streamingAssetsPath };
    }

    static void BuiltPostfix() => Put();

    // Ours at the end of the game's list, in order, each once; the additions last.
    static void Put()
    {
        try
        {
            var roots = GameNames.DataRoots?.ToList();
            if (roots == null)
                return;
            var ours = added.ToList();
            if (additions)
                ours.Add((DataAdditions.RootName, AdditionsFolder));
            var names = ours.Select(r => r.name).ToList();
            if (roots.Skip(roots.Count - names.Count).Select(g => g.Name).SequenceEqual(names))
                return;
            var kept = roots.Where(g => !names.Contains(g.Name)).ToList();
            foreach (var (name, folder) in ours)
                kept.Add(roots.FirstOrDefault(g => g.Name == name) ??
                    // The class has no managed constructor: a bare object of it, its fields set.
                    new GameDataRoot(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<GameDataRoot>.NativeClassPtr))
                    {
                        Enabled = true,
                        Name = name,
                        Folder = folder,
                    });
            roots = kept;
            GameNames.DataRoots = new Il2CppReferenceArray<GameDataRoot>(roots.ToArray());
            Game.Log?.Invoke($"DataRoots: {string.Join(", ", roots.Select(r => $"{r.Name} = {r.Folder}"))}");
        }
        catch (Exception e) { Game.Log?.Invoke($"DataRoots: {e.GetType().Name}: {e.Message}"); }
    }
}
