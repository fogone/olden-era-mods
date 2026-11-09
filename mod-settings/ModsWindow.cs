using System;
using System.Collections.Generic;
using System.Linq;
using Hex.Settings.UI;
using Hex.UI;
using Hex.UI.Elements;
using Hex.UI.GeneralKit;
using OldenEra.Modding.Common;
using static OldenEra.Modding.Common.UiLayout;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// The mods' own window (issues #6, #13, #14). A "Mods" button in the main menu under Additional (a copy of the game's
// Options button) opens a copy of the game's Load screen (Hex.UI.SaveLoad.ScLoad), a screen of ours in the game's screen
// registry (ModScreen: the game opens and hides it, gives it the camera, the input and Escape), its scripts taken off and
// its parts filled with ours:
//   its title and close cross; its tabs (BhSimpleTabs: Installed / Catalog / Sources, a third tab copied); its filter row
//   (a text field from the game's input box, its sorting dropdown, its filter for the tags and a copy for the sources);
//   its list with the scrollbar, rows made from its save rows; its bottom left group (select all, the actions on the
//   selected; adding a source on Sources); its information panel on the right as a mod's or a source's details; a copy
//   of its title as the state line. Adding a source asks with the game's own message box and input box.
// The data and the actions are the mod manager's (ModCatalog, mod-common/ModMenu.cs).
//
// The window's code by concern:
//   ModsWindow.cs          the main menu button, building the window from the Load screen, opening, filling, closing;
//   ModsWindow.Rows.cs     the rows of the Installed and Catalog tabs, the filters, the bottom left group;
//   ModsWindow.Details.cs  the right panel and the pictures (screenshots, icons, the big screenshot);
//   ModsWindow.Sources.cs  the Sources tab and adding a source through the game's boxes;
//   ModsWindow.Widgets.cs  copies of the game's widgets and the layout helpers.
public partial class Plugin
{
    const string ModsButtonName = "OE_ModsButton", WindowName = "OE_ModsWindow", W = "oe.settings.w.";
    // The Load screen's scripts that run it (taken off the copy: it shows our data, not saves).
    static readonly HashSet<string> ScreenScripts = new()
    {
        "ScLoad", "BhLoadingRoot", "BhSaveLoadBase", "BhVirtualizedList", "BhSaveInfoPanel", "BhDescriptionBlocks", "BhCloudQuotaValue",
        "BhSessionInfoElement", "BhMapPreview", "BhHeroSlotsContainer", "UiAnimatorSOPrime", "BhMapPreviewInfoView",
    };

    enum WindowTab { Installed, Catalog, Sources }

    static readonly WindowTab[] WindowTabs = { WindowTab.Installed, WindowTab.Catalog, WindowTab.Sources };

    BhNavigationButton modsButton;
    // The window is a screen of ours in the game's screen registry (ModScreen): the game opens and hides it, gives it the
    // UI camera, the input and Escape; drawn just under the game's message and input boxes.
    ModScreen screen;
    GameObject window, staging;
    int windowRevision = -1;
    bool windowDirty, filling;

    // The parts of the copied screen.
    TMP_Text titleText, statusText, nothingText, selectAllText, panelTitle, versionLabel, versionValue, originLabel, originValue;
    BhSimpleTabs simpleTabs;
    readonly List<BhSimpleTab> tabList = new();
    Transform filterRow, listContent, bottomGroup, panel, emptyPanel, infoContent, bigShotView;
    BhToggle selectAll;
    BhGenericButton primaryButton, deleteButton, secondaryButton;
    TMP_InputField filterInput, searchInput;
    BhDropdown sortDropdown;
    Hex.UI.SaveLoad.BhFilter tagBox, sourceBox;
    GameObject tagLabel, sourceLabel;
    string tagBoxKeys, sourceBoxKeys, sortOptions;

    // Copy sources: rows, texts, chips, buttons (all from the copy), and the settings' switch, the input box's field.
    GameObject rowSource, lineTemplate, wrapTemplate, chipTemplate, textButtonTemplate, crossSource, dropdownSource, deleteTemplate;
    BhSettingsCheckbox switchSource;
    TMP_InputField inputTemplate;

    // What the player chose.
    WindowTab tab = WindowTab.Installed;
    // The Catalog's order: by name, not installed first (the default), installed first.
    int sortMode = 1;
    string filterText = "", searchText = "", installedPick, catalogPick, sourcePick;
    readonly HashSet<string> selected = new();
    readonly HashSet<string> screenshotsAsked = new();

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
        // In the main menu, under "Additional" and its sub-menu (the Options / Exit group is laid out by the game); a new
        // main menu (back from a game) gets it again.
        if (modsButton == null && MainMenu.AddButton(ModsButtonName, TabKey, OpenWindow, view => view.additionallyButton) is { } added)
        {
            modsButton = added;
            Logs.Info("Mods button: in the main menu");
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
        if (!screen.IsOpen)
            return;
        if (ModCatalog.ProblemRevision != shownProblem && ModCatalog.ProblemKey != null && !GameBoxes.AnyShown())
        {
            TellProblem();
            return;
        }
        if (bigShotIndex >= 0 && (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
            Redraw(() => StepBigShot(Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 1));
        // Not while the mouse button is held: a press on a row and the release on its redrawn copy would lose the click.
        else if ((windowDirty || windowRevision != ModMenu.Revision) && !Input.GetMouseButton(0))
            Guard("fill", FillWindow);
        GreyLater();
    }

    void OpenWindow()
    {
        try
        {
            if (window == null && !BuildWindow())
                return;
            suspended = false;
            screen.Open();
            screenshotsAsked.Clear();
            windowRevision = -1;
            FillWindow();
        }
        catch (Exception e) { Log.LogError($"mods window: {e}"); }
    }

    // Escape while the window is the game's top screen: the big screenshot closes first, then the window. Not the one
    // that closed a game box over it (the box calls back in the same frame).
    void Escape()
    {
        if (suspended || Time.frameCount <= resumedAt + 1)
            return;
        if (bigShotIndex >= 0)
            Redraw(() => bigShotIndex = -1);
        else
            screen.Hide();
    }

    // ---------- building ----------

    // The window: a copy of the game's Load screen (its own canvas and scaler), its scripts off, its parts ours.
    bool BuildWindow()
    {
        var load = Scene.Find<Hex.UI.SaveLoad.ScLoad>();
        if (load == null)
        {
            Log.LogWarning("mods window: the game's Load screen (ScLoad) is not loaded");
            return false;
        }
        if (GameObject.Find(WindowName) is { } old)
            UnityEngine.Object.DestroyImmediate(old);
        // Built shown, as the Load screen is laid out (its tabs' frames are placed by their layout), then made a screen of
        // the game's registry, which hides it until it opens.
        window = ScreenCopy.Make(load.gameObject, WindowName, ScreenScripts);
        staging = new GameObject("OE_Staging", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        staging.transform.SetParent(window.transform, false);
        staging.SetActive(false);

        var root = window.transform;
        var sar = root.Find("ShowAnimationRoot");
        // The screen as it is when shown: its show animation (taken off) starts it a little smaller and lower (0.98,
        // 58 lower on 0.82.7) and ends at its own size, 18.6 lower than the middle (measured against the shown screen).
        sar.localScale = Vector3.one;
        sar.localPosition = new Vector3(0f, -18.6f, 0f);
        BuildHeader(root, sar);
        nothingText = sar.Find("NoSavesHeader")?.GetComponent<TMP_Text>();
        if (!BuildList(sar))
        {
            Log.LogWarning("mods window: no save row to copy");
            UnityEngine.Object.DestroyImmediate(window);
            window = null;
            return false;
        }
        BuildPanel(sar);
        BuildBottomGroup(sar);
        BuildFilterRow(sar);
        BuildTabs(sar);

        // A screenshot big, over everything (a click closes it).
        bigShotView = NewRect("OE_BigShot", root);
        Stretch(bigShotView.Cast<RectTransform>());
        bigShotView.gameObject.SetActive(false);

        switchSource = switchTemplate != null ? switchTemplate.GetComponentInChildren<BhSettingsCheckbox>(true) : null;
        Logs.Info($"Mods window: built from the game's Load screen; tabs {tabList.Count}, input {(inputTemplate != null)}, sorting {(sortDropdown != null)}, filters {(tagBox != null)}, " +
                  $"chip {(chipTemplate != null)}, text {(lineTemplate != null)}, buttons {(textButtonTemplate != null)}, switch {(switchSource != null)}");
        screen = ModScreen.Adopt(window, ModScreen.UnderGameBoxes());
        screen.Escape = () => Guard("escape", Escape);
        return true;
    }

    // The backdrop, the title, the close cross and the state line under the title.
    void BuildHeader(Transform root, Transform sar)
    {
        // Its backdrop opaque: the Load screen hides the menu under it, our copy does not.
        if (root.Find("WindowBack")?.GetComponent<Image>() is { } backdrop)
            backdrop.color = new Color(backdrop.color.r, backdrop.color.g, backdrop.color.b, 1f);
        titleText = root.Find("MainHeader")?.GetComponent<TMP_Text>();
        Hide(root, "ButtonBack", "BackToSaving", "ExampleImage");
        if (root.Find("ButtonClose")?.GetComponent<BhGenericButton>() is { } close)
            Bind(close, null, () => screen.Hide(), false);
        // The cloud quota and the autosaves' header and switch are not ours.
        Hide(sar, "CloudQuota", "AutosaveShow", "AutosavesHeader");
        if (titleText == null)
            return;
        // The state line: a copy of the title, smaller, just under it (the same parent and anchors).
        var line = UnityEngine.Object.Instantiate(titleText.gameObject, root, false);
        line.name = "OE_Status";
        var rect = line.transform.Cast<RectTransform>();
        rect.anchoredPosition = titleText.rectTransform.anchoredPosition + new Vector2(0f, -33f);
        rect.sizeDelta = new Vector2(1400f, 50f);
        // The title's font style would set its size back.
        foreach (var style in line.GetComponents<MonoBehaviour>().Where(c => c.GetIl2CppType().Name == "BhFont").ToList())
            UnityEngine.Object.DestroyImmediate(style);
        statusText = line.GetComponent<TMP_Text>();
        statusText.enableAutoSizing = false;
        statusText.fontSize = 20f;
        statusText.color = new Color(0.65f, 0.65f, 0.65f, 1f);
        statusText.enableWordWrapping = false;
        statusText.overflowMode = TextOverflowModes.Overflow;
        statusText.alignment = TextAlignmentOptions.Center;
    }

    // The list: its rows laid out by a layout group of ours (the game's virtualized list, taken off, placed them), its
    // scrollbar kept; a save row kept as the rows' source. False when there is no save row to copy.
    bool BuildList(Transform sar)
    {
        var list = sar.Find("VirtualizedList");
        listContent = list.Find("Viewport/Content");
        FitViewport(list, 24f);
        var firstRow = listContent.childCount > 0 ? listContent.GetChild(0).gameObject : null;
        if (firstRow == null)
        {
            // No save shown: its row prefab among the loaded objects.
            var prefab = Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(t => t != null && t.name == "SessionElement");
            if (prefab != null)
            {
                firstRow = UnityEngine.Object.Instantiate(prefab.gameObject, staging.transform, false);
                ScreenCopy.StripScripts(firstRow, ScreenScripts);
            }
        }
        if (firstRow == null)
            return false;
        firstRow.transform.SetParent(staging.transform, false);
        rowSource = firstRow;
        OwnRow(rowSource);
        Clear(listContent);
        var column = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        column.spacing = 6f;
        column.padding.left = column.padding.right = 10;
        column.padding.top = column.padding.bottom = 6;
        column.childAlignment = TextAnchor.UpperLeft;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var contentRect = listContent.Cast<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, contentRect.sizeDelta.y);
        if (list.Find("InvisibleContainer") is { } invisible)
            Clear(invisible);
        // A tag chip of the row: the chips' source.
        chipTemplate = firstRow.GetComponentInChildren<BhTag>(true)?.gameObject;
        return true;
    }

    // The save row's own script (an obfuscated slot class on its root, with its selection and double click) and its
    // button's persistent click off: they answered the clicks before ours. The Unity parts of the root are kept.
    void OwnRow(GameObject row)
    {
        foreach (var script in row.GetComponents<MonoBehaviour>().ToList())
            if (script != null && !(script.GetIl2CppType().FullName ?? "").StartsWith("UnityEngine.", StringComparison.Ordinal))
            {
                Logs.Once($"Mods window: the row's script {script.GetIl2CppType().FullName} taken off");
                UnityEngine.Object.DestroyImmediate(script);
            }
        if (row.GetComponent<Button>() is { } button)
            button.onClick = new Button.ButtonClickedEvent();
    }

    // The information panel: its title, picture, texts and buttons; the empty panel with a hint.
    void BuildPanel(Transform sar)
    {
        panel = sar.Find("SaveInfoPanel/RightPanelPopup");
        emptyPanel = sar.Find("SaveInfoPanel/EmptryPanel");
        var header = panel.Find("Container/Header");
        panelTitle = header.Find("header-window (1)")?.GetComponent<TMP_Text>();
        Hide(header, "Rename", "Legend");
        crossSource = header.Find("CROSS BUTTON (1)")?.gameObject;
        var back = panel.Find("Container/Main/Back");
        Hide(back, "MinimapScreenShot", "MissionIcon");
        FindPictureSquares(back);
        var info = panel.Find("InfoScrollView");
        infoContent = info.Find("Viewport/Content");
        FitViewport(info, 20f);
        lineTemplate = infoContent.Find("DifficultyLine")?.gameObject;
        wrapTemplate = infoContent.Find("Description")?.gameObject;
        foreach (var part in new[] { lineTemplate, wrapTemplate })
            part?.transform.SetParent(staging.transform, false);
        Clear(infoContent);
        if (info.Find("Scrollbar Vertical") is { } infoBar)
            infoBar.gameObject.SetActive(true);
        versionLabel = panel.Find("TimeSaving")?.GetComponent<TMP_Text>();
        versionValue = panel.Find("TimeSavingValue")?.GetComponent<TMP_Text>();
        originLabel = panel.Find("GameTimeSaving")?.GetComponent<TMP_Text>();
        originValue = panel.Find("GameTimeSavingValue")?.GetComponent<TMP_Text>();
        Hide(panel, "CloudButton", "SaveButton");
        primaryButton = panel.Find("LoadButton")?.GetComponent<BhGenericButton>();
        secondaryButton = panel.Find("SetupInLobby")?.GetComponent<BhGenericButton>();
        deleteButton = panel.Find("DeleteButton")?.GetComponent<BhGenericButton>();
        textButtonTemplate = secondaryButton != null ? UnityEngine.Object.Instantiate(secondaryButton.gameObject, staging.transform, false) : null;
        deleteTemplate = deleteButton != null ? UnityEngine.Object.Instantiate(deleteButton.gameObject, staging.transform, false) : null;
        if (emptyPanel == null || lineTemplate == null)
            return;
        var hint = UnityEngine.Object.Instantiate(lineTemplate, emptyPanel, false);
        hint.name = "OE_Hint";
        var rect = hint.transform.Cast<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(440f, 80f);
        var text = hint.GetComponent<TMP_Text>();
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        hint.SetActive(true);
    }

    // The bottom left group: select all and its text; the cloud and the delete wraps off (our buttons in their place).
    void BuildBottomGroup(Transform sar)
    {
        bottomGroup = sar.Find("LeftBottomGroup");
        selectAll = bottomGroup.Find("Toggle")?.GetComponent<BhToggle>();
        selectAllText = bottomGroup.Find("main")?.GetComponent<TMP_Text>();
        if (selectAllText != null)
        {
            selectAllText.enableWordWrapping = false;
            Size(selectAllText.gameObject, 250f, -1f);
        }
        Hide(bottomGroup, "CloudWrap", "DeleteWrap");
        Widen(bottomGroup.Cast<RectTransform>(), 1040f);
        if (bottomGroup.GetComponent<HorizontalLayoutGroup>() is { } bottomRow)
        {
            bottomRow.spacing = 14f;
            bottomRow.childAlignment = TextAnchor.MiddleLeft;
        }
        selectAll?.toggle.onValueChanged.AddListener((UnityAction<bool>)(value => Guard("select all", () =>
        {
            if (!filling)
                Redraw(() =>
                {
                    foreach (var mod in InstalledShown())
                        Select(mod.Id, value);
                });
        })));
    }

    // The filter row: the text fields; on the Catalog the sorting dropdown, the Load screen's own filter (a funnel
    // opening check marks) for the tags and a copy of it for the sources, each after a label in the style of the row's
    // own "game type" label.
    void BuildFilterRow(Transform sar)
    {
        filterRow = sar.Find("FilterSortGroup");
        Hide(filterRow, "GameTypeDropdown");
        var sorting = filterRow.Find("SortingDropdown")?.GetComponent<BhDropdown>();
        if (sorting != null)
        {
            dropdownSource = UnityEngine.Object.Instantiate(sorting.gameObject, staging.transform, false);
            sortDropdown = sorting;
            BindDropdown(sortDropdown, i => Redraw(() => sortMode = Math.Max(0, i)));
        }
        var labelSource = filterRow.Find("GameTypeHeader")?.gameObject;
        if (filterRow.Find("Filter")?.GetComponent<Hex.UI.SaveLoad.BhFilter>() is { } filter)
        {
            // The save list's own options (Campaign, Template...) are elements under its list, not in the filter's list
            // of them: gone, ours are made by its set-up.
            Clear(filter.contentList);
            tagBox = filter;
            tagLabel = FilterLabel(labelSource, W + "tagsLabel", filter.transform.GetSiblingIndex());
            var copy = UnityEngine.Object.Instantiate(filter.gameObject, filterRow, false);
            copy.name = "OE_SourceFilter";
            sourceBox = copy.GetComponent<Hex.UI.SaveLoad.BhFilter>();
            sourceLabel = FilterLabel(labelSource, W + "sourcesLabel", copy.transform.GetSiblingIndex());
        }
        labelSource?.SetActive(false);
        inputTemplate = Scene.Find<ScInputBox>()?.inputField;
        filterInput = NewInput(filterRow, W + "filter", value => Redraw(() => filterText = value ?? ""));
        searchInput = NewInput(filterRow, W + "search", value => Redraw(() => searchText = value ?? ""));
        filterInput?.transform.SetAsFirstSibling();
        searchInput?.transform.SetAsFirstSibling();
        Widen(filterRow.Cast<RectTransform>(), 1040f);
        if (sortDropdown != null)
            Size(sortDropdown.gameObject, 300f, -1f);
        if (filterRow.GetComponent<HorizontalLayoutGroup>() is { } filterLayout)
        {
            filterLayout.spacing = 14f;
            filterLayout.childAlignment = TextAnchor.MiddleLeft;
        }
    }

    // The tabs: the game's two and a third, set up by the game's own Init (their click selects through Select).
    void BuildTabs(Transform sar)
    {
        simpleTabs = sar.Find("Tabs")?.GetComponent<BhSimpleTabs>();
        if (simpleTabs == null || simpleTabs.tabs.Count == 0)
            return;
        var first = simpleTabs.tabs[0];
        var third = UnityEngine.Object.Instantiate(first.gameObject, first.transform.parent, false).GetComponent<BhSimpleTab>();
        third.name = "OE_Tab3";
        simpleTabs.tabs.Add(third);
        tabList.Clear();
        for (var i = 0; i < simpleTabs.tabs.Count; i++)
            tabList.Add(simpleTabs.tabs[i]);
        var width = simpleTabs.transform.Cast<RectTransform>().rect.width / 3f;
        foreach (var t in tabList)
        {
            Size(t.gameObject, width, -1f);
            t.transform.Cast<RectTransform>().sizeDelta = new Vector2(width, t.transform.Cast<RectTransform>().sizeDelta.y);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(simpleTabs.transform.Cast<RectTransform>());
        var data = new Il2CppSystem.Collections.Generic.List<GameSimpleTabData>();
        foreach (var which in WindowTabs)
        {
            var item = new GameSimpleTabData();
            item.Label = TabLabel(which);
            var chosen = which;
            item.Click = (Il2CppSystem.Action)(Action)(() => Guard("tab", () =>
            {
                if (!filling && tab != chosen)
                    Redraw(() => SwitchTab(chosen));
            }));
            data.Add(item);
        }
        filling = true;
        try { simpleTabs.Init(data, (int)tab); }
        finally { filling = false; }
        // The game's tab answers the press (OnPointerDown); a click (keyboard, tools) selects it the same way, once.
        for (var i = 0; i < tabList.Count; i++)
        {
            var index = i;
            Clickable(tabList[i].gameObject, () =>
            {
                if ((int)tab != index)
                    simpleTabs.Select(index, true);
            });
        }
    }

    // A scroll view's viewport over the whole view but its scrollbar's strip on the right. The Load screen sizes it
    // itself when it shows (its list script, taken off): on a copy of a screen never shown it is 0x0 and draws nothing.
    static void FitViewport(Transform view, float scrollbar)
    {
        if (view?.Find("Viewport") is not { } viewport)
            return;
        var rect = viewport.Cast<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(-scrollbar, 0f);
        if (view.GetComponent<ScrollRect>() is { } scroll)
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
    }

    // ---------- filling ----------

    void Redraw(Action change)
    {
        change();
        windowDirty = true;
    }

    void SwitchTab(WindowTab which)
    {
        tab = which;
        bigShotIndex = -1;
        shotIndex = 0;
    }

    static string TabLabel(WindowTab which) => string.Format(Texts.Get(W + "tab." + which.ToString().ToLowerInvariant()), which switch
    {
        WindowTab.Installed => Installed().Count,
        WindowTab.Catalog => Catalog().Count,
        _ => ModCatalog.Sources.Count,
    });

    void FillWindow()
    {
        windowRevision = ModMenu.Revision;
        windowDirty = false;
        filling = true;
        try
        {
            if (titleText != null)
                SetText(titleText, Texts.Get(ModCatalog.TitleKey ?? TabKey));
            var status = ModCatalog.StatusKey != null ? Texts.Get(ModCatalog.StatusKey) : "";
            // The manager says itself that it works; the window's own word only when it says nothing.
            if (ModCatalog.Busy && status.Length == 0)
                status = Texts.Get(W + "busy");
            if (statusText != null)
                SetText(statusText, status);

            var installedIds = Installed().Select(m => m.Id).ToHashSet();
            selected.RemoveWhere(id => !installedIds.Contains(id));
            if (installedPick != null && !installedIds.Contains(installedPick) && installedPick != ModCatalog.Manager?.Id)
                installedPick = null;
            if (catalogPick != null && Catalog().All(m => m.Id != catalogPick))
                catalogPick = null;
            if (sourcePick != null && ModCatalog.Sources.All(s => s.Url != sourcePick))
                sourcePick = null;

            // The tabs' labels (with the counts): the game's tab keeps them in its labels.
            for (var i = 0; i < tabList.Count && i < WindowTabs.Length; i++)
                foreach (var label in tabList[i].labels)
                    SetText(label, TabLabel(WindowTabs[i]));

            filterInput?.gameObject.SetActive(tab == WindowTab.Installed);
            searchInput?.gameObject.SetActive(tab == WindowTab.Catalog);
            foreach (var part in new Component[] { sortDropdown, tagBox, sourceBox })
                part?.gameObject.SetActive(tab == WindowTab.Catalog);
            tagLabel?.SetActive(tab == WindowTab.Catalog);
            sourceLabel?.SetActive(tab == WindowTab.Catalog);

            Clear(listContent);
            ClearBottom();
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
            FillBigShot();
            LayoutRebuilder.ForceRebuildLayoutImmediate(window.GetComponent<RectTransform>());
        }
        finally { filling = false; }
    }

    // ---------- closing ----------

    void HideModsWindow()
    {
        // At once: the reloaded build looks for them in the same frame.
        screen?.Destroy();
        screen = null;
        MainMenu.Remove(modsButton);
        pictures?.Clear();
        window = staging = null;
        modsButton = null;
        titleText = statusText = nothingText = selectAllText = panelTitle = versionLabel = versionValue = originLabel = originValue = null;
        simpleTabs = null;
        tabList.Clear();
        filterRow = listContent = bottomGroup = panel = emptyPanel = infoContent = bigShotView = null;
        selectAll = null;
        shotSlots.Clear();
        squaresBack = null;
        panelShots.Clear();
        bigShotIndex = -1;
        primaryButton = deleteButton = secondaryButton = null;
        filterInput = searchInput = inputTemplate = null;
        sortDropdown = null;
        tagBox = sourceBox = null;
        tagLabel = sourceLabel = null;
        tagBoxKeys = sourceBoxKeys = sortOptions = null;
        rowSource = lineTemplate = wrapTemplate = chipTemplate = textButtonTemplate = crossSource = dropdownSource = deleteTemplate = null;
        switchSource = null;
        greyed.Clear();
        suspended = false;
        pendingBox = null;
    }

    void Guard(string what, Action action)
    {
        try { action(); }
        catch (Exception e) { Log.LogError($"mods window: {what}: {e}"); }
    }
}
