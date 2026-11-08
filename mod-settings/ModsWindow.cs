using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hex.Settings.UI;
using Hex.UI;
using Hex.UI.Elements;
using Hex.UI.GeneralKit;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using OldenEra.Modding.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// The mods' own window (issue #13, #14, #6): a "Mods" button in the main menu under Additional (a copy of the game's
// Options button) opens a copy of the game's Load screen (Hex.UI.SaveLoad.ScLoad, fogone 08.11: the game's existing
// screen style) on a canvas of its own, its scripts taken off and its parts filled with ours:
//   its title and close cross; its tabs (BhSimpleTabs: Installed / Catalog / Sources, a third tab copied); its filter row
//   (a text field from the game's input box, its sorting dropdown as the tag and source filters); its list with the
//   scrollbar, rows made from its save slot rows (check box, picture, name, tag chips, information on the right); its
//   bottom left group (select all, the actions on the selected; Add by URL / from GitHub on Sources); its information
//   panel on the right as a mod's or a source's details (picture, texts, chips, buttons); the cloud quota line as the
//   state line. Adding a source asks with the game's own message box and input box.
// The data and the actions are the mod manager's (ModCatalog, mod-common/ModMenu.cs).
public partial class Plugin
{
    const string ModsButtonName = "OE_ModsButton", WindowName = "OE_ModsWindow", CloseKey = "oe.settings.close", W = "oe.settings.w.";
    // Drawn as the game's screens are, over its Load screen (PlaceCanvas); without a UI camera over everything, under the
    // 30000 of Unity's dropdown lists.
    const int WindowOrder = 29000, ScreenOrder = 1600;
    static readonly string[] CheckEveryValues = { "start", "daily", "weekly", "manual" };

    // The Load screen's scripts that run it (taken off the copy: it shows our data, not saves).
    static readonly HashSet<string> ScreenScripts = new()
    {
        "ScLoad", "BhLoadingRoot", "BhSaveLoadBase", "BhVirtualizedList", "BhSaveInfoPanel", "BhDescriptionBlocks", "BhCloudQuotaValue",
        "BhSessionInfoElement", "BhMapPreview", "BhHeroSlotsContainer", "BhFilter", "UiAnimatorSOPrime", "BhMapPreviewInfoView",
    };

    enum WindowTab { Installed, Catalog, Sources }

    BhNavigationButton modsButton;
    GameObject window, staging;
    int windowRevision = -1, nextButtonSearch;
    bool windowDirty, filling;

    // The parts of the copied screen.
    TMP_Text titleText, statusText, nothingText, selectAllText, panelTitle, versionLabel, versionValue, originLabel, originValue;
    BhSimpleTabs simpleTabs;
    readonly List<BhSimpleTab> tabList = new();
    Transform filterRow, listContent, bottomGroup, panel, emptyPanel, infoContent, bigShot;
    BhToggle selectAll;
    RawImage panelPicture;
    BhGenericButton primaryButton, deleteButton, secondaryButton;
    TMP_InputField filterInput, searchInput;
    BhDropdown tagDropdown, sourceDropdown;
    string tagOptions, sourceOptions;

    // Copy sources: rows, texts, chips, buttons (all from the copy), and the settings' switch, the input box's field.
    GameObject rowSource, lineTemplate, wrapTemplate, chipTemplate, textButtonTemplate, crossSource, dropdownSource, deleteTemplate;
    BhSettingsCheckbox switchSource;
    TMP_InputField inputTemplate;

    // What the player chose.
    WindowTab tab = WindowTab.Installed;
    string filterText = "", searchText = "", tagFilter, sourceFilter, installedPick, catalogPick, sourcePick, bigShotPath;
    int shotIndex;
    readonly HashSet<string> selected = new();
    readonly HashSet<string> screenshotsAsked = new();
    readonly Dictionary<string, Texture2D> textures = new();

    // A game box (the warning, the input box) is open: our window waits hidden under it.
    bool suspended;
    int suspendedAt, resumedAt = -10;
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
        // The Escape that closed a game box over the window is not the window's (the box calls back in the same frame).
        if (Input.GetKeyDown(KeyCode.Escape) && Time.frameCount > resumedAt + 1)
        {
            if (bigShotPath != null)
                Redraw(() => bigShotPath = null);
            else
                window.SetActive(false);
        }
        else if (windowDirty || windowRevision != ModMenu.Revision)
            Guard("fill", FillWindow);
        GreyLater();
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
            PlaceCanvas();
            window.SetActive(true);
            suspended = false;
            screenshotsAsked.Clear();
            windowRevision = -1;
            FillWindow();
        }
        catch (Exception e) { Log.LogError($"mods window: {e}"); }
    }

    // ---------- building ----------

    // The Load screen's running scripts off a copy (they would show saves).
    static void StripScreenScripts(GameObject go)
    {
        foreach (var script in go.GetComponentsInChildren<MonoBehaviour>(true).ToList())
            if (script != null && ScreenScripts.Contains(script.GetIl2CppType().Name))
                UnityEngine.Object.DestroyImmediate(script);
    }

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
        // Made under an inactive holder first: the copy's scripts do not wake up before they are taken off.
        var holder = new GameObject("OE_ModsHolder");
        holder.SetActive(false);
        window = UnityEngine.Object.Instantiate(load.gameObject, holder.transform, false);
        window.name = WindowName;
        StripScreenScripts(window);
        window.transform.SetParent(null, false);
        UnityEngine.Object.Destroy(holder);
        UnityEngine.Object.DontDestroyOnLoad(window);
        window.SetActive(true);
        var canvas = window.GetComponent<Canvas>();
        canvas.enabled = true;
        PlaceCanvas();
        if (window.GetComponent<CanvasGroup>() is { } rootGroup)
        {
            rootGroup.alpha = 1f;
            rootGroup.interactable = rootGroup.blocksRaycasts = true;
        }
        staging = new GameObject("OE_Staging", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        staging.transform.SetParent(window.transform, false);
        staging.SetActive(false);

        var root = window.transform;
        var sar = root.Find("ShowAnimationRoot");
        // The screen as it is when shown: its show animation (taken off) starts it a little smaller and lower (0.98,
        // 58 lower on 0.82.7) and ends at its own size, 18.6 lower than the middle (measured on screen against a copy of
        // the shown screen, 08.11).
        sar.localScale = Vector3.one;
        sar.localPosition = new Vector3(0f, -18.6f, 0f);
        // Its backdrop opaque: the Load screen hides the menu under it, our copy does not.
        if (root.Find("WindowBack")?.GetComponent<Image>() is { } backdrop)
            backdrop.color = new Color(backdrop.color.r, backdrop.color.g, backdrop.color.b, 1f);
        titleText = root.Find("MainHeader")?.GetComponent<TMP_Text>();
        Hide(root, "ButtonBack", "BackToSaving", "ExampleImage");
        if (root.Find("ButtonClose")?.GetComponent<BhGenericButton>() is { } close)
            Bind(close, null, () => window.SetActive(false), false);
        // The state line: a copy of the title, smaller, just under it (the same parent and anchors); the cloud quota and
        // the autosaves' header and switch off.
        Hide(sar, "CloudQuota", "AutosaveShow", "AutosavesHeader");
        if (titleText != null)
        {
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
        nothingText = sar.Find("NoSavesHeader")?.GetComponent<TMP_Text>();

        // The list: its rows' layout ours (the game's virtualized list placed them itself), its scrollbar kept.
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
                StripScreenScripts(firstRow);
            }
        }
        if (firstRow == null)
        {
            Log.LogWarning("mods window: no save row to copy");
            UnityEngine.Object.DestroyImmediate(window);
            window = null;
            return false;
        }
        firstRow.transform.SetParent(staging.transform, false);
        rowSource = firstRow;
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

        // The information panel: its texts and buttons.
        panel = sar.Find("SaveInfoPanel/RightPanelPopup");
        emptyPanel = sar.Find("SaveInfoPanel/EmptryPanel");
        var header = panel.Find("Container/Header");
        panelTitle = header.Find("header-window (1)")?.GetComponent<TMP_Text>();
        Hide(header, "Rename", "Legend");
        crossSource = header.Find("CROSS BUTTON (1)")?.gameObject;
        var back = panel.Find("Container/Main/Back");
        Hide(back, "MinimapScreenShot", "MissionIcon");
        panelPicture = back.Find("TemplatePreview")?.GetComponent<RawImage>();
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
        if (emptyPanel != null && lineTemplate != null)
        {
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

        // The bottom left group: select all and its text; the cloud and the delete wrap off (our buttons in their place).
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
        if (selectAll != null)
            selectAll.toggle.onValueChanged.AddListener((UnityAction<bool>)(value => Guard("select all", () =>
            {
                if (filling)
                    return;
                Redraw(() =>
                {
                    foreach (var mod in InstalledShown())
                        if (value)
                            selected.Add(mod.Id);
                        else
                            selected.Remove(mod.Id);
                });
            })));

        // The filter row: the sorting dropdown as the tag filter, a copy of it as the source filter, the text fields.
        filterRow = sar.Find("FilterSortGroup");
        Hide(filterRow, "Filter", "GameTypeHeader", "GameTypeDropdown");
        var sorting = filterRow.Find("SortingDropdown")?.GetComponent<BhDropdown>();
        if (sorting != null)
        {
            dropdownSource = UnityEngine.Object.Instantiate(sorting.gameObject, staging.transform, false);
            tagDropdown = sorting;
            BindDropdown(tagDropdown, i => Redraw(() => tagFilter = i <= 0 ? null : TagKeys().ElementAtOrDefault(i - 1)));
            sourceDropdown = UnityEngine.Object.Instantiate(dropdownSource, filterRow, false).GetComponent<BhDropdown>();
            sourceDropdown.name = "OE_SourceDropdown";
            sourceDropdown.gameObject.SetActive(true);
            BindDropdown(sourceDropdown, i => Redraw(() => sourceFilter = i <= 0 ? null : SourceKeys().ElementAtOrDefault(i - 1)));
        }
        inputTemplate = Scene.Find<ScInputBox>()?.inputField;
        filterInput = NewInput(filterRow, W + "filter", value => Redraw(() => filterText = value ?? ""));
        searchInput = NewInput(filterRow, W + "search", value => Redraw(() => searchText = value ?? ""));
        filterInput?.transform.SetAsFirstSibling();
        searchInput?.transform.SetAsFirstSibling();
        Widen(filterRow.Cast<RectTransform>(), 1040f);
        if (filterRow.GetComponent<HorizontalLayoutGroup>() is { } filterLayout)
        {
            filterLayout.spacing = 14f;
            filterLayout.childAlignment = TextAnchor.MiddleLeft;
        }

        // The tabs: the game's two and a third, set up by the game's own Init (their click selects through Select).
        simpleTabs = sar.Find("Tabs")?.GetComponent<BhSimpleTabs>();
        if (simpleTabs != null && simpleTabs.tabs.Count > 0)
        {
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
            foreach (var which in new[] { WindowTab.Installed, WindowTab.Catalog, WindowTab.Sources })
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

        // A screenshot big, over everything (a click closes it).
        bigShot = NewRect("OE_BigShot", root);
        Stretch(bigShot.Cast<RectTransform>());
        bigShot.gameObject.SetActive(false);

        switchSource = switchTemplate != null ? switchTemplate.GetComponentInChildren<BhSettingsCheckbox>(true) : null;
        Logs.Info($"Mods window: built from the game's Load screen; tabs {tabList.Count}, input {(inputTemplate != null)}, dropdown {(tagDropdown != null)}, " +
                  $"chip {(chipTemplate != null)}, text {(lineTemplate != null)}, buttons {(textButtonTemplate != null)}, switch {(switchSource != null)}");
        return true;
    }

    // A scroll view's viewport over the whole view but its scrollbar's strip on the right. The Load screen sizes it
    // itself when it shows (its list script, taken off): a copy made while the screen was never shown had it 0x0, and
    // nothing in it was drawn (08.11).
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

    static void Hide(Transform parent, params string[] names)
    {
        if (parent == null)
            return;
        foreach (var name in names)
            if (parent.Find(name) is { } part)
                part.gameObject.SetActive(false);
    }

    // ---------- filling ----------

    void Redraw(Action change)
    {
        change();
        windowDirty = true;
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
            if (ModCatalog.Busy)
                status = status.Length > 0 ? $"{status} · {Texts.Get(W + "busy")}" : Texts.Get(W + "busy");
            if (statusText != null)
            {
                SetText(statusText, status);
            }

            var installedIds = Installed().Select(m => m.Id).ToHashSet();
            selected.RemoveWhere(id => !installedIds.Contains(id));
            if (installedPick != null && !installedIds.Contains(installedPick) && installedPick != ModCatalog.Manager?.Id)
                installedPick = null;
            if (catalogPick != null && Catalog().All(m => m.Id != catalogPick))
                catalogPick = null;
            if (sourcePick != null && ModCatalog.Sources.All(s => s.Url != sourcePick))
                sourcePick = null;

            // The tabs' labels (with the counts): the game's tab keeps them in its labels.
            var tabs = new[] { WindowTab.Installed, WindowTab.Catalog, WindowTab.Sources };
            for (var i = 0; i < tabList.Count && i < tabs.Length; i++)
                foreach (var label in tabList[i].labels)
                    SetText(label, TabLabel(tabs[i]));

            filterInput?.gameObject.SetActive(tab == WindowTab.Installed);
            searchInput?.gameObject.SetActive(tab == WindowTab.Catalog);
            tagDropdown?.gameObject.SetActive(tab == WindowTab.Catalog);
            sourceDropdown?.gameObject.SetActive(tab == WindowTab.Catalog);

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

    // A row made from the game's save slot row: check box, picture, name, tag chips; on the right the version, the
    // note; the switch, the update (to the new version) and the remove cross. The manager: its update only.
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
        var check = row.transform.Find("Toggle")?.GetComponent<BhToggle>();
        if (check != null)
        {
            check.gameObject.SetActive(!catalog && !isManager);
            if (!catalog && !isManager)
            {
                check.ShowValue(selected.Contains(mod.Id));
                check.toggle.onValueChanged.AddListener((UnityAction<bool>)(value => Guard("check", () =>
                {
                    if (filling)
                        return;
                    Redraw(() =>
                    {
                        if (value)
                            selected.Add(mod.Id);
                        else
                            selected.Remove(mod.Id);
                    });
                })));
            }
        }
        // The row's picture is its catalog's icon (fogone 08.11; screenshots are the details'), else its first letter.
        RowPicture(row, CatalogIcon(mod.SourceNameKey), Texts.Get(mod.NameKey));
        RowName(row, Texts.Get(mod.NameKey), mod.TagKeys);
        if (catalog)
            RowInfo(row, mod.SourceNameKey != null ? Texts.Get(mod.SourceNameKey) : "", mod.Available ?? mod.Installed ?? "",
                mod.Installed != null ? Texts.Get(W + "installed") : mod.NoteKey != null ? Texts.Get(mod.NoteKey) : "");
        else
        {
            // The catalog it comes from on top (fogone 08.11), "by hand" when none; the version; the note ("by hand" once).
            var origin = mod.SourceNameKey != null ? Texts.Get(mod.SourceNameKey) : Texts.Get(W + "byHand");
            var note = mod.NoteKey != null ? Texts.Get(mod.NoteKey) : "";
            RowInfo(row, origin, mod.Installed ?? "", note == origin ? "" : note);
        }
        if (catalog)
            return;

        // The controls, left of the information.
        var controls = NewRect("OE_Controls", row.transform);
        controls.anchorMin = controls.anchorMax = controls.pivot = new Vector2(1f, 0.5f);
        controls.anchoredPosition = new Vector2(-370f, 0f);
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

    // The row's own parts: its selected look, the cloud and the map's icons off, a click picks it.
    void RowBase(GameObject row, bool picked, Action pick)
    {
        var t = row.transform;
        Hide(t, "CloudOn", "CloudOff", "InfoIcons", "CampaignInfo");
        Hide(t.Find("HeaderGroup"), "RenameBtn");
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

    // The row's picture: a screenshot or the catalog's icon in the save's icon frame; with none, the name's first
    // letter in the frame, in the name's own font and colour (as the game's campaign rows show their act's numeral).
    void RowPicture(GameObject row, string path, string name)
    {
        var icon = row.transform.Find("IconFrame/Icon");
        if (icon == null)
            return;
        // The save's icon is not ours.
        if (icon.GetComponent<Image>() is { } image)
            image.enabled = false;
        if (Picture(path) != null)
        {
            var picture = NewRect("OE_Shot", icon).gameObject.AddComponent<RawImage>();
            Stretch(picture.rectTransform);
            picture.raycastTarget = false;
            ShowPicture(picture, path, crop: true);
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
        // The game's two chips: the first two tags.
        var chips = group.GetComponentsInChildren<BhTag>(true).ToList();
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
    static void ShowChip(BhTag chip, string key)
    {
        chip.ShowText(Texts.Get(key));
        chip.ShowColor(new Color(0.33f, 0.19f, 0.40f, 1f));
        if (chip.label != null)
            chip.label.color = new Color(0.92f, 0.88f, 0.80f, 1f);
    }

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
        tagOptions = SetOptions(tagDropdown, W + "tags.all", tagKeys, tagFilter, tagOptions);
        sourceOptions = SetOptions(sourceDropdown, W + "sources.all", sourceKeys, sourceFilter, sourceOptions);

        var mods = Catalog().Where(m => Matches(m, searchText) && (tagFilter == null || (m.TagKeys?.Contains(tagFilter) ?? false)) &&
                                        (sourceFilter == null || m.SourceNameKey == sourceFilter)).ToList();
        foreach (var mod in mods)
            ModRow(mod, isManager: false, catalog: true);
        ShowNothing(mods.Count == 0, W + "nothing");
    }

    // ---------- Sources ----------

    void FillSources()
    {
        foreach (var source in ModCatalog.Sources)
        {
            var row = Copy(rowSource, listContent);
            row.name = "OE_Source";
            row.SetActive(true);
            var url = source.Url;
            RowBase(row, sourcePick == url, () => sourcePick = url);
            row.transform.Find("Toggle")?.gameObject.SetActive(false);
            RowPicture(row, source.IconPath, Texts.Get(source.TitleKey));
            RowName(row, Texts.Get(source.TitleKey), source.BuiltIn ? new[] { W + "builtIn" } : null);
            RowInfo(row, string.Format(Texts.Get(W + "mods"), source.Mods), Checked(source), source.NoteKey != null ? Texts.Get(source.NoteKey) : "");
        }
        ShowNothing(ModCatalog.Sources.Count == 0, W + "nothing");
        BottomButton(W + "addUrl", ModCatalog.AddSource != null, () => AskForSource(W + "url.caption", W + "url.placeholder", ModCatalog.AddSource), 260f);
        BottomButton(W + "addGitHub", ModCatalog.AddGitHubSource != null, () => AskForSource(W + "github.caption", W + "github.placeholder", ModCatalog.AddGitHubSource), 260f);
        BottomButton(W + "checkAll", ModCatalog.CheckAll != null, () => ModCatalog.CheckAll?.Invoke(), 220f);
    }

    static string Checked(CatalogSource source) => source.LastChecked is { } when
        ? string.Format(Texts.Get(W + "checked"), when.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture))
        : Texts.Get(W + "checkedNever");

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

    // ---------- details ----------

    void FillDetails()
    {
        Clear(infoContent);
        if (tab == WindowTab.Sources)
        {
            var source = ModCatalog.Sources.FirstOrDefault(s => s.Url == sourcePick);
            ShowPanel(source != null, W + "pickSource");
            if (source != null)
                SourceDetails(source);
            return;
        }
        var id = tab == WindowTab.Installed ? installedPick : catalogPick;
        var mod = id == null ? null : ModCatalog.Manager?.Id == id ? ModCatalog.Manager : ModCatalog.Mods.FirstOrDefault(m => m.Id == id);
        ShowPanel(mod != null, W + "pickMod");
        if (mod == null)
            return;
        if (screenshotsAsked.Add(mod.Id) && ModCatalog.LoadScreenshots is { } load)
            Guard("load screenshots " + mod.Id, () => load(mod.Id));

        if (panelTitle != null)
            SetText(panelTitle, Texts.Get(mod.NameKey));
        // The picture: the screenshot chosen (a click shows it big); none: the catalog's icon; neither: the panel's own
        // ornaments.
        var shots = (mod.Screenshots ?? Array.Empty<ModScreenshot>()).Where(s => s?.Path != null).ToList();
        if (shotIndex >= shots.Count)
            shotIndex = 0;
        if (shots.Count > 0 && panelPicture != null)
        {
            ShowPanelPicture(null);
            panelPicture.gameObject.SetActive(true);
            ShowPicture(panelPicture, shots[shotIndex].Path, crop: false);
            var path = shots[shotIndex].Path;
            Clickable(panelPicture.gameObject, () => Redraw(() => bigShotPath = path));
        }
        else
            ShowPanelPicture(CatalogIcon(mod.SourceNameKey));
        SetPair(versionLabel, versionValue, Texts.Get(W + "versionLabel"),
            mod.Installed != null && mod.HasUpdate && mod.Available != null ? $"{mod.Installed} / {mod.Available}" : mod.Installed ?? mod.Available ?? "");
        SetPair(originLabel, originValue, Texts.Get(W + "catalogLabel"), mod.SourceNameKey != null ? Texts.Get(mod.SourceNameKey) : Texts.Get(W + "byHand"));

        if (!string.IsNullOrEmpty(mod.Author))
            InfoLine(string.Format(Texts.Get(W + "author"), mod.Author));
        InfoLine(mod.Installed != null && mod.HasUpdate && mod.Available != null ? string.Format(Texts.Get(W + "versions"), mod.Installed, mod.Available)
            : mod.Installed != null ? string.Format(Texts.Get(W + "version"), mod.Installed)
            : mod.Available != null ? string.Format(Texts.Get(W + "available"), mod.Available) : "");
        if (mod.NoteKey != null)
            InfoLine($"<color=#A5A5A5>{Texts.Get(mod.NoteKey)}</color>");
        // The thumbnails: a click shows that one in the picture.
        if (shots.Count > 1)
        {
            var strip = NewRect("OE_Shots", infoContent);
            Size(strip.gameObject, 440f, 70f);
            HRow(strip.gameObject, 8f, TextAnchor.MiddleLeft);
            for (var i = 0; i < shots.Count && i < 5; i++)
            {
                var thumb = NewRect("Shot", strip);
                Size(thumb.gameObject, 112f, 63f);
                var picture = thumb.gameObject.AddComponent<RawImage>();
                ShowPicture(picture, shots[i].Path, crop: true);
                var index = i;
                Clickable(thumb.gameObject, () => Redraw(() => shotIndex = index));
            }
        }
        if (mod.TagKeys is { Count: > 0 } tagKeys)
            Chips(tagKeys);
        if (mod.DescriptionKey != null)
            InfoText(Texts.Get(mod.DescriptionKey));
        if (mod.RequiresKeys is { Count: > 0 } requires)
            InfoText(string.Format(Texts.Get(W + "requires"), string.Join(", ", requires.Select(Texts.Get))));

        // The buttons by tab: the big one Update / Install, the bin Remove.
        // No Update and no Remove for a mod BepInEx loaded itself (ByHand).
        if (tab == WindowTab.Installed)
        {
            PanelButton(primaryButton, W + "update", mod.HasUpdate && !mod.ByHand && ModCatalog.Update != null, () => ModCatalog.Update?.Invoke(new[] { mod.Id }));
            PanelButton(deleteButton, null, mod != ModCatalog.Manager && mod.Installed != null && !mod.RequiredByOthers && !mod.ByHand && ModCatalog.Remove != null,
                () => ModCatalog.Remove?.Invoke(new[] { mod.Id }));
        }
        else
        {
            PanelButton(primaryButton, W + "install", mod.Installed == null && mod.Available != null && ModCatalog.Install != null, () => ModCatalog.Install?.Invoke(mod.Id));
            PanelButton(deleteButton, null, false, null);
        }
        PanelButton(secondaryButton, null, false, null);
    }

    void SourceDetails(CatalogSource source)
    {
        if (panelTitle != null)
            SetText(panelTitle, Texts.Get(source.TitleKey));
        ShowPanelPicture(source.IconPath);
        SetPair(versionLabel, versionValue, Texts.Get(W + "modsLabel"), source.Mods.ToString());
        SetPair(originLabel, originValue, Texts.Get(W + "checkedLabel"), source.LastChecked is { } when
            ? when.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture) : Texts.Get(W + "checkedNever"));
        InfoText($"<color=#A5A5A5>{source.Url}</color>");
        if (source.BuiltIn)
            InfoLine(Texts.Get(W + "builtIn"));
        if (source.NoteKey != null)
            InfoText(Texts.Get(source.NoteKey));
        if (source.SetCheckEvery is { } setEvery && dropdownSource != null)
        {
            InfoLine(Texts.Get(W + "every"));
            var holder = NewRect("OE_Every", infoContent);
            Size(holder.gameObject, 440f, 40f);
            // Laid out from the left by the holder (the dropdown's own anchors are its row's).
            var everyRow = HRow(holder.gameObject, 0f, TextAnchor.MiddleLeft);
            everyRow.childControlWidth = everyRow.childControlHeight = false;
            var dropdown = Copy(dropdownSource, holder).GetComponent<BhDropdown>();
            dropdown.gameObject.SetActive(true);
            BindDropdown(dropdown, i =>
            {
                if (i >= 0 && i < CheckEveryValues.Length)
                    Guard("check every", () => setEvery(CheckEveryValues[i]));
            });
            var texts = Enumerable.Range(0, CheckEveryValues.Length).Select(i => Texts.Get($"{W}every.{i}")).ToArray();
            dropdown.Init(new Il2CppSystem.Array(new Il2CppStringArray(texts).Pointer), Math.Max(0, Array.IndexOf(CheckEveryValues, source.CheckEvery)), EDropdownType.Text);
        }
        PanelButton(primaryButton, W + "check", source.Check != null, () => source.Check?.Invoke());
        PanelButton(deleteButton, null, source.Remove != null && !source.BuiltIn, () => source.Remove?.Invoke());
        PanelButton(secondaryButton, null, false, null);
    }

    // An icon in the panel's picture area: whole, centred, at most a square in its middle (the area is the screenshot's,
    // much bigger than an icon); null: the area off (the panel's ornaments show).
    void ShowPanelPicture(string iconPath)
    {
        if (panelPicture == null)
            return;
        Clear(panelPicture.transform);
        var texture = Picture(iconPath);
        panelPicture.gameObject.SetActive(texture != null);
        if (texture == null)
            return;
        panelPicture.texture = null;
        panelPicture.color = new Color(0f, 0f, 0f, 0f);
        Clickable(panelPicture.gameObject, () => { });
        var icon = NewRect("OE_Icon", panelPicture.transform).gameObject.AddComponent<RawImage>();
        icon.raycastTarget = false;
        icon.texture = texture;
        var rect = icon.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        var side = Math.Min(260f, Math.Min(panelPicture.rectTransform.rect.width, panelPicture.rectTransform.rect.height) - 40f);
        var aspect = texture.height > 0 ? (float)texture.width / texture.height : 1f;
        rect.sizeDelta = aspect >= 1f ? new Vector2(side, side / aspect) : new Vector2(side * aspect, side);
    }

    // The panel, or the empty one with a hint.
    void ShowPanel(bool show, string hintKey)
    {
        panel?.gameObject.SetActive(show);
        if (emptyPanel == null)
            return;
        emptyPanel.gameObject.SetActive(!show);
        if (emptyPanel.Find("OE_Hint")?.GetComponent<TMP_Text>() is { } hint)
            SetText(hint, Texts.Get(hintKey));
    }

    static void SetPair(TMP_Text label, TMP_Text value, string labelText, string valueText)
    {
        if (label != null)
            SetText(label, labelText);
        if (value != null)
            SetText(value, valueText);
    }

    // A line of the panel's information, in its own style.
    void InfoLine(string text)
    {
        if (lineTemplate == null || string.IsNullOrEmpty(text))
            return;
        var line = Copy(lineTemplate, infoContent);
        line.name = "OE_Line";
        line.SetActive(true);
        var tmp = line.GetComponent<TMP_Text>();
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        SetText(tmp, text);
    }

    // A text that wraps (the description), as the panel's own description.
    void InfoText(string text)
    {
        var template = wrapTemplate ?? lineTemplate;
        if (template == null || string.IsNullOrEmpty(text))
            return;
        var block = Copy(template, infoContent);
        block.name = "OE_Text";
        block.SetActive(true);
        var tmp = block.GetComponent<TMP_Text>();
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        SetText(tmp, text);
        if (block.GetComponent<LayoutElement>() is { } element)
            element.preferredHeight = element.minHeight = -1f;
        if (block.GetComponent<ContentSizeFitter>() == null)
            block.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    // The tags as the game's chips, in rows as wide as the panel.
    void Chips(IReadOnlyList<string> keys)
    {
        if (chipTemplate == null)
            return;
        Transform row = null;
        var width = 0f;
        foreach (var key in keys)
        {
            if (row == null || width > 360f)
            {
                row = NewRect("OE_Chips", infoContent);
                Size(row.gameObject, 440f, 26f);
                HRow(row.gameObject, 6f, TextAnchor.MiddleLeft);
                width = 0f;
            }
            var chip = Copy(chipTemplate, row).GetComponent<BhTag>();
            chip.gameObject.SetActive(true);
            ShowChip(chip, key);
            width += (chip.layoutElement != null ? chip.layoutElement.preferredWidth : 80f) + 6f;
        }
    }

    // A button of the panel: our label (null: keep its picture) and action, hidden while not available.
    void PanelButton(BhGenericButton button, string key, bool available, Action click)
    {
        if (button == null)
            return;
        button.gameObject.SetActive(available);
        if (available)
            Bind(button, key != null ? Texts.Get(key) : null, click, true);
        if (available && ModCatalog.Busy)
            GreyOut(button, true);
    }

    // ---------- the big screenshot ----------

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
        var area = window.GetComponent<RectTransform>().rect;
        var aspect = texture != null && texture.height > 0 ? (float)texture.width / texture.height : 16f / 9f;
        var width = Math.Min(area.width - 80f, (area.height - 80f) * aspect);
        rect.sizeDelta = new Vector2(width, width / aspect);
    }

    // ---------- the game's boxes: adding a source ----------

    // First the game's own message box warning that nobody reviews other sources, then its input box for the address;
    // they come over our window (PlaceCanvas), which waits under them.
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

    // A game box is open over the window: the window stays (drawn under it, PlaceCanvas), only its own Escape waits;
    // when the window is drawn over everything (no camera to share), it hides meanwhile.
    void SuspendWindow()
    {
        suspended = true;
        suspendedAt = Time.frameCount;
        if (window != null && window.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay)
            window.SetActive(false);
    }

    void ResumeWindow()
    {
        if (!suspended || window == null)
            return;
        suspended = false;
        resumedAt = Time.frameCount;
        window.SetActive(true);
        windowDirty = true;
    }

    // The window drawn as the game's screens are (its Load screen's canvas: the camera, its layer, an order above the
    // Load screen's), so the game's message and input boxes, drawn above the screens, come over it. Without a camera
    // to share (none found): over everything, and hidden while a box is open.
    void PlaceCanvas()
    {
        var canvas = window.GetComponent<Canvas>();
        var load = Scene.Find<Hex.UI.SaveLoad.ScLoad>()?.GetComponent<Canvas>();
        var camera = load?.worldCamera ?? Scene.Find<ScMessageBox>()?.canvas?.worldCamera
                     ?? UnityEngine.Object.FindObjectsOfType<Canvas>().FirstOrDefault(c => c != null && c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera != null)?.worldCamera;
        if (camera == null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = WindowOrder;
            Logs.Once("Mods window: no UI camera, drawn over everything");
            return;
        }
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        if (load != null)
        {
            canvas.planeDistance = load.planeDistance;
            canvas.sortingLayerID = load.sortingLayerID;
        }
        canvas.overrideSorting = true;
        canvas.sortingOrder = (load != null ? load.sortingOrder : ScreenOrder) + 50;
        Logs.Once($"Mods window: drawn with the camera {camera.name}, order {canvas.sortingOrder}");
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

    void SwitchTab(WindowTab which)
    {
        tab = which;
        bigShotPath = null;
        shotIndex = 0;
    }

    // A text field: a copy of the game's input box's, in the filter row's size (its font halved: that box is drawn on
    // a canvas twice as big), its placeholder ours, each change reported.
    TMP_InputField NewInput(Transform parent, string placeholderKey, Action<string> changed)
    {
        if (inputTemplate == null)
            return null;
        var copy = Copy(inputTemplate.gameObject, parent);
        copy.name = "OE_Input";
        Size(copy, 380f, 31f);
        copy.transform.Cast<RectTransform>().sizeDelta = new Vector2(380f, 31f);
        copy.SetActive(true);
        var input = copy.GetComponent<TMP_InputField>();
        foreach (var text in copy.GetComponentsInChildren<TMP_Text>(true))
            text.fontSize = 18f;
        input.text = "";
        input.characterLimit = 0;
        if (input.placeholder != null && input.placeholder.GetComponent<TMP_Text>() is { } placeholder)
            SetKey(placeholder, placeholderKey);
        input.onValueChanged.AddListener((UnityAction<string>)(value => Guard("input", () => changed(value))));
        return input;
    }

    // What a dropdown does with the option chosen (not while the window fills it).
    void BindDropdown(BhDropdown dropdown, Action<int> changed) =>
        dropdown.onValueChanged.AddListener((UnityAction<int>)(i => Guard("dropdown", () =>
        {
            if (!filling)
                changed(i);
        })));

    // The dropdown's options: "All ..." and the names; set again only when they changed (Init closes a list the player
    // has open). Returns what they are now.
    static string SetOptions(BhDropdown dropdown, string allKey, List<string> keys, string chosen, string was)
    {
        if (dropdown == null)
            return was;
        var now = $"{Texts.Language}|{string.Join("|", keys)}|{chosen}";
        if (now == was)
            return was;
        var texts = new[] { Texts.Get(allKey) }.Concat(keys.Select(Texts.Get)).ToArray();
        var index = chosen != null ? keys.IndexOf(chosen) + 1 : 0;
        dropdown.Init(new Il2CppSystem.Array(new Il2CppStringArray(texts).Pointer), index, EDropdownType.Text);
        return now;
    }

    // The switch: a copy of the settings tab's switch, only its knob shown, centred (half size: the settings' canvas is
    // twice as big).
    void NewSwitch(Transform line, bool on, Action<bool> set)
    {
        var holder = NewRect("OE_Switch", line);
        Size(holder.gameObject, 70f, 50f);
        var copy = UnityEngine.Object.Instantiate(switchSource.gameObject, holder, false);
        copy.name = "Switch";
        copy.SetActive(true);
        var copyRect = copy.transform.Cast<RectTransform>();
        copyRect.anchorMin = copyRect.anchorMax = copyRect.pivot = new Vector2(0.5f, 0.5f);
        copyRect.sizeDelta = new Vector2(140f, 100f);
        copyRect.anchoredPosition = Vector2.zero;
        copyRect.localScale = new Vector3(0.5f, 0.5f, 1f);
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

    // The remove cross: the information panel's own.
    void NewCross(Transform line, Action click)
    {
        var cross = Copy(crossSource, line).GetComponent<BhGenericButton>();
        cross.name = "OE_Remove";
        Size(cross.gameObject, 37f, 37f);
        cross.gameObject.SetActive(true);
        Bind(cross, null, click, true);
    }

    // A text button: a copy of the panel's "Set up in the lobby" button.
    BhGenericButton TextButton(Transform parent, string label, float width, Action click)
    {
        var button = Copy(textButtonTemplate, parent).GetComponent<BhGenericButton>();
        button.name = "OE_Button";
        Size(button.gameObject, width, 40f);
        button.transform.Cast<RectTransform>().sizeDelta = new Vector2(width, 40f);
        button.gameObject.SetActive(true);
        Bind(button, label, click, true);
        if (button.label != null)
        {
            button.label.enableAutoSizing = true;
            button.label.fontSizeMin = 10f;
            button.label.fontSizeMax = Math.Max(button.label.fontSize, 12f);
        }
        if (ModCatalog.Busy)
            GreyOut(button, true);
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

    // A new button's Start (on its first shown frame) makes it available again: greyed out then, and on the frame after.
    readonly List<(BhGenericButton button, int until)> greyed = new();

    void GreyOut(BhGenericButton button, bool grey)
    {
        button.SetAvailable(!grey);
        // Its label dimmed too (the grey picture keeps a bright label).
        if (button.label != null)
            button.label.alpha = grey ? 0.45f : 1f;
        if (grey)
            greyed.Add((button, Time.frameCount + 2));
    }

    void GreyLater()
    {
        for (var i = greyed.Count - 1; i >= 0; i--)
        {
            var (button, until) = greyed[i];
            if (button == null || Time.frameCount > until)
            {
                greyed.RemoveAt(i);
                continue;
            }
            button.SetAvailable(false);
        }
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

    // Wider, its left edge where it was (the group lays its children out from the left).
    static void Widen(RectTransform rect, float width)
    {
        var grow = width - rect.rect.width;
        if (grow <= 0f)
            return;
        rect.sizeDelta = new Vector2(rect.sizeDelta.x + grow, rect.sizeDelta.y);
        rect.anchoredPosition += new Vector2(grow * rect.pivot.x, 0f);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // A copy of a widget, made inactive (its scripts wake up when it is shown), then put under the parent.
    GameObject Copy(GameObject template, Transform parent)
    {
        var copy = UnityEngine.Object.Instantiate(template, staging.transform, false);
        copy.SetActive(false);
        copy.transform.SetParent(parent, false);
        return copy;
    }

    static void Clear(Transform parent)
    {
        if (parent == null)
            return;
        for (var i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }

    static HorizontalLayoutGroup HRow(GameObject go, float spacing, TextAnchor alignment)
    {
        var line = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
        line.spacing = spacing;
        line.childAlignment = alignment;
        line.childControlWidth = true;
        line.childControlHeight = true;
        line.childForceExpandWidth = line.childForceExpandHeight = false;
        return line;
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
        titleText = statusText = nothingText = selectAllText = panelTitle = versionLabel = versionValue = originLabel = originValue = null;
        simpleTabs = null;
        tabList.Clear();
        filterRow = listContent = bottomGroup = panel = emptyPanel = infoContent = bigShot = null;
        selectAll = null;
        panelPicture = null;
        primaryButton = deleteButton = secondaryButton = null;
        filterInput = searchInput = inputTemplate = null;
        tagDropdown = sourceDropdown = null;
        tagOptions = sourceOptions = null;
        rowSource = lineTemplate = wrapTemplate = chipTemplate = textButtonTemplate = crossSource = dropdownSource = deleteTemplate = null;
        switchSource = null;
        greyed.Clear();
        suspended = false;
        pendingBox = null;
    }
}
