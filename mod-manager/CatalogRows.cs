using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Hex.UI;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.Manager;

// The Mods window's data (mod-settings draws it from ModCatalog): the manager's own entry, every mod (installed and the
// catalog's), the sources and the actions.
// The names, descriptions, notes and tags are composed per language (a mod's name in that language from its meta
// file), under keys stable per mod id. Made again when what they show changes (Refresh, from ManagerService.Pump).
public static class CatalogRows
{
    const string Prefix = "oe.manager.row.";
    static string shown;

    // Once a few frames: the data again when what it shows changed.
    public static void Refresh(string managerVersion, string gameVersion)
    {
        var status = new Updater(Paths.BepInExRootPath, null).Status();
        var catalog = ManagerService.Catalog;
        var signature = string.Join("|", ManagerService.Busy, ManagerService.LastFailed, catalog?.Count ?? -1, ManagerService.Updates.Count,
            status.State, status.Version, string.Join(",", ModLoader.State.Mods.Select(m => m.Id + m.Version + m.Enabled)), string.Join(",", ModLoader.Pending.Keys),
            ManagerService.ScreensRevision, SourcesSignature(), ByHandSignature());
        if (signature == shown)
            return;
        shown = signature;
        Build(catalog, status, managerVersion, gameVersion);
    }

    // What the sources show: their URLs, names, counts, states, check settings, and the last note on adding or removing one.
    static string SourcesSignature() => string.Join(";", ManagerService.Sources.Select(s => s + "," + ManagerService.SourceSettings(s)?.Name + "," + ManagerService.SourceSettings(s)?.CheckEvery + "," + ManagerService.SourceSettings(s)?.LastChecked?.Ticks)) + "/" +
        string.Join(";", ManagerService.SourceResults.Select(r => $"{r.Url},{r.Name},{r.Mods},{r.State}")) + "/" + ManagerService.SourceNote?.Key + ManagerService.SourceNote?.Args.FirstOrDefault();

    // The mods with a menu that the manager did not install: their set, versions, switches and state.
    static IEnumerable<ModMenu> ByHand() => ModMenu.All.Where(m => ManagerService.IsByHand(m.Guid));

    static string ByHandSignature() => string.Join(",", ByHand().Select(m => m.Guid + m.Version + m.Enabled.Value + m.IsOff));

    static string Compose(string key, Func<ELang, string> text) => Texts.Compose(Prefix + key, text);

    static string F(ELang language, string key, params object[] args) => string.Format(Texts.GetIn(language, key), args);

    static string Note(string id, string key, params object[] args) => key == null ? null : Compose(id + ".note", l => F(l, key, args));

    static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // The Sources view: the built-in source first (no Remove), then the player's in the order added. A name is the
    // root file's (the one read last, else the one remembered), the URL when there is none. Returns the title key of
    // each source by its URL (the mods show their source's name).
    static Dictionary<string, string> BuildSources(bool busy, out List<CatalogSource> list)
    {
        var results = ManagerService.SourceResults;
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        list = new List<CatalogSource>();
        var index = 0;
        foreach (var url in ManagerService.Sources)
        {
            var builtIn = Same(url, ManagerService.BuiltInSource);
            var result = results.FirstOrDefault(r => Same(r.Url, url));
            var settings = ManagerService.SourceSettings(url);
            // Read, but the root file has no name: the URL stands in. Not read now (failed, not checked): the name it had.
            var name = result == null || result.State == SourceState.Failed || result.State == SourceState.NotChecked ? settings?.Name : result.Name;
            if (string.IsNullOrWhiteSpace(name))
                name = url;
            var noteKey = result == null ? null
                : result.State == SourceState.Failed ? "oe.manager.src.note.failed"
                : result.State == SourceState.NotChecked ? "oe.manager.src.note.notChecked"
                : result.NoName ? "oe.manager.src.note.noName"
                : result.State == SourceState.Cached ? "oe.manager.src.note.cached" : "oe.manager.src.note.read";
            var titleKey = Compose("src." + index + ".name", _ => name);
            var note = noteKey == null ? null : Compose("src." + index + ".note", l => F(l, noteKey));
            var source = url;
            titles[url] = titleKey;
            list.Add(new CatalogSource(titleKey, url, result?.Mods ?? 0, note, builtIn, settings?.LastChecked, CheckSchedule.Normalize(settings?.CheckEvery),
                busy ? null : () => ManagerService.Check(source),
                every => ManagerService.SetCheckEvery(source, every),
                builtIn || busy ? null : () => ManagerService.RemoveSource(source),
                ManagerService.IconPath(result?.Icon)));
            index++;
        }
        return titles;
    }

    static void Build(IReadOnlyList<ModMeta> catalog, UpdateStatus status, string managerVersion, string gameVersion)
    {
        var updates = ManagerService.Updates;
        var busy = ManagerService.Busy;

        // A line under the heading only when there is something to say: working, the last thing failed, the catalog not
        // read yet, the outcome of adding or removing a source. Not the counts: the tabs show them.
        var statusKey = busy ? "oe.manager.status.busy"
            : ManagerService.LastFailed ? "oe.manager.status.failed"
            : catalog == null ? "oe.manager.status.none" : null;
        if (!busy && ManagerService.SourceNote is { } sourceNote)
            ModCatalog.StatusKey = Compose("status", l => F(l, sourceNote.Key, sourceNote.Args));
        else
            ModCatalog.StatusKey = statusKey == null ? null : Compose("status", l => F(l, statusKey, catalog?.Count ?? 0, updates.Count));

        // The actions: none while busy (the window's buttons wait); screenshots load in the background and never wait.
        ModCatalog.Busy = busy;
        ModCatalog.Install = busy ? null : ManagerService.Install;
        ModCatalog.Update = busy ? null : ManagerService.Update;
        ModCatalog.Remove = busy ? null : ManagerService.Remove;
        ModCatalog.SetEnabled = busy ? null : ManagerService.SetEnabled;
        ModCatalog.UpdateAll = busy || updates.Count < 2 ? null : ManagerService.UpdateAll;
        ModCatalog.LoadScreenshots = ManagerService.LoadScreenshots;
        ModCatalog.CheckAll = busy ? null : ManagerService.CheckAll;
        ModCatalog.AddSource = busy ? null : ManagerService.AddSource;
        ModCatalog.AddGitHubSource = busy ? null : ManagerService.AddGitHubSource;

        var sourceTitles = BuildSources(busy, out var sources);
        var metas = catalog ?? Array.Empty<ModMeta>();
        ModMeta MetaOf(string id) => metas.FirstOrDefault(m => Same(m.Id, id));

        // A mod's name in each language (its meta file's; its id when nothing names it), one key per id.
        string NameKey(string id) => Compose(id + ".name", l => Same(id, ManagerService.ManagerId) ? Texts.GetIn(l, "oe.manager.name") : MetaOf(id)?.NameIn(l.ToString()) ?? id);

        // The fields every entry has from its meta file: description, author, source, tags, screenshots.
        ModEntry Entry(string id, ModMeta meta, string installed, string available, bool hasUpdate, bool? enabled, bool required,
            IEnumerable<string> requires, string note)
        {
            var description = meta == null || meta.Description.Count == 0 ? null
                : Compose(id + ".desc", l => meta.Description.TryGetValue(l.ToString(), out var text) || meta.Description.TryGetValue("english", out text) ? text : "");
            var tags = (meta?.Tags ?? new List<string>()).Select(tag => Compose("tag." + tag, l => Tags.TagNameIn(l, tag))).ToList();
            var shots = (meta?.Screenshots ?? new List<ScreenshotFile>()).Select((shot, i) => new ModScreenshot(ManagerService.ScreenshotPath(shot),
                shot.Caption != null && shot.Caption.Values.Any(c => !string.IsNullOrWhiteSpace(c))
                    ? Compose($"{id}.shot.{i}", l => shot.CaptionIn(l.ToString()) ?? "") : null)).ToList();
            var sourceKey = meta?.SourceUrl != null && sourceTitles.TryGetValue(meta.SourceUrl, out var title) ? title : null;
            return new ModEntry(id, NameKey(id), description, meta?.Author, sourceKey, tags, installed, available, hasUpdate, enabled, required,
                requires.Select(NameKey).ToList(), shots, note);
        }

        // The manager itself: its update, and what the patcher did at the start when it staged or rolled back an update.
        var self = updates.FirstOrDefault(u => u.Id == ManagerService.ManagerId);
        var managerMeta = MetaOf(ManagerService.ManagerId);
        var managerRelease = managerMeta == null ? null : CatalogClient.ReleaseFor(managerMeta, gameVersion);
        var managerNote = status.State == UpdateStatus.Staged ? Note("manager", "oe.manager.note.staged", status.Version)
            : status.State == UpdateStatus.RolledBack ? Note("manager", "oe.manager.note.failed", status.PreviousVersion) : null;
        var manager = Entry(ManagerService.ManagerId, managerMeta, managerVersion, self?.Release.Version ?? managerRelease?.Version,
            self != null, null, true, Array.Empty<string>(), managerNote);

        // Every mod: the installed ones (even when the catalog does not list them any more) and the catalog's.
        // Not what BepInEx loads by itself from BepInEx\plugins (those are not the manager's).
        var installedMods = ModLoader.State.Mods.ToList();
        var ids = installedMods.Select(m => m.Id)
            .Concat(metas.Where(m => !Same(m.Id, ManagerService.ManagerId) && !BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance.Plugins.ContainsKey(m.Id)).Select(m => m.Id))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var mods = new List<ModEntry>();
        foreach (var id in ids)
        {
            var meta = MetaOf(id);
            var installed = ModLoader.State.Find(id);
            var update = updates.FirstOrDefault(u => Same(u.Id, id));
            if (installed != null)
            {
                // A mod updates only from the source it was installed from: another source's release is no update.
                var sameSource = meta != null && (string.IsNullOrEmpty(installed.Source) || Same(installed.Source, meta.Url));
                var release = sameSource ? CatalogClient.ReleaseFor(meta, gameVersion) : null;
                var needed = ModLoader.RequiredBy(id).Count > 0;
                var note = ModLoader.Pending.ContainsKey(id) ? "oe.manager.note.restart" : null;
                mods.Add(Entry(id, meta, installed.Version, update?.Release.Version ?? release?.Version, update != null, needed ? null : installed.Enabled, needed,
                    installed.Requires, Note(id, note)));
            }
            else
            {
                var release = meta == null ? null : CatalogClient.ReleaseFor(meta, gameVersion);
                var removed = ModLoader.Pending.TryGetValue(id, out var asked) && asked == "removed";
                var note = removed ? "oe.manager.note.removed" : release == null ? "oe.manager.note.noRelease" : null;
                // Not installable while it waits for the restart that removes it, or when it has no release for this game.
                mods.Add(Entry(id, meta, null, removed ? null : release?.Version, false, null, false,
                    release?.Requires.Select(r => r.Id) ?? Array.Empty<string>(), Note(id, note)));
            }
        }
        // Mods BepInEx loaded by itself (plugins, devplugins): shown with their menu's version and switch; the catalog's
        // release is only information (the manager does not replace files it did not put there).
        foreach (var menu in ByHand())
        {
            var guid = menu.Guid;
            var meta = MetaOf(guid);
            var release = meta == null ? null : CatalogClient.ReleaseFor(meta, gameVersion);
            var entry = Entry(guid, meta, menu.Version, release?.Version, false, menu.IsOff ? null : menu.Enabled.Value, false, Array.Empty<string>(),
                Note(guid, menu.IsOff ? "oe.manager.note.byHandOff" : "oe.manager.note.byHand"));
            // The name from the mod's own title (not the meta file's).
            mods.RemoveAll(m => Same(m.Id, guid));
            mods.Add(entry with { NameKey = Compose(guid + ".name", l => Texts.GetIn(l, $"oe.{menu.Id}.title")), ByHand = true });
        }
        mods = mods.OrderBy(m => Texts.GetIn(Texts.Language, m.NameKey), StringComparer.CurrentCultureIgnoreCase).ToList();
        ModCatalog.Set("oe.manager.catalog", manager, mods, sources);
    }
}
