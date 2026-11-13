using System.Numerics;
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
    public CameraSpec Camera { get; set; }
    public float[] Offset { get; set; } = { 0, -2000, 0 };
    // The scene's objects taken out (with what is under them): by name, or by its beginning ending in "*".
    public string[] Remove { get; set; }
    public ModelsSpec Models { get; set; }

    // Prefabs of a model bundle (its path from the JSON's folder) put into the scene, at a place: the town's buildings
    // and its surroundings modelled in Unity. Their roots are the scene's objects by name, slots name them as any other.
    public sealed class ModelsSpec
    {
        public string Bundle { get; set; }
        public string[] Prefabs { get; set; }
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
    }

    // The scene's camera made the town's: the object, and the controller's settings (the game's town values by
    // default: cityDungeon's; no turning: a scene of flat layers).
    public sealed class CameraSpec
    {
        public string Object { get; set; }
        public float RotationSpeed { get; set; } = 6;
        public float DeltaHorizontalAngle { get; set; }
        public float DeltaVerticalAngle { get; set; }
        public float MaxMovementSpeed { get; set; } = 4;
        public float TransitionTime { get; set; } = 0.5f;
        public uint CullingMask { get; set; } = 40727; // cityDungeon's camera's
        // Where the camera stands and how much an orthographic one shows (half its height), when the scene's own camera
        // frames something else; the scene's by default.
        public float[] Position { get; set; }
        public float? Size { get; set; }
        // A perspective camera: its turn (a quaternion: x, y, z, w), its vertical field of view, how far it sees.
        public float[] Rotation { get; set; }
        public float? Fov { get; set; }
        public float? Far { get; set; }
    }

    public static CitySpec Read(string file)
    {
        var spec = JsonSerializer.Deserialize<CitySpec>(File.ReadAllText(file),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (spec.Models != null)
            spec.Models.Bundle = Path.GetFullPath(spec.Models.Bundle, Path.GetDirectoryName(Path.GetFullPath(file)));
        return spec;
    }
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
        foreach (var pattern in spec.Remove ?? Array.Empty<string>())
            RemoveObjects(pattern);
        if (spec.Models is { } models)
        {
            var assets = new ModelImport(am, scene, shared, sharedFileId).Add(models.Bundle, models.Prefabs, models.Position, models.Rotation);
            Preload(assets.ToArray());
            nextScene = scene.file.Metadata.AssetInfos.Max(a => a.PathId) + 1;
            nextShared = shared.file.Metadata.AssetInfos.Max(a => a.PathId) + 1;
        }

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
            var made = new Dictionary<string, long>();
            foreach (var level in levels)
            {
                // The same object on several levels (the art of the last one kept for the levels after it).
                if (made.TryGetValue(level, out var known))
                {
                    buildings.Add(known);
                    continue;
                }
                long levelGo, levelTransform;
                if (level.Length == 0)
                {
                    // A level with no art: an empty building, as a slot of the city's data needs one per level.
                    levelGo = nextScene++;
                    levelTransform = nextScene++;
                    GameObject(levelGo, $"{slot}_{buildings.Count + 1}", layer, levelTransform);
                    Transform(levelTransform, levelGo, transform);
                }
                else
                {
                    levelGo = Object(level);
                    levelTransform = TransformOf(levelGo);
                    Reparent(levelTransform, transform);
                }
                children.Add(levelTransform);
                long building = nextScene++, collider = nextScene++;
                Component(building, levelGo, "Hex.Session.City", "BhBuilding", BuildingFields(), v =>
                {
                    v["buildingHeight"].AsFloat = spec.BuildingHeight;
                    v["buildingVfxScale"].AsFloat = 1;
                });
                var (center, size) = Bounds(levelGo);
                Native(collider, (int)AssetClassID.BoxCollider, TypeTrees.BoxCollider(), v =>
                {
                    v["m_GameObject.m_PathID"].AsLong = levelGo;
                    v["m_Enabled"].AsBool = true;
                    (v["m_Center.x"].AsFloat, v["m_Center.y"].AsFloat, v["m_Center.z"].AsFloat) = (center.X, center.Y, center.Z);
                    // Thin in depth at least: a flat layer's quad.
                    (v["m_Size.x"].AsFloat, v["m_Size.y"].AsFloat, v["m_Size.z"].AsFloat) = (size.X, size.Y, Math.Max(size.Z, 0.1f));
                });
                AddComponents(levelGo, building, collider);
                buildings.Add(building);
                made[level] = building;
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

        if (spec.Camera is { } camera)
            AddCamera(camera, Object(camera.Object), layer, spec.Offset);

        // The scene away from the map: the game's town scenes are loaded with the session, at their own places.
        foreach (var info in scene.file.GetAssetsOfType(AssetClassID.Transform))
        {
            var t = am.GetBaseField(scene, info);
            if (t["m_Father.m_PathID"].AsLong != 0)
                continue;
            t["m_LocalPosition.x"].AsFloat += spec.Offset[0];
            t["m_LocalPosition.y"].AsFloat += spec.Offset[1];
            t["m_LocalPosition.z"].AsFloat += spec.Offset[2];
            info.SetNewData(t);
        }
    }

    // The town's camera of the game's kind (cityDungeon: CityCamera [BhCityCameraController, BhCityCameraHelper] with
    // MainCamera under it [Camera, BhCameraContext, PhysicsRaycaster...]): the scene's camera put under a CityCamera root,
    // the controller's settings (SoCityCameraSettings) an asset of the scene's own, its one position node the camera's.
    void AddCamera(CitySpec.CameraSpec spec, long cameraGo, uint layer, float[] offset)
    {
        var cameraTransform = TransformOf(cameraGo);
        var position = Field(scene, cameraTransform)["m_LocalPosition"];
        float x = position["x"].AsFloat, y = position["y"].AsFloat, z = position["z"].AsFloat;
        if (spec.Position is { Length: 3 } p)
        {
            (x, y, z) = (p[0], p[1], p[2]);
            Edit(cameraTransform, v => { var at = v["m_LocalPosition"]; at["x"].AsFloat = x; at["y"].AsFloat = y; at["z"].AsFloat = z; });
        }
        if (spec.Rotation is { Length: 4 } r)
            Edit(cameraTransform, v => (v["m_LocalRotation.x"].AsFloat, v["m_LocalRotation.y"].AsFloat, v["m_LocalRotation.z"].AsFloat,
                v["m_LocalRotation.w"].AsFloat) = (r[0], r[1], r[2], r[3]));

        long rig = nextScene++, rigTransform = nextScene++, controller = nextScene++, helper = nextScene++;
        long context = nextScene++, raycaster = nextScene++;
        Reparent(cameraTransform, rigTransform);
        GameObject(rig, "CityCamera", layer, rigTransform, controller, helper);
        Transform(rigTransform, rig, 0, cameraTransform);

        var settings = SharedAsset("Hex.Session.City", "SoCityCameraSettings", SettingsFields(), v =>
        {
            v["m_Name"].AsString = "city";
            v["rotationSpeed"].AsFloat = spec.RotationSpeed;
            v["deltaHorizontalAngle"].AsFloat = spec.DeltaHorizontalAngle;
            v["deltaVerticalAngle"].AsFloat = spec.DeltaVerticalAngle;
            v["maxMovementSpeed"].AsFloat = spec.MaxMovementSpeed;
            v["transitionTime"].AsFloat = spec.TransitionTime;
            // The movement speed curves of the game's town camera (cityDungeon's): with none the camera never arrives.
            Curve(v["firstLoopMovementSpeedCurve"], new[] { (-0.005264282f, 0.14910889f, 4.898253f), (0.040480554f, 0.37317866f, 2.6204855f),
                (0.10144019f, 0.4825136f, 1.2879217f), (0.59018254f, 0.7359377f, 0.25286824f), (1f, 0.82741547f, 0.22321585f) });
            Curve(v["movementSpeedCurve"], new[] { (0f, 1f, 0f), (1f, 1f, 0f) });
            var nodes = v["positionNodes.Array"];
            // The controller builds a spline of the nodes (it needs four at least): four at the camera, a hair apart, so it
            // stays where it is.
            for (var i = 0; i < 4; i++)
            {
                var node = ValueBuilder.DefaultValueFieldFromArrayTemplate(nodes);
                node["x"].AsFloat = offset[0] + x + i * 0.001f;
                node["y"].AsFloat = offset[1] + y;
                node["z"].AsFloat = offset[2] + z;
                nodes.Children.Add(node);
            }
        });
        Component(controller, rig, "Hex.Session.City", "BhCityCameraController", ControllerFields(), v =>
        {
            v["settings.m_FileID"].AsInt = sharedFileId;
            v["settings.m_PathID"].AsLong = settings;
            v["lookAtTransform.m_PathID"].AsLong = rigTransform;
            v["cameraTransform.m_PathID"].AsLong = cameraTransform;
        });
        Component(helper, rig, "Hex.Session.City", "BhCityCameraHelper", TypeTrees.PPtr(1, "cameraController", "BhCityCameraController"),
            v => v["cameraController.m_PathID"].AsLong = controller);
        Component(context, cameraGo, "Hex.Render", "BhCameraContext", Enumerable.Empty<TypeTrees.Node>(), _ => { });
        Component(raycaster, cameraGo, "UnityEngine.EventSystems", "PhysicsRaycaster", new[]
        {
            new TypeTrees.Node(1, "BitField", "m_EventMask", 4, 0, Version: 2), new TypeTrees.Node(2, "unsigned int", "m_Bits", 4, 0),
            new TypeTrees.Node(1, "int", "m_MaxRayIntersections", 4, 0),
        }, v => v["m_EventMask.m_Bits"].AsUInt = 0xFFFFFFFF, "UnityEngine.UI.dll");
        AddComponents(cameraGo, context, raycaster);
        // Tagged MainCamera, as the game's town camera (Camera.main: the interface's cameras are stacked on it).
        Edit(cameraGo, v => v["m_Tag"].AsUInt = 5);
        // The Camera as the game's town camera's: drawn first (the interface's cameras over it), the game's layers.
        foreach (var c in Field(scene, cameraGo)["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong)
                     .Where(c => scene.file.GetAssetInfo(c).TypeId == (int)AssetClassID.Camera).ToList())
            Edit(c, v =>
            {
                v["m_Depth"].AsFloat = 0;
                v["m_CullingMask.m_Bits"].AsUInt = spec.CullingMask;
                if (spec.Size is { } size)
                    v["orthographic size"].AsFloat = size;
                if (spec.Fov is { } fov)
                {
                    v["orthographic"].AsBool = false;
                    v["field of view"].AsFloat = fov;
                }
                if (spec.Far is { } far)
                    v["far clip plane"].AsFloat = far;
            });
        Console.WriteLine($"  camera: {spec.Object} under CityCamera, at ({x}, {y}, {z})");
    }

    // A curve's keys (time, value, the tangent in and out), clamped before and after as the game's.
    static void Curve(AssetTypeValueField curve, (float time, float value, float tangent)[] keys)
    {
        var array = curve["m_Curve.Array"];
        foreach (var (time, value, tangent) in keys)
        {
            var k = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
            k["time"].AsFloat = time;
            k["value"].AsFloat = value;
            k["inSlope"].AsFloat = k["outSlope"].AsFloat = tangent;
            k["inWeight"].AsFloat = k["outWeight"].AsFloat = 1f / 3;
            array.Children.Add(k);
        }
        curve["m_PreInfinity"].AsInt = curve["m_PostInfinity"].AsInt = 2;
        curve["m_RotationOrder"].AsInt = 4;
    }

    static IEnumerable<TypeTrees.Node> SettingsFields() => new[]
    {
        new TypeTrees.Node(1, "float", "rotationSpeed", 4, 0), new TypeTrees.Node(1, "float", "deltaHorizontalAngle", 4, 0),
        new TypeTrees.Node(1, "float", "deltaVerticalAngle", 4, 0), new TypeTrees.Node(1, "float", "maxMovementSpeed", 4, 0),
    }.Concat(TypeTrees.AnimationCurve(1, "firstLoopMovementSpeedCurve")).Concat(TypeTrees.AnimationCurve(1, "movementSpeedCurve"))
     .Concat(TypeTrees.Vector3Array(1, "positionNodes")).Append(new TypeTrees.Node(1, "float", "transitionTime", 4, 0));

    static IEnumerable<TypeTrees.Node> ControllerFields() => TypeTrees.PPtr(1, "settings", "SoCityCameraSettings")
        .Concat(TypeTrees.PPtr(1, "lookAtTransform", "Transform")).Concat(TypeTrees.PPtr(1, "cameraTransform", "Transform"));

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
    void Component(long id, long go, string ns, string cls, IEnumerable<TypeTrees.Node> fields, Action<AssetTypeValueField> fill,
        string assembly = Assembly)
    {
        if (!scripts.TryGetValue($"{ns}.{cls}", out var index))
        {
            var scriptId = Script(ns, cls, assembly);
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

    readonly Dictionary<string, long> scriptIds = new();

    // A game script's MonoScript among the shared assets, made once and preloaded with the scene.
    long Script(string ns, string cls, string assembly)
    {
        if (scriptIds.TryGetValue($"{ns}.{cls}", out var known))
            return known;
        var sharedMeta = shared.file.Metadata;
        if (sharedMeta.FindTypeTreeTypeByID((int)AssetClassID.MonoScript) == null)
            sharedMeta.TypeTreeTypes.Add(TypeTrees.MonoScript());
        var scriptId = nextShared++;
        New(shared, scriptId, (int)AssetClassID.MonoScript, 0xFFFF, v =>
        {
            v["m_Name"].AsString = cls;
            v["m_ClassName"].AsString = cls;
            v["m_Namespace"].AsString = ns;
            v["m_AssemblyName"].AsString = assembly;
        });
        Preload(scriptId);
        return scriptIds[$"{ns}.{cls}"] = scriptId;
    }

    // An asset of a game script (a ScriptableObject: a MonoBehaviour of no GameObject) among the shared assets.
    long SharedAsset(string ns, string cls, IEnumerable<TypeTrees.Node> fields, Action<AssetTypeValueField> fill)
    {
        var scriptId = Script(ns, cls, Assembly);
        var meta = shared.file.Metadata;
        var index = (ushort)meta.ScriptTypes.Count;
        meta.ScriptTypes.Add(new AssetPPtr(0, scriptId));
        meta.TypeTreeTypes.Add(TypeTrees.MonoBehaviour(index, fields));
        var id = nextShared++;
        New(shared, id, (int)AssetClassID.MonoBehaviour, index, v =>
        {
            v["m_Enabled"].AsByte = 1;
            v["m_Script.m_PathID"].AsLong = scriptId;
            fill(v);
        });
        Preload(id);
        return id;
    }

    // The shared assets' preload list (PreloadData): the scene's scripts and assets loaded with it.
    void Preload(params long[] ids)
    {
        foreach (var info in shared.file.GetAssetsOfType(AssetClassID.PreloadData))
        {
            var v = am.GetBaseField(shared, info);
            var array = v["m_Assets.Array"];
            foreach (var id in ids)
            {
                var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
                e["m_FileID"].AsInt = 0;
                e["m_PathID"].AsLong = id;
                array.Children.Add(e);
            }
            info.SetNewData(v);
        }
    }

    // The scene's objects of a name (or a name's beginning, "name*") taken out with all under them.
    void RemoveObjects(string pattern)
    {
        bool Matches(string name) => pattern.EndsWith('*') ? name.StartsWith(pattern[..^1]) : name == pattern;
        var hierarchy = new Hierarchy(am, scene);
        var removed = 0;
        foreach (var info in scene.file.GetAssetsOfType(AssetClassID.GameObject).ToList())
        {
            if (scene.file.GetAssetInfo(info.PathId) == null || !Matches(am.GetBaseField(scene, info)["m_Name"].AsString))
                continue;
            var transform = TransformOf(info.PathId);
            var father = Field(scene, transform)["m_Father.m_PathID"].AsLong;
            if (father != 0)
                Edit(father, v => v["m_Children.Array"].Children.RemoveAll(c => c["m_PathID"].AsLong == transform));
            foreach (var id in hierarchy.Objects(info.PathId))
                scene.file.Metadata.RemoveAssetInfo(scene.file.GetAssetInfo(id));
            removed++;
        }
        Console.WriteLine($"  removed {removed} objects {pattern}");
    }

    // The box around what an object draws (its meshes' and those of the objects under it), in its own space.
    (Vector3 center, Vector3 size) Bounds(long go)
    {
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        void Walk(long g, Matrix4x4 toLevel)
        {
            foreach (var c in Field(scene, g)["m_Component.Array"].Children.Select(c => c["component.m_PathID"].AsLong))
            {
                if (scene.file.GetAssetInfo(c).TypeId != (int)AssetClassID.MeshFilter)
                    continue;
                var mesh = Field(scene, c)["m_Mesh"];
                var file = mesh["m_FileID"].AsInt == 0 ? scene : mesh["m_FileID"].AsInt == sharedFileId ? shared : null;
                if (file == null || mesh["m_PathID"].AsLong == 0)
                    continue;
                var box = Field(file, mesh["m_PathID"].AsLong)["m_LocalAABB"];
                var at = Vec(box["m_Center"]);
                var extent = Vec(box["m_Extent"]);
                for (var i = 0; i < 8; i++)
                {
                    var corner = Vector3.Transform(at + extent * new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1), toLevel);
                    min = Vector3.Min(min, corner);
                    max = Vector3.Max(max, corner);
                }
            }
            foreach (var child in Field(scene, TransformOf(g))["m_Children.Array"].Children.Select(c => c["m_PathID"].AsLong))
            {
                var t = Field(scene, child);
                var local = Matrix4x4.CreateScale(Vec(t["m_LocalScale"]))
                    * Matrix4x4.CreateFromQuaternion(new Quaternion(Vec(t["m_LocalRotation"]), t["m_LocalRotation.w"].AsFloat))
                    * Matrix4x4.CreateTranslation(Vec(t["m_LocalPosition"]));
                Walk(t["m_GameObject.m_PathID"].AsLong, local * toLevel);
            }
        }
        Walk(go, Matrix4x4.Identity);
        return min.X > max.X ? (Vector3.Zero, new Vector3(1, 1, 0)) : ((min + max) / 2, max - min);
    }

    static Vector3 Vec(AssetTypeValueField v) => new(v["x"].AsFloat, v["y"].AsFloat, v["z"].AsFloat);

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
