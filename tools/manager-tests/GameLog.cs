namespace Nobirds.OldenEra.Modding.Common;

// The library's log as DataAdditions writes to it (Game.Log in the library), here to the console.
internal static class Game
{
    public static Action<string> Log = message => Console.WriteLine($"      {message}");
}
