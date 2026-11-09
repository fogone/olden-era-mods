using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nobirds.OldenEra.Modding.Manager;

// What the manager has installed (issue #10): BepInEx\oldenera-mods\mods.json, one entry per mod, and each mod's files
// in BepInEx\oldenera-mods\<id>\<version>\. The catalog client (later) writes entries and files; the loader reads them.
//
//   {
//     "schema": 1,
//     "mods": [
//       { "id": "nobirds.oldenera.battlecounter", "version": "0.48.0", "enabled": true,
//         "files": [ "OldenEraBattleCounter.dll" ], "requires": [ "nobirds.oldenera.core" ], "source": "<meta file URL>" }
//     ]
//   }
public sealed class InstalledMod
{
    // The mod's id: its plugin GUID (one plugin per mod).
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; }
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    // The DLLs, relative to the mod's version folder; each holds a BepInEx plugin or a library the plugin uses.
    [JsonPropertyName("files")] public List<string> Files { get; set; } = new();
    // The ids of the mods it needs loaded first (the core, another mod).
    [JsonPropertyName("requires")] public List<string> Requires { get; set; } = new();
    // Where it came from: the meta file it was installed by (the catalog), or null for one put by hand.
    [JsonPropertyName("source")] public string Source { get; set; }
}

public sealed class ManagerState
{
    public const int Schema = 1;

    [JsonPropertyName("schema")] public int SchemaVersion { get; set; } = Schema;
    [JsonPropertyName("mods")] public List<InstalledMod> Mods { get; set; } = new();

    public static string Folder { get; private set; }
    static string FilePath => Path.Combine(Folder, "mods.json");

    static readonly JsonSerializerOptions options = new() { WriteIndented = true };

    // The folder and the state in it; an empty state when there is none yet. A state file it cannot read is kept aside
    // (mods.json.bad) rather than overwritten, and an empty state used.
    public static ManagerState Load(string bepInExRoot, Action<string> warn)
    {
        Folder = Path.Combine(bepInExRoot, "oldenera-mods");
        Directory.CreateDirectory(Folder);
        if (!File.Exists(FilePath))
            return new ManagerState();
        try
        {
            var state = JsonSerializer.Deserialize<ManagerState>(File.ReadAllText(FilePath), options) ?? new ManagerState();
            if (state.SchemaVersion > Schema)
                warn($"{FilePath} is of a newer manager (schema {state.SchemaVersion}); read as far as this one understands it");
            state.Mods.RemoveAll(m => string.IsNullOrWhiteSpace(m?.Id) || string.IsNullOrWhiteSpace(m.Version));
            return state;
        }
        catch (Exception e)
        {
            warn($"{FilePath} cannot be read ({e.Message}): kept as mods.json.bad, starting with no mods");
            try { File.Copy(FilePath, FilePath + ".bad", true); } catch (Exception) { }
            return new ManagerState();
        }
    }

    // Written through a temporary file, so a crash while writing does not leave half a file.
    public void Save()
    {
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, options));
        File.Move(temporary, FilePath, true);
    }

    public InstalledMod Find(string id) => Mods.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

    // A mod's version folder.
    public static string FolderOf(InstalledMod mod) => Path.Combine(Folder, mod.Id, mod.Version);
}
