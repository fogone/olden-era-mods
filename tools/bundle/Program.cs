using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Nobirds.OldenEra.Modding.Bundle;

// oe-bundle units <bundle> <out bundle> <unit.json>...   a unit's battle and map prefabs of the game's kind, made from
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
foreach (var spec in args.Skip(3))
{
    Console.WriteLine(spec);
    prefabs.Add(UnitSpec.Read(spec));
}
prefabs.Save();
bundle.file.BlockAndDirInfo.DirectoryInfos[index].SetNewData(file.file);
using (var writer = new AssetsFileWriter(target))
    bundle.file.Write(writer);
Console.WriteLine($"written {target}");
return 0;
