using System;
using System.Collections.Generic;
using System.Linq;
using Hex.UI;
using Hex.UI.GeneralKit;
using OldenEra.Modding.Common;
using static OldenEra.Modding.Common.UiLayout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// The Mods window's rows of the Installed and Catalog tabs (made from the Load screen's save row), the Catalog's filters
// and sorting, the bottom left group.
public partial class Plugin
{
    const float ModRowHeight = 96f;

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

    // A row made from the game's save row: check box, picture, name, tag chips; on the right the catalog, the version,
    // the note; on the Installed tab the update (to the new version), the switch and the remove cross. The manager: its
    // update only.
    void ModRow(ModEntry mod, bool isManager, bool catalog = false)
    {
        var row = Copy(rowSource, listContent);
        row.name = "OE_Row_" + mod.Id;
        row.SetActive(true);
        var picked = (catalog ? catalogPick : installedPick) == mod.Id;
        RowBase(row, picked, () =>
        {
            if (catalog)
                catalogPick = mod.Id;
            else
                installedPick = mod.Id;
            shotIndex = 0;
        });
        var checkable = !catalog && !isManager;
        if (row.transform.Find("Toggle")?.GetComponent<BhToggle>() is { } check)
        {
            check.gameObject.SetActive(checkable);
            if (checkable)
            {
                GameUi.Check(check, selected.Contains(mod.Id), value => Guard("check", () =>
                {
                    if (!filling)
                        Redraw(() => Select(mod.Id, value));
                }));
            }
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
        var controls = NewRect("OE_Controls", row.transform);
        controls.anchorMin = controls.anchorMax = controls.pivot = new Vector2(1f, 0.5f);
        controls.anchoredPosition = new Vector2(InfoLeft(row) - 24f, 0f);
        controls.sizeDelta = new Vector2(330f, 60f);
        HRow(controls.gameObject, 10f, TextAnchor.MiddleRight);
        // No update and no cross for a mod BepInEx loaded itself (ByHand).
        if (mod.HasUpdate && !mod.ByHand && ModCatalog.Update != null && textButtonTemplate != null)
            TextButton(controls, string.Format(Texts.Get(W + "updateTo"), mod.Available), 170f, () => ModCatalog.Update?.Invoke(new[] { mod.Id }));
        if (!isManager && mod.Enabled is { } on && !mod.RequiredByOthers && ModCatalog.SetEnabled != null && switchSource != null)
            NewSwitch(controls, on, value => Guard("switch " + mod.Id, () => ModCatalog.SetEnabled?.Invoke(new[] { mod.Id }, value)));
        if (!isManager && !mod.RequiredByOthers && !mod.ByHand && ModCatalog.Remove != null && crossSource != null)
            NewCross(controls, () => ModCatalog.Remove?.Invoke(new[] { mod.Id }));
    }

    // The row's own parts laid out for a mod: as high as it needs, the check box and the picture vertically centred at
    // the left, the name above the middle (the tags under it, RowName), the three lines of information on the right
    // around the middle; its selected look, the cloud and the map's icons off, a click picks it.
    void RowBase(GameObject row, bool picked, Action pick)
    {
        var t = row.transform;
        Hide(t, "CloudOn", "CloudOff", "InfoIcons", "CampaignInfo");
        Hide(t.Find("HeaderGroup"), "RenameBtn");
        Size(row, -1f, ModRowHeight);
        var rowRect = row.transform.Cast<RectTransform>();
        PinLeft(t.Find("Toggle"), rowRect, 0f);
        PinLeft(t.Find("IconFrame"), rowRect, 0f);
        PinLeft(t.Find("HeaderGroup"), rowRect, 15f);
        PinRight(t.Find("LastSaveHeader"), rowRect, 27f);
        PinRight(t.Find("LastSaveTime"), rowRect, 0f);
        PinRight(t.Find("LastSaveGameTime"), rowRect, -27f);
        // The row's backs (default, hovered, selected) are the save row's fixed 122 for its 110 (6 over each edge, their
        // pictures' own margins making the gaps between rows): as much over ours, centred, or the rows run together.
        foreach (var backName in new[] { "DefaultBack", "HoveredBack", "SelectedBack" })
            if (t.Find(backName) is { } back)
            {
                var rect = back.Cast<RectTransform>();
                var width = rect.rect.width;
                rect.anchorMin = new Vector2(rect.anchorMin.x, 0.5f);
                rect.anchorMax = new Vector2(rect.anchorMax.x, 0.5f);
                rect.pivot = new Vector2(rect.pivot.x, 0.5f);
                rect.sizeDelta = new Vector2(rect.anchorMin.x == rect.anchorMax.x ? width : rect.sizeDelta.x, ModRowHeight + 12f);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, 0f);
                // The selected back's frame: 5 over the save row's 110.
                if (back.Find("Frame") is { } frame)
                {
                    var frameRect = frame.Cast<RectTransform>();
                    if (frameRect.anchorMin.y == frameRect.anchorMax.y)
                        frameRect.sizeDelta = new Vector2(frameRect.sizeDelta.x, ModRowHeight + 5f);
                }
            }
        // The row's check box drawn as the bottom group's (the save row's is a faint outline until hovered).
        if (t.Find("Toggle/Background")?.GetComponent<Image>() is { } box && selectAll?.transform.Find("Background")?.GetComponent<Image>() is { } shown)
            box.color = shown.color;
        if (t.Find("SelectedBack") is { } selectedBack)
            selectedBack.gameObject.SetActive(picked);
        if (t.Find("HoveredBack")?.GetComponent<CanvasGroup>() is { } hovered)
            hovered.alpha = 0f;
        if (row.GetComponent<CanvasGroup>() is { } group)
        {
            group.alpha = 1f;
            group.interactable = group.blocksRaycasts = true;
        }
        Clickable(row, () => Redraw(pick));
    }

    // A part of the row kept where it is across, pinned to the row's left (or right) edge at a height from its middle
    // (anchors and pivot middle; the row's own anchors are the save row's, for its 110).
    static void PinLeft(Transform part, RectTransform row, float y)
    {
        if (part == null)
            return;
        var rect = part.Cast<RectTransform>();
        var size = rect.rect.size;
        var left = rect.localPosition.x + rect.rect.xMin - row.rect.xMin;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(left, y);
    }

    static void PinRight(Transform part, RectTransform row, float y)
    {
        if (part == null)
            return;
        var rect = part.Cast<RectTransform>();
        var size = rect.rect.size;
        var right = row.rect.xMax - (rect.localPosition.x + rect.rect.xMax);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(-right, y);
    }

    // Where the information on the right begins (from the row's right edge, negative): its widest line's text.
    static float InfoLeft(GameObject row)
    {
        var left = 0f;
        foreach (var name in new[] { "LastSaveHeader", "LastSaveTime", "LastSaveGameTime" })
            if (row.transform.Find(name)?.GetComponent<TMP_Text>() is { } text && !string.IsNullOrEmpty(text.text))
                left = Math.Min(left, text.rectTransform.anchoredPosition.x - text.GetPreferredValues(text.text).x);
        return left;
    }

    // The row's picture: the catalog's icon whole in the save's icon frame; with none, the name's first letter in the
    // frame, in the name's own font and colour (as the game's campaign rows show their act's numeral).
    void RowPicture(GameObject row, string iconPath, string name)
    {
        var icon = row.transform.Find("IconFrame/Icon");
        if (icon == null)
            return;
        // The save's icon is not ours.
        if (icon.GetComponent<Image>() is { } image)
            image.enabled = false;
        if (Picture(iconPath) is { } texture)
        {
            var picture = NewRect("OE_Shot", icon).gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            picture.texture = texture;
            Pictures.FitInside(picture.rectTransform, texture, icon.Cast<RectTransform>().rect.size);
            return;
        }
        var title = row.transform.Find("HeaderGroup/SessionName")?.GetComponent<TMP_Text>();
        var letter = string.IsNullOrWhiteSpace(name) ? "" : name.Trim().Substring(0, 1).ToUpperInvariant();
        if (title == null || letter.Length == 0)
            return;
        var text = NewRect("OE_Letter", icon).gameObject.AddComponent<TextMeshProUGUI>();
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

    void RowName(GameObject row, string name, IReadOnlyList<string> tagKeys)
    {
        var group = row.transform.Find("HeaderGroup");
        if (group == null)
            return;
        if (group.Find("SessionName")?.GetComponent<TMP_Text>() is { } title)
            SetText(title, name);
        // The game's chips: the first four tags, on their own line under the name (the save row has them after its
        // name; ours read better under it).
        var chips = group.GetComponentsInChildren<BhTag>(true).ToList();
        var line = row.transform.Find("OE_Tags");
        if (line == null)
        {
            var rect = NewRect("OE_Tags", row.transform);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 24f);
            rect.anchoredPosition = new Vector2(group.Cast<RectTransform>().anchoredPosition.x, -17f);
            HRow(rect.gameObject, 6f, TextAnchor.MiddleLeft);
            line = rect;
        }
        // The row has two chips; more are copies of the first, up to four tags.
        while (chips.Count > 0 && chips.Count < 4)
            chips.Add(UnityEngine.Object.Instantiate(chips[0].gameObject, line, false).GetComponent<BhTag>());
        foreach (var chip in chips)
            chip.transform.SetParent(line, false);
        for (var i = 0; i < chips.Count; i++)
        {
            var key = tagKeys != null && i < tagKeys.Count ? tagKeys[i] : null;
            chips[i].gameObject.SetActive(key != null);
            if (key != null)
                ShowChip(chips[i], key);
        }
    }

    // A tag chip: its text, sized by the game (BhTag.ShowText), in the colours of the save list's "Template" chip (the
    // row prefab's own chip is a bare pink one the save list recolours by the session's tag).
    static void ShowChip(BhTag chip, string key) =>
        GameUi.ShowChip(chip, Texts.Get(key), new Color(0.33f, 0.19f, 0.40f, 1f), new Color(0.92f, 0.88f, 0.80f, 1f));

    static void RowInfo(GameObject row, string header, string value, string note)
    {
        var t = row.transform;
        if (t.Find("LastSaveHeader")?.GetComponent<TMP_Text>() is { } h)
            SetText(h, $"<color=#A5A5A5>{header}</color>");
        if (t.Find("LastSaveTime")?.GetComponent<TMP_Text>() is { } v)
            SetText(v, value);
        if (t.Find("LastSaveGameTime")?.GetComponent<TMP_Text>() is { } n)
            SetText(n, note);
    }

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
