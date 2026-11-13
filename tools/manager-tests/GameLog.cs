namespace Nobirds.OldenEra.Modding.Common;

// The library's log as DataAdditions writes to it (Game.Log in the library), here to the console.
internal static class Game
{
    public static Action<string> Log = message => Console.WriteLine($"      {message}");
}

// The mods' town scenes (ModScenes in the library: Unity's scenes): none in these tests, so no city is rewritten.
internal static class ModScenes
{
    public static IEnumerable<(string path, System.Text.Json.Nodes.JsonObject file)> Cities(IEnumerable<(string name, string folder)> roots) =>
        Enumerable.Empty<(string, System.Text.Json.Nodes.JsonObject)>();
}
