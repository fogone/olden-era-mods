using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace Nobirds.OldenEra.Modding.Common;

// The game's components in a mod's bundle by readable names (#62). A prefab of the game's kind carries the game's
// components (a battle unit's view helper, the receiver of its animation events...), and a bundle refers to each by its
// script: assembly, namespace, class. Some of the game's classes have names the obfuscator changes with every patch
// (the view helper is emp on 0.82.08), so a mod's bundle names them in namespace OldenEra by what they are
// (OldenEra.UnitViewHelper), and the library puts this game version's names in when it takes the bundle from the mod
// (ModAssets: once, into the cache). A bundle made with a Unity project's stub scripts of these names, or by a tool,
// then works on every game version whose names the library knows. The classes the game names readably
// (Hex.Session.Battle.BhBattleTimelineHelper) are referred to as they are.
public static class BundleNames
{
    public const string Namespace = "OldenEra";

    // Readable name -> the game's class (the generated names, GameNames.g.cs).
    static readonly (string name, Func<Type> type)[] components =
    {
        ("UnitViewHelper", () => typeof(GameUnitViewHelper)),            // a battle unit's points, skinned mesh, idle effects
        ("UnitAnimationEvents", () => typeof(GameUnitAnimationEvents)),  // the sounds the clips of a unit's model call
    };

    static Dictionary<string, (string ns, string cls, string assembly)> names;

    static Dictionary<string, (string ns, string cls, string assembly)> Names() => names ??= components.ToDictionary(
        c => c.name, c => { var t = c.type(); return (t.Namespace ?? "", t.Name, t.Assembly.GetName().Name + ".dll"); });

    // What the renaming depends on: a bundle renamed for another game version is renamed again.
    public static string Stamp => GameNames.GameVersion + ";" + string.Join(",", Names().Select(n => $"{n.Key}={n.Value.ns}.{n.Value.cls}"));

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
            var renamed = 0;
            var directory = bundle.file.BlockAndDirInfo.DirectoryInfos;
            for (var i = 0; i < directory.Count; i++)
            {
                if (!bundle.file.IsAssetsFile(i))
                    continue;
                var file = manager.LoadAssetsFileFromBundle(bundle, i, false);
                var changed = false;
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
                    script["m_ClassName"].AsString = name.cls;
                    script["m_Namespace"].AsString = name.ns;
                    script["m_AssemblyName"].AsString = name.assembly;
                    info.SetNewData(script);
                    changed = true;
                    renamed++;
                }
                if (changed)
                    directory[i].SetNewData(file.file);
            }
            if (renamed == 0)
                return 0;
            // Written whole, then packed with LZ4 (Unity's chunk-based compression, loaded as is): a cache of a
            // faction's bundles stays about the size of the mod's.
            using var whole = new MemoryStream();
            bundle.file.Write(new AssetsFileWriter(whole));
            whole.Position = 0;
            var written = new AssetBundleFile();
            written.Read(new AssetsFileReader(whole));
            using (var writer = new AssetsFileWriter(target))
                written.Pack(writer, AssetBundleCompressionType.LZ4, true, null);
            return renamed;
        }
        finally
        {
            manager.UnloadAll(true);
        }
    }
}
