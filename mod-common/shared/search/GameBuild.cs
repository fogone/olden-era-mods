using System;
using System.IO;
using System.Security.Cryptography;

namespace Nobirds.OldenEra.Modding.Common;

// Which build of the game the files are: the start of the SHA-256 of its IL2CPP metadata (global-metadata.dat), where
// the obfuscated names live. Two builds may report one version (Application.version): the Steam build of 0.82.08 has
// other obfuscated names than the Game Pass one (#68), so the generated names (GameNames.GameBuild) are checked
// against this too. Here with the searches: the name generator (tools/game-names) and the library both use it.
public static class GameBuild
{
    public static string MetadataPath(string gameDir) =>
        Path.Combine(gameDir, "HeroesOldenEra_Data", "il2cpp_data", "Metadata", "global-metadata.dat");

    // Null when the file cannot be read.
    public static string Of(string gameDir)
    {
        try
        {
            using var file = File.OpenRead(MetadataPath(gameDir));
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(file))[..12].ToLowerInvariant();
        }
        catch (Exception) { return null; }
    }
}
