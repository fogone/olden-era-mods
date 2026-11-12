using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Il2CppInterop.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Nobirds.OldenEra.Modding.Common;

// A mod's own assets (models, sounds..., #62) among the game's, by path. The game takes its prefabs and the like by
// path through its resource manager (Hex.ResManager, Game.GameResources): first in its table of the assets of the
// AssetBundles it loaded, then in Resources. A unit's map model, for one, is "units/" + the view's mapMesh. So a mod's
// bundle is given to the game's own loader, and what it holds is found by the game where it looks:
//
//   Bundles/<bundle>                 an AssetBundle built with the game's Unity version: loaded by the game's resource
//                                    manager (ResManager.Load) as its own would be. Its assets under Assets/Resources/
//                                    are found at their paths: Assets/Resources/units/me.myfaction/goblin_map.prefab
//                                    is the view's "mapMesh": "me.myfaction/goblin_map"
//   Models/<path>.json               a model of the game's kind made from one of a bundle's, found at <path>:
//                                    {"like": "<a prefab of the game>", "model": "<an asset of the mod's bundles>",
//                                     "scale": 1}. The game's prefab with its scripts and points (the squad helper, the
//                                    effects point, the quest marker), its animated model (the part with the Animator)
//                                    replaced by the mod's, as big as the one it replaces times "scale"; for a bundle
//                                    made without the game's scripts
//
// What the game makes of the prefab then (the squad on the map, its collider, materials, the scale from the view) it
// makes itself, as of its own. The bundles are loaded on the first frame, before any game is started.
public static class ModAssets
{
    sealed class Bundle
    {
        public string Root, Name, File, LoadedAs;
    }

    sealed class Model
    {
        public string Root, Path, Like, Source;
        public float Scale = 1;
    }

    static readonly List<Bundle> bundles = new();
    static readonly List<Model> models = new();
    static bool started;
    static GameObject holder;

    static string CacheFolder => System.IO.Path.Combine(BepInEx.Paths.CachePath, "oldenera-mod-assets");

    // A bundle of a mod's data root, from its archive: put into the cache folder when it is not there as it is.
    internal static void AddBundle(string root, string name, long length, DateTimeOffset written, Action<Stream> copyTo)
    {
        var file = System.IO.Path.Combine(CacheFolder, Safe(root), name);
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length != length || info.LastWriteTimeUtc != written.UtcDateTime)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
                using (var target = File.Create(file))
                    copyTo(target);
                File.SetLastWriteTimeUtc(file, written.UtcDateTime);
            }
        }
        catch (Exception e)
        {
            Game.Log?.Invoke($"ModAssets: {root}: bundle {name}: {e.GetType().Name}: {e.Message}");
            return;
        }
        bundles.RemoveAll(b => b.Root == root && b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        bundles.Add(new Bundle { Root = root, Name = name, File = file });
        Start();
    }

    // A model of a mod's data root (Models/<path>.json).
    internal static void AddModel(string root, string path, byte[] json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var e = document.RootElement;
            var model = new Model
            {
                Root = root,
                Path = path,
                Like = e.TryGetProperty("like", out var like) ? like.GetString() : null,
                Source = e.TryGetProperty("model", out var source) ? source.GetString() : null,
                Scale = e.TryGetProperty("scale", out var scale) && scale.TryGetSingle(out var s) ? s : 1,
            };
            if (string.IsNullOrEmpty(model.Like) || string.IsNullOrEmpty(model.Source))
            {
                Game.Log?.Invoke($"ModAssets: {root}: Models/{path}.json: \"like\" (a prefab of the game) and \"model\" (an asset of the mod's bundles) are needed");
                return;
            }
            models.RemoveAll(m => m.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
            models.Add(model);
            Start();
        }
        catch (Exception x) { Game.Log?.Invoke($"ModAssets: {root}: Models/{path}.json: {x.Message}"); }
    }

    static void Start()
    {
        if (started)
            return;
        started = true;
        Ticker.Add(typeof(ModAssets), Load);
    }

    // On the first frame: the bundles to the game's resource manager, then the models into its table.
    static void Load()
    {
        Ticker.RemoveOwner(typeof(ModAssets));
        started = false;
        var manager = GameNames.ResourceManager();
        if (manager == null)
        {
            Game.Log?.Invoke("ModAssets: the game's resource manager is not there");
            return;
        }
        // The game loads a bundle from StreamingAssets/bundles/<name>: ours by its path from there.
        var from = System.IO.Path.Combine(Application.streamingAssetsPath, "bundles");
        foreach (var bundle in bundles.Where(b => b.LoadedAs == null))
        {
            var name = System.IO.Path.GetRelativePath(from, bundle.File).Replace('\\', '/');
            var before = manager.Assets.Count;
            Hex.ResManager.Load(name);
            bundle.LoadedAs = name;
            Game.Log?.Invoke($"ModAssets: {bundle.Root}: bundle {bundle.Name}, {manager.Assets.Count - before} assets, {GameShaders(manager, name)} materials on the game's shaders");
        }
        foreach (var model in models)
            if (Compose(model) is { } prefab)
                Put(manager, model.Root, model.Path, prefab);
    }

    // A bundle carries a copy of each shader its materials use, made from what the bundle's author had (the game's
    // Hex/Lit as a stand-in of one pass: no fog of war, no shadows). The bundle's materials are put on the game's own
    // shader of that name.
    static int GameShaders(Hex.ResManager manager, string bundle)
    {
        var count = 0;
        var material = Il2CppType.Of<Material>();
        foreach (var entry in manager.Assets.Values)
        {
            if (entry.Key.Bundle != bundle || !entry.Key.AssetType.Equals(material) || entry.Asset.TryCast<Material>() is not { } m || m.shader == null)
                continue;
            if (Shader.Find(m.shader.name) is { } shader && shader != m.shader)
            {
                m.shader = shader;
                count++;
            }
        }
        return count;
    }

    // An asset of a mod's bundles in the game's table: by the end of its path in the bundle (no extension), of a type.
    static Object Find(Hex.ResManager manager, string root, string path, Il2CppSystem.Type type)
    {
        var names = bundles.Where(b => b.Root == root && b.LoadedAs != null).Select(b => b.LoadedAs).ToHashSet();
        path = path.Replace('\\', '/').ToLowerInvariant();
        foreach (var entry in manager.Assets.Values)
        {
            var key = entry.Key;
            if (names.Contains(key.Bundle) && key.AssetType.Equals(type) && (key.Path == path || key.Path.EndsWith("/" + path)))
                return entry.Asset;
        }
        return null;
    }

    // An asset into the game's table at a path of the game's (as from a bundle's Assets/Resources/<path>).
    static void Put(Hex.ResManager manager, string root, string path, Object asset)
    {
        var key = new GameResourceKey
        {
            Path = $"{Hex.ResManager.rootPath}/{path.Replace('\\', '/').ToLowerInvariant()}",
            Bundle = root,
            AssetType = asset.GetIl2CppType(),
        };
        manager.Assets[key] = new GameResourceEntry { Key = key, Asset = asset };
        Game.Log?.Invoke($"ModAssets: {root}: {path} made");
    }

    // The game's prefab `like`, copied, its animated model replaced by the mod's: a prefab kept under an inactive holder
    // (what the game makes of it is active, as from its own prefab).
    static GameObject Compose(Model model)
    {
        var manager = GameNames.ResourceManager();
        var gameObject = Il2CppType.Of<GameObject>();
        var like = Resources.Load(model.Like, gameObject)?.TryCast<GameObject>();
        if (like == null)
        {
            Game.Log?.Invoke($"ModAssets: {model.Root}: {model.Path}: no prefab {model.Like} in the game");
            return null;
        }
        if (Find(manager, model.Root, model.Source, gameObject)?.TryCast<GameObject>() is not { } source)
        {
            Game.Log?.Invoke($"ModAssets: {model.Root}: {model.Path}: no model {model.Source} in the mod's bundles");
            return null;
        }
        if (holder == null)
        {
            holder = new GameObject("OE_ModAssets");
            holder.SetActive(false);
            Object.DontDestroyOnLoad(holder);
        }
        var copy = Object.Instantiate(like, holder.transform, false);
        copy.name = System.IO.Path.GetFileName(model.Path);
        var old = copy.GetComponentInChildren<Animator>(true);
        if (old == null)
        {
            Game.Log?.Invoke($"ModAssets: {model.Root}: {model.Path}: {model.Like} has no animated model");
            return copy;
        }
        var place = old.transform;
        var replaced = Object.Instantiate(source, place.parent, false);
        replaced.name = place.name;
        replaced.transform.localPosition = place.localPosition;
        replaced.transform.localRotation = place.localRotation;
        // As big as the game's model it replaces (models are made in their own units: the game's troglodyte is 0.03
        // high, a model made in metres 1.7), then the mod's "scale".
        var size = Size(replaced.transform);
        var fit = size > 0 && Size(place) is var placeSize && placeSize > 0 ? placeSize / size : 1;
        replaced.transform.localScale = place.localScale * fit * model.Scale;
        replaced.transform.SetSiblingIndex(place.GetSiblingIndex());
        Object.DestroyImmediate(place.gameObject);
        return copy;
    }

    // The largest side of a model's meshes as they are made (their bounds in their own space, scaled as under the
    // model), in the model's units.
    static float Size(Transform model)
    {
        var size = 0f;
        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            var mesh = renderer.TryCast<SkinnedMeshRenderer>()?.sharedMesh ?? renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null)
                continue;
            var bounds = mesh.bounds.size;
            var scale = renderer.transform.lossyScale.y / model.lossyScale.y;
            size = Mathf.Max(size, Mathf.Max(bounds.x, Mathf.Max(bounds.y, bounds.z)) * Mathf.Abs(scale));
        }
        return size;
    }

    static string Safe(string name) => string.Concat(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
