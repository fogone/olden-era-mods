using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using Hex.UI;
using OldenEra.Modding.Common;

namespace OldenEra.Modding.Manager;

// The mods window's table (issue #10, #13: the Mods button in the main menu; mod-settings draws it from ModCatalog).
//   Mod                Version   State                         On   Action     Remove
//   Mod manager        0.2.2     0.3.0 available                    [Update]
//   Battle counter     0.48.0    0.48.1 available              [x]  [Update]   [x]
//   Visited buildings  0.35.1    not installed                      [Install]
//   Map tooltip        0.9.2                                   [ ]             [x]
//   Mods core          0.7.1     other mods need it
//   [Check] [Update all] [Close]
// The names and notes are composed per language (a mod's name in that language from its meta file). Made again when
// what they show changes (Refresh, from ManagerService.Pump).
public static class CatalogRows
{
    const string Prefix = "oe.manager.row.";
    static string shown;

    // Once a few frames: the rows again when what they show changed.
    public static void Refresh(string managerVersion, string gameVersion)
    {
        var status = new Updater(Paths.BepInExRootPath, null).Status();
        var catalog = ManagerService.Catalog;
        var signature = string.Join("|", ManagerService.Busy, ManagerService.LastFailed, catalog?.Count ?? -1, ManagerService.Updates.Count,
            status.State, status.Version, string.Join(",", ModLoader.State.Mods.Select(m => m.Id + m.Version + m.Enabled)), string.Join(",", ModLoader.Pending.Keys));
        if (signature == shown)
            return;
        shown = signature;
        Build(catalog, status, managerVersion, gameVersion);
    }

    static string Compose(string key, Func<ELang, string> text) => Texts.Compose(Prefix + key, text);

    static string F(ELang language, string key, params object[] args) => string.Format(Texts.GetIn(language, key), args);

    static string Note(string id, string key, params object[] args) => key == null ? null : Compose(id + ".note", l => F(l, key, args));

    static void Build(IReadOnlyList<ModMeta> catalog, UpdateStatus status, string managerVersion, string gameVersion)
    {
        var rows = new List<CatalogRow>();
        var updates = ManagerService.Updates;
        var busy = ManagerService.Busy;
        CatalogButton Button(string key, Action click) => busy ? null : new CatalogButton(key, click);

        // The catalog's state under the heading; Check and Update all at the bottom.
        var statusKey = busy ? "oe.manager.status.busy"
            : ManagerService.LastFailed ? "oe.manager.status.failed"
            : catalog == null ? "oe.manager.status.none" : "oe.manager.status.read";
        ModCatalog.StatusKey = Compose("status", l => F(l, statusKey, catalog?.Count ?? 0, updates.Count));
        ModCatalog.Check = busy ? null : new CatalogButton("oe.manager.check", ManagerService.Refresh);
        ModCatalog.UpdateAll = busy || updates.Count < 2 ? null : new CatalogButton("oe.manager.updateAll", ManagerService.UpdateAll);

        // The manager itself: its update, what the patcher did at the start (no "updated from": fogone 07.11).
        var self = updates.FirstOrDefault(u => u.Id == ManagerService.ManagerId);
        var managerName = Compose("manager.name", l => Texts.GetIn(l, "oe.manager.name"));
        if (status.State == UpdateStatus.Staged)
            rows.Add(new CatalogRow("Manager", managerName, managerVersion, Note("manager", "oe.manager.note.staged", status.Version), null, null, null, null));
        else if (status.State == UpdateStatus.RolledBack)
            rows.Add(new CatalogRow("Manager", managerName, managerVersion, Note("manager", "oe.manager.note.failed", status.PreviousVersion), null, null, null, null));
        else if (self != null)
            rows.Add(new CatalogRow("Manager", managerName, managerVersion, Note("manager", "oe.manager.note.available", self.Release.Version), null, null,
                Button("oe.manager.update", () => ManagerService.Update(self.Id)), null));
        else
            rows.Add(new CatalogRow("Manager", managerName, managerVersion, null, null, null, null, null));

        // The mods of the catalog (not the manager), by their names in the game's language.
        // Not what BepInEx loads by itself from BepInEx\plugins (the Mods tab, which comes with the manager).
        foreach (var meta in (catalog ?? Array.Empty<ModMeta>()).Where(m => m.Id != ManagerService.ManagerId && !BepInEx.Unity.IL2CPP.IL2CPPChainloader.Instance.Plugins.ContainsKey(m.Id))
                     .OrderBy(m => m.NameIn(Texts.Language.ToString()), StringComparer.CurrentCultureIgnoreCase))
        {
            var id = meta.Id;
            var installed = ModLoader.State.Find(id);
            var release = CatalogClient.ReleaseFor(meta, gameVersion);
            var update = updates.FirstOrDefault(u => u.Id == id);
            var name = Compose(id + ".name", l => meta.NameIn(l.ToString()));
            if (installed == null && ModLoader.Pending.TryGetValue(id, out var asked) && asked == "removed")
                rows.Add(new CatalogRow(id, name, "", Note(id, "oe.manager.note.removed"), null, null, null, null));
            else if (installed == null && release == null)
                rows.Add(new CatalogRow(id, name, "", Note(id, "oe.manager.note.noRelease"), null, null, null, null));
            else if (installed == null)
                rows.Add(new CatalogRow(id, name, release.Version, Note(id, "oe.manager.note.notInstalled"), null, null,
                    Button("oe.manager.install", () => ManagerService.Install(id)), null));
            else
            {
                // Installed: its switch and Remove (not while others need it), Update when there is one.
                var needed = ModLoader.RequiredBy(id).Count > 0;
                var note = ModLoader.Pending.ContainsKey(id) ? "oe.manager.note.restart"
                    : update != null ? "oe.manager.note.available"
                    : needed ? "oe.manager.note.required" : null;
                rows.Add(new CatalogRow(id, name, installed.Version, Note(id, note, update?.Release.Version),
                    needed ? null : installed.Enabled, needed || busy ? null : on => ManagerService.SetEnabled(id, on),
                    update != null ? Button("oe.manager.update", () => ManagerService.Update(id)) : null,
                    needed || busy ? null : () => ManagerService.Remove(id)));
            }
        }
        ModCatalog.Set("oe.manager.catalog", rows);
    }
}
