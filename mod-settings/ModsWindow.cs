using System;
using System.Collections.Generic;
using System.Linq;
using Hex.UI;
using Il2CppInterop.Runtime.InteropTypes;
using Hex.UI.GeneralKit;
using OldenEra.Modding.Common;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// The mods' own window (issue #13, fogone 07.11: managing the mods out of the settings): a "Mods" button in the main menu
// under Additional, a copy of the game's own Options button (BhNavigationButton, its click action ours), opening a window
// made of the game's message box (ScMessageBox: its frame, heading, text and buttons copied onto a canvas of ours).
// The rows are the mod manager's (ModCatalog): a mod's name, version and state, and its buttons (install, update,
// switch off or on, remove); at the bottom Check, Update all and Close. Without the manager (no catalog) no button.
public partial class Plugin
{
    const string ModsButtonName = "OE_ModsButton", WindowName = "OE_ModsWindow", CloseKey = "oe.settings.close";
    const string ColumnModKey = "oe.settings.col.mod", ColumnVersionKey = "oe.settings.col.version", ColumnStateKey = "oe.settings.col.state";
    const float WindowWidth = 2500f, RowWidth = 2340f, CrossScale = 2.2f, HeaderFontSize = 32f, BottomButtonWidth = 440f, BottomButtonHeight = 72f, RowButtonWidth = 300f, RowButtonHeight = 56f, RowHeight = 90f, RowSpacing = 6f, ListHeight = 1100f;
    const float VersionWidth = 220f, NoteWidth = 640f, SwitchWidth = 170f, CrossWidth = 80f, CellGap = 24f;
    BhNavigationButton modsButton;
    GameObject window;
    Transform windowList, windowHeader, windowStatus, windowButtons;
    BhGenericButton buttonTemplate2, rowButtonTemplate, crossTemplate;
    Hex.Settings.UI.BhSettingsCheckbox switchSource;
    Transform tableHeader;
    LayoutElement scrollSize;
    int windowRevision = -1, nextButtonSearch;

    // Every frame: the button in the main menu while there is a catalog; the window's rows when they changed.
    void ShowModsWindow()
    {
        if (ModCatalog.TitleKey == null)
        {
            Logs.Once("Mods window: no catalog (ModCatalog.TitleKey not set by the manager)");
            return;
        }
        Logs.Once($"Mods window: the catalog has {ModCatalog.Rows.Count} rows");
        // Looked for every two seconds, only in the main menu.
        if (modsButton == null && Time.frameCount >= nextButtonSearch)
        {
            nextButtonSearch = Time.frameCount + 120;
            if (InMainMenu())
                AddModsButton();
        }
        if (window != null && window.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                window.SetActive(false);
            else if (windowRevision != ModMenu.Revision)
                FillWindow();
        }
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
            windowRevision = -1;
            FillWindow();
        }
        catch (Exception e) { Log.LogError($"mods window: {e}"); }
    }

    // The window: a canvas of ours over everything, the game's message box (its blocker and its container) copied onto it.
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
        canvas.sortingOrder = 30000;
        var scaler = window.AddComponent<CanvasScaler>();
        if (box.GetComponent<CanvasScaler>() is { } gameScaler)
        {
            scaler.uiScaleMode = gameScaler.uiScaleMode;
            scaler.referenceResolution = gameScaler.referenceResolution;
            scaler.screenMatchMode = gameScaler.screenMatchMode;
            scaler.matchWidthOrHeight = gameScaler.matchWidthOrHeight;
        }
        window.AddComponent<GraphicRaycaster>();
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
        if (inner.Find("ContainerLabelSCALE") is { } label)
            label.gameObject.SetActive(false);
        // Wider than the box: its parent lays it out at its own width (the frame follows it).
        inner.Cast<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, WindowWidth);

        // The table: its header, then the rows in a scrolling list (the mouse wheel) under the state line.
        var header = new GameObject("OE_ModsHeader", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        header.transform.SetParent(inner, false);
        header.transform.SetSiblingIndex(windowStatus.GetSiblingIndex() + 1);
        header.transform.Cast<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, RowWidth);
        tableHeader = header.transform;

        var scroll = new GameObject("OE_ModsScroll", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        scroll.transform.SetParent(inner, false);
        scroll.transform.SetSiblingIndex(header.transform.GetSiblingIndex() + 1);
        scroll.transform.Cast<RectTransform>().SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, RowWidth);
        scroll.AddComponent<RectMask2D>();
        // A transparent image: the wheel over the gaps between rows scrolls too.
        scroll.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        scrollSize = scroll.AddComponent<LayoutElement>();
        var list = new GameObject("OE_ModsList", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        list.transform.SetParent(scroll.transform, false);
        var listRect = list.transform.Cast<RectTransform>();
        listRect.anchorMin = new Vector2(0f, 1f);
        listRect.anchorMax = new Vector2(1f, 1f);
        listRect.pivot = new Vector2(0.5f, 1f);
        listRect.sizeDelta = Vector2.zero;
        var column = list.AddComponent<VerticalLayoutGroup>();
        column.spacing = RowSpacing;
        column.childAlignment = TextAnchor.UpperCenter;
        column.childControlWidth = column.childControlHeight = true;
        column.childForceExpandWidth = true;
        column.childForceExpandHeight = false;
        var fitter = list.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.content = listRect;
        scrollRect.viewport = scroll.transform.Cast<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = RowHeight;
        windowList = list.transform;

        // The switches: the settings tab's own (a copy of the game's switch, off); the remove button: the game's cross.
        switchSource = switchTemplate != null ? switchTemplate.GetComponentInChildren<Hex.Settings.UI.BhSettingsCheckbox>(true) : null;
        crossTemplate = Resources.FindObjectsOfTypeAll<BhGenericButton>().FirstOrDefault(b => b != null && b.name == "CROSS BUTTON" && b.gameObject.scene.IsValid());
        // The rows' buttons: the settings screen's own small ones (its Reset, 351x45); the message box's are too big for a row.
        rowButtonTemplate = Scene.Find<Hex.Settings.UI.ScSettings>()?.transform.Find("ResetButton")?.GetComponent<BhGenericButton>();
        // The bottom buttons: the box's two and a third copy (Check, Update all, Close).
        buttonTemplate2 = windowButtons.GetComponentsInChildren<BhGenericButton>(true).FirstOrDefault();
        var buttons = windowButtons.GetComponentsInChildren<BhGenericButton>(true).ToList();
        if (buttonTemplate2 == null || buttons.Count == 0)
        {
            Log.LogWarning("mods window: no buttons in the game's message box");
            return false;
        }
        // The settings screen's buttons at the bottom too (the box's big ones looked broken, fogone 07.11): the box's hidden.
        if (rowButtonTemplate != null)
        {
            foreach (var own in buttons)
                UnityEngine.Object.DestroyImmediate(own.gameObject);
            buttons.Clear();
            while (buttons.Count < 3)
            {
                var copy = UnityEngine.Object.Instantiate(rowButtonTemplate.gameObject, windowButtons, false).GetComponent<BhGenericButton>();
                Size(copy.gameObject, BottomButtonWidth, BottomButtonHeight);
                buttons.Add(copy);
            }
        }
        while (buttons.Count < 3)
            buttons.Add(UnityEngine.Object.Instantiate(buttonTemplate2.gameObject, windowButtons, false).GetComponent<BhGenericButton>());
        buttons[0].name = "OE_Check";
        buttons[1].name = "OE_UpdateAll";
        buttons[2].name = "OE_Close";
        if (windowButtons.GetComponent<LayoutElement>() is { } buttonsSize)
            buttonsSize.preferredWidth = buttonsSize.minWidth = 1600f;
        // The box's row does not size its buttons (they kept the settings screen's width, 683: three stuck out of the
        // window): its layout sizes them now, by their LayoutElement, and the row is as wide as three.
        if (windowButtons.GetComponent<HorizontalLayoutGroup>() is { } buttonsRow)
        {
            buttonsRow.childControlWidth = buttonsRow.childControlHeight = true;
            buttonsRow.childForceExpandWidth = buttonsRow.childForceExpandHeight = false;
            buttonsRow.childAlignment = TextAnchor.MiddleCenter;
            buttonsRow.spacing = 40f;
        }
        var buttonsRect = windowButtons.Cast<RectTransform>();
        buttonsRect.sizeDelta = new Vector2(1600f, buttonsRect.sizeDelta.y);
        Logs.Info("Mods window: built from the game's message box");
        return true;
    }

    // The heading, the state, the table from ModCatalog, and the bottom buttons.
    void FillWindow()
    {
        windowRevision = ModMenu.Revision;
        SetText(windowHeader, Texts.Get(ModCatalog.TitleKey ?? TabKey));
        SetText(windowStatus, ModCatalog.StatusKey != null ? Texts.Get(ModCatalog.StatusKey) : "");
        // Made again each time, as the rows are: the column names in the language of now (they stayed in the old one
        // after the game's language changed).
        for (var i = tableHeader.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(tableHeader.GetChild(i).gameObject);
        {
            var line = Line(tableHeader.gameObject, RowHeight);
            var dim = windowStatus.GetComponent<TMP_Text>().color * new Color(1f, 1f, 1f, 0.6f);
            TableCell(line, Texts.Get(ColumnModKey), -1f, dim, HeaderFontSize);
            TableCell(line, Texts.Get(ColumnVersionKey), VersionWidth, dim, HeaderFontSize);
            TableCell(line, Texts.Get(ColumnStateKey), NoteWidth, dim, HeaderFontSize);
            Space(line, SwitchWidth + RowButtonWidth + CrossWidth + 2 * CellGap);
        }
        for (var i = windowList.childCount - 1; i >= 0; i--)
            UnityEngine.Object.DestroyImmediate(windowList.GetChild(i).gameObject);
        foreach (var row in ModCatalog.Rows)
            WindowRow(row);
        // The list as high as its rows, at most ListHeight (then it scrolls).
        var rows = ModCatalog.Rows.Count;
        scrollSize.preferredHeight = scrollSize.minHeight = Math.Min(ListHeight, rows * RowHeight + Math.Max(0, rows - 1) * RowSpacing);
        var bottom = windowButtons.GetComponentsInChildren<BhGenericButton>(true).ToDictionary(b => b.name);
        SetButton(bottom["OE_Check"], ModCatalog.Check);
        SetButton(bottom["OE_UpdateAll"], ModCatalog.UpdateAll);
        SetButton(bottom["OE_Close"], new CatalogButton(CloseKey, () => window.SetActive(false)));
        LayoutRebuilder.ForceRebuildLayoutImmediate(window.GetComponent<RectTransform>());
    }

    // A row of the table: name | version | state | switch | Install or Update | remove.
    void WindowRow(CatalogRow row)
    {
        var go = new GameObject("OE_Row_" + row.Name, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        go.transform.SetParent(windowList, false);
        var line = Line(go, RowHeight);
        var color = windowStatus.GetComponent<TMP_Text>().color;
        TableCell(line, Texts.Get(row.TitleKey), -1f, color);
        TableCell(line, row.Version ?? "", VersionWidth, color);
        TableCell(line, row.NoteKey != null ? Texts.Get(row.NoteKey) : "", NoteWidth, color * new Color(1f, 1f, 1f, 0.6f));

        // The switch: a copy of the settings tab's switch, set to the mod's state.
        if (row.Enabled is { } on && row.SetEnabled != null && switchSource != null)
        {
            var holder = Space(line, SwitchWidth);
            var copy = UnityEngine.Object.Instantiate(switchSource.gameObject, holder, false);
            copy.name = "Switch";
            copy.SetActive(true);
            // The settings row's switch is a whole row (its frame, name and separator): only its knob shown, centred.
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
            var set = row.SetEnabled;
            copy.GetComponent<Hex.Settings.UI.BhSettingsCheckbox>().Init((Action<bool>)(value =>
            {
                try { set(value); }
                catch (Exception e) { Log.LogError($"mods window: switch {row.Name}: {e}"); }
            }), on);
        }
        else
            Space(line, SwitchWidth);

        if (row.Action != null)
        {
            var button = UnityEngine.Object.Instantiate((rowButtonTemplate ?? buttonTemplate2).gameObject, line, false).GetComponent<BhGenericButton>();
            button.name = "Action";
            Size(button.gameObject, RowButtonWidth, RowButtonHeight);
            SetButton(button, row.Action);
        }
        else
            Space(line, RowButtonWidth);

        if (row.Remove != null && crossTemplate != null)
        {
            var holder = Space(line, CrossWidth);
            var cross = UnityEngine.Object.Instantiate(crossTemplate.gameObject, holder, false).GetComponent<BhGenericButton>();
            cross.name = "Remove";
            var crossRect = cross.transform.Cast<RectTransform>();
            crossRect.anchorMin = crossRect.anchorMax = crossRect.pivot = new Vector2(0.5f, 0.5f);
            crossRect.anchoredPosition = Vector2.zero;
            crossRect.localScale = new Vector3(CrossScale, CrossScale, 1f);
            SetButton(cross, new CatalogButton(null, row.Remove));
        }
        else
            Space(line, CrossWidth);
    }

    // A horizontal line of cells.
    Transform Line(GameObject go, float height)
    {
        var line = go.GetComponent<HorizontalLayoutGroup>() ?? go.AddComponent<HorizontalLayoutGroup>();
        line.spacing = CellGap;
        line.childAlignment = TextAnchor.MiddleLeft;
        line.childControlWidth = line.childControlHeight = true;
        line.childForceExpandWidth = line.childForceExpandHeight = false;
        Size(go, RowWidth, height);
        return go.transform;
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

    // An empty cell of a width (a holder for a switch or a cross, or a gap).
    Transform Space(Transform line, float width)
    {
        var go = new GameObject("Space", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        go.transform.SetParent(line, false);
        Size(go, width, -1f);
        return go.transform;
    }

    // A text cell in the box's own font and size, left-aligned, on one line; width < 0: takes what the others leave.
    // Ellipsis also cuts a line taller than the cell (the font's line is 85 high at 40): the rows are taller than that.
    void TableCell(Transform line, string text, float width, Color color, float fontSize = 0f)
    {
        var status = windowStatus.GetComponent<TMP_Text>();
        var go = new GameObject("Cell", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
        go.transform.SetParent(line, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = status.font;
        tmp.fontSharedMaterial = status.fontSharedMaterial;
        tmp.fontSize = fontSize > 0f ? fontSize : status.fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        tmp.text = text;
        var size = Size(go, width, -1f);
        if (width < 0f)
        {
            size.minWidth = 0f;
            size.flexibleWidth = 1f;
        }
    }
    // A game button with our label and action; hidden without one.
    void SetButton(BhGenericButton button, CatalogButton spec)
    {
        button.gameObject.SetActive(spec != null);
        if (spec == null)
            return;
        // No key: a button without a label (the cross).
        if (spec.Key != null)
            SetKey(button.label, spec.Key);
        // The copy keeps the message box's own click (a persistent listener): a new event with ours only.
        button.UnityClickEvent = new UnityEvent();
        var click = spec.Click;
        button.UnityClickEvent.AddListener((UnityAction)(() =>
        {
            try { click(); }
            catch (Exception e) { Log.LogError($"mods window: {spec.Key}: {e}"); }
        }));
    }

    void HideModsWindow()
    {
        // At once: the reloaded build looks for them in the same frame.
        if (window != null)
            UnityEngine.Object.DestroyImmediate(window);
        if (modsButton != null)
            UnityEngine.Object.DestroyImmediate(modsButton.gameObject);
        window = null;
        modsButton = null;
        windowList = windowHeader = windowStatus = windowButtons = null;
        buttonTemplate2 = rowButtonTemplate = crossTemplate = null;
        switchSource = null;
        tableHeader = null;
        scrollSize = null;
    }
}
