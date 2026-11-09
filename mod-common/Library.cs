using System.Runtime.CompilerServices;
using BepInEx.Logging;

namespace Nobirds.OldenEra.Modding.Common;

// The library's own start: what its shared code finds in the game (the generated names' lookups, the helpers) goes to a
// log of its own, one for every mod that uses it.
static class Library
{
    [ModuleInitializer]
    internal static void Start() => Game.Log ??= new ModLog(Logger.CreateLogSource("Olden Era Library")).Info;
}
