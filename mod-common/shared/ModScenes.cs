using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json.Nodes;
using HarmonyLib;
using Hex.GameScenes;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
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
// registry under the number through the registry's own method. Closing a town, the game turns off its own town scenes by
// its list, a mod's scene with them (a hook on that method: a mod's scene cannot be on the list).
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
            WatchClosing();
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
        if (Reloading())
            return;
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
            Terrains(scene, unity);
            GameShaders(scene, unity);
            Prefabs(scene, unity);
            try { PostProcessing(scene, unity); }
            catch (Exception e) { Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name}: post-processing: {e.GetType().Name}: {e.Message}"); }
            // The game's entry, made as the game makes its own once a scene is loaded: chs(EScene, Scene), then it takes
            // the scene's roots and turns them off (chs.rat on 0.82.08).
            var entry = new GameSceneEntry((EScene)scene.Id, unity);
            entry.TakeRoots();
            registry.AddScenes(new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<GameSceneEntry>(new[] { entry }));
            Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name} in the town registry as {scene.Id} ({entry.Roots?.Count ?? 0} roots)");
        }
    }

    // A mod's scene made again from a new bundle file while the game runs (a modder's quick look, the research console's
    // "scene reload"): the scene out of the town registry and unloaded, its bundle unloaded, the file put in the cache in
    // place of the bundle's (the game's components named as in this game, as at the start), the bundle loaded; the next
    // ticks load the scene and give it to the registry as at the start. The town must be closed. Returns what it does.
    public static string Reload(string name, string file)
    {
        var scene = scenes.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (scene == null)
            return $"no mod scene {name}";
        if (!File.Exists(file))
            return $"no file {file}";
        var registry = GameNames.Session?.TryCast<GameMapGameSession>()?.Scenes?.City?.Registry;
        if (registry?.Scenes?.ContainsKey((EScene)scene.Id) == true)
        {
            registry.SetActive((EScene)scene.Id, false);
            registry.Scenes.Remove((EScene)scene.Id);
        }
        scene.Loading = null;
        reloading = (scene, file, SceneManager.GetSceneByName(scene.Name).isLoaded ? SceneManager.UnloadSceneAsync(scene.Name) : null);
        return $"reloading {scene.Name} from {file}";
    }

    static (Scene scene, string file, AsyncOperation unloading)? reloading;

    // The reload's steps once the scene is unloaded: the bundle out, the new file in, the bundle in.
    static bool Reloading()
    {
        if (reloading is not { } r)
            return false;
        if (r.unloading is { isDone: false })
            return true;
        reloading = null;
        try
        {
            foreach (var bundle in Resources.FindObjectsOfTypeAll<AssetBundle>())
                if (bundle.isStreamedSceneAssetBundle && bundle.GetAllScenePaths().Any(p => System.IO.Path.GetFileNameWithoutExtension(p).Equals(r.scene.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    var cached = ModAssets.CachedFile(r.scene.Root, bundle.name);
                    bundle.Unload(true);
                    if (BundleNames.Rename(r.file, cached + ".renamed", out _) > 0)
                        File.Copy(cached + ".renamed", cached, true);
                    else
                        File.Copy(r.file, cached, true);
                    File.Delete(cached + ".renamed");
                    AssetBundle.LoadFromFile(cached);
                    Game.Log?.Invoke($"ModScenes: {r.scene.Root}: scene {r.scene.Name} reloaded from {r.file}");
                    return false;
                }
            Game.Log?.Invoke($"ModScenes: {r.scene.Root}: no loaded bundle holds the scene {r.scene.Name}");
        }
        catch (Exception e) { Game.Log?.Invoke($"ModScenes: {r.scene.Root}: reload: {e.GetType().Name}: {e.Message}"); }
        return false;
    }

    // A mod scene's terrains (Unity's Terrain, as the game's towns have theirs; oe-bundle city: terrain) drawn as the
    // game's: the terrain's material is named after a material of the game (its terrain shader, Hex/TerrainUnity), which
    // is copied; the copy's textures (_Texture0..3, mixed by the terrain's splat map) are the layers' own (a mod's image)
    // or the game's textures the layers are named after, one per layer, repeated every layer's tile size. A bundle
    // carries neither the game's material nor its textures.
    static void Terrains(Scene scene, UnityEngine.SceneManagement.Scene unity)
    {
        foreach (var root in unity.GetRootGameObjects())
            foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                var data = terrain.terrainData;
                if (data == null)
                    continue;
                var ours = terrain.materialTemplate;
                if (ours != null && GameObjects<Material>(ours.name, m => m != ours && m.shader != null && m.shader.name.StartsWith("Hex/")) is { } game)
                {
                    var material = new Material(game) { name = ours.name };
                    var layers = data.terrainLayers;
                    for (var i = 0; i < layers.Length; i++)
                    {
                        if (layers[i] == null)
                            continue;
                        if ((layers[i].diffuseTexture ?? GameObjects<Texture2D>(layers[i].name, _ => true)) is { } texture)
                            material.SetTexture($"_Texture{i}", texture);
                        else
                            Game.Log?.Invoke($"ModScenes: {scene.Root}: terrain {terrain.name}: the game has no texture {layers[i].name}");
                        var tile = layers[i].tileSize;
                        material.SetTextureScale($"_Texture{i}", new Vector2(1 / tile.x, 1 / tile.y));
                    }
                    terrain.materialTemplate = material;
                }
                else if (ours != null)
                    Game.Log?.Invoke($"ModScenes: {scene.Root}: terrain {terrain.name}: the game has no material {ours.name}");
                Game.Log?.Invoke($"ModScenes: {scene.Root}: terrain {terrain.name} ({data.heightmapResolution}, {data.size}) with {terrain.materialTemplate?.name}");
            }
    }

    // A mod scene's places of prefabs taken by path (oe-bundle city: prefabs; a creature by its lair, from the game's
    // units or a mod's): an empty object named "OE_Prefab <path>" gets the prefab the game finds at that path (the
    // resource manager's table, then Resources), as its child where it stands, as big as the place is. A bundle cannot
    // carry the game's prefabs nor a mod's model made at load (ModAssets' Models).
    const string PrefabPlace = "OE_Prefab ";

    static void Prefabs(Scene scene, UnityEngine.SceneManagement.Scene unity)
    {
        foreach (var root in unity.GetRootGameObjects())
            foreach (var place in root.GetComponentsInChildren<Transform>(true))
            {
                if (!place.name.StartsWith(PrefabPlace, StringComparison.Ordinal) || place.childCount > 0)
                    continue;
                var path = place.name[PrefabPlace.Length..].Trim();
                if (ModAssets.Prefab(path) is not { } prefab)
                {
                    Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name}: no prefab {path} for {place.name}");
                    continue;
                }
                var made = UnityEngine.Object.Instantiate(prefab, place, false);
                made.SetActive(true);
            }
    }

    // A mod scene's town camera with the game's post-processing layer, as the game's town cameras have it: the town's
    // light, fog and effects come in global volumes of the scene (oe-bundle city: lighting; the game's
    // Hex.Render.LightingSettings among them), which only a camera with a layer renders. The layer needs the game's own
    // resources (its post-processing shaders), which a bundle cannot carry: it is made here with the resources and the
    // volume layers of a camera of the game's.
    static void PostProcessing(Scene scene, UnityEngine.SceneManagement.Scene unity)
    {
        PostProcessLayer game = null;
        foreach (var layer in Resources.FindObjectsOfTypeAll<PostProcessLayer>())
            // A camera that renders the volumes' layer 12, as the game's town and map cameras (the unit preview's do not).
            if (layer.gameObject.scene != unity && layer.m_Resources != null && (layer.volumeLayer.value & (1 << 12)) != 0)
            {
                game = layer;
                break;
            }
        foreach (var root in unity.GetRootGameObjects())
            foreach (var camera in root.GetComponentsInChildren<Camera>(true))
            {
                if (camera.tag != "MainCamera" || camera.GetComponent<PostProcessLayer>() != null)
                    continue;
                if (game == null)
                {
                    Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name}: no camera of the game's with post-processing to copy");
                    return;
                }
                var made = camera.gameObject.AddComponent<PostProcessLayer>();
                made.Init(game.m_Resources);
                made.volumeLayer = game.volumeLayer;
                made.volumeTrigger = camera.transform;
                made.antialiasingMode = game.antialiasingMode;
                made.stopNaNPropagation = game.stopNaNPropagation;
                Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name}: camera {camera.name} renders the post-processing volumes (layers {game.volumeLayer.value})");
            }
    }

    // A mod scene's materials drawn as the game's: a bundle carries a stand-in of each shader its materials use (made
    // from what the bundle's author had, or a template's renamed: oe-bundle model's "shader"). A material whose shader
    // the game has by that name is made a copy of a game material on that shader (its render states and settings the
    // game's: a stand-in's defaults are not; one with no glow of its own), with the mod material's main texture.
    static void GameShaders(Scene scene, UnityEngine.SceneManagement.Scene unity)
    {
        var made = new Dictionary<IntPtr, Material>();
        foreach (var root in unity.GetRootGameObjects())
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < materials.Length; i++)
                {
                    var ours = materials[i];
                    if (ours?.shader is not { } stand || Shader.Find(stand.name) is not { } shader || shader == stand)
                        continue;
                    if (!made.TryGetValue(ours.Pointer, out var copy))
                    {
                        var template = GameObjects<Material>(null, m => m.shader == shader && m != ours && (!m.HasProperty("_EmissionTex") || m.GetTexture("_EmissionTex") == null));
                        copy = template != null ? new Material(template) : new Material(shader);
                        copy.name = ours.name;
                        if (ours.HasProperty("_MainTex") && copy.HasProperty("_MainTex"))
                        {
                            copy.mainTexture = ours.mainTexture;
                            copy.mainTextureScale = ours.mainTextureScale;
                            copy.mainTextureOffset = ours.mainTextureOffset;
                        }
                        made[ours.Pointer] = copy;
                    }
                    materials[i] = copy;
                    changed = true;
                }
                if (changed)
                    renderer.sharedMaterials = materials;
            }
        if (made.Count > 0)
            Game.Log?.Invoke($"ModScenes: {scene.Root}: scene {scene.Name}: {made.Count} materials drawn as the game's");
    }

    // A loaded object of the game by its name (any name: null).
    static T GameObjects<T>(string name, Func<T, bool> fits) where T : UnityEngine.Object =>
        Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(o => (name == null || o.name == name) && fits(o));

    // Closing a town, the game turns off its town scenes but the ones kept, by its own list: the scenes a session loads by
    // their EScene names, which a mod's scene cannot be among. A mod's scene is turned off with them, in the same call: its
    // camera off before the map's is on (the game's renderer takes the camera turned on last), not a frame later.
    static Harmony harmony;

    static void WatchClosing()
    {
        if (harmony != null)
            return;
        harmony = new Harmony("nobirds.oldenera.common.mod-scenes");
        harmony.PatchEach(GameTargets.CityScenesOff, postfix: new HarmonyMethod(typeof(ModScenes), nameof(ClosedPostfix)));
    }

    static void ClosedPostfix(GameCityScenes __instance, Il2CppSystem.Collections.Generic.HashSet<EScene> __0)
    {
        var registry = __instance.Registry;
        foreach (var scene in scenes)
            if (__0?.Contains((EScene)scene.Id) != true && registry?.Scenes?.ContainsKey((EScene)scene.Id) == true)
                registry.SetActive((EScene)scene.Id, false);
    }
}
