using System;
using System.Collections.Generic;
using System.Linq;
using Hex.UI;
using Hex.UI.GeneralKit;
using Nobirds.OldenEra.Modding.Common;
using static Nobirds.OldenEra.Modding.Common.UiLayout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Settings;

// The Mods window's rows of the Installed and Catalog tabs (made from the Load screen's save row), the Catalog's filters
// and sorting, the bottom left group.
public partial class Plugin
{
    // The mods installed (the manager is apart, on top) and the catalog's (from a source), as the window lists them.
    static List<ModEntry> Installed() => ModCatalog.Mods.Where(m => m.Installed != null)
        .OrderBy(m => Texts.Get(m.NameKey), StringComparer.CurrentCultureIgnoreCase).ToList();

    static List<ModEntry> Catalog() => ModCatalog.Mods.Where(m => m.SourceNameKey != null)
        .OrderBy(m => m.Installed != null).ThenBy(m => Texts.Get(m.NameKey), StringComparer.CurrentCultureIgnoreCase).ToList();

    List<ModEntry> InstalledShown() => Installed().Where(m => Matches(m, filterText)).ToList();

    // The text typed matches a mod: its name, description or author.
    static bool Matches(ModEntry mod, string text) =>
        string.IsNullOrWhiteSpace(text) || new[] { Texts.Get(mod.NameKey), mod.DescriptionKey != null ? Texts.Get(mod.DescriptionKey) : "", mod.Author ?? "" }
            .Any(s => s.IndexOf(text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0);

    void Select(string id, bool on)
    {
        if (on)
            selected.Add(id);
        else
            selected.Remove(id);
    }

    void ShowNothing(bool nothing, string key)
    {
        if (nothingText == null)
            return;
        nothingText.gameObject.SetActive(nothing);
        if (nothing)
            SetText(nothingText, Texts.Get(key));
    }

    // ---------- Installed ----------

    void FillInstalled()
    {
        var mods = InstalledShown();
        if (ModCatalog.Manager is { } manager)
            ModRow(manager, isManager: true);
        foreach (var mod in mods)
            ModRow(mod, isManager: false);
        ShowNothing(mods.Count == 0, ModCatalog.Mods.Any(m => m.Installed != null) ? W + "nothing" : W + "noneInstalled");

        // The bottom left: select all (the mods listed), how many are selected, the actions on them.
        selectAll?.gameObject.SetActive(true);
        selectAll?.ShowValue(mods.Count > 0 && mods.All(m => selected.Contains(m.Id)));
        if (selectAllText != null)
        {
            selectAllText.gameObject.SetActive(true);
            SetText(selectAllText, string.Format(Texts.Get(W + "selectAll"), selected.Count));
        }
        var chosen = mods.Where(m => selected.Contains(m.Id)).ToList();
        var ids = chosen.Select(m => m.Id).ToList();
        // A mod BepInEx loaded itself (ByHand) is switched like the others but never updated nor removed here.
        BottomButton(W + "enable", ModCatalog.SetEnabled != null && chosen.Any(m => m.Enabled == false && !m.RequiredByOthers), () => ModCatalog.SetEnabled?.Invoke(ids, true));
        BottomButton(W + "disable", ModCatalog.SetEnabled != null && chosen.Any(m => m.Enabled == true && !m.RequiredByOthers), () => ModCatalog.SetEnabled?.Invoke(ids, false));
        var updatable = chosen.Where(m => m.HasUpdate && !m.ByHand).Select(m => m.Id).ToList();
        BottomButton(W + "update", ModCatalog.Update != null && updatable.Count > 0, () => ModCatalog.Update?.Invoke(updatable));
        var updateAll = ModCatalog.UpdateAll != null && (ModCatalog.Mods.Any(m => m.HasUpdate) || ModCatalog.Manager?.HasUpdate == true);
        BottomButton(W + "updateAll", updateAll, () => ModCatalog.UpdateAll?.Invoke());
        var removable = chosen.Where(m => !m.RequiredByOthers && !m.ByHand).Select(m => m.Id).ToList();
        if (deleteTemplate != null)
        {
            var remove = Copy(deleteTemplate, bottomGroup).GetComponent<BhGenericButton>();
            remove.name = "OE_RemoveSelected";
            remove.gameObject.SetActive(true);
            Bind(remove, null, () =>
            {
                ModCatalog.Remove?.Invoke(removable);
                selected.ExceptWith(removable);
            }, true);
            if (ModCatalog.Remove == null || removable.Count == 0)
                GreyOut(remove, true);
        }
    }

    // A row of the game's save list (SaveRow): check box, picture, name, tag chips; on the right the catalog, the version,
    // the note; on the Installed tab the update (to the new version), the switch and the remove cross. The manager: its
    // update only.
    void ModRow(ModEntry mod, bool isManager, bool catalog = false)
    {
        var row = NewRow("OE_Row_" + mod.Id, (catalog ? catalogPick : installedPick) == mod.Id, () =>
        {
            if (catalog)
                catalogPick = mod.Id;
            else
                installedPick = mod.Id;
            shotIndex = 0;
        });
        if (row == null)
            return;
        if (!catalog && !isManager)
        {
            row.Check(selected.Contains(mod.Id), value => Guard("check", () =>
            {
                if (!filling)
                    Redraw(() => Select(mod.Id, value));
            }));
        }
        // The row's picture is its catalog's icon (the screenshots are the details'), else its first letter.
        RowPicture(row, CatalogIcon(mod.SourceNameKey), Texts.Get(mod.NameKey));
        RowName(row, Texts.Get(mod.NameKey), mod.TagKeys);
        if (catalog)
        {
            RowInfo(row, mod.SourceNameKey != null ? Texts.Get(mod.SourceNameKey) : "", mod.Available ?? mod.Installed ?? "",
                mod.Installed != null ? Texts.Get(W + "installed") : mod.NoteKey != null ? Texts.Get(mod.NoteKey) : "");
            return;
        }
        // The catalog it comes from on top, "by hand" when none; the version; the note (unless it says the same).
        var origin = mod.SourceNameKey != null ? Texts.Get(mod.SourceNameKey) : Texts.Get(W + "byHand");
        var note = mod.NoteKey != null ? Texts.Get(mod.NoteKey) : "";
        RowInfo(row, origin, mod.Installed ?? "", string.Equals(note, origin, StringComparison.CurrentCultureIgnoreCase) ? "" : note);

        // The controls in one group, vertically centred, just left of the information on the right.
        var controls = NewRect("OE_Controls", row.Root.transform);
        controls.anchorMin = controls.anchorMax = controls.pivot = new Vector2(1f, 0.5f);
        controls.anchoredPosition = new Vector2(InfoLeft(row) - 24f, 0f);
        controls.sizeDelta = new Vector2(330f, 60f);
        HRow(controls.gameObject, 10f, TextAnchor.MiddleRight);
        // No update and no cross for a mod BepInEx loaded itself (ByHand).
        if (mod.HasUpdate && !mod.ByHand && ModCatalog.Update != null && textButtonTemplate != null)
            NewUpdateButton(controls, mod.Available, () => ModCatalog.Update?.Invoke(new[] { mod.Id }));
        if (!isManager && mod.Enabled is { } on && !mod.RequiredByOthers && ModCatalog.SetEnabled != null && switchSource != null)
            NewSwitch(controls, on, value => Guard("switch " + mod.Id, () => ModCatalog.SetEnabled?.Invoke(new[] { mod.Id }, value)));
        if (!isManager && !mod.RequiredByOthers && !mod.ByHand && ModCatalog.Remove != null && crossSource != null)
            NewCross(controls, () => ModCatalog.Remove?.Invoke(new[] { mod.Id }));
    }

    // A row of the list, picked or not; a click picks it.
    SaveRow NewRow(string name, bool picked, Action pick)
    {
        var row = SaveRow.Make(listContent, name);
        if (row == null)
            return null;
        row.Selected(picked);
        row.OnClick(() => Guard("click", () => Redraw(pick)));
        return row;
    }

    // Where the information on the right begins (from the row's right edge, negative): its widest line's text.
    static float InfoLeft(SaveRow row)
    {
        var rowRect = row.Root.transform.Cast<RectTransform>();
        var left = 0f;
        foreach (var text in new TMP_Text[] { row.View.lastSaveHeader, row.View.lastSaveTime, row.View.lastSaveGameTime })
            if (text != null && text.gameObject.activeSelf && !string.IsNullOrEmpty(text.text))
            {
                // The lines are the row's children, their text aligned to their right edge.
                var right = text.rectTransform.localPosition.x + text.rectTransform.rect.xMax - rowRect.rect.xMax;
                left = Math.Min(left, right - text.GetPreferredValues(text.text).x);
            }
        return left;
    }

    // The row's picture: the catalog's icon; with none, the name's first letter in the icon frame, in the name's own font
    // and colour (as the game's campaign rows show their act's numeral).
    void RowPicture(SaveRow row, string iconPath, string name)
    {
        var texture = Picture(iconPath);
        row.Icon(texture);
        var icon = row.View.sessionIcon;
        var title = row.View.sessionName;
        var letter = string.IsNullOrWhiteSpace(name) ? "" : name.Trim().Substring(0, 1).ToUpperInvariant();
        if (texture != null || icon == null || title == null || letter.Length == 0)
            return;
        var text = NewRect("OE_Letter", icon.transform).gameObject.AddComponent<TextMeshProUGUI>();
        Stretch(text.rectTransform);
        text.font = title.font;
        text.fontSharedMaterial = title.fontSharedMaterial;
        text.color = title.color;
        text.fontSize = 52f;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        text.text = letter;
    }

    // The icon of the catalog a mod comes from (its name key is the source's), or null.
    static string CatalogIcon(string sourceNameKey) =>
        sourceNameKey == null ? null : ModCatalog.Sources.FirstOrDefault(s => s.TitleKey == sourceNameKey)?.IconPath;

    // The name and the game's chips for the first four tags (in the colour of the save list's "Template" chip), on their
    // own line under the name.
    static void RowName(SaveRow row, string name, IReadOnlyList<string> tagKeys)
    {
        row.Name(name);
        row.Tags(tagKeys?.Where(k => k != null).Take(4).Select(k => (Texts.Get(k), ChipColor)).ToList());
        row.TagsUnderName();
    }

    static readonly Color ChipColor = new(0.33f, 0.19f, 0.40f, 1f);

    // A tag chip of the details panel: its text, sized by the game (BhTag.ShowText), in the chips' colours.
    static void ShowChip(BhTag chip, string key) =>
        GameUi.ShowChip(chip, Texts.Get(key), ChipColor, new Color(0.92f, 0.88f, 0.80f, 1f));

    static void RowInfo(SaveRow row, string header, string value, string note) =>
        row.Info(string.IsNullOrEmpty(header) ? "" : $"<color=#A5A5A5>{header}</color>", value, note);
    // ---------- Catalog ----------

    // The tags and the sources of the catalog's mods, in the order of their names: the filters' options.
    static List<string> TagKeys() => ModCatalog.Mods.Where(m => m.SourceNameKey != null).SelectMany(m => m.TagKeys ?? Array.Empty<string>())
        .Where(k => k != null).Distinct().OrderBy(Texts.Get, StringComparer.CurrentCultureIgnoreCase).ToList();

    static List<string> SourceKeys() => ModCatalog.Mods.Select(m => m.SourceNameKey).Where(k => k != null).Distinct()
        .OrderBy(Texts.Get, StringComparer.CurrentCultureIgnoreCase).ToList();

    void FillCatalog()
    {
        // What is ticked filters (any of the ticked; none ticked: all). A filter with nothing to offer (no tags yet) and
        // its label are hidden.
        var tagKeys = TagKeys();
        var sourceKeys = SourceKeys();
        tagBoxKeys = SetUpFilter(tagBox, tagKeys, tagBoxKeys);
        sourceBoxKeys = SetUpFilter(sourceBox, sourceKeys, sourceBoxKeys);
        tagBox?.gameObject.SetActive(tagKeys.Count > 0);
        tagLabel?.SetActive(tagKeys.Count > 0);
        var tags = GameUi.Ticked(tagBox, tagKeys);
        var sources = GameUi.Ticked(sourceBox, sourceKeys);
        sortOptions = SetSortOptions(sortDropdown, sortOptions);

        var mods = Catalog().Where(m => Matches(m, searchText) && (tags.Count == 0 || (m.TagKeys?.Any(tags.Contains) ?? false)) &&
                                        (sources.Count == 0 || sources.Contains(m.SourceNameKey))).ToList();
        var name = (Func<ModEntry, string>)(m => Texts.Get(m.NameKey));
        mods = sortMode switch
        {
            0 => mods.OrderBy(name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            2 => mods.OrderBy(m => m.Installed == null).ThenBy(name, StringComparer.CurrentCultureIgnoreCase).ToList(),
            _ => mods.OrderBy(m => m.Installed != null).ThenBy(name, StringComparer.CurrentCultureIgnoreCase).ToList(),
        };
        foreach (var mod in mods)
            ModRow(mod, isManager: false, catalog: true);
        ShowNothing(mods.Count == 0, W + "nothing");
    }

    // The game's filter set up with these names (localization keys: it translates them), again only when they changed
    // (setting it up unticks them all). Returns what they are now.
    string SetUpFilter(Hex.UI.SaveLoad.BhFilter box, List<string> keys, string was)
    {
        if (box == null)
            return was;
        var now = $"{Texts.Language}|{string.Join("|", keys)}";
        if (now == was)
            return was;
        GameUi.SetUpFilter(box, keys, () => Guard("filter", () => windowDirty = true));
        return now;
    }

    // The sorting dropdown's options (by name, not installed first, installed first), set again on a language change
    // only (setting them closes a list the player has open).
    string SetSortOptions(BhDropdown dropdown, string was)
    {
        if (dropdown == null)
            return was;
        var now = $"{Texts.Language}";
        if (now == was)
            return was;
        GameUi.SetDropdownTexts(dropdown, Enumerable.Range(0, 3).Select(i => Texts.Get($"{W}sort.{i}")), sortMode);
        return now;
    }

    // A label before a filter: a copy of the filter row's own label.
    GameObject FilterLabel(GameObject source, string key, int index)
    {
        if (source == null)
            return null;
        var label = UnityEngine.Object.Instantiate(source, source.transform.parent, false);
        label.name = "OE_Label";
        label.transform.SetSiblingIndex(index);
        var text = label.GetComponent<TMP_Text>();
        text.enableWordWrapping = false;
        SetText(text, Texts.Get(key));
        Size(label, text.GetPreferredValues(Texts.Get(key)).x + 4f, -1f);
        label.SetActive(true);
        return label;
    }

    // ---------- the bottom left group ----------

    void ClearBottom()
    {
        if (bottomGroup == null)
            return;
        for (var i = bottomGroup.childCount - 1; i >= 0; i--)
            if (bottomGroup.GetChild(i).name.StartsWith("OE_", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(bottomGroup.GetChild(i).gameObject);
        selectAll?.gameObject.SetActive(false);
        selectAllText?.gameObject.SetActive(false);
    }

    void BottomButton(string key, bool available, Action click, float width = 150f)
    {
        if (textButtonTemplate == null || bottomGroup == null)
            return;
        var button = TextButton(bottomGroup, Texts.Get(key), width, click);
        button.name = "OE_" + key.Substring(W.Length);
        if (!available)
            GreyOut(button, true);
    }
}
