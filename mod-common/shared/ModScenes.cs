using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using Hex.GameScenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Nobirds.OldenEra.Modding.Common;

// A mod's town scenes (#62). A session loads the game's city scenes with it and keeps each in the town's scene registry
// by its EScene (GameCityScenes.Registry); a city's "sceneName" is parsed to EScene and opening the town activates the
// registry's entry. The game names a scene by EScene's name (Enum.ToString), and a number is no scene name to Unity
// (it is a build index), so a mod's scene keeps its own name:
//
//   Bundles/<bundle>      a scene bundle (built from Assets/.../<scene>.unity): loaded by the game's resource manager
//                         like any of the mod's bundles (ModAssets)
//   a city's sceneName    the scene's name: "sceneName": "myfaction_city"
//
// The library gives each mod scene an EScene number of its own (from 0x100, in the order the mods are read), writes the
// number into the cities naming the scene (an entry of the data additions' root, DataAdditions), and when a session's
// town registry is there, loads the scene by its name (additive, its roots off, as the game's) and adds it to the
// registry under the number through the registry's own method. Nothing of the game is hooked.
public static class ModScenes
{
    sealed class Scene
    {
        public string Root, Name;
        public int Id;
        public AsyncOperation Loading;
    }

    const int First = 0x100;
    static readonly List<Scene> scenes = new();
    static bool ticking;

    public static bool Any => scenes.Count > 0;

    // A scene of a mod's bundle (ModAssets).
    internal static void Add(string root, string name)
    {
        if (scenes.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;
        scenes.Add(new Scene { Root = root, Name = name, Id = First + scenes.Count });
        Game.Log?.Invoke($"ModScenes: {root}: scene {name} is EScene {First + scenes.Count - 1}");
        if (!ticking)
        {
            ticking = true;
            Ticker.Add(typeof(ModScenes), Tick);
        }
    }

    internal static void Clear() => scenes.Clear();

    // The scene's number by its name (a city's sceneName), null for a name of no mod's scene.
    public static int? Id(string name) => scenes.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Id;

    // The cities of the mods' roots that name a mod's scene, with the number in place of the name: files under
    // DB/objects_logic/cities/ of the roots (their archives, their copies too), a later root's entry replacing.
    internal static IEnumerable<(string path, JsonObject file)> Cities(IEnumerable<(string name, string folder)> roots)
    {
        if (!Any)
            yield break;
        var entries = new Dictionary<string, (string path, JsonObject entry)>(StringComparer.Ordinal);
        foreach (var (name, folder) in roots.Where(r => r.name != "game" && Directory.Exists(r.folder)))
            foreach (var zip in Directory.GetFiles(folder, "*.zip").OrderBy(f => f, StringComparer.Ordinal))
            {
                using var archive = ZipFile.OpenRead(zip);
                foreach (var e in archive.Entries.Where(e => e.FullName.Replace('\\', '/').StartsWith("DB/objects_logic/cities/", StringComparison.OrdinalIgnoreCase)
                                                             && e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                {
                    using var stream = e.Open();
                    using var bytes = new MemoryStream();
                    stream.CopyTo(bytes);
                    if (DataAdditions.Parse(bytes.ToArray(), $"{name} {e.FullName}") is not JsonObject file || file["array"] is not JsonArray array)
                        continue;
                    foreach (var city in array.OfType<JsonObject>())
                        if ((string)city["id"] is { } id && (string)(city["sceneName"] as JsonValue) is { } scene && Id(scene) is { } number)
                        {
                            var copy = (JsonObject)DataAdditions.Clone(city);
                            copy["sceneName"] = number.ToString();
                            entries[id] = (e.FullName.Replace('\\', '/'), copy);
                        }
                }
            }
        foreach (var group in entries.Values.GroupBy(e => e.path, StringComparer.OrdinalIgnoreCase))
        {
            Game.Log?.Invoke($"ModScenes: {group.Key}: {string.Join(", ", group.Select(e => $"{e.entry["id"]} -> {e.entry["sceneName"]}"))}");
            yield return (group.Key, new JsonObject { ["array"] = new JsonArray(group.Select(e => (JsonNode)e.entry).ToArray()) });
        }
    }

    // Each frame: a session's town registry without a mod's scene gets it (loaded first when it is not).
    static void Tick()
    {
        var registry = GameNames.Session?.TryCast<GameMapGameSession>()?.Scenes?.City?.Registry;
        var known = registry?.Scenes;
        if (known == null || !known.ContainsKey(EScene.cityCore))
            return;
        foreach (var scene in scenes)
        {
            if (known.ContainsKey((EScene)scene.Id))
                continue;
            var unity = SceneManager.GetSceneByName(scene.Name);
            if (!unity.isLoaded)
            {
                if (scene.Loading == null)
                {
                    scene.Loading = SceneManager.LoadSceneAsync(scene.Name, LoadSceneMode.Additive);
                    if (scene.Loading == null)
                        Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name} could not be loaded (is its bundle there?)");
                }
                continue;
            }
            scene.Loading = null;
            // The game's entry, made as the game makes its own once a scene is loaded: chs(EScene, Scene), then it takes
            // the scene's roots and turns them off (chs.rat on 0.82.08).
            var entry = new GameSceneEntry((EScene)scene.Id, unity);
            entry.TakeRoots();
            registry.AddScenes(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameSceneEntry>(new[] { entry }));
            Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name} in the town registry as {scene.Id} ({entry.Roots?.Count ?? 0} roots)");
        }
    }
}
