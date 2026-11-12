using System.Text.Json;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Nobirds.OldenEra.Modding.Bundle;

// A unit's prefabs of the game's kind made from a model prefab of the bundle (an animated skinned mesh), laid out as
// the game's own (troglodyte, 0.82.08):
//
//   battle: <name>              OldenEra.UnitViewHelper (projectile and shield points, skinned mesh, idle effects),
//                               PlayableDirector, BhBattleTimelineHelper (its director; no timelines),
//                               BhParticleAccelerator
//             Scale_roll_pos    the first child: the game sets its scale to the view's "scale"; turned by "turn"
//               <model>         a copy of the model, plus OldenEra.UnitAnimationEvents (the sounds its clips call)
//   map:    <name>              BhSquadViewHelper (the quest marker's point)
//             Scale_roll_pos    turned by "turn", scaled by "scale" (mirrored when "mirror")
//               <model>         a copy of the model
//             FX_empty
//             quest_marker_point   at "questMarker" above the ground
//
// The prefabs go to assets/resources/<path>.prefab: the unit view's "mesh" / "mapMesh" is <path> without "units/".
// The game's components whose class names change with every patch are written by readable names (the library puts
// the running game's in, BundleNames); those the game names readably as they are.
sealed class UnitSpec
{
    public string Model { get; set; }
    public BattleSpec Battle { get; set; }
    public MapSpec Map { get; set; }

    public sealed class BattleSpec
    {
        public string Path { get; set; }
        public float Turn { get; set; }
        public string ProjectileSpawn { get; set; }
        public string ProjectileEnd { get; set; }
        public string Shield { get; set; }
        public float ShieldScale { get; set; } = 1;
    }

    public sealed class MapSpec
    {
        public string Path { get; set; }
        public float Turn { get; set; }
        public float Scale { get; set; } = 1;
        public bool Mirror { get; set; }
        public float QuestMarker { get; set; } = 1.5f;
    }

    public static UnitSpec Read(string file) => JsonSerializer.Deserialize<UnitSpec>(File.ReadAllText(file),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
}

sealed class UnitPrefabs
{
    // The game's scripts: readable name -> namespace, class, assembly as written into the bundle.
    static readonly Dictionary<string, (string ns, string cls)> scripts = new()
    {
        ["UnitViewHelper"] = ("OldenEra", "UnitViewHelper"),
        ["UnitAnimationEvents"] = ("OldenEra", "UnitAnimationEvents"),
        ["BhBattleTimelineHelper"] = ("Hex.Session.Battle", "BhBattleTimelineHelper"),
        ["BhParticleAccelerator"] = ("Hex.Session.Battle", "BhParticleAccelerator"),
        ["BhSquadViewHelper"] = ("Hex.Session.World", "BhSquadViewHelper"),
    };
    const string Assembly = "Hex.dll";

    // The script's serialized fields (those the tool fills; the rest keep the game's defaults).
    static IEnumerable<TypeTrees.Node> Fields(string script) => script switch
    {
        "UnitViewHelper" => new[]
        {
            TypeTrees.PPtr(1, "projectileSpawnPoint", "Transform"), TypeTrees.PPtr(1, "projectileEndPoint", "Transform"),
            TypeTrees.PPtr(1, "shieldPoint", "Transform"), TypeTrees.Vector3(1, "shieldScale"),
            TypeTrees.PPtr(1, "meshRenderer", "SkinnedMeshRenderer"), TypeTrees.PPtrArray(1, "idleVFX", "ParticleSystem"),
        }.SelectMany(n => n),
        "BhBattleTimelineHelper" => new[]
        {
            TypeTrees.PPtr(1, "director", "PlayableDirector"),
            TypeTrees.EmptyDictionary(1, "TimelineDictionary", "startAttackTimelines"),
            TypeTrees.EmptyDictionary(1, "TimelineDictionary", "impactAttackTimelines"),
        }.SelectMany(n => n),
        "BhSquadViewHelper" => TypeTrees.PPtr(1, "questMarkerPoint", "Transform"),
        _ => Enumerable.Empty<TypeTrees.Node>(),
    };

    readonly AssetsManager am;
    readonly AssetsFileInstance inst;
    readonly AssetsFile file;
    readonly AssetFileInfo bundleInfo;
    readonly AssetTypeValueField bundle;
    long next;

    public UnitPrefabs(AssetsManager am, AssetsFileInstance inst)
    {
        this.am = am;
        this.inst = inst;
        file = inst.file;
        bundleInfo = file.GetAssetsOfType(AssetClassID.AssetBundle).Single();
        bundle = am.GetBaseField(inst, bundleInfo);
        next = file.Metadata.AssetInfos.Max(a => a.PathId) + 1;
    }

    public void Add(UnitSpec spec)
    {
        var (modelEntry, modelGo) = ModelEntry(spec.Model);
        if (spec.Battle is { } battle)
            AddBattle(battle, modelEntry, modelGo);
        if (spec.Map is { } map)
            AddMap(map, modelEntry, modelGo);
    }

    public void Save() => bundleInfo.SetNewData(bundle);

    (AssetTypeValueField entry, long go) ModelEntry(string model)
    {
        var key = model.ToLowerInvariant();
        var entry = bundle["m_Container.Array"].Children.FirstOrDefault(c =>
        {
            var path = c["first"].AsString;
            return path == key || path.EndsWith("/" + key) || path == key + ".prefab" || path.EndsWith("/" + key + ".prefab");
        }) ?? throw new Exception($"the bundle has no asset {model}");
        var go = entry["second.asset.m_PathID"].AsLong;
        if (file.GetAssetInfo(go).TypeId != (int)AssetClassID.GameObject)
            throw new Exception($"{model} is not a prefab");
        return (entry, go);
    }

    void AddBattle(UnitSpec.BattleSpec spec, AssetTypeValueField modelEntry, long modelGo)
    {
        var added = new List<long>();
        var (copyGo, copyTransform, copies) = new Hierarchy(am, inst).Copy(modelGo, () => next++);
        added.AddRange(copies.Values);
        var layer = am.GetBaseField(inst, copyGo)["m_Layer"].AsUInt;

        long root = Id(added), rootTransform = Id(added), scale = Id(added), scaleTransform = Id(added);
        long helper = Id(added), director = Id(added), timelines = Id(added), particles = Id(added), events = Id(added);

        GameObject(root, Name(spec.Path), layer, rootTransform, helper, director, timelines, particles);
        Transform(rootTransform, root, 0, Quaternion(0), 1, scaleTransform);
        GameObject(scale, "Scale_roll_pos", layer, scaleTransform);
        Transform(scaleTransform, scale, rootTransform, Quaternion(spec.Turn), 1, copyTransform);
        Reparent(copyTransform, scaleTransform);
        Component(events, copyGo, "UnitAnimationEvents", _ => { });
        AddComponents(copyGo, events);

        var hierarchy = new Hierarchy(am, inst);
        Component(helper, root, "UnitViewHelper", v =>
        {
            Point(v, "projectileSpawnPoint", hierarchy.TransformNamed(copyGo, spec.ProjectileSpawn));
            Point(v, "projectileEndPoint", hierarchy.TransformNamed(copyGo, spec.ProjectileEnd));
            Point(v, "shieldPoint", hierarchy.TransformNamed(copyGo, spec.Shield));
            v["shieldScale.x"].AsFloat = v["shieldScale.y"].AsFloat = v["shieldScale.z"].AsFloat = spec.ShieldScale;
            v["meshRenderer.m_PathID"].AsLong = hierarchy.First(copyGo, AssetClassID.SkinnedMeshRenderer);
        });
        Native(director, TypeTrees.PlayableDirector(), 320, v => { v["m_GameObject.m_PathID"].AsLong = root; v["m_Enabled"].AsByte = 1; });
        Component(timelines, root, "BhBattleTimelineHelper", v => v["director.m_PathID"].AsLong = director);
        Component(particles, root, "BhParticleAccelerator", _ => { });

        AddPrefab(spec.Path, root, modelEntry, added);
        Console.WriteLine($"  battle: {Asset(spec.Path)}");
    }

    void AddMap(UnitSpec.MapSpec spec, AssetTypeValueField modelEntry, long modelGo)
    {
        var added = new List<long>();
        var (copyGo, copyTransform, copies) = new Hierarchy(am, inst).Copy(modelGo, () => next++);
        added.AddRange(copies.Values);
        var layer = am.GetBaseField(inst, copyGo)["m_Layer"].AsUInt;

        long root = Id(added), rootTransform = Id(added), scale = Id(added), scaleTransform = Id(added);
        long fx = Id(added), fxTransform = Id(added), marker = Id(added), markerTransform = Id(added), helper = Id(added);

        GameObject(root, Name(spec.Path), layer, rootTransform, helper);
        Transform(rootTransform, root, 0, Quaternion(0), 1, scaleTransform, fxTransform, markerTransform);
        GameObject(scale, "Scale_roll_pos", layer, scaleTransform);
        Transform(scaleTransform, scale, rootTransform, Quaternion(spec.Turn), spec.Scale, copyTransform);
        if (spec.Mirror)
            Edit(scaleTransform, v => v["m_LocalScale.x"].AsFloat = -spec.Scale);
        Reparent(copyTransform, scaleTransform);
        GameObject(fx, "FX_empty", layer, fxTransform);
        Transform(fxTransform, fx, rootTransform, Quaternion(0), 1);
        GameObject(marker, "quest_marker_point", layer, markerTransform);
        Transform(markerTransform, marker, rootTransform, Quaternion(0), 1);
        Edit(markerTransform, v => v["m_LocalPosition.y"].AsFloat = spec.QuestMarker);
        Component(helper, root, "BhSquadViewHelper", v => v["questMarkerPoint.m_PathID"].AsLong = markerTransform);

        AddPrefab(spec.Path, root, modelEntry, added);
        Console.WriteLine($"  map: {Asset(spec.Path)}");
    }

    long Id(List<long> added)
    {
        var id = next++;
        added.Add(id);
        return id;
    }

    static string Name(string path) => path.Split('/').Last();

    static string Asset(string path) => $"assets/resources/{path.ToLowerInvariant()}.prefab";

    static float[] Quaternion(float yawDegrees)
    {
        var half = yawDegrees * Math.PI / 360;
        return new[] { 0f, (float)Math.Sin(half), 0f, (float)Math.Cos(half) };
    }

    static void Point(AssetTypeValueField v, string field, long transform)
    {
        if (transform != 0)
            v[field + ".m_PathID"].AsLong = transform;
    }

    void Edit(long id, Action<AssetTypeValueField> change)
    {
        var info = file.GetAssetInfo(id);
        var v = am.GetBaseField(inst, info);
        change(v);
        info.SetNewData(v);
    }

    void Reparent(long transform, long father) => Edit(transform, v => v["m_Father.m_PathID"].AsLong = father);

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

    void GameObject(long id, string name, uint layer, params long[] components) => New(id, (int)AssetClassID.GameObject, 0xFFFF, v =>
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

    void Transform(long id, long go, long father, float[] rotation, float scale, params long[] children) =>
        New(id, (int)AssetClassID.Transform, 0xFFFF, v =>
        {
            v["m_GameObject.m_PathID"].AsLong = go;
            v["m_LocalRotation.x"].AsFloat = rotation[0];
            v["m_LocalRotation.y"].AsFloat = rotation[1];
            v["m_LocalRotation.z"].AsFloat = rotation[2];
            v["m_LocalRotation.w"].AsFloat = rotation[3];
            v["m_LocalScale.x"].AsFloat = v["m_LocalScale.y"].AsFloat = v["m_LocalScale.z"].AsFloat = scale;
            var array = v["m_Children.Array"];
            foreach (var c in children)
            {
                var e = ValueBuilder.DefaultValueFieldFromArrayTemplate(array);
                e["m_PathID"].AsLong = c;
                array.Children.Add(e);
            }
            v["m_Father.m_PathID"].AsLong = father;
        });

    void Native(long id, TypeTreeType type, int typeId, Action<AssetTypeValueField> fill)
    {
        if (file.Metadata.FindTypeTreeTypeByID(typeId) == null)
            file.Metadata.TypeTreeTypes.Add(type);
        New(id, typeId, 0xFFFF, fill);
    }

    void Component(long id, long go, string script, Action<AssetTypeValueField> fill)
    {
        var (scriptId, index) = Script(script);
        New(id, (int)AssetClassID.MonoBehaviour, index, v =>
        {
            v["m_GameObject.m_PathID"].AsLong = go;
            v["m_Enabled"].AsByte = 1;
            v["m_Script.m_PathID"].AsLong = scriptId;
            fill(v);
        });
    }

    readonly Dictionary<string, (long id, ushort index)> scriptIds = new();

    // The script's MonoScript and its component's type: the bundle's when it has them, else added.
    (long id, ushort index) Script(string name)
    {
        if (scriptIds.TryGetValue(name, out var known))
            return known;
        var (ns, cls) = scripts[name];
        var meta = file.Metadata;
        long id = 0;
        foreach (var info in file.GetAssetsOfType(AssetClassID.MonoScript))
        {
            var s = am.GetBaseField(inst, info);
            if (s["m_ClassName"].AsString == cls && s["m_Namespace"].AsString == ns)
                id = info.PathId;
        }
        if (id == 0)
        {
            if (meta.FindTypeTreeTypeByID((int)AssetClassID.MonoScript) == null)
                meta.TypeTreeTypes.Add(TypeTrees.MonoScript());
            id = next++;
            New(id, (int)AssetClassID.MonoScript, 0xFFFF, v =>
            {
                v["m_Name"].AsString = cls;
                v["m_ClassName"].AsString = cls;
                v["m_Namespace"].AsString = ns;
                v["m_AssemblyName"].AsString = Assembly;
            });
        }
        var index = meta.ScriptTypes.FindIndex(p => p.FileId == 0 && p.PathId == id);
        if (index < 0)
        {
            index = meta.ScriptTypes.Count;
            meta.ScriptTypes.Add(new AssetPPtr(0, id));
        }
        if (meta.FindTypeTreeTypeByScriptIndex((ushort)index) == null)
            meta.TypeTreeTypes.Add(TypeTrees.MonoBehaviour((ushort)index, Fields(name)));
        return scriptIds[name] = (id, (ushort)index);
    }


    void New(long id, int typeId, ushort scriptIndex, Action<AssetTypeValueField> fill)
    {
        var meta = file.Metadata;
        var type = scriptIndex == 0xFFFF ? meta.FindTypeTreeTypeByID(typeId) : meta.FindTypeTreeTypeByScriptIndex(scriptIndex);
        var template = new AssetTypeTemplateField();
        template.FromTypeTree(type);
        var value = ValueBuilder.DefaultValueFieldFromTemplate(template);
        fill(value);
        var info = AssetFileInfo.Create(file, id, typeId, scriptIndex, null, false);
        info.SetNewData(value);
        meta.AddAssetInfo(info);
    }

    // The prefab in the container, preloading what the model's prefab does (its meshes, materials, controller...),
    // the new objects and the scripts.
    void AddPrefab(string path, long root, AssetTypeValueField modelEntry, List<long> added)
    {
        var preload = bundle["m_PreloadTable.Array"];
        int start = modelEntry["second.preloadIndex"].AsInt, size = modelEntry["second.preloadSize"].AsInt;
        var ids = preload.Children.Skip(start).Take(size).Select(p => (p["m_FileID"].AsInt, p["m_PathID"].AsLong)).ToList();
        ids.AddRange(scriptIds.Values.Select(s => (0, s.id)).Concat(added.Select(a => (0, a))));
        var first = preload.Children.Count;
        foreach (var (fileId, id) in ids.Distinct())
        {
            var p = ValueBuilder.DefaultValueFieldFromArrayTemplate(preload);
            p["m_FileID"].AsInt = fileId;
            p["m_PathID"].AsLong = id;
            preload.Children.Add(p);
        }
        var container = bundle["m_Container.Array"];
        var asset = Asset(path);
        container.Children.RemoveAll(c => c["first"].AsString == asset);
        var entry = ValueBuilder.DefaultValueFieldFromArrayTemplate(container);
        entry["first"].AsString = asset;
        entry["second.preloadIndex"].AsInt = first;
        entry["second.preloadSize"].AsInt = preload.Children.Count - first;
        entry["second.asset.m_PathID"].AsLong = root;
        var at = container.Children.FindIndex(c => string.CompareOrdinal(c["first"].AsString, asset) > 0);
        container.Children.Insert(at < 0 ? container.Children.Count : at, entry);
    }
}
