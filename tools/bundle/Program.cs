using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Nobirds.OldenEra.Modding.Bundle;

// oe-bundle units <bundle> <out bundle> <unit.json>... [--uncompressed]   a unit's battle and map prefabs of the game's kind, made from
//                                                       a model prefab of the bundle, written with the bundle into <out>
if (args.Length < 4 || args[0] != "units")
{
    Console.Error.WriteLine("oe-bundle units <bundle> <out bundle> <unit.json>...  (see README.md)");
    return 2;
}
var (source, target) = (args[1], args[2]);
var manager = new AssetsManager();
var bundle = manager.LoadBundleFile(source, true);
var index = Enumerable.Range(0, bundle.file.BlockAndDirInfo.DirectoryInfos.Count).First(i => bundle.file.IsAssetsFile(i));
var file = manager.LoadAssetsFileFromBundle(bundle, index, false);
if (!file.file.Metadata.TypeTreeEnabled)
{
    Console.Error.WriteLine($"{source}: built without type trees: the game reads a mod's bundle by them (build it with them, Unity's default)");
    return 1;
}
var prefabs = new UnitPrefabs(manager, file);
foreach (var spec in args.Skip(3).Where(a => !a.StartsWith("--")))
{
    Console.WriteLine(spec);
    prefabs.Add(UnitSpec.Read(spec));
}
prefabs.Save();
bundle.file.BlockAndDirInfo.DirectoryInfos[index].SetNewData(file.file);
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
