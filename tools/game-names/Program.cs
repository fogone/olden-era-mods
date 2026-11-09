using System.Text;
using System.Text.RegularExpressions;
using Nobirds.OldenEra.Modding.Common;
using Nobirds.OldenEra.Modding.GameNamesTool;
using Nobirds.OldenEra.Modding.Research;

// The generated names (docs/game-names.md) made from the game's files, with no game running: the interop assemblies
// BepInEx made for this game version (BepInEx\interop, made on the first start after a patch), GameAssembly.dll and
// global-metadata.dat. The searches are the ones the plugins have (mod-common/shared/Game*.cs, Lookup.Generate), run
// on the interop types; the machine code they read comes from the files (FileCode).
//
//   dotnet run --project tools/game-names -- [<game dir>] [<output .g.cs>]
// Writes mod-common/shared/GameNames.g.cs (pins in GameNames.pins.txt next to it); exit code 1 when nothing was
// written (the problems are listed).

var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var gameDir = args.Length > 0 ? args[0] : @"C:\XboxGames\Heroes of Might and Magic- Olden Era\Content";
var output = args.Length > 1 ? args[1] : Path.Combine(repo, "mod-common", "shared", "GameNames.g.cs");

// After a game patch BepInEx makes its interop again only when the game starts: until then the interop is the old
// version's and its types would be matched to the new metadata by name.
var interopDll = Path.Combine(gameDir, "BepInEx", "interop", "Hex.dll");
if (File.GetLastWriteTimeUtc(interopDll) < File.GetLastWriteTimeUtc(Path.Combine(gameDir, "GameAssembly.dll")))
{
    Console.WriteLine($"error GN0001: {interopDll} is older than the game's GameAssembly.dll: start the game once (to the main menu) so BepInEx makes its interop for this version, then build again.");
    return 1;
}

var watch = System.Diagnostics.Stopwatch.StartNew();
Game.Log = line => Console.WriteLine("  " + line);
var interop = new[] { typeof(Hex.UI.Tooltips.BhTooltip).Assembly, typeof(UnityEngine.Object).Assembly, typeof(UnityEngine.Camera).Assembly,
                      typeof(UnityEngine.Canvas).Assembly, typeof(TMPro.TMP_Text).Assembly, typeof(Il2CppSystem.String).Assembly }
    .Distinct().SelectMany(Game.SafeTypes);
var savedOut = Console.Out;
Console.SetOut(TextWriter.Null);   // LibCpp2IL talks a lot while reading
var fileCode = new FileCode(gameDir, interop);
CodeSource.Current = fileCode;
Console.SetOut(savedOut);
if (Environment.GetEnvironmentVariable("OE_LINKS") is { Length: > 0 } dumped)
{
    foreach (var type in interop.Where(t => t.Name == dumped || fileCode.GameName(t) == dumped))
        fileCode.DumpLinks(type);
    return 0;
}
Console.WriteLine($"Read the game's files in {watch.Elapsed.TotalSeconds:0.0} s");

var version = GameVersion(Path.Combine(gameDir, "HeroesOldenEra_Data", "globalgamemanagers"));
Console.WriteLine($"Game version {version}");
// Each problem as an error the build shows ("error GN0002: ..."), not only an exit code.
var ok = true;
foreach (var line in NamesGenerator.Generate(output, version))
{
    ok &= !line.StartsWith("not written");
    Console.WriteLine(!ok && line.StartsWith("  ") ? $"error GN0002: {line.Trim()}" : line);
}
Console.WriteLine($"Done in {watch.Elapsed.TotalSeconds:0.0} s");
return ok ? 0 : 1;

// The version the game reports (Application.version, "0.82.07"): Unity keeps it in globalgamemanagers (the player
// settings' bundle version) as a length-prefixed string. The only version-like string there besides Unity's own.
static string GameVersion(string path)
{
    var bytes = File.ReadAllBytes(path);
    var found = new HashSet<string>();
    var shape = new Regex(@"^\d+\.\d+(\.\d+)+$");
    for (var i = 0; i + 4 < bytes.Length; i += 4)
    {
        var length = BitConverter.ToInt32(bytes, i);
        if (length is < 3 or > 24 || i + 4 + length > bytes.Length)
            continue;
        var text = Encoding.ASCII.GetString(bytes, i + 4, length);
        if (shape.IsMatch(text))
            found.Add(text);
    }
    return found.Count == 1 ? found.First()
        : throw new InvalidOperationException($"the game's version in {path}: {(found.Count == 0 ? "none" : string.Join(", ", found))}");
}
