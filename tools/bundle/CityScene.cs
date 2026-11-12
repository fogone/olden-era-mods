using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Nobirds.OldenEra.Modding.Bundle;

// A town scene's buildings of the game's kind, written into a scene bundle (#62). The game's town scene (cityDungeon,
// 0.82.08) has a root with BhBuildingsManager and under it a slot per building (OldenEra.BuildingSlot: its name, the
// city data's "sceneSlot", and its buildings by level), each level a BhBuilding with a collider (what the town's clicks
// hit). From a JSON naming, for each slot, the scene's objects that are its levels:
//
//   { "slots": { "Main": ["capitol_1", "capitol_2", "capitol_3"], "Tier_1": ["dwelling_1", "dwelling_1_upg"], ... } }
//
// the objects are put under their slots, each given a BhBuilding and a box collider of its mesh's size.
sealed class CitySpec
{
    public Dictionary<string, string[]> Slots { get; set; }
    public float BuildingHeight { get; set; } = 2;

    public static CitySpec Read(string file) => JsonSerializer.Deserialize<CitySpec>(File.ReadAllText(file),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
}

sealed class CityScene
{
    const string Assembly = "Hex.dll";

    readonly AssetsManager am;
    readonly AssetsFileInstance scene, shared;
    readonly int sharedFileId;
    long nextScene, nextShared;

    public CityScene(AssetsManager am, AssetsFileInstance scene, AssetsFileInstance shared, int sharedFileId)
    {
        this.am = am;
        this.scene = scene;
        this.shared = shared;
        this.sharedFileId = sharedFileId;
        nextScene = scene.file.Metadata.AssetInfos.Max(a => a.PathId) + 1;
        nextShared = shared.file.Metadata.AssetInfos.Max(a => a.PathId) + 1;
    }

    public void Add(CitySpec spec)
    {
        var objects = scene.file.GetAssetsOfType(AssetClassID.GameObject)
            .ToDictionary(i => am.GetBaseField(scene, i)["m_Name"].AsString, i => i.PathId);
        long Object(string name) => objects.TryGetValue(name, out var id) ? id : throw new Exception($"the scene has no object {name}");

        var layer = am.GetBaseField(scene, scene.file.GetAssetInfo(objects.Values.First()))["m_Layer"].AsUInt;
        long root = nextScene++, rootTransform = nextScene++, manager = nextScene++;
        var slotTransforms = new List<long>();
        foreach (var (slot, levels) in spec.Slots)
        {
            long go = nextScene++, transform = nextScene++, component = nextScene++;
            slotTransforms.Add(transform);
            var buildings = new List<long>();
            var children = new List<long>();
            foreach (var level in levels)
            {
                var levelGo = Object(level);
                var levelTransform = TransformOf(levelGo);
                Reparent(levelTransform, transform);
                children.Add(levelTransform);
                long building = nextScene++, collider = nextScene++;
                Component(building, levelGo, "Hex.Session.City", "BhBuilding", BuildingFields(), v =>
                {
                    v["buildingHeight"].AsFloat = spec.BuildingHeight;
                    v["buildingVfxScale"].AsFloat = 1;
                });
                Native(collider, (int)AssetClassID.BoxCollider, TypeTrees.BoxCollider(), v =>
                {
                    v["m_GameObject.m_PathID"].AsLong = levelGo;
                    v["m_Enabled"].AsBool = true;
                    // A quad's mesh (Golden-Era's layers): 1 x 1 in its own space; thin in depth.
                    v["m_Size.x"].AsFloat = 1;
                    v["m_Size.y"].AsFloat = 1;
                    v["m_Size.z"].AsFloat = 0.1f;
                });
                AddComponents(levelGo, building, collider);
                buildings.Add(building);
            }
            GameObject(go, slot, layer, transform, component);
            Transform(transform, go, rootTransform, children.ToArray());
            Component(component, go, "OldenEra", "BuildingSlot", SlotFields(), v =>
            {
                v["sceneSlot"].AsString = slot;
                var array = v["buildingsInSlot.Array"];
                foreach (var b in buildings)
                {
                    var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
                    e["m_PathID"].AsLong = b;
                    array.Children.Add(e);
                }
            });
            Console.WriteLine($"  slot {slot}: {string.Join(", ", levels)}");
        }
        GameObject(root, "Buildings", layer, rootTransform, manager);
        Transform(rootTransform, root, 0, slotTransforms.ToArray());
        Component(manager, root, "Hex.Session.City", "BhBuildingsManager", ManagerFields(), v => v["constructOnStart"].AsBool = false);
    }

    static IEnumerable<TypeTrees.Node> BuildingFields() => new[]
    {
        new TypeTrees.Node(1, "float", "buildingHeight", 4, 0), new TypeTrees.Node(1, "float", "buildingBaseOffset", 4, 0),
        new TypeTrees.Node(1, "float", "buildingVfxScale", 4, 0),
    }.Concat(TypeTrees.PPtr(1, "vfxRoot", "GameObject"));

    static IEnumerable<TypeTrees.Node> SlotFields() => new[]
    {
        new TypeTrees.Node(1, "string", "sceneSlot", -1, 0x8000), new TypeTrees.Node(2, "Array", "Array", -1, TypeTrees.Aligned, Array: true),
        new TypeTrees.Node(3, "int", "size", 4, 0), new TypeTrees.Node(3, "char", "data", 1, 0),
    }.Concat(TypeTrees.PPtrArray(1, "buildingsInSlot", "BhBuilding"));

    static IEnumerable<TypeTrees.Node> ManagerFields() => new[] { new TypeTrees.Node(1, "UInt8", "constructOnStart", 1, TypeTrees.Aligned) };

    AssetTypeValueField Field(AssetsFileInstance file, long id) => am.GetBaseField(file, file.file.GetAssetInfo(id));

    long TransformOf(long go) => Field(scene, go)["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong)
        .First(c => scene.file.GetAssetInfo(c).TypeId == (int)AssetClassID.Transform);

    void Edit(long id, Action<AssetTypeValueField> change)
    {
        var info = scene.file.GetAssetInfo(id);
        var v = am.GetBaseField(scene, info);
        change(v);
        info.SetNewData(v);
    }

    // A transform moved under another: off its old parent's children, its father the new one.
    void Reparent(long transform, long father)
    {
        var old = Field(scene, transform)["m_Father.m_PathID"].AsLong;
        if (old != 0)
            Edit(old, v => v["m_Children.Array"].Children.RemoveAll(c => c["m_PathID"].AsLong == transform));
        Edit(transform, v => v["m_Father.m_PathID"].AsLong = father);
    }

    void AddComponents(long go, params long[] components) => Edit(go, v =>
    {
        var array = v["m_Component.Array"];
        foreach (var c in components)
        {
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
            e["component.m_PathID"].AsLong = c;
            array.Children.Add(e);
        }
    });

    void GameObject(long id, string name, uint layer, params long[] components) => New(scene, id, (int)AssetClassID.GameObject, 0xFFFF, v =>
    {
        var array = v["m_Component.Array"];
        foreach (var c in components)
        {
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
            e["component.m_PathID"].AsLong = c;
            array.Children.Add(e);
        }
        v["m_Layer"].AsUInt = layer;
        v["m_Name"].AsString = name;
        v["m_IsActive"].AsBool = true;
    });

    void Transform(long id, long go, long father, params long[] children) => New(scene, id, (int)AssetClassID.Transform, 0xFFFF, v =>
    {
        v["m_GameObject.m_PathID"].AsLong = go;
        v["m_LocalRotation.w"].AsFloat = 1;
        v["m_LocalScale.x"].AsFloat = v["m_LocalScale.y"].AsFloat = v["m_LocalScale.z"].AsFloat = 1;
        var array = v["m_Children.Array"];
        foreach (var c in children)
        {
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
            e["m_PathID"].AsLong = c;
            array.Children.Add(e);
        }
        v["m_Father.m_PathID"].AsLong = father;
    });

    void Native(long id, int typeId, TypeTreeType type, Action<AssetTypeValueField> fill)
    {
        if (scene.file.Metadata.FindTypeTreeTypeByID(typeId) == null)
            scene.file.Metadata.TypeTreeTypes.Add(type);
        New(scene, id, typeId, 0xFFFF, fill);
    }

    readonly Dictionary<string, ushort> scripts = new();

    // A component of a game script: its MonoScript among the shared assets (made once), referred to from the scene.
    void Component(long id, long go, string ns, string cls, IEnumerable<TypeTrees.Node> fields, Action<AssetTypeValueField> fill)
    {
        if (!scripts.TryGetValue($"{ns}.{cls}", out var index))
        {
            var sharedMeta = shared.file.Metadata;
            if (sharedMeta.FindTypeTreeTypeByID((int)AssetClassID.MonoScript) == null)
                sharedMeta.TypeTreeTypes.Add(TypeTrees.MonoScript());
            var scriptId = nextShared++;
            New(shared, scriptId, (int)AssetClassID.MonoScript, 0xFFFF, v =>
            {
                v["m_Name"].AsString = cls;
                v["m_ClassName"].AsString = cls;
                v["m_Namespace"].AsString = ns;
                v["m_AssemblyName"].AsString = Assembly;
            });
            Preload(scriptId);
            var meta = scene.file.Metadata;
            index = (ushort)meta.ScriptTypes.Count;
            meta.ScriptTypes.Add(new AssetPPtr(sharedFileId, scriptId));
            meta.TypeTreeTypes.Add(TypeTrees.MonoBehaviour(index, fields));
            scripts[$"{ns}.{cls}"] = index;
        }
        var scriptPtr = scene.file.Metadata.ScriptTypes[index];
        New(scene, id, (int)AssetClassID.MonoBehaviour, index, v =>
        {
            v["m_GameObject.m_PathID"].AsLong = go;
            v["m_Enabled"].AsByte = 1;
            v["m_Script.m_FileID"].AsInt = scriptPtr.FileId;
            v["m_Script.m_PathID"].AsLong = scriptPtr.PathId;
            fill(v);
        });
    }

    // The shared assets' preload list (PreloadData): the scene's scripts loaded with it.
    void Preload(long id)
    {
        foreach (var info in shared.file.GetAssetsOfType(AssetClassID.PreloadData))
        {
            var v = am.GetBaseField(shared, info);
            var array = v["m_Assets.Array"];
            var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
            e["m_FileID"].AsInt = 0;
            e["m_PathID"].AsLong = id;
            array.Children.Add(e);
            info.SetNewData(v);
        }
    }

    void New(AssetsFileInstance file, long id, int typeId, ushort scriptIndex, Action<AssetTypeValueField> fill)
    {
        var meta = file.file.Metadata;
        var type = scriptIndex == 0xFFFF ? meta.FindTypeTreeTypeByID(typeId) : meta.FindTypeTreeTypeByScriptIndex(scriptIndex);
        var template = new AssetTypeTemplateField();
        template.FromTypeTree(type);
        var value = ValueBuilder.DefaultValueFieldFromTemplate(template);
        fill(value);
        var info = AssetFileInfo.Create(file.file, id, typeId, scriptIndex, null, false);
        info.SetNewData(value);
        meta.AddAssetInfo(info);
    }
}
