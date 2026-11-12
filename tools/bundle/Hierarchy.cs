using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Nobirds.OldenEra.Modding.Bundle;

// A prefab's objects in a bundle: its GameObjects, their components, the children's, as Unity links them (a
// GameObject lists its components, a Transform its children).
sealed class Hierarchy
{
    readonly AssetsManager am;
    readonly AssetsFileInstance inst;

    public Hierarchy(AssetsManager am, AssetsFileInstance inst)
    {
        this.am = am;
        this.inst = inst;
    }

    AssetTypeValueField Field(long id) => am.GetBaseField(inst, inst.file.GetAssetInfo(id));

    IEnumerable<long> Components(long go) => Field(go)["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong);

    long TransformOf(long go) => Components(go).First(c => inst.file.GetAssetInfo(c).TypeId is (int)AssetClassID.Transform or (int)AssetClassID.RectTransform);

    // Every object of the hierarchy under the GameObject: GameObjects and their components.
    public List<long> Objects(long go)
    {
        var all = new List<long>();
        void Walk(long g)
        {
            all.Add(g);
            var components = Components(g).ToList();
            all.AddRange(components);
            foreach (var child in Field(TransformOf(g))["m_Children.Array"].Children)
                Walk(Field(child["m_PathID"].AsLong)["m_GameObject.m_PathID"].AsLong);
        }
        Walk(go);
        return all;
    }

    // A transform under the GameObject whose GameObject has the name (0: none, or no name given).
    public long TransformNamed(long go, string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;
        foreach (var id in Objects(go).Where(i => inst.file.GetAssetInfo(i).TypeId == (int)AssetClassID.GameObject))
            if (Field(id)["m_Name"].AsString == name)
                return TransformOf(id);
        throw new Exception($"no object {name} in the model");
    }

    // The first component of the type under the GameObject.
    public long First(long go, AssetClassID type) =>
        Objects(go).FirstOrDefault(i => inst.file.GetAssetInfo(i).TypeId == (int)type);

    // A copy of the hierarchy: every object again under a new id, the links inside it to the copies, the rest (meshes,
    // materials, the controller, the avatar) shared. Returns the copy's GameObject, its Transform, old -> new ids.
    public (long go, long transform, Dictionary<long, long> copies) Copy(long go, Func<long> newId)
    {
        var file = inst.file;
        var objects = Objects(go);
        var copies = objects.ToDictionary(o => o, _ => newId());
        foreach (var old in objects)
        {
            var info = file.GetAssetInfo(old);
            var value = am.GetBaseField(inst, info);
            Relink(value, copies);
            var scriptIndex = info.GetScriptIndex(file);
            var copy = AssetFileInfo.Create(file, copies[old], info.TypeId, scriptIndex, null, false);
            copy.SetNewData(value);
            file.Metadata.AddAssetInfo(copy);
        }
        // The copy's root has no parent.
        var transform = copies[TransformOf(go)];
        var t = am.GetBaseField(inst, file.GetAssetInfo(transform));
        t["m_Father.m_PathID"].AsLong = 0;
        file.GetAssetInfo(transform).SetNewData(t);
        return (copies[go], transform, copies);
    }

    static void Relink(AssetTypeValueField field, Dictionary<long, long> copies)
    {
        if (field.TypeName.StartsWith("PPtr<") && field["m_PathID"] is { IsDummy: false } path && field["m_FileID"].AsInt == 0)
        {
            if (copies.TryGetValue(path.AsLong, out var to))
                path.AsLong = to;
            return;
        }
        foreach (var child in field.Children)
            Relink(child, copies);
    }
}
