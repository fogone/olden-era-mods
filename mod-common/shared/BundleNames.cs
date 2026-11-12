using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Nobirds.OldenEra.Modding.Common;

// The game's components in a mod's bundle by readable names (#62). A prefab or a scene of the game's kind carries the
// game's components (a battle unit's view helper, a town's building slots...), and a bundle refers to each by its
// script: assembly, namespace, class, and stores its fields by name. Some of the game's classes, and some serialized
// fields (auto-properties' backing fields), have names the obfuscator changes with every patch (the view helper is emp
// on 0.82.08, a building slot's name <clsg>k__BackingField), so a mod's bundle names them in namespace OldenEra by what
// they are (OldenEra.UnitViewHelper, OldenEra.BuildingSlot with its field sceneSlot), and the library puts this game
// version's names in when it takes the bundle from the mod (ModAssets: once, into the cache). A bundle made with a Unity
// project's stub scripts of these names, or by a tool, then works on every game version whose names the library knows.
// The classes the game names readably (Hex.Session.Battle.BhBattleTimelineHelper) are referred to as they are.
public static class BundleNames
{
    public const string Namespace = "OldenEra";

    sealed record Component(string Name, Func<Type> Type, Func<Dictionary<string, string>> Fields = null);

    // Readable name -> the game's class (the generated names, GameNames.g.cs), and its fields by readable name.
    static readonly Component[] components =
    {
        new("UnitViewHelper", () => typeof(GameUnitViewHelper)),            // a battle unit's points, skinned mesh, idle effects
        new("UnitAnimationEvents", () => typeof(GameUnitAnimationEvents)),  // the sounds the clips of a unit's model call
        new("BuildingSlot", () => typeof(GameBuildingSlot),                 // a town's building slot: its name, its buildings
            () => new() { ["sceneSlot"] = GameNames.BuildingSlotNameField }),
        new("CityCameraPoint", () => typeof(GameCityCameraPoint)),          // a town camera's point
    };

    sealed record Name(string Ns, string Cls, string Assembly, Dictionary<string, string> Fields);

    static Dictionary<string, Name> names;

    static Dictionary<string, Name> Names() => names ??= components.ToDictionary(c => c.Name, c =>
    {
        var t = c.Type();
        return new Name(t.Namespace ?? "", t.Name, t.Assembly.GetName().Name + ".dll", c.Fields?.Invoke() ?? new());
    });

    // What the renaming depends on: a bundle renamed for another game version is renamed again.
    public static string Stamp => GameNames.GameVersion + ";" + string.Join(",", Names().Select(n =>
        $"{n.Key}={n.Value.Ns}.{n.Value.Cls}{string.Concat(n.Value.Fields.Select(f => $":{f.Key}={f.Value}"))}"));

    // The bundle `source` with this version's names written to `target`; the number of scripts renamed (0: nothing
    // written, the bundle has no readable names). A bundle is read whole into memory, the result packed with LZ4.
    // `scenes`: the scenes of a scene bundle (by their names, what Unity loads them by), none for another.
    public static int Rename(string source, string target, out List<string> scenes)
    {
        var manager = new AssetsManager();
        scenes = new List<string>();
        try
        {
            using var stream = File.OpenRead(source);
            var bundle = manager.LoadBundleFile(stream, source, true);
            var directory = bundle.file.BlockAndDirInfo.DirectoryInfos;
            var files = Enumerable.Range(0, directory.Count).Where(i => bundle.file.IsAssetsFile(i))
                .ToDictionary(i => i, i => manager.LoadAssetsFileFromBundle(bundle, i, false));
            var changed = new HashSet<int>();

            // The scripts, and which of them have fields renamed: by the file's name in the bundle and the script's id.
            var renamedScripts = new Dictionary<(string file, long id), Dictionary<string, string>>();
            foreach (var (i, file) in files)
            {
                foreach (var info in file.file.GetAssetsOfType(AssetClassID.AssetBundle))
                {
                    var assetBundle = manager.GetBaseField(file, info);
                    if (assetBundle["m_IsStreamedSceneAssetBundle"].AsBool)
                        scenes.AddRange(assetBundle["m_Container.Array"].Children.Select(c => Path.GetFileNameWithoutExtension(c["first"].AsString)));
                }
                foreach (var info in file.file.GetAssetsOfType(AssetClassID.MonoScript))
                {
                    var script = manager.GetBaseField(file, info);
                    if (script["m_Namespace"].AsString != Namespace || !Names().TryGetValue(script["m_ClassName"].AsString, out var name))
                        continue;
                    script["m_ClassName"].AsString = name.Cls;
                    script["m_Namespace"].AsString = name.Ns;
                    script["m_AssemblyName"].AsString = name.Assembly;
                    info.SetNewData(script);
                    changed.Add(i);
                    renamedScripts[(directory[i].Name, info.PathId)] = name.Fields;
                }
            }
            if (renamedScripts.Count == 0)
                return 0;

            // The fields of the renamed scripts' components, in every file's type trees (a scene's components refer to the
            // scripts of its shared assets).
            foreach (var (i, file) in files)
            {
                var meta = file.file.Metadata;
                foreach (var type in meta.TypeTreeTypes.Where(t => t.TypeId == (int)AssetClassID.MonoBehaviour && t.ScriptTypeIndex != 0xFFFF))
                {
                    if (type.ScriptTypeIndex >= meta.ScriptTypes.Count)
                        continue;
                    var script = meta.ScriptTypes[type.ScriptTypeIndex];
                    var scriptFile = script.FileId == 0 ? directory[i].Name : meta.Externals[script.FileId - 1].PathName.Split('/').Last();
                    if (renamedScripts.TryGetValue((scriptFile, script.PathId), out var fields) && fields.Count > 0 && RenameFields(type, fields))
                        changed.Add(i);
                }
            }
            foreach (var i in changed)
                directory[i].SetNewData(files[i].file);

            // Written whole, then packed with LZ4 (Unity's chunk-based compression, loaded as is): a cache of a
            // faction's bundles stays about the size of the mod's.
            using var whole = new MemoryStream();
            bundle.file.Write(new AssetsFileWriter(whole));
            whole.Position = 0;
            var written = new AssetBundleFile();
            written.Read(new AssetsFileReader(whole));
            using (var writer = new AssetsFileWriter(target))
                written.Pack(writer, AssetBundleCompressionType.LZ4, true, null);
            return renamedScripts.Count;
        }
        finally
        {
            manager.UnloadAll(true);
        }
    }

    // A component's type tree with its fields (the script's own, right under the root) renamed; the strings written
    // again (Unity's common strings kept as references).
    static bool RenameFields(TypeTreeType type, Dictionary<string, string> fields)
    {
        var nodes = type.Nodes;
        var buffer = type.StringBufferBytes;
        var names = nodes.Select(n => n.GetNameString(buffer, TypeTreeType.COMMON_STRING_TABLE)).ToList();
        if (!nodes.Where((n, k) => n.Level == 1 && fields.ContainsKey(names[k])).Any())
            return false;
        var strings = new List<byte>();
        var offsets = new Dictionary<string, uint>();
        uint Offset(uint old, string text)
        {
            if ((old & 0x80000000) != 0)
                return old;
            if (!offsets.TryGetValue(text, out var at))
            {
                at = offsets[text] = (uint)strings.Count;
                strings.AddRange(Encoding.ASCII.GetBytes(text));
                strings.Add(0);
            }
            return at;
        }
        for (var k = 0; k < nodes.Count; k++)
        {
            var n = nodes[k];
            var typeName = n.GetTypeString(buffer, TypeTreeType.COMMON_STRING_TABLE);
            var name = n.Level == 1 && fields.TryGetValue(names[k], out var renamed) ? renamed : names[k];
            n.TypeStrOffset = Offset(n.TypeStrOffset, typeName);
            n.NameStrOffset = name != names[k] ? Offset(0, name) : Offset(n.NameStrOffset, name);
        }
        type.StringBufferBytes = strings.ToArray();
        return true;
    }
}
