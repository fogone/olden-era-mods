using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Nobirds.OldenEra.Modding.Bundle;

// oe-bundle units <bundle> <out bundle> <unit.json>... [--uncompressed]   a unit's battle and map prefabs of the game's
//     kind, made from a model prefab of the bundle, written with the bundle into <out>
// oe-bundle city <scene bundle> <out bundle> <city.json> [--uncompressed]  a town scene's buildings of the game's kind
//     (the buildings' manager, its slots, a building per level) from the scene's objects
// oe-bundle model <bundle> <out bundle> <model.json> [--uncompressed]   a glTF model (.glb) made a prefab, in the shape of
//     a bundle's prefab of a textured mesh; the bundle's other assets taken out
var commands = new[] { "units", "city", "model" };
if (args.Length < 4 || !commands.Contains(args[0]))
{
    Console.Error.WriteLine("oe-bundle units <bundle> <out bundle> <unit.json>...\noe-bundle city <scene bundle> <out bundle> <city.json>\noe-bundle model <bundle> <out bundle> <model.json>\n(see README.md)");
    return 2;
}
var (source, target) = (args[1], args[2]);
var specs = args.Skip(3).Where(a => !a.StartsWith("--")).ToList();
var manager = new AssetsManager();
var bundle = manager.LoadBundleFile(source, true);
var directory = bundle.file.BlockAndDirInfo.DirectoryInfos;
var files = Enumerable.Range(0, directory.Count).Where(i => bundle.file.IsAssetsFile(i)).ToList();
if (files.Select(i => manager.LoadAssetsFileFromBundle(bundle, i, false)).Any(f => !f.file.Metadata.TypeTreeEnabled))
{
    Console.Error.WriteLine($"{source}: built without type trees: the game reads a mod's bundle by them (build it with them, Unity's default)");
    return 1;
}

if (args[0] == "units")
{
    var index = files[0];
    var file = manager.LoadAssetsFileFromBundle(bundle, index, false);
    var prefabs = new UnitPrefabs(manager, file);
    foreach (var spec in specs)
    {
        Console.WriteLine(spec);
        prefabs.Add(UnitSpec.Read(spec));
    }
    prefabs.Save();
    directory[index].SetNewData(file.file);
}
else if (args[0] == "model")
{
    var index = files[0];
    var file = manager.LoadAssetsFileFromBundle(bundle, index, false);
    Console.WriteLine(specs[0]);
    var spec = GlbSpec.Read(specs[0]);
    new GlbPrefab(manager, file).Make(spec, GlbModel.Read(spec.Glb));
    directory[index].SetNewData(file.file);
    // A bundle of its own: Unity loads no two bundles of one file name (CAB-...), so the files are named after the
    // model; its resource file (.resS) has nothing left in it.
    var cab = "CAB-" + Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(spec.Name))).ToLowerInvariant();
    for (var i = 0; i < directory.Count; i++)
    {
        if (directory[i].Name.EndsWith(".resS"))
            directory[i].SetNewData(Array.Empty<byte>());
        directory[i].Name = directory[i].Name.EndsWith(".resS") ? cab + ".resS" : cab;
    }
}
else
{
    // A scene bundle: the scene's file (BuildPlayer-<scene>) and its shared assets (.sharedAssets).
    var sceneIndex = files.First(i => !directory[i].Name.EndsWith(".sharedAssets"));
    var sharedIndex = files.First(i => directory[i].Name.EndsWith(".sharedAssets"));
    var scene = manager.LoadAssetsFileFromBundle(bundle, sceneIndex, false);
    var shared = manager.LoadAssetsFileFromBundle(bundle, sharedIndex, false);
    var sharedFileId = scene.file.Metadata.Externals.FindIndex(e => e.PathName.EndsWith("/" + directory[sharedIndex].Name)) + 1;
    if (sharedFileId == 0)
        throw new Exception("the scene does not refer to its shared assets");
    var city = new CityScene(manager, scene, shared, sharedFileId);
    Console.WriteLine(specs[0]);
    city.Add(CitySpec.Read(specs[0]));
    directory[sceneIndex].SetNewData(scene.file);
    directory[sharedIndex].SetNewData(shared.file);
}

// Written whole, then packed with LZ4 as Unity's ChunkBasedCompression (the game loads it as is); --uncompressed keeps
// it whole.
var uncompressed = args.Contains("--uncompressed");
using (var whole = new MemoryStream())
{
    bundle.file.Write(new AssetsFileWriter(whole));
    if (uncompressed)
        File.WriteAllBytes(target, whole.ToArray());
    else
    {
        whole.Position = 0;
        var written = new AssetBundleFile();
        written.Read(new AssetsFileReader(whole));
        using var writer = new AssetsFileWriter(target);
        written.Pack(writer, AssetBundleCompressionType.LZ4, true, null);
    }
}
Console.WriteLine($"written {target} ({new FileInfo(target).Length / 1024 / 1024} MB{(uncompressed ? "" : ", LZ4")})");
return 0;
