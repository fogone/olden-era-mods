using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OldenEra.Modding.Manager;

// Updating what cannot be reloaded while the game runs: the manager itself and the library (and the patcher), in
// BepInEx\plugins and BepInEx\patchers (issue #10). Compiled into the manager (it stages an update and confirms a
// start), the preloader patcher (it applies or rolls back at the start, before BepInEx loads any plugin) and the tests.
// Only files: no BepInEx, no game.
//
//   BepInEx\oldenera-mods\.update\update.json   the status
//   BepInEx\oldenera-mods\.update\staged\        the new files, waiting for the next start
//   BepInEx\oldenera-mods\.update\previous\      the files they replaced, for a rollback
//
// The way: Stage (the manager, the game running) -> at the next start Apply moves the current files to previous\ and
// the staged ones in, "trying" -> the new manager, once it has started, Confirm: "ok". A start that finds "trying"
// again (the new version crashed or did not load) rolls back: the previous files are put back and the version is
// marked failed, not offered again.
public sealed class UpdateFile
{
    [JsonPropertyName("name")] public string Name { get; set; }
    // The folder under BepInEx it goes to: "plugins" or "patchers".
    [JsonPropertyName("folder")] public string Folder { get; set; } = "plugins";
}

public sealed class UpdateStatus
{
    public const string None = "none", Staged = "staged", Trying = "trying", Ok = "ok", RolledBack = "rolled-back";

    [JsonPropertyName("state")] public string State { get; set; } = None;
    // The version staged or tried, and the one before it.
    [JsonPropertyName("version")] public string Version { get; set; }
    [JsonPropertyName("previous")] public string PreviousVersion { get; set; }
    [JsonPropertyName("files")] public List<UpdateFile> Files { get; set; } = new();
    // The versions that did not start: not offered again.
    [JsonPropertyName("failed")] public List<string> Failed { get; set; } = new();
    // What the last start did, for the Mods tab ("updated to ...", "the update to ... failed, back to ...").
    [JsonPropertyName("message")] public string Message { get; set; }
}

public sealed class Updater
{
    readonly string root;
    readonly Action<string> log;
    static readonly JsonSerializerOptions options = new() { WriteIndented = true };

    // `bepInExRoot`: the BepInEx folder (its plugins, patchers, oldenera-mods).
    public Updater(string bepInExRoot, Action<string> log)
    {
        root = bepInExRoot;
        this.log = log ?? (_ => { });
    }

    string UpdateFolder => Path.Combine(root, "oldenera-mods", ".update");
    string StagedFolder => Path.Combine(UpdateFolder, "staged");
    string PreviousFolder => Path.Combine(UpdateFolder, "previous");
    string StatusPath => Path.Combine(UpdateFolder, "update.json");
    string Target(UpdateFile file) => Path.Combine(root, file.Folder, file.Name);

    public UpdateStatus Status()
    {
        try
        {
            return File.Exists(StatusPath) ? JsonSerializer.Deserialize<UpdateStatus>(File.ReadAllText(StatusPath), options) ?? new UpdateStatus() : new UpdateStatus();
        }
        catch (Exception e)
        {
            log($"update status unreadable ({e.Message}): taken as none");
            return new UpdateStatus();
        }
    }

    void Save(UpdateStatus status)
    {
        Directory.CreateDirectory(UpdateFolder);
        var temporary = StatusPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(status, options));
        File.Move(temporary, StatusPath, true);
    }

    public bool WasFailed(string version) => Status().Failed.Contains(version);

    // The manager, with the game running: the new version's files (already checked by their hash) wait in staged\ for
    // the next start. A version that failed before is refused. Staging again replaces what was staged.
    public bool Stage(string version, string currentVersion, IEnumerable<(UpdateFile file, byte[] bytes)> files)
    {
        var status = Status();
        if (status.Failed.Contains(version))
        {
            log($"update {version}: it failed to start before, not staged");
            return false;
        }
        if (Directory.Exists(StagedFolder))
            Directory.Delete(StagedFolder, true);
        Directory.CreateDirectory(StagedFolder);
        var list = new List<UpdateFile>();
        foreach (var (file, bytes) in files)
        {
            File.WriteAllBytes(Path.Combine(StagedFolder, file.Name), bytes);
            list.Add(file);
        }
        status.State = UpdateStatus.Staged;
        status.Version = version;
        status.PreviousVersion = currentVersion;
        status.Files = list;
        status.Message = null;
        Save(status);
        log($"update {version} staged ({list.Count} files): applied at the next start");
        return true;
    }

    // The patcher, at the start: a "trying" left from the last start is rolled back; a staged update is applied. All
    // its files or none: when one cannot be moved (a file in use), the ones moved go back and the update stays staged.
    public void ApplyAtStart()
    {
        var status = Status();
        if (status.State == UpdateStatus.Trying)
        {
            RollBack(status, $"the update to {status.Version} did not start");
            return;
        }
        if (status.State != UpdateStatus.Staged)
            return;
        if (Directory.Exists(PreviousFolder))
            Directory.Delete(PreviousFolder, true);
        Directory.CreateDirectory(PreviousFolder);
        var done = new List<UpdateFile>();
        try
        {
            foreach (var file in status.Files)
            {
                var staged = Path.Combine(StagedFolder, file.Name);
                if (!File.Exists(staged))
                    throw new FileNotFoundException($"staged {file.Name} is missing");
                var target = Target(file);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target))
                    File.Move(target, Path.Combine(PreviousFolder, file.Name), true);
                done.Add(file);
                // Copied, not moved: the staged files stay until every one is in place.
                File.Copy(staged, target, true);
            }
        }
        catch (Exception e)
        {
            log($"update {status.Version} not applied ({e.Message}): put back, it stays staged");
            foreach (var file in done)
                PutBack(file);
            return;
        }
        Directory.Delete(StagedFolder, true);
        status.State = UpdateStatus.Trying;
        status.Message = null;
        Save(status);
        log($"update {status.Version} applied, trying it");
    }

    // The new manager, once it has started (loaded and found the game's version): the update is kept.
    public void Confirm()
    {
        var status = Status();
        if (status.State != UpdateStatus.Trying)
            return;
        status.State = UpdateStatus.Ok;
        status.Message = $"updated from {status.PreviousVersion} to {status.Version}";
        Save(status);
        log(status.Message);
    }

    // The player's "back to the previous version" (the tab), with the game running: the previous files are staged as
    // an update, applied at the next start; the version left is marked failed.
    public bool StageRollback()
    {
        var status = Status();
        if (status.State != UpdateStatus.Ok || status.Files.Count == 0)
            return false;
        var files = new List<(UpdateFile, byte[])>();
        foreach (var file in status.Files)
        {
            var previous = Path.Combine(PreviousFolder, file.Name);
            if (File.Exists(previous))
                files.Add((file, File.ReadAllBytes(previous)));
        }
        var left = status.Version;
        status.Failed.Add(left);
        Save(status);
        return Stage(status.PreviousVersion, left, files);
    }

    void RollBack(UpdateStatus status, string why)
    {
        foreach (var file in status.Files)
            PutBack(file);
        if (status.Version != null && !status.Failed.Contains(status.Version))
            status.Failed.Add(status.Version);
        status.Message = $"{why}: back to {status.PreviousVersion}";
        status.State = UpdateStatus.RolledBack;
        (status.Version, status.PreviousVersion) = (status.PreviousVersion, status.Version);
        Save(status);
        log(status.Message);
    }

    // The file before the update in its place; a file the update added (none before) goes.
    void PutBack(UpdateFile file)
    {
        var previous = Path.Combine(PreviousFolder, file.Name);
        var target = Target(file);
        try
        {
            if (File.Exists(previous))
                File.Copy(previous, target, true);
            else if (File.Exists(target))
                File.Delete(target);
        }
        catch (Exception e) { log($"rollback of {file.Name}: {e.Message}"); }
    }
}
