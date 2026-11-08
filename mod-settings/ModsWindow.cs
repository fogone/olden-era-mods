using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hex.GameHub.UICommon;
using Hex.QuickStart;
using Hex.Settings.UI;
using Hex.UI;
using Hex.UI.GeneralKit;
using Hex.UI.MapPreview;
using Hex.UI.SaveLoad;
using OldenEra.Modding.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// The mods' own window (issue #13, #14 "mockup v4", #6): a "Mods" button in the main menu under Additional (a copy of
// the game's Options button) opens a window made of the game's widgets on a canvas of ours: the message box's frame,
// heading, state line and button row (ScMessageBox), the game hub's tabs (BhTab), the input box's text field
// (ScInputBox), the settings' dropdowns and switches (BhLocalizedDropdown, BhSettingsCheckbox), the save list filter's
// check marks (BhToggle), the quick start's map cards and tag chips (BhMapCard, BhDescriptionTag), the settings' small
// buttons and the game's cross. Adding a source asks with the game's own message box and input box.
// Three tabs: Installed (managing: filter, rows with a check mark, switch, update and remove, actions on the selected),
// Catalog (browsing: search, tag and source filters, cards), Sources (the list, add, check); a mod's or a source's
// details on the right. The data and the actions are the mod manager's (ModCatalog, mod-common/ModMenu.cs).
public partial class Plugin
{
    const string ModsButtonName = "OE_ModsButton", WindowName = "OE_ModsWindow", CloseKey = "oe.settings.close", W = "oe.settings.w.";
    // Under the game's 30000 of the dropdown lists (Unity's own), over the menus.
    const int WindowOrder = 29000;
    const float WindowWidth = 3000f, BodyWidth = 2860f, BodyHeight = 1330f, ListWidth = 1860f, DetailsX = 1900f, DetailsWidth = 960f;
    const float PageTop = 90f, PageHeight = BodyHeight - PageTop, RowHeight = 84f, RowSpacing = 6f, CellGap = 20f;
    const float ButtonHeight = 66f, BottomButtonWidth = 440f, CrossScale = 2.2f, CheckScale = 3.5f, DropdownScale = 0.7f;
    const float CardWidth = 440f, CardHeight = 560f, CardScale = 1.5f;
    static readonly string[] CheckEveryValues = { "start", "daily", "weekly", "manual" };

    enum WindowTab { Installed, Catalog, Sources }

    BhNavigationButton modsButton;
    GameObject window, staging;
    Transform windowHeader, windowStatus, windowButtons;
    int windowRevision = -1, nextButtonSearch;
    bool windowDirty;

    // The game's widgets copied (found when the window is built).
    BhGenericButton rowButtonTemplate, crossTemplate;
    BhSettingsCheckbox switchSource;
    BhTab tabTemplate;
    TMP_InputField inputTemplate;
    BhToggle checkTemplate;
    BhDescriptionTag tagTemplate;
    BhMapCard cardTemplate;
    TMP_Text textStyle;

    // The window's parts.
    readonly Dictionary<WindowTab, BhTab> tabs = new();
    readonly Dictionary<WindowTab, GameObject> pages = new();
    Transform installedList, installedHeader, installedActions, catalogGrid, sourcesList, sourcesActions, details, detailsButtons, bigShot;
    TMP_InputField filterInput, searchInput;
    BhLocalizedDropdown tagDropdown, sourceDropdown;
    string tagOptions, sourceOptions;

    // What the player chose.
    WindowTab tab = WindowTab.Installed;
    string filterText = "", searchText = "", tagFilter, sourceFilter, installedPick, catalogPick, sourcePick, bigShotPath;
    readonly HashSet<string> selected = new();
    readonly HashSet<string> screenshotsAsked = new();
    readonly Dictionary<string, Texture2D> textures = new();

    // A game box (the warning, the input box) is open: our window waits hidden under it.
    bool suspended;
    int suspendedAt;
    Action pendingBox;
    int pendingAt;

    // ---------- the button in the main menu, opening ----------

    // Every frame: the button in the main menu while there is a catalog; the window's content when it changed.
    void ShowModsWindow()
    {
        if (ModCatalog.TitleKey == null)
        {
            Logs.Once("Mods window: no catalog (ModCatalog.TitleKey not set by the manager)");
            return;
        }
        Logs.Once($"Mods window: the catalog has {ModCatalog.Mods.Count} mods, {ModCatalog.Sources.Count} sources");
        // Looked for every two seconds, only in the main menu.
        if (modsButton == null && Time.frameCount >= nextButtonSearch)
        {
            nextButtonSearch = Time.frameCount + 120;
            if (InMainMenu())
                AddModsButton();
        }
        if (window == null)
            return;
        if (pendingBox != null && Time.frameCount >= pendingAt)
        {
            var open = pendingBox;
            pendingBox = null;
            Guard("open a game box", open);
        }
        if (suspended)
        {
            WatchGameBoxes();
            return;
        }
        if (!window.activeSelf)
            return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (bigShotPath != null)
                Redraw(() => bigShotPath = null);
            else
                window.SetActive(false);
        }
        else if (windowDirty || windowRevision != ModMenu.Revision)
            Guard("fill", FillWindow);
    }

    void AddModsButton()
    {
        var options = UnityEngine.Object.FindObjectsOfType<BhNavigationButton>()
            .FirstOrDefault(b => b != null && b.name == "OptionsButton" && b.gameObject.activeInHierarchy);
        if (options == null)
        {
            Logs.Once("Mods window: no OptionsButton in the main menu yet");
            return;
        }
        // In the main column under "Additional" (and its submenu), where there is room: the Options / Exit group is the
        // game's to lay out (it puts it back whenever the menu shows; moving it there fought the game, 07.11).
        var additional = UnityEngine.Object.FindObjectsOfType<BhNavigationButton>()
            .FirstOrDefault(b => b != null && b.name == "AdditionallyButton" && b.gameObject.activeInHierarchy);
        if (additional == null)
        {
            Logs.Once("Mods window: no AdditionallyButton in the main menu yet");
            return;
        }
        var column = additional.transform.parent;
        // Buttons left by earlier builds (in this column, or under Options where they used to be): gone first.
        foreach (var parent in new[] { column, options.transform.parent })
            for (var i = parent.childCount - 1; i >= 0; i--)
                if (parent.GetChild(i).name == ModsButtonName)
                    UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        var place = additional.transform.GetSiblingIndex() + 1;
        if (place < column.childCount && column.GetChild(place).GetComponent<Hex.MainMenu.MainMenuSubNavView>() != null)
            place++;
        var copy = UnityEngine.Object.Instantiate(options.gameObject, column, false);
        copy.name = ModsButtonName;
        copy.transform.SetSiblingIndex(place);
        modsButton = copy.GetComponent<BhNavigationButton>();
        SetKey(modsButton.label, TabKey);
        modsButton.MenuClick = (Il2CppSystem.Action)(Action)OpenWindow;
        Logs.Info($"Mods button: under {additional.name} in {column.name}");
    }

    void OpenWindow()
    {
        try
        {
            if (window == null && !BuildWindow())
                return;
            window.SetActive(true);
            suspended = false;
            screenshotsAsked.Clear();
            windowRevision = -1;
            FillWindow();
        }
        catch (Exception e) { Log.LogError($"mods window: {e}"); }
    }

    // ---------- building ----------

    // The window: a canvas of ours over the menu, the game's message box (its blocker and its container) copied onto
    // it, a body of our layout in place of its text.
    bool BuildWindow()
    {
        var box = Scene.Find<ScMessageBox>();
        var master = box != null ? box.transform.Find("ContainerMaster") : null;
        if (master == null)
        {
            Log.LogWarning("mods window: the game's message box is not there");
            return false;
        }
        if (GameObject.Find(WindowName) is { } old)
            UnityEngine.Object.Destroy(old);
        window = new GameObject(WindowName, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        UnityEngine.Object.DontDestroyOnLoad(window);
        var canvas = window.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = WindowOrder;
        var scaler = window.AddComponent<CanvasScaler>();
        if (box.GetComponent<CanvasScaler>() is { } gameScaler)
        {
            scaler.uiScaleMode = gameScaler.uiScaleMode;
            scaler.referenceResolution = gameScaler.referenceResolution;
            scaler.screenMatchMode = gameScaler.screenMatchMode;
            scaler.matchWidthOrHeight = gameScaler.matchWidthOrHeight;
        }
        window.AddComponent<GraphicRaycaster>();
        // Copies are made here first, inactive (their scripts do not wake up before they are stripped and placed).
        staging = new GameObject("OE_Staging", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        staging.transform.SetParent(window.transform, false);
        staging.SetActive(false);
        if (box.transform.Find("RaycastBlocker") is { } blocker)
            UnityEngine.Object.Instantiate(blocker.gameObject, window.transform, false).SetActive(true);
        var container = UnityEngine.Object.Instantiate(master.gameObject, window.transform, false).transform;
        container.gameObject.SetActive(true);
        // The box's own fade (its container's group); the buttons' groups are theirs (their grey and glow layers).
        foreach (var group in container.GetComponentsInChildren<CanvasGroup>(true).Where(g => g.GetComponentInParent<BhGenericButton>(true) == null))
        {
            group.alpha = 1f;
            group.interactable = group.blocksRaycasts = true;
        }
        var inner = container.Find("Container");
        // The box's background is see-through (a message over the game): ours is a window over the menu, opaque.
        foreach (var image in inner.Find("popupFrame").GetComponentsInChildren<Image>(true))
            image.color = new Color(image.color.r, image.color.g, image.color.b, 1f);
        windowHeader = inner.Find("GameObject/Header");
        windowStatus = inner.Find("Notification");
        windowButtons = inner.Find("Buttons");
        textStyle = windowStatus.GetComponent<TMP_Text>();
        if (inner.Find("ContainerLabelSCALE") is { } label)
            label.gameObject.SetActive(false);
        // Wider than the box: its parent lays it out at its own width (the frame follows it).
        inner.Cast<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, WindowWidth);
        foreach (var own in windowButtons.GetComponentsInChildren<BhGenericButton>(true).ToList())
            UnityEngine.Object.DestroyImmediate(own.gameObject);
        // The box's row does not size its buttons: its layout sizes them now, by their LayoutElement.
        if (windowButtons.GetComponent<HorizontalLayoutGroup>() is { } buttonsRow)
        {
            buttonsRow.childControlWidth = buttonsRow.childControlHeight = true;
            buttonsRow.childForceExpandWidth = buttonsRow.childForceExpandHeight = false;
            buttonsRow.childAlignment = TextAnchor.MiddleCenter;
            buttonsRow.spacing = 40f;
        }
        if (windowButtons.GetComponent<LayoutElement>() is { } buttonsSize)
            buttonsSize.preferredWidth = buttonsSize.minWidth = 1600f;
        var buttonsRect = windowButtons.Cast<RectTransform>();
        buttonsRect.sizeDelta = new Vector2(1600f, buttonsRect.sizeDelta.y);

        FindTemplates();
        if (rowButtonTemplate == null)
        {
            Log.LogWarning("mods window: no button to copy (the settings screen's Reset)");
            UnityEngine.Object.Destroy(window);
            window = null;
            return false;
        }

        // The body: under the state line, our own layout (absolute places, each part laying out its own content).
        var body = NewRect("OE_ModsBody", inner);
        body.SetSiblingIndex(windowStatus.GetSiblingIndex() + 1);
        Size(body.gameObject, BodyWidth, BodyHeight);
        body.sizeDelta = new Vector2(BodyWidth, BodyHeight);

        var tabBar = NewRect("Tabs", body);
        Place(tabBar, 0f, 0f, ListWidth, 72f);
        HRow(tabBar.gameObject, 24f, TextAnchor.MiddleLeft);
        tabs.Clear();
        tabButtons.Clear();
        foreach (var which in new[] { WindowTab.Installed, WindowTab.Catalog, WindowTab.Sources })
            NewTab(tabBar, which);

        pages.Clear();
        foreach (var which in new[] { WindowTab.Installed, WindowTab.Catalog, WindowTab.Sources })
        {
            var page = NewRect("Page_" + which, body);
            Place(page, 0f, PageTop, ListWidth, PageHeight);
            pages[which] = page.gameObject;
        }

        // Installed: the filter, the header (select all, how many selected), the rows, the actions on the selected.
        var installed = pages[WindowTab.Installed].transform;
        filterInput = NewInput(installed, W + "filter", 0f, 0f, 760f, value => Redraw(() => filterText = value ?? ""));
        installedHeader = NewRect("Header", installed);
        Place(installedHeader.Cast<RectTransform>(), 0f, 76f, ListWidth, 64f);
        installedList = NewScroll(installed, 0f, 150f, ListWidth, PageHeight - 150f - ButtonHeight - 30f, false);
        installedActions = NewRect("Actions", installed);
        Place(installedActions.Cast<RectTransform>(), 0f, PageHeight - ButtonHeight - 10f, ListWidth, ButtonHeight + 10f);
        HRow(installedActions.gameObject, CellGap, TextAnchor.MiddleLeft);

        // Catalog: the cards, the filters over them (last: their dropdown lists draw over the cards).
        var catalog = pages[WindowTab.Catalog].transform;
        catalogGrid = NewScroll(catalog, 0f, 86f, ListWidth, PageHeight - 86f, true);
        searchInput = NewInput(catalog, W + "search", 0f, 0f, 760f, value => Redraw(() => searchText = value ?? ""));
        tagDropdown = NewDropdown(catalog, 790f, 0f, i => Redraw(() => tagFilter = i <= 0 ? null : TagKeys().ElementAtOrDefault(i - 1)));
        sourceDropdown = NewDropdown(catalog, 1330f, 0f, i => Redraw(() => sourceFilter = i <= 0 ? null : SourceKeys().ElementAtOrDefault(i - 1)));

        // Sources: the list, Add by URL and Add from GitHub under it.
        var sources = pages[WindowTab.Sources].transform;
        sourcesList = NewScroll(sources, 0f, 0f, ListWidth, PageHeight - ButtonHeight - 30f, false);
        sourcesActions = NewRect("Actions", sources);
        Place(sourcesActions.Cast<RectTransform>(), 0f, PageHeight - ButtonHeight - 10f, ListWidth, ButtonHeight + 10f);
        HRow(sourcesActions.gameObject, CellGap, TextAnchor.MiddleLeft);

        // The details on the right: a scrolling column, its buttons under it.
        details = NewScroll(body, DetailsX, 0f, DetailsWidth, BodyHeight - ButtonHeight - 30f, false);
        if (details.GetComponent<VerticalLayoutGroup>() is { } column)
        {
            column.spacing = 18f;
            column.childAlignment = TextAnchor.UpperLeft;
        }
        detailsButtons = NewRect("DetailsButtons", body);
        Place(detailsButtons.Cast<RectTransform>(), DetailsX, BodyHeight - ButtonHeight - 10f, DetailsWidth, ButtonHeight + 10f);
        HRow(detailsButtons.gameObject, CellGap, TextAnchor.MiddleLeft);

        // A screenshot big, over the body (a click closes it).
        bigShot = NewRect("BigShot", body);
        Place(bigShot.Cast<RectTransform>(), 0f, 0f, BodyWidth, BodyHeight);
        bigShot.gameObject.SetActive(false);

        Logs.Info($"Mods window: built from the game's message box; tabs {(tabTemplate != null ? tabTemplate.name : "none")}, input {(inputTemplate != null)}, " +
                  $"check {(checkTemplate != null)}, tag {(tagTemplate != null)}, card {(cardTemplate != null)}, dropdown {(dropdownTemplate != null)}, switch {(switchSource != null)}");
        return true;
    }

    // The game's widgets to copy, from the scenes the main menu has loaded (or their prefabs).
    void FindTemplates()
    {
        // The rows' and the bottom's buttons: the settings screen's own small ones (its Reset); the cross: the game's.
        rowButtonTemplate = Scene.Find<ScSettings>()?.transform.Find("ResetButton")?.GetComponent<BhGenericButton>();
        crossTemplate = Resources.FindObjectsOfTypeAll<BhGenericButton>().FirstOrDefault(b => b != null && b.name == "CROSS BUTTON" && b.gameObject.scene.IsValid());
        // The switches: the settings tab's own (a copy of the game's switch, off).
        switchSource = switchTemplate != null ? switchTemplate.GetComponentInChildren<BhSettingsCheckbox>(true) : null;
        // The tabs: the game hub's (the profile's Statistics / Match history, the leaderboard's).
        var allTabs = Resources.FindObjectsOfTypeAll<BhTab>().Where(t => t != null && t.label != null && t.selectedFon != null && t.notSelectedFon != null).ToList();
        tabTemplate = allTabs.FirstOrDefault(t => t.gameObject.scene.IsValid() && t.transform.parent != null && t.transform.parent.parent?.name == "window")
                      ?? allTabs.FirstOrDefault(t => t.gameObject.scene.IsValid()) ?? allTabs.FirstOrDefault();
        // The text field: the input box's (the messageboxes scene, always loaded).
        inputTemplate = Scene.Find<ScInputBox>()?.inputField;
        // The check marks: the save list filter's.
        var checks = Resources.FindObjectsOfTypeAll<BhToggle>().Where(t => t != null && t.toggle != null).ToList();
        checkTemplate = checks.FirstOrDefault(t => t.GetComponentInParent<BhFilterElement>(true) != null) ?? checks.FirstOrDefault();
        // The tag chips: the quick start's map details' (and the save info's).
        tagTemplate = Resources.FindObjectsOfTypeAll<BhDescriptionTag>().FirstOrDefault(t => t != null && t.label != null);
        // The cards: the quick start's map cards.
        cardTemplate = Resources.FindObjectsOfTypeAll<BhMapCard>().FirstOrDefault(c => c != null && c.minimapImage != null && c.title != null);
    }

    // ---------- filling ----------

    void Redraw(Action change)
    {
        change();
        windowDirty = true;
    }

    void FillWindow()
    {
        windowRevision = ModMenu.Revision;
        windowDirty = false;
        SetText(windowHeader, Texts.Get(ModCatalog.TitleKey ?? TabKey));
        var status = ModCatalog.StatusKey != null ? Texts.Get(ModCatalog.StatusKey) : "";
        if (ModCatalog.Busy)
            status = status.Length > 0 ? $"{status} · {Texts.Get(W + "busy")}" : Texts.Get(W + "busy");
        SetText(windowStatus, status);

        var installedIds = Installed().Select(m => m.Id).ToHashSet();
        selected.RemoveWhere(id => !installedIds.Contains(id));
        if (installedPick != null && !installedIds.Contains(installedPick) && installedPick != ModCatalog.Manager?.Id)
            installedPick = null;
        if (catalogPick != null && Catalog().All(m => m.Id != catalogPick))
            catalogPick = null;
        if (sourcePick != null && ModCatalog.Sources.All(s => s.Url != sourcePick))
            sourcePick = null;

        foreach (var which in new[] { WindowTab.Installed, WindowTab.Catalog, WindowTab.Sources })
        {
            var label = string.Format(Texts.Get(W + "tab." + which.ToString().ToLowerInvariant()), which switch
            {
                WindowTab.Installed => Installed().Count,
                WindowTab.Catalog => Catalog().Count,
                _ => ModCatalog.Sources.Count,
            });
            if (tabs.TryGetValue(which, out var hubTab))
            {
                SetText(hubTab.label, label);
                hubTab.SetAvailable(true);
                hubTab.ShowSelected(which == tab);
            }
            else if (tabButtons.TryGetValue(which, out var button))
            {
                SetText(button.label, label);
                button.SetAvailable(which != tab);
            }
        }
        foreach (var (which, page) in pages)
            page.SetActive(which == tab);

        switch (tab)
        {
            case WindowTab.Installed:
                FillInstalled();
                break;
            case WindowTab.Catalog:
                FillCatalog();
                break;
            default:
                FillSources();
                break;
        }
        FillDetails();
        FillBottom();
        FillBigShot();
        LayoutRebuilder.ForceRebuildLayoutImmediate(window.GetComponent<RectTransform>());
    }

    // The mods installed (the manager is apart, on top) and the catalog's (from a source), as the window lists them.
    static List<ModEntry> Installed() => ModCatalog.Mods.Where(m => m.Installed != null)
        .OrderBy(m => Texts.Get(m.NameKey), StringComparer.CurrentCultureIgnoreCase).ToList();

    static List<ModEntry> Catalog() => ModCatalog.Mods.Where(m => m.SourceNameKey != null)
        .OrderBy(m => m.Installed != null).ThenBy(m => Texts.Get(m.NameKey), StringComparer.CurrentCultureIgnoreCase).ToList();

    // The text typed matches a mod: its name, description or author.
    static bool Matches(ModEntry mod, string text) =>
        string.IsNullOrWhiteSpace(text) || new[] { Texts.Get(mod.NameKey), mod.DescriptionKey != null ? Texts.Get(mod.DescriptionKey) : "", mod.Author ?? "" }
            .Any(s => s.IndexOf(text.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0);

    // ---------- Installed ----------

    void FillInstalled()
    {
        var mods = Installed().Where(m => Matches(m, filterText)).ToList();

        // The header: select all (the mods listed), how many are selected.
        Clear(installedHeader);
        HRow(installedHeader.gameObject, CellGap, TextAnchor.MiddleLeft);
        var header = installedHeader;
        var all = mods.Count > 0 && mods.All(m => selected.Contains(m.Id));
        NewCheck(header, all, value => Redraw(() =>
        {
            foreach (var mod in mods)
                if (value)
                    selected.Add(mod.Id);
                else
                    selected.Remove(mod.Id);
        }));
        TextCell(header, string.Format(Texts.Get(W + "selected"), selected.Count), -1f, Dim(), textStyle.fontSize * 0.85f);

        Clear(installedList);
        if (ModCatalog.Manager is { } manager)
            InstalledRow(manager, true);
        foreach (var mod in mods)
            InstalledRow(mod, false);
        if (mods.Count == 0)
            TextCell(Line(NewRect("Empty", installedList).gameObject, RowHeight), Texts.Get(W + (ModCatalog.Mods.Any(m => m.Installed != null) ? "nothing" : "noneInstalled")), -1f, Dim());

        // The actions on the selected.
        Clear(installedActions);
        var chosen = mods.Where(m => selected.Contains(m.Id)).ToList();
        var ids = chosen.Select(m => m.Id).ToList();
        ActionButton(installedActions, W + "enable", ModCatalog.SetEnabled != null && chosen.Any(m => m.Enabled == false && !m.RequiredByOthers),
            () => ModCatalog.SetEnabled?.Invoke(ids, true));
        ActionButton(installedActions, W + "disable", ModCatalog.SetEnabled != null && chosen.Any(m => m.Enabled == true && !m.RequiredByOthers),
            () => ModCatalog.SetEnabled?.Invoke(ids, false));
        var updatable = chosen.Where(m => m.HasUpdate).Select(m => m.Id).ToList();
        ActionButton(installedActions, W + "update", ModCatalog.Update != null && updatable.Count > 0, () => ModCatalog.Update?.Invoke(updatable), green: true);
        var removable = chosen.Where(m => !m.RequiredByOthers).Select(m => m.Id).ToList();
        ActionButton(installedActions, W + "remove", ModCatalog.Remove != null && removable.Count > 0, () =>
        {
            ModCatalog.Remove?.Invoke(removable);
            selected.ExceptWith(removable);
        });
    }

    // A row: check mark, name, version, note, switch, update (the new version), remove. The manager: its update only.
    void InstalledRow(ModEntry mod, bool isManager)
    {
        var go = NewRect("OE_Row_" + mod.Id, installedList).gameObject;
        var line = Line(go, RowHeight);
        Pickable(go, installedPick == mod.Id, () => installedPick = mod.Id);
        if (isManager)
            Space(line, CheckWidth);
        else
            NewCheck(line, selected.Contains(mod.Id), value => Redraw(() =>
            {
                if (value)
                    selected.Add(mod.Id);
                else
                    selected.Remove(mod.Id);
            }));
        TextCell(line, Texts.Get(mod.NameKey), -1f, textStyle.color);
        TextCell(line, mod.Installed ?? "", 200f, textStyle.color);
        TextCell(line, mod.NoteKey != null ? Texts.Get(mod.NoteKey) : "", 380f, Dim());

        if (!isManager && mod.Enabled is { } on && !mod.RequiredByOthers && ModCatalog.SetEnabled != null && switchSource != null)
            NewSwitch(line, on, value => Guard("switch " + mod.Id, () => ModCatalog.SetEnabled?.Invoke(new[] { mod.Id }, value)));
        else
            Space(line, SwitchWidth);

        if (mod.HasUpdate && ModCatalog.Update != null)
            NewButton(line, string.Format(Texts.Get(W + "updateTo"), mod.Available), 300f, () => ModCatalog.Update?.Invoke(new[] { mod.Id }), green: true);
        else
            Space(line, 300f);

        if (!isManager && !mod.RequiredByOthers && ModCatalog.Remove != null && crossTemplate != null)
            NewCross(line, () => ModCatalog.Remove?.Invoke(new[] { mod.Id }));
        else
            Space(line, CrossWidth);
    }

    // ---------- Catalog ----------

    // The tags and the sources of the catalog's mods, in the order of their names: the dropdowns' options after "All".
    static List<string> TagKeys() => ModCatalog.Mods.Where(m => m.SourceNameKey != null).SelectMany(m => m.TagKeys ?? Array.Empty<string>())
        .Where(k => k != null).Distinct().OrderBy(Texts.Get, StringComparer.CurrentCultureIgnoreCase).ToList();

    static List<string> SourceKeys() => ModCatalog.Mods.Select(m => m.SourceNameKey).Where(k => k != null).Distinct()
        .OrderBy(Texts.Get, StringComparer.CurrentCultureIgnoreCase).ToList();

    void FillCatalog()
    {
        var tagKeys = TagKeys();
        var sourceKeys = SourceKeys();
        if (tagFilter != null && !tagKeys.Contains(tagFilter))
            tagFilter = null;
        if (sourceFilter != null && !sourceKeys.Contains(sourceFilter))
            sourceFilter = null;
        tagOptions = InitDropdown(tagDropdown, "oe.settings.w.tagopt.", W + "tags.all", tagKeys, tagFilter, tagOptions);
        sourceOptions = InitDropdown(sourceDropdown, "oe.settings.w.sourceopt.", W + "sources.all", sourceKeys, sourceFilter, sourceOptions);

        Clear(catalogGrid);
        var mods = Catalog().Where(m => Matches(m, searchText) && (tagFilter == null || (m.TagKeys?.Contains(tagFilter) ?? false)) &&
                                        (sourceFilter == null || m.SourceNameKey == sourceFilter)).ToList();
        foreach (var mod in mods)
            Card(mod);
        if (mods.Count == 0)
            TextCell(Line(NewRect("Empty", catalogGrid).gameObject, RowHeight), Texts.Get(W + "nothing"), -1f, Dim());
    }

    // The dropdown's options: "All ..." and the keys (looked up by number: aliases <prefix>0..N); set again only when
    // they changed (Init closes a list the player has open). Returns what they are now.
    string InitDropdown(BhLocalizedDropdown dropdown, string prefix, string allKey, List<string> keys, string chosen, string was)
    {
        if (dropdown == null)
            return was;
        var now = $"{Texts.Language}|{string.Join("|", keys)}|{chosen}";
        if (now == was)
            return was;
        Texts.Alias(prefix + 0, allKey);
        for (var i = 0; i < keys.Count; i++)
            Texts.Alias(prefix + (i + 1), keys[i]);
        var values = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>(keys.Count + 1);
        for (var i = 0; i <= keys.Count; i++)
            values[i] = i;
        var index = chosen != null ? keys.IndexOf(chosen) + 1 : 0;
        dropdown.Init(dropdownChanged[dropdown.Pointer], new Il2CppSystem.Array(values.Pointer), index, false, prefix, EDropdownType.Text);
        return now;
    }

    // A card: the first screenshot as its picture, the name, the source (or "installed"), the tags.
    void Card(ModEntry mod)
    {
        var cell = NewRect("OE_Card_" + mod.Id, catalogGrid);
        var column = cell.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 8f;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        Pickable(cell.gameObject, catalogPick == mod.Id, () => catalogPick = mod.Id);

        var pictureHolder = NewRect("Picture", cell);
        Size(pictureHolder.gameObject, CardWidth, 267f * CardScale);
        var shot = mod.Screenshots?.FirstOrDefault(s => s?.Path != null)?.Path;
        if (cardTemplate != null)
        {
            var card = Copy(cardTemplate.gameObject, pictureHolder);
            var minimap = card.GetComponentsInChildren<RawImage>(true).FirstOrDefault(r => r.name == cardTemplate.minimapImage.name && r.GetComponentInParent<Mask>(true) == null);
            var title = card.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.name == cardTemplate.title.name);
            // The map's info icons and favourite mark are not ours.
            foreach (var part in new[] { cardTemplate.mapInfoView != null ? cardTemplate.mapInfoView.name : null, "IconsSeparators", cardTemplate.fav != null ? cardTemplate.fav.name : null })
                if (part != null)
                    foreach (var t in card.GetComponentsInChildren<Transform>(true).Where(t => t.name == part).ToList())
                        t.gameObject.SetActive(false);
            Strip<BhMapCard>(card);
            Strip<BhMapPreviewInfoView>(card);
            foreach (var group in card.GetComponents<CanvasGroup>())
            {
                group.alpha = 1f;
                group.interactable = group.blocksRaycasts = true;
            }
            var rect = card.transform.Cast<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = new Vector3(CardScale, CardScale, 1f);
            card.SetActive(true);
            if (title != null)
            {
                SetText(title, Texts.Get(mod.NameKey));
                title.enableWordWrapping = false;
                title.overflowMode = TextOverflowModes.Ellipsis;
            }
            if (minimap != null)
                ShowPicture(minimap, shot, crop: true);
        }
        else
        {
            // No card in the loaded scenes: the picture and the name in the window's own font.
            var picture = NewRect("Shot", pictureHolder).gameObject.AddComponent<RawImage>();
            Stretch(picture.rectTransform);
            ShowPicture(picture, shot, crop: true);
            TextCell(Line(NewRect("Name", cell).gameObject, 60f, CardWidth), Texts.Get(mod.NameKey), -1f, textStyle.color);
        }
        var origin = mod.Installed != null ? Texts.Get(W + "installed") : mod.SourceNameKey != null ? Texts.Get(mod.SourceNameKey) : "";
        var originLine = Line(NewRect("Origin", cell).gameObject, 48f, CardWidth);
        originLine.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        var originCell = TextCell(originLine, origin, -1f, Dim(), textStyle.fontSize * 0.8f);
        originCell.alignment = TextAlignmentOptions.Midline;
        var chips = NewRect("Tags", cell);
        Size(chips.gameObject, CardWidth, 50f);
        chips.gameObject.AddComponent<RectMask2D>();
        var chipsRow = HRow(chips.gameObject, 8f, TextAnchor.MiddleCenter, controlWidth: false);
        chipsRow.childControlHeight = false;
        foreach (var key in (mod.TagKeys ?? Array.Empty<string>()).Take(3))
            Chip(chips, key);
    }

    // ---------- Sources ----------

    void FillSources()
    {
        Clear(sourcesList);
        foreach (var source in ModCatalog.Sources)
        {
            var go = NewRect("OE_Source", sourcesList).gameObject;
            var line = Line(go, RowHeight);
            var url = source.Url;
            Pickable(go, sourcePick == url, () => sourcePick = url);
            TextCell(line, Texts.Get(source.TitleKey), -1f, textStyle.color);
            TextCell(line, string.Format(Texts.Get(W + "mods"), source.Mods), 220f, textStyle.color);
            TextCell(line, source.NoteKey != null ? Texts.Get(source.NoteKey) : "", 520f, Dim());
            TextCell(line, Checked(source), 460f, Dim());
        }
        Clear(sourcesActions);
        ActionButton(sourcesActions, W + "addUrl", ModCatalog.AddSource != null, () => AskForSource(W + "url.caption", W + "url.placeholder", ModCatalog.AddSource), width: 520f);
        ActionButton(sourcesActions, W + "addGitHub", ModCatalog.AddGitHubSource != null, () => AskForSource(W + "github.caption", W + "github.placeholder", ModCatalog.AddGitHubSource), width: 520f);
    }

    static string Checked(CatalogSource source) => source.LastChecked is { } when
        ? string.Format(Texts.Get(W + "checked"), when.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture))
        : Texts.Get(W + "checkedNever");

    // ---------- details ----------

    void FillDetails()
    {
        Clear(details);
        Clear(detailsButtons);
        if (tab == WindowTab.Sources)
        {
            if (ModCatalog.Sources.FirstOrDefault(s => s.Url == sourcePick) is { } source)
                SourceDetails(source);
            else
                DetailText(Texts.Get(W + "pickSource"), Dim(), textStyle.fontSize);
            return;
        }
        var id = tab == WindowTab.Installed ? installedPick : catalogPick;
        var mod = id == null ? null : ModCatalog.Manager?.Id == id ? ModCatalog.Manager : ModCatalog.Mods.FirstOrDefault(m => m.Id == id);
        if (mod == null)
        {
            DetailText(Texts.Get(W + "pickMod"), Dim(), textStyle.fontSize);
            return;
        }
        if (screenshotsAsked.Add(mod.Id) && ModCatalog.LoadScreenshots is { } load)
            Guard("load screenshots " + mod.Id, () => load(mod.Id));

        DetailText(Texts.Get(mod.NameKey), textStyle.color, textStyle.fontSize * 1.5f);
        var origin = new List<string>
        {
            mod.SourceNameKey != null ? string.Format(Texts.Get(W + "catalogOf"), Texts.Get(mod.SourceNameKey)) : Texts.Get(W + "byHand"),
        };
        if (!string.IsNullOrEmpty(mod.Author))
            origin.Add(string.Format(Texts.Get(W + "author"), mod.Author));
        DetailText(string.Join(" · ", origin), Dim(), textStyle.fontSize * 0.85f);
        DetailText(mod.Installed != null && mod.HasUpdate && mod.Available != null ? string.Format(Texts.Get(W + "versions"), mod.Installed, mod.Available)
            : mod.Installed != null ? string.Format(Texts.Get(W + "version"), mod.Installed)
            : mod.Available != null ? string.Format(Texts.Get(W + "available"), mod.Available) : "", textStyle.color, textStyle.fontSize * 0.85f);
        if (mod.NoteKey != null)
            DetailText(Texts.Get(mod.NoteKey), Dim(), textStyle.fontSize * 0.85f);

        // The screenshots: thumbnails, a click shows one big.
        if (mod.Screenshots is { Count: > 0 } shots)
        {
            var strip = NewRect("Screenshots", details);
            Size(strip.gameObject, DetailsWidth, 135f);
            HRow(strip.gameObject, 12f, TextAnchor.MiddleLeft);
            strip.gameObject.AddComponent<RectMask2D>();
            foreach (var shot in shots.Take(4))
            {
                var thumb = NewRect("Shot", strip);
                Size(thumb.gameObject, 228f, 128f);
                var picture = thumb.gameObject.AddComponent<RawImage>();
                ShowPicture(picture, shot?.Path, crop: true);
                if (shot?.Path is { } path)
                    Clickable(thumb.gameObject, () => Redraw(() => bigShotPath = path));
            }
        }

        // The tags, in rows as wide as the panel.
        if (mod.TagKeys is { Count: > 0 } tagKeys)
        {
            Transform row = null;
            var width = 0f;
            foreach (var key in tagKeys)
            {
                if (row == null || width > DetailsWidth - 220f)
                {
                    row = NewRect("Tags", details);
                    Size(row.gameObject, DetailsWidth, 50f);
                    var layout = HRow(row.gameObject, 8f, TextAnchor.MiddleLeft, controlWidth: false);
                    layout.childControlHeight = false;
                    width = 0f;
                }
                width += Chip(row, key) + 8f;
            }
        }
        if (mod.DescriptionKey != null)
            DetailText(Texts.Get(mod.DescriptionKey), textStyle.color, textStyle.fontSize * 0.85f, wrap: true);
        if (mod.RequiresKeys is { Count: > 0 } requires)
            DetailText(string.Format(Texts.Get(W + "requires"), string.Join(", ", requires.Select(Texts.Get))), Dim(), textStyle.fontSize * 0.85f, wrap: true);

        // The buttons by tab.
        if (tab == WindowTab.Installed)
        {
            ActionButton(detailsButtons, W + "update", mod.HasUpdate && ModCatalog.Update != null, () => ModCatalog.Update?.Invoke(new[] { mod.Id }), green: true, hideWhenOff: true);
            ActionButton(detailsButtons, W + "remove", mod != ModCatalog.Manager && mod.Installed != null && !mod.RequiredByOthers && ModCatalog.Remove != null,
                () => ModCatalog.Remove?.Invoke(new[] { mod.Id }), hideWhenOff: true);
        }
        else
            ActionButton(detailsButtons, W + "install", mod.Installed == null && mod.Available != null && ModCatalog.Install != null,
                () => ModCatalog.Install?.Invoke(mod.Id), green: true, hideWhenOff: true);
    }

    void SourceDetails(CatalogSource source)
    {
        DetailText(Texts.Get(source.TitleKey), textStyle.color, textStyle.fontSize * 1.5f);
        DetailText(source.Url ?? "", Dim(), textStyle.fontSize * 0.75f, wrap: true);
        if (source.BuiltIn)
            DetailText(Texts.Get(W + "builtIn"), Dim(), textStyle.fontSize * 0.85f);
        DetailText(string.Format(Texts.Get(W + "mods"), source.Mods), textStyle.color, textStyle.fontSize * 0.85f);
        DetailText(Checked(source), textStyle.color, textStyle.fontSize * 0.85f);
        if (source.NoteKey != null)
            DetailText(Texts.Get(source.NoteKey), Dim(), textStyle.fontSize * 0.85f, wrap: true);
        if (source.SetCheckEvery is { } setEvery)
        {
            DetailText(Texts.Get(W + "every"), textStyle.color, textStyle.fontSize * 0.85f);
            var holder = NewRect("CheckEvery", details);
            Size(holder.gameObject, DetailsWidth, 72f);
            var dropdown = NewDropdown(holder, 0f, 0f, i =>
            {
                if (i >= 0 && i < CheckEveryValues.Length)
                    Guard("check every", () => setEvery(CheckEveryValues[i]));
            });
            if (dropdown != null)
            {
                const string prefix = "oe.settings.w.every.";
                var values = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>(CheckEveryValues.Length);
                for (var i = 0; i < CheckEveryValues.Length; i++)
                    values[i] = i;
                dropdown.Init(dropdownChanged[dropdown.Pointer], new Il2CppSystem.Array(values.Pointer), Math.Max(0, Array.IndexOf(CheckEveryValues, source.CheckEvery)),
                    false, prefix, EDropdownType.Text);
            }
        }
        ActionButton(detailsButtons, W + "check", source.Check != null, () => source.Check?.Invoke(), hideWhenOff: true);
        ActionButton(detailsButtons, W + "remove", source.Remove != null && !source.BuiltIn, () => source.Remove?.Invoke(), hideWhenOff: true);
    }

    TMP_Text DetailText(string text, Color color, float size, bool wrap = false)
    {
        var go = NewRect("Text", details).gameObject;
        var tmp = Text(go, text, color, size);
        tmp.enableWordWrapping = wrap;
        tmp.overflowMode = wrap ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
        var element = Size(go, DetailsWidth, -1f);
        // Wrapped text grows with its lines (its preferred height); one line keeps the font's height.
        if (!wrap)
            element.preferredHeight = element.minHeight = size * 1.6f;
        else
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return tmp;
    }

    // ---------- the bottom, the big screenshot ----------

    void FillBottom()
    {
        Clear(windowButtons);
        if (tab == WindowTab.Installed)
            ActionButton(windowButtons, W + "updateAll", ModCatalog.UpdateAll != null && (ModCatalog.Mods.Any(m => m.HasUpdate) || ModCatalog.Manager?.HasUpdate == true),
                () => ModCatalog.UpdateAll?.Invoke(), width: BottomButtonWidth, green: true);
        if (tab == WindowTab.Sources)
            ActionButton(windowButtons, W + "checkAll", ModCatalog.CheckAll != null, () => ModCatalog.CheckAll?.Invoke(), width: BottomButtonWidth);
        NewButton(windowButtons, Texts.Get(CloseKey), BottomButtonWidth, () => window.SetActive(false), waits: false);
    }

    void FillBigShot()
    {
        Clear(bigShot);
        var path = bigShotPath;
        bigShot.gameObject.SetActive(path != null);
        if (path == null)
            return;
        bigShot.SetAsLastSibling();
        var shade = bigShot.gameObject.GetComponent<Image>() ?? bigShot.gameObject.AddComponent<Image>();
        shade.color = new Color(0f, 0f, 0f, 0.85f);
        Clickable(bigShot.gameObject, () => Redraw(() => bigShotPath = null));
        var picture = NewRect("Shot", bigShot).gameObject.AddComponent<RawImage>();
        picture.raycastTarget = false;
        var texture = Picture(path);
        picture.texture = texture;
        var rect = picture.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        var aspect = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 16f / 9f;
        var width = Math.Min(BodyWidth - 40f, (BodyHeight - 40f) * aspect);
        rect.sizeDelta = new Vector2(width, width / aspect);
    }

    // ---------- the game's boxes: adding a source ----------

    // First the game's own message box warning that nobody reviews other sources, then its input box for the address;
    // our window waits hidden while they are open (the game's boxes draw under our canvas).
    void AskForSource(string captionKey, string placeholderKey, Action<string> add)
    {
        if (add == null)
            return;
        var options = new GameMessageBoxOptions();
        options.Caption = Texts.Get(W + "warn.title");
        options.Text = Texts.Get(W + "warn.text");
        var go = new GameMessageBoxButton();
        go.Label = Texts.Get(W + "warn.go");
        go.Click = (Il2CppSystem.Action)(Action)(() => Guard("warning confirmed", () =>
        {
            // The input box a moment later: the message box closes first.
            pendingBox = () => OpenInputBox(captionKey, placeholderKey, add);
            pendingAt = Time.frameCount + 20;
            suspendedAt = Time.frameCount + 20;
        }));
        var cancel = new GameMessageBoxButton();
        cancel.Label = GameText("button_cancel", W + "cancel");
        cancel.Click = (Il2CppSystem.Action)(Action)(() => Guard("warning cancelled", ResumeWindow));
        options.Confirm = go;
        options.Cancel = cancel;
        SuspendWindow();
        ScMessageBox.Open(options);
    }

    void OpenInputBox(string captionKey, string placeholderKey, Action<string> add)
    {
        var options = new GameInputBoxOptions();
        options.Caption = Texts.Get(captionKey);
        options.Placeholder = Texts.Get(placeholderKey);
        options.ConfirmText = Texts.Get(W + "add");
        options.CancelText = GameText("button_cancel", W + "cancel");
        options.OnConfirm = (Il2CppSystem.Action<string>)(Action<string>)(text => Guard("source typed", () =>
        {
            ResumeWindow();
            if (!string.IsNullOrWhiteSpace(text))
                add(text.Trim());
        }));
        options.OnCancel = (Il2CppSystem.Action)(Action)(() => Guard("input cancelled", ResumeWindow));
        SuspendWindow();
        ScInputBox.Open(options);
    }

    void SuspendWindow()
    {
        suspended = true;
        suspendedAt = Time.frameCount;
        window.SetActive(false);
    }

    void ResumeWindow()
    {
        if (!suspended || window == null)
            return;
        suspended = false;
        window.SetActive(true);
        windowDirty = true;
    }

    // A box closed without telling us (the game's Escape, or a scene change): the window comes back once neither box
    // is shown for a while.
    void WatchGameBoxes()
    {
        if (pendingBox != null || Time.frameCount < suspendedAt + 60)
            return;
        if (Shown(Scene.Find<ScMessageBox>()) || Shown(Scene.Find<ScInputBox>()))
        {
            suspendedAt = Time.frameCount;
            return;
        }
        ResumeWindow();
    }

    static bool Shown(BhScreen screen) => screen != null && screen.gameObject.activeInHierarchy && (screen.cg == null || screen.cg.alpha > 0.01f);

    // A text of the game's own (its Cancel), ours when it has none.
    static string GameText(string gameKey, string ownKey)
    {
        string text = null;
        try { text = Game.Translate(gameKey); }
        catch (Exception) { }
        return string.IsNullOrEmpty(text) || text == gameKey ? Texts.Get(ownKey) : text;
    }

    // ---------- widgets ----------

    const float CheckWidth = 64f, SwitchWidth = 150f, CrossWidth = 80f;

    // The tabs when there is no hub tab in the loaded scenes: the settings' small buttons (the current one greyed out).
    readonly Dictionary<WindowTab, BhGenericButton> tabButtons = new();

    void NewTab(Transform parent, WindowTab which)
    {
        if (tabTemplate == null)
        {
            tabButtons[which] = NewButton(parent, which.ToString(), 560f, () => Redraw(() => SwitchTab(which)), waits: false);
            Logs.Once("Mods window: no hub tab to copy, buttons instead");
            return;
        }
        var copy = Copy(tabTemplate.gameObject, parent);
        copy.name = "Tab_" + which;
        Size(copy, 560f, 66f);
        copy.SetActive(true);
        var tab = copy.GetComponent<BhTab>();
        tab.Click = (Action<GameTabItem>)(_ => Guard("tab", () => Redraw(() => SwitchTab(which))));
        tabs[which] = tab;
    }

    void SwitchTab(WindowTab which)
    {
        tab = which;
        bigShotPath = null;
    }

    // A text field: a copy of the game's input box's, its placeholder ours, each change reported.
    TMP_InputField NewInput(Transform parent, string placeholderKey, float x, float y, float width, Action<string> changed)
    {
        if (inputTemplate == null)
            return null;
        var copy = Copy(inputTemplate.gameObject, parent);
        copy.name = "Input";
        Place(copy.transform.Cast<RectTransform>(), x, y, width, 64f);
        copy.SetActive(true);
        var input = copy.GetComponent<TMP_InputField>();
        input.text = "";
        input.characterLimit = 0;
        if (input.placeholder != null && input.placeholder.GetComponent<TMP_Text>() is { } placeholder)
            SetKey(placeholder, placeholderKey);
        input.onValueChanged.AddListener((UnityAction<string>)(value => Guard("input", () => changed(value))));
        return input;
    }

    // What each of our dropdowns does with the option chosen, by the dropdown (the click is set again on each Init).
    readonly Dictionary<IntPtr, Action<int>> dropdownChanged = new();

    // A dropdown: a copy of the settings' dropdown row with only its dropdown shown (the switch's way below).
    BhLocalizedDropdown NewDropdown(Transform parent, float x, float y, Action<int> changed)
    {
        if (dropdownTemplate == null)
            return null;
        var holder = NewRect("Dropdown", parent);
        Place(holder, x, y, 841f * DropdownScale + 20f, 70f);
        // Its list opens over what follows it.
        var canvas = holder.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = WindowOrder + 10;
        holder.gameObject.AddComponent<GraphicRaycaster>();
        var copy = Copy(dropdownTemplate.gameObject, holder);
        copy.name = "Row";
        var rect = copy.transform.Cast<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        if (copy.GetComponent<Image>() is { } rowImage)
            rowImage.color = new Color(0f, 0f, 0f, 0f);
        for (var i = 0; i < copy.transform.childCount; i++)
        {
            var part = copy.transform.GetChild(i);
            if (part.name == "Dropdown")
            {
                var own = part.Cast<RectTransform>();
                own.anchorMin = own.anchorMax = new Vector2(0f, 0.5f);
                own.pivot = new Vector2(0f, 0.5f);
                own.anchoredPosition = Vector2.zero;
                own.localScale = new Vector3(DropdownScale, DropdownScale, 1f);
            }
            else
                part.gameObject.SetActive(false);
        }
        copy.SetActive(true);
        var dropdown = copy.GetComponent<BhLocalizedDropdown>();
        dropdownChanged[dropdown.Pointer] = i => Guard("dropdown", () => changed(i));
        return dropdown;
    }

    // A check mark: a copy of the save list filter's, scaled up.
    void NewCheck(Transform line, bool value, Action<bool> changed)
    {
        var holder = Space(line, CheckWidth);
        if (checkTemplate == null)
            return;
        var copy = Copy(checkTemplate.gameObject, holder);
        copy.name = "Check";
        var rect = copy.transform.Cast<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = new Vector3(CheckScale, CheckScale, 1f);
        copy.SetActive(true);
        var check = copy.GetComponent<BhToggle>();
        check.ShowValue(value);
        check.toggle.onValueChanged.AddListener((UnityAction<bool>)(v => Guard("check", () => changed(v))));
    }

    // The switch: a copy of the settings tab's switch, only its knob shown, centred.
    void NewSwitch(Transform line, bool on, Action<bool> set)
    {
        var holder = Space(line, SwitchWidth);
        var copy = UnityEngine.Object.Instantiate(switchSource.gameObject, holder, false);
        copy.name = "Switch";
        copy.SetActive(true);
        var copyRect = copy.transform.Cast<RectTransform>();
        copyRect.anchorMin = copyRect.anchorMax = copyRect.pivot = new Vector2(0.5f, 0.5f);
        copyRect.sizeDelta = new Vector2(SwitchWidth, RowHeight);
        copyRect.anchoredPosition = Vector2.zero;
        if (copy.GetComponent<Image>() is { } rowImage)
            rowImage.color = new Color(0f, 0f, 0f, 0f);
        for (var i = 0; i < copy.transform.childCount; i++)
        {
            var part = copy.transform.GetChild(i);
            if (part.name == "CheckBox")
            {
                var knob = part.Cast<RectTransform>();
                knob.anchorMin = knob.anchorMax = knob.pivot = new Vector2(0.5f, 0.5f);
                knob.anchoredPosition = Vector2.zero;
            }
            else
                part.gameObject.SetActive(false);
        }
        copy.GetComponent<BhSettingsCheckbox>().Init((Action<bool>)set, on);
    }

    void NewCross(Transform line, Action click)
    {
        var holder = Space(line, CrossWidth);
        var cross = UnityEngine.Object.Instantiate(crossTemplate.gameObject, holder, false).GetComponent<BhGenericButton>();
        cross.name = "Remove";
        var crossRect = cross.transform.Cast<RectTransform>();
        crossRect.anchorMin = crossRect.anchorMax = crossRect.pivot = new Vector2(0.5f, 0.5f);
        crossRect.anchoredPosition = Vector2.zero;
        crossRect.localScale = new Vector3(CrossScale, CrossScale, 1f);
        Bind(cross, null, click, true);
    }

    // A tag chip: a copy of the quick start's, its text the game's translation of our key (BhDescriptionTag.Show sizes
    // it to the text). Returns its width.
    float Chip(Transform parent, string key)
    {
        if (tagTemplate == null)
        {
            var label = Text(NewRect("Tag", parent).gameObject, Texts.Get(key), Dim(), textStyle.fontSize * 0.7f);
            label.enableWordWrapping = false;
            var width = label.preferredWidth;
            label.rectTransform.sizeDelta = new Vector2(width, 44f);
            return width;
        }
        var copy = Copy(tagTemplate.gameObject, parent);
        copy.name = "Tag";
        copy.SetActive(true);
        var chip = copy.GetComponent<BhDescriptionTag>();
        chip.Show(key);
        copy.SetActive(true);
        return copy.transform.Cast<RectTransform>().rect.width;
    }

    // A button with our label: shown greyed out while not available (or hidden), waiting while the manager is busy.
    void ActionButton(Transform parent, string key, bool available, Action click, bool green = false, float width = 320f, bool hideWhenOff = false)
    {
        if (!available && hideWhenOff)
            return;
        var button = NewButton(parent, Texts.Get(key), width, click, green);
        if (!available)
            button.SetAvailable(false);
    }

    BhGenericButton NewButton(Transform parent, string label, float width, Action click, bool green = false, bool waits = true)
    {
        var button = UnityEngine.Object.Instantiate(rowButtonTemplate.gameObject, parent, false).GetComponent<BhGenericButton>();
        button.name = "Button";
        Size(button.gameObject, width, ButtonHeight);
        button.gameObject.SetActive(true);
        Bind(button, label, click, waits);
        // Install and Update stand out (green in the mockup; the game has no green button loaded in the menu: the
        // blue one's own pictures tinted).
        if (green && button.itemActive != null)
            foreach (var image in button.itemActive.GetComponentsInChildren<Image>(true))
                image.color = new Color(image.color.r * 0.45f, image.color.g, image.color.b * 0.35f, image.color.a);
        if (waits && ModCatalog.Busy)
            button.SetAvailable(false);
        return button;
    }

    // Our label and action on a game button (the copy keeps the original's persistent click: a new event with ours).
    void Bind(BhGenericButton button, string label, Action click, bool waits)
    {
        if (label != null)
            SetText(button.label, label);
        button.UnityClickEvent = new UnityEvent();
        button.UnityClickEvent.AddListener((UnityAction)(() =>
        {
            if (waits && ModCatalog.Busy)
                return;
            Guard(label ?? button.name, click);
            windowDirty = true;
        }));
    }

    // ---------- pictures ----------

    // A screenshot's file as a texture, loaded once (the window's; destroyed with it).
    Texture2D Picture(string path)
    {
        if (path == null)
            return null;
        if (textures.TryGetValue(path, out var known))
            return known;
        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }
        }
        catch (Exception e)
        {
            Logs.Once($"Mods window: screenshot {path}: {e.Message}");
            texture = null;
        }
        textures[path] = texture;
        return texture;
    }

    // A picture in a frame of its own size: cut to fill it (crop) or whole.
    void ShowPicture(RawImage image, string path, bool crop)
    {
        var texture = Picture(path);
        image.texture = texture;
        image.color = texture != null ? Color.white : new Color(0f, 0f, 0f, 0.35f);
        if (texture == null || !crop || texture.height == 0)
        {
            image.uvRect = new Rect(0f, 0f, 1f, 1f);
            return;
        }
        var frame = image.rectTransform.rect;
        var frameAspect = frame.height > 0 ? frame.width / frame.height : 1f;
        if (image.GetComponent<LayoutElement>() is { preferredHeight: > 0 } size)
            frameAspect = size.preferredWidth / size.preferredHeight;
        var aspect = (float)texture.width / texture.height;
        image.uvRect = aspect > frameAspect
            ? new Rect(0.5f - frameAspect / aspect / 2f, 0f, frameAspect / aspect, 1f)
            : new Rect(0f, 0.5f - aspect / frameAspect / 2f, 1f, aspect / frameAspect);
    }

    // ---------- layout helpers ----------

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        go.transform.SetParent(parent, false);
        return go.transform.Cast<RectTransform>();
    }

    // At (x, y) from the parent's top left, of this size.
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // A copy of a game widget, made inactive (its scripts wake up when it is shown), then put under the parent.
    GameObject Copy(GameObject template, Transform parent)
    {
        var copy = UnityEngine.Object.Instantiate(template, staging.transform, false);
        copy.SetActive(false);
        copy.transform.SetParent(parent, false);
        return copy;
    }

    static void Strip<T>(GameObject go) where T : Component
    {
        foreach (var component in go.GetComponentsInChildren<T>(true).ToList())
            UnityEngine.Object.DestroyImmediate(component);
    }

    static void Clear(Transform parent)
    {
        if (parent == null)
            return;
        for (var i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    // A scrolling list (the mouse wheel) at a place: a column of rows, or a grid of cards. Returns its content.
    Transform NewScroll(Transform parent, float x, float y, float width, float height, bool grid)
    {
        var scroll = NewRect("Scroll", parent);
        Place(scroll, x, y, width, height);
        scroll.gameObject.AddComponent<RectMask2D>();
        // A transparent image: the wheel over the gaps between rows scrolls too.
        scroll.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var content = NewRect("Content", scroll);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        if (grid)
        {
            var cells = content.gameObject.AddComponent<GridLayoutGroup>();
            cells.cellSize = new Vector2(CardWidth, CardHeight);
            cells.spacing = new Vector2(CellGap, CellGap * 1.5f);
            cells.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cells.constraintCount = Math.Max(1, (int)((width + CellGap) / (CardWidth + CellGap)));
        }
        else
        {
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = RowSpacing;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
        }
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = scroll;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = RowHeight;
        return content;
    }

    static HorizontalLayoutGroup HRow(GameObject go, float spacing, TextAnchor alignment, bool controlWidth = true)
    {
        var line = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
        line.spacing = spacing;
        line.childAlignment = alignment;
        line.childControlWidth = controlWidth;
        line.childControlHeight = true;
        line.childForceExpandWidth = line.childForceExpandHeight = false;
        return line;
    }

    // A horizontal line of cells, as wide as the list.
    static Transform Line(GameObject go, float height, float width = ListWidth)
    {
        HRow(go, CellGap, TextAnchor.MiddleLeft);
        Size(go, width, height);
        return go.transform;
    }

    // A row or card that shows the details on a click; the one picked is lit.
    void Pickable(GameObject go, bool picked, Action pick)
    {
        var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        image.color = picked ? textStyle.color * new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0f);
        Clickable(go, () => Redraw(pick));
    }

    void Clickable(GameObject go, Action click)
    {
        if (go.GetComponent<Graphic>() == null)
            go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener((UnityAction)(() => Guard("click", click)));
    }

    static LayoutElement Size(GameObject go, float width, float height)
    {
        var size = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (width >= 0f)
            size.preferredWidth = size.minWidth = width;
        if (height >= 0f)
            size.preferredHeight = size.minHeight = height;
        return size;
    }

    // An empty cell of a width (a holder for a switch, a check mark or a cross, or a gap).
    static Transform Space(Transform line, float width)
    {
        var go = NewRect("Space", line).gameObject;
        Size(go, width, -1f);
        return go.transform;
    }

    Color Dim() => textStyle.color * new Color(1f, 1f, 1f, 0.6f);

    // A text in the box's own font.
    TextMeshProUGUI Text(GameObject go, string text, Color color, float size)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = textStyle.font;
        tmp.fontSharedMaterial = textStyle.fontSharedMaterial;
        tmp.fontSize = size > 0f ? size : textStyle.fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.raycastTarget = false;
        tmp.text = text ?? "";
        return tmp;
    }

    // A text cell, left-aligned, on one line; width < 0: takes what the others leave.
    TextMeshProUGUI TextCell(Transform line, string text, float width, Color color, float fontSize = 0f)
    {
        var go = NewRect("Cell", line).gameObject;
        var tmp = Text(go, text, color, fontSize);
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        var size = Size(go, width, -1f);
        if (width < 0f)
        {
            size.minWidth = 0f;
            size.flexibleWidth = 1f;
        }
        return tmp;
    }

    void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { Log.LogError($"mods window: {what}: {e}"); }
    }

    void HideModsWindow()
    {
        // At once: the reloaded build looks for them in the same frame.
        if (window != null)
            UnityEngine.Object.DestroyImmediate(window);
        if (modsButton != null)
            UnityEngine.Object.DestroyImmediate(modsButton.gameObject);
        foreach (var texture in textures.Values)
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        textures.Clear();
        window = staging = null;
        modsButton = null;
        windowHeader = windowStatus = windowButtons = null;
        installedList = installedHeader = installedActions = catalogGrid = sourcesList = sourcesActions = details = detailsButtons = bigShot = null;
        rowButtonTemplate = crossTemplate = null;
        switchSource = null;
        tabTemplate = null;
        inputTemplate = filterInput = searchInput = null;
        checkTemplate = null;
        tagTemplate = null;
        cardTemplate = null;
        tagDropdown = sourceDropdown = null;
        textStyle = null;
        tabs.Clear();
        tabButtons.Clear();
        pages.Clear();
        dropdownChanged.Clear();
        suspended = false;
        pendingBox = null;
    }
}
