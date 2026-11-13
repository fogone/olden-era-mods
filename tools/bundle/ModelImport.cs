using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Nobirds.OldenEra.Modding.Bundle;

// Prefabs of another bundle made objects of a scene: what a model bundle built in Unity holds (meshes, materials,
// textures, shaders, particle systems, animators), put where a scene bundle keeps it. A scene bundle has two files: the
// scene's (its GameObjects and their components) and its shared assets (what they draw with), so a prefab's GameObjects
// and components go into the first and the assets they refer to into the second, the references between them made
// references between the files. Both bundles must be of the game's Unity version: the objects are copied as their type
// trees read them. A texture's pixels kept in the bundle's resource file (.resS) are put into the texture itself.
sealed class ModelImport
{
    readonly AssetsManager am;
    readonly AssetsFileInstance scene, shared;
    readonly int sharedFileId;

    public ModelImport(AssetsManager am, AssetsFileInstance scene, AssetsFileInstance shared, int sharedFileId)
    {
        this.am = am;
        this.scene = scene;
        this.shared = shared;
        this.sharedFileId = sharedFileId;
    }

    // The prefabs (all of the bundle's when none are named) put into the scene at a place. The shared assets imported
    // are returned, for the scene to preload.
    public List<long> Add(string bundlePath, string[] names, float[] position, float[] rotation)
    {
        var bundle = am.LoadBundleFile(bundlePath, true);
        var directory = bundle.file.BlockAndDirInfo.DirectoryInfos;
        var index = Enumerable.Range(0, directory.Count).First(i => bundle.file.IsAssetsFile(i));
        var source = am.LoadAssetsFileFromBundle(bundle, index, false);
        var resources = Enumerable.Range(0, directory.Count).Where(i => directory[i].Name.EndsWith(".resS"))
            .ToDictionary(i => directory[i].Name, i => Read(bundle.file, i));
        if (source.file.Metadata.Externals.Count > 0)
            throw new Exception($"{bundlePath}: refers to other files ({string.Join(", ", source.file.Metadata.Externals.Select(e => e.PathName))}): only a bundle of its own is taken");

        AssetTypeValueField Field(long id) => am.GetBaseField(source, source.file.GetAssetInfo(id));
        var hierarchy = new Hierarchy(am, source);
        var roots = new Dictionary<string, long>();
        foreach (var info in source.file.GetAssetsOfType(AssetClassID.Transform))
        {
            var t = am.GetBaseField(source, info);
            if (t["m_Father.m_PathID"].AsLong != 0)
                continue;
            var go = t["m_GameObject.m_PathID"].AsLong;
            var name = Field(go)["m_Name"].AsString;
            if (names == null || names.Contains(name))
                roots[name] = go;
        }
        if (names?.FirstOrDefault(n => !roots.ContainsKey(n)) is { } missing)
            throw new Exception($"{bundlePath}: no prefab {missing}");

        // The prefabs' objects and everything they refer to, each with its value.
        var values = new Dictionary<long, AssetTypeValueField>();
        var pending = new Stack<long>(roots.Values.SelectMany(hierarchy.Objects));
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (values.ContainsKey(id))
                continue;
            var v = Field(id);
            values[id] = v;
            foreach (var p in Pointers(v))
                if (p["m_FileID"].AsInt == 0 && p["m_PathID"].AsLong is var to && to != 0 && !values.ContainsKey(to))
                    pending.Push(to);
        }

        // The prefabs' roots at the place given (Unity's prefabs are at their own origin).
        foreach (var go in roots.Values)
        {
            var t = values[values[go]["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong)
                .First(c => source.file.GetAssetInfo(c).TypeId == (int)AssetClassID.Transform)];
            if (position is { Length: 3 })
                (t["m_LocalPosition.x"].AsFloat, t["m_LocalPosition.y"].AsFloat, t["m_LocalPosition.z"].AsFloat) = (position[0], position[1], position[2]);
            if (rotation is { Length: 4 })
                (t["m_LocalRotation.x"].AsFloat, t["m_LocalRotation.y"].AsFloat, t["m_LocalRotation.z"].AsFloat, t["m_LocalRotation.w"].AsFloat) =
                    (rotation[0], rotation[1], rotation[2], rotation[3]);
        }

        // A scene's object: a GameObject or a component of one; the rest are assets. Each is given the next id of its
        // file: a scene's objects are numbered from 1 up, as Unity builds it.
        bool InScene(long id) => source.file.GetAssetInfo(id).TypeId == (int)AssetClassID.GameObject || !values[id]["m_GameObject"].IsDummy;
        long nextScene = scene.file.Metadata.AssetInfos.Max(a => a.PathId) + 1, nextShared = shared.file.Metadata.AssetInfos.Max(a => a.PathId) + 1;
        var ids = values.Keys.OrderBy(id => id).ToDictionary(id => id, id => InScene(id) ? nextScene++ : nextShared++);
        var assets = new List<long>();
        foreach (var (id, v) in values)
        {
            var info = source.file.GetAssetInfo(id);
            var inScene = InScene(id);
            var target = inScene ? scene : shared;
            foreach (var p in Pointers(v))
            {
                if (p["m_FileID"].AsInt != 0 || p["m_PathID"].AsLong is var to && to == 0)
                    continue;
                p["m_PathID"].AsLong = ids[to];
                if (InScene(to) == inScene)
                    continue;
                if (!inScene)
                    throw new Exception($"{bundlePath}: asset {id} refers to the prefab's object {to}");
                p["m_FileID"].AsInt = sharedFileId;
            }
            if (info.TypeId == (int)AssetClassID.Texture2D && v["m_StreamData.path"].AsString is { Length: > 0 } path)
            {
                // archive:/<bundle's file>/<resource file>
                var data = resources[path[(path.LastIndexOf('/') + 1)..]];
                var (offset, size) = ((long)v["m_StreamData.offset"].AsULong, (int)v["m_StreamData.size"].AsUInt);
                v["image data"].AsByteArray = data.AsSpan((int)offset, size).ToArray();
                v["m_StreamData.offset"].AsULong = 0;
                v["m_StreamData.size"].AsUInt = 0;
                v["m_StreamData.path"].AsString = "";
            }
            if (info.TypeId == (int)AssetClassID.MonoBehaviour)
                throw new Exception($"{bundlePath}: object {id} is a script's: a scene takes no scripts of another bundle");
            if (target.file.Metadata.FindTypeTreeTypeByID(info.TypeId) == null)
                target.file.Metadata.TypeTreeTypes.Add(source.file.Metadata.FindTypeTreeTypeByID(info.TypeId));
            var copy = AssetFileInfo.Create(target.file, ids[id], info.TypeId, 0xFFFF, null, false);
            copy.SetNewData(v);
            target.file.Metadata.AddAssetInfo(copy);
            if (!inScene)
                assets.Add(ids[id]);
        }
        Console.WriteLine($"  models: {roots.Count} prefabs of {Path.GetFileName(bundlePath)}, {values.Count - assets.Count} scene objects, {assets.Count} assets");
        return assets;
    }

    // Every reference of an object's value (a PPtr: m_FileID and m_PathID).
    static IEnumerable<AssetTypeValueField> Pointers(AssetTypeValueField v)
    {
        if (v.TypeName.StartsWith("PPtr<"))
        {
            yield return v;
            yield break;
        }
        if (v.Children == null)
            yield break;
        foreach (var c in v.Children)
            foreach (var p in Pointers(c))
                yield return p;
    }

    static byte[] Read(AssetBundleFile bundle, int index)
    {
        bundle.GetFileRange(index, out var offset, out var length);
        var reader = bundle.DataReader;
        reader.Position = offset;
        return reader.ReadBytes((int)length);
    }
}
