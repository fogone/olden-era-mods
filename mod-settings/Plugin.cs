using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Hex.Settings.UI;
using Hex.UI.GeneralKit;
using BhSettingsButton = Hex.Settings.UI.BhSettingsButton;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Hex.UI.Tooltips;
using OldenEra.Modding.Common;
using UnityEngine;
using UnityEngine.UI;

namespace OldenEra.Modding.Settings;

// A "Моды" tab in the game's settings: the menus our plugins register with OldenEra.Modding.Common.ModMenu
// (a title with the version, the master switch, switches, choices as one row per option).
// The switches are copies of the game's own settings rows; flipping one writes the entry, which the plugin reads
// when it needs it.
[BepInPlugin("nobirds.oldenera.settings", "Olden Era Mod Settings", "0.17.2")]
public partial class Plugin : ModBase
{
    const string RowPrefix = "OE_Mod_", TabKey = "oe.settings.tab", DefaultsKey = "oe.settings.defaults", ResetKey = "oe.settings.reset";
    protected override bool HasMenu => false;
    // The patches below are static (Harmony); they reach the plugin through this.
    static Plugin instance;
    ConfigEntry<bool> logTree;

    protected override string[] ObjectPrefixes => new[] { RowPrefix, SectionName, TabName, TableName, ModsButtonName, WindowName };

    protected override void OnLoad()
    {
        instance = this;
        Game.Log = Logs.Info;
        logTree = DebugFlag("LogTree", "Write the settings sections' object trees and our section's state after tab clicks to BepInEx/LogOutput.log.");
        try
        {
            var harmony = Harmony;
            harmony.PatchEach(GameTargets.SettingsSectionFills, postfix: new HarmonyMethod(typeof(Plugin), nameof(ShowPostfix)));
            OnTick(Tick);
            On(ModEvents.LanguageChanged, language => Logs.Guard("language", () => OnLanguageChanged(language)));
            harmony.PatchOne(AccessTools.Method(typeof(ScSettings), nameof(ScSettings.Hide)), "the settings screen's hide", postfix: new HarmonyMethod(typeof(Plugin), nameof(ScreenClosedPostfix)));
            harmony.PatchOne(AccessTools.Method(typeof(ScSettings), nameof(ScSettings.OnBackButton)), "the settings screen's back button", postfix: new HarmonyMethod(typeof(Plugin), nameof(ScreenClosedPostfix)));
            harmony.PatchOne(AccessTools.Method(typeof(BhSettingsTab), nameof(BhSettingsTab.OnClick)), "the settings tab's click",
                prefix: new HarmonyMethod(typeof(Plugin), nameof(TabClickPrefix)), postfix: new HarmonyMethod(typeof(Plugin), nameof(TabClickPostfix)));
            // Loaded again by the dev loader: the General section started long ago, the tab is built right away.
            if (Scene.Find<BhGeneralSection>() is { } general)
                BuildModsTab(general);
        }
        catch (Exception e)
        {
            Log.LogError($"Setup failed, plugin disabled: {e}");
        }
    }


    const string SectionName = "OE_ModsSection", TabName = "OE_ModsTab";
    // The settings type our section and tab answer to: one the game declares but has no section for.
    const ESettingsType ModsType = ESettingsType.Gameplay;
    Transform modsList, rowTemplate, switchTemplate;

    static void ShowPostfix(System.Reflection.MethodBase __originalMethod, BhSettingsSection __instance) => instance?.OnShowPostfix(__originalMethod, __instance);

    void OnShowPostfix(System.Reflection.MethodBase fill, BhSettingsSection section)
    {
        try
        {
            if (logTree.Value)
                Logs.Once($"SECTION {section.GetIl2CppType().Name} {section.name} via {fill.Name}");
            if (section.name == SectionName)
            {
                // Its Start (the Sound section's, run on its first showing) puts back the Sound type; ours again, so
                // the game's switch (SettingsSectionsView.ktn, by type) keeps finding it.
                section.SectionTypeStored = ModsType;
                FillRows(); // the values may have been changed in the config files
                if (logTree.Value && dumped.Add("mods"))
                    Scene.Tree(section.transform, Log.LogMessage);
                return;
            }
            var general = section.TryCast<BhGeneralSection>();
            if (general != null && modsList == null)
                BuildModsTab(general);
        }
        catch (Exception e)
        {
            Logs.Report("show", e);
        }
    }

    // A tab "Моды" after the game's tabs and a section of its own: a copy of the Sound section (the smallest) with
    // the game's rows hidden and ours in their place, registered with the game under ModsType, so the game's own
    // tab switching shows it.
    void BuildModsTab(BhGeneralSection general)
    {
        var template = general.showTooltipsForBattleEventsCheckbox ?? general.GetComponentsInChildren<BhSettingsCheckbox>(true).LastOrDefault();
        // The screen is not a parent of its sections; it and the sections view are looked up among loaded objects.
        var screen = general.GetComponentInParent<ScSettings>(true) ?? Scene.Find<ScSettings>();
        var view = general.GetComponentInParent<SettingsSectionsView>(true) ?? screen?.sectionsController?.sectionsView ?? Scene.Find<SettingsSectionsView>();
        if (template == null || view == null || screen == null || screen.tabs == null || screen.tabs.Length == 0)
        {
            Logs.Once($"settings screen not found: checkbox={template != null} screen={screen != null} view={view != null} tabs={screen?.tabs?.Length}");
            return;
        }
        // The template's row: the ancestor that is one item of the list of settings rows.
        rowTemplate = template.transform;
        while (rowTemplate.parent != null && rowTemplate.parent.GetComponentsInChildren<BhSettingsCheckbox>(true).Length < 3)
            rowTemplate = rowTemplate.parent;
        // The copies keep the template's knob position as their "off" place and move it by a fixed step when switched
        // on: the template must be a row that is off (a row that was on put the knob a step too far).
        if (rowTemplate.GetComponentInChildren<BhCheckbox>(true)?.SwitchedOn() == true && rowTemplate.parent is { } rows)
            rowTemplate = Enumerable.Range(0, rows.childCount).Select(rows.GetChild)
                .FirstOrDefault(r => r.GetComponentInChildren<BhCheckbox>(true) is { } b && !b.SwitchedOn()) ?? rowTemplate;
        if (logTree.Value && dumped.Add("general"))
            Scene.Tree(general.transform, Log.LogMessage);

        var source = view.sections.FirstOrDefault(s => s != null && s.TryCast<BhSoundSection>() != null) ?? (BhSettingsSection)general;
        var copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
        copy.name = SectionName;
        var section = copy.GetComponent<BhSettingsSection>();
        section.SectionTypeStored = ModsType;
        // The list of rows: the copy's element of the same path as the template row's parent in General.
        modsList = copy.GetComponentsInChildren<VerticalLayoutGroup>(true).FirstOrDefault(g => g.name == rowTemplate.parent.name)?.transform;
        if (modsList == null)
        {
            Logs.Once($"no list '{rowTemplate.parent.name}' in the copied section");
            UnityEngine.Object.Destroy(copy);
            return;
        }
        for (var i = 0; i < modsList.childCount; i++)
            modsList.GetChild(i).gameObject.SetActive(false);
        // Our own hidden copy of the off row, taken now: the game's row may be switched on by the time a plugin adds a
        // switch later (a reloaded plugin), and a copy of an on row gets its knob a step too far.
        switchTemplate = UnityEngine.Object.Instantiate(rowTemplate.gameObject, copy.transform, false).transform;
        switchTemplate.name = RowPrefix + "SwitchTemplate";
        switchTemplate.gameObject.SetActive(false);
        view.sections = Append(view.sections, section);

        var lastTab = screen.tabs[screen.tabs.Length - 1];
        var tabObject = UnityEngine.Object.Instantiate(lastTab.gameObject, lastTab.transform.parent, false);
        tabObject.name = TabName;
        var tab = tabObject.GetComponent<BhSettingsTab>();
        tab.sectionType = ModsType;
        tab.ClickAction = lastTab.ClickAction;
        SetKey(tab.text, TabKey);
        screen.tabs = Append(screen.tabs, tab);
        modsTab = tab;
        settingsScreen = screen;
        // The button row: a copy source for the per-mod reset.
        // The dropdown row: a copy source for choices.
        dropdownTemplate = (general.cityCameraMode ?? general.GetComponentInChildren<BhLocalizedDropdown>(true))?.transform;
        while (dropdownTemplate != null && dropdownTemplate.parent != rowTemplate.parent)
            dropdownTemplate = dropdownTemplate.parent;
        buttonTemplate = (general.emoWheelSettingsButton ?? general.GetComponentInChildren<BhSettingsButton>(true))?.transform;
        while (buttonTemplate != null && buttonTemplate.parent != rowTemplate.parent)
            buttonTemplate = buttonTemplate.parent;
        allTabs = screen.tabs;
        tab.ShowSelected(false); // the copy may carry the selected look of its source
        FillRows();
        Log.LogInfo($"Mods tab added after {lastTab.name}, section copied from {source.name}, {Rows().Count} rows");
    }

    BhSettingsTab modsTab;
    ScSettings settingsScreen;
    Transform buttonTemplate, dropdownTemplate;
    Il2CppReferenceArray<BhSettingsTab> allTabs;

    // The screen hands its tabs their click action when it opens; ours was copied before that may have none yet.
    static void TabClickPrefix(BhSettingsTab __instance) => instance?.OnTabClickPrefix(__instance);

    void OnTabClickPrefix(BhSettingsTab tab)
    {
        try
        {
            if (logTree.Value && modsTab != null && tab.Pointer == modsTab.Pointer && modsList != null && dumped.Add("mods on click"))
                Scene.Tree(modsList.GetComponentInParent<BhSettingsSection>(true).transform, Log.LogMessage);
            if (modsTab == null || tab.Pointer != modsTab.Pointer || tab.ClickAction != null)
                return;
            tab.ClickAction = allTabs?.FirstOrDefault(t => t != null && t.Pointer != modsTab.Pointer && t.ClickAction != null)?.ClickAction;
            Logs.Once($"Mods tab click action taken from another tab: {tab.ClickAction != null}");
        }
        catch (Exception e)
        {
            Logs.Report("tab click", e);
        }
    }


    // The copied list keeps a zero height and a scroll offset far out (2e7) until its layout is rebuilt: rebuild it
    // and scroll to the top whenever our tab is opened.
    void FixLayout(bool toTop = true)
    {
        if (modsList == null)
            return;
        var rect = modsList.GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        if (!toTop)
            return; // a redraw after a click keeps the list where the user scrolled it
        rect.anchoredPosition = Vector2.zero;
        if (modsList.GetComponentInParent<ScrollRect>(true) is { } scroll)
        {
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    // After a tab click (the game has shown the section itself): the screen's Apply and Reset off on our tab, our list
    // laid out.
    static void TabClickPostfix(BhSettingsTab __instance) => instance?.OnTabClickPostfix(__instance);

    void OnTabClickPostfix(BhSettingsTab tab)
    {
        try
        {
            watchFrames = 120;
            if (modsTab == null || modsList == null)
                return;
            var section = modsList.GetComponentInParent<BhSettingsSection>(true);
            var ours = tab.Pointer == modsTab.Pointer;
            // The game's switch shows and hides the sections itself (SettingsSectionsView.ktn: the shown one (Shown), found
            // by type among sections; ours is there under ModsType). Said in the log when it did not find ours.
            if (ours && settingsScreen?.sectionsController?.sectionsView is { } view && (view.ShownSection == null || view.ShownSection.Pointer != section.Pointer))
                Logs.Once($"the game did not show the mods section: it shows {(view.ShownSection != null ? view.ShownSection.name : "none")}, ours has type {section.SectionType}");
            ShowScreenButtons(!ours);
            if (ours)
                FixLayout();
        }
        catch (Exception e)
        {
            Logs.Report("tab switch", e);
        }
    }

    int watchFrames;
    string lastSectionState;

    // Opening the screen again shows a game section without a tab click: ours gives way to any other one shown.
    static void ScreenClosedPostfix() => instance?.OnScreenClosed();

    void OnScreenClosed()
    {
        try
        {
            // The sections are not under the screen: ours is hidden by hand when the screen closes.
            modsList?.GetComponentInParent<BhSettingsSection>(true)?.gameObject.SetActive(false);
            ShowScreenButtons(true);
            Logs.Once("settings screen closed");
        }
        catch (Exception e)
        {
            Logs.Report("screen closed", e);
        }
    }

    // The screen's Apply and Reset work on the game's settings only: hidden on our tab (our switches save at once,
    // each mod has its own reset row), back on the game's tabs.
    void ShowScreenButtons(bool show)
    {
        if (settingsScreen == null)
            return;
        settingsScreen.applyButton?.gameObject.SetActive(show);
        settingsScreen.resetButton?.gameObject.SetActive(show);
    }

    internal void WatchSection()
    {
        if (watchFrames <= 0 || modsList == null || !logTree.Value)
            return;
        watchFrames--;
        try
        {
            var section = modsList.GetComponentInParent<BhSettingsSection>(true);
            var scroll = modsList.GetComponentInParent<ScrollRect>(true);
            var rect = modsList.GetComponent<RectTransform>();
            var state = $"section active={section.gameObject.activeInHierarchy} canvas={section.canvas?.enabled} alpha={section.GetComponent<CanvasGroup>()?.alpha} " +
                        $"scroll alpha={scroll?.GetComponent<CanvasGroup>()?.alpha} list size={rect.rect.size} pos={rect.anchoredPosition} " +
                        $"others visible: {string.Join(", ", section.transform.parent.GetComponentsInChildren<BhSettingsSection>(true).Where(s => s.gameObject.activeInHierarchy).Select(s => s.name))}";
            if (state != lastSectionState)
            {
                lastSectionState = state;
                Log.LogMessage($"MODS {Time.frameCount}: {state}");
            }
        }
        catch (Exception e)
        {
            Logs.Report("watch section", e);
        }
    }

    static Il2CppReferenceArray<T> Append<T>(Il2CppReferenceArray<T> array, T item) where T : Il2CppObjectBase
    {
        var result = new Il2CppReferenceArray<T>(array.Length + 1);
        for (var i = 0; i < array.Length; i++)
            result[i] = array[i];
        result[array.Length] = item;
        return result;
    }

    // Our text on a game text: its localization key cleared first, or the game puts its own text back.
    // A game text showing one of our localization keys: the game looks it up and follows a language change.
    static void SetKey(Component text, string key)
    {
        if (text == null)
            return;
        if (text.GetComponent<GameLocalizedText>() is { } localized)
            localized.textSid = key;
        SetText(text, Texts.Get(key));
        if (text.GetComponent<GameLocalizedText>() is { } again)
            again.textSid = key;
    }

    static void SetText(Component text, string value)
    {
        if (text == null)
            return;
        if (text.GetComponent<GameLocalizedText>() is { } localized)
        {
            localized.textSid = "";
            // A copy that has not woken up yet has no text element of its own; then the TMP text below is enough.
            try { localized.HardSetText(value); }
            catch (Exception) { }
        }
        if (text.GetComponent<TMPro.TMP_Text>() is { } tmp)
            tmp.text = value;
    }

    // One row of the tab, by its kind: a title, a switch with how to read and write its value, a choice, a button. Name:
    // the row object's name; Label: a localization key.
    abstract record Row(string Name, string Label);
    sealed record TitleSpec(string Name, string Label) : Row(Name, Label);
    sealed record SwitchSpec(string Name, string Label, Func<bool> Get, Action<bool> Set) : Row(Name, Label);
    sealed record ChoiceSpec(string Name, string Label, ChoiceItem Item) : Row(Name, Label);
    // A button row; its button's label (ButtonKey, the Reset text by default), no button when Click is null.
    sealed record ButtonSpec(string Name, string Label, Action Click, string ButtonKey = null) : Row(Name, Label);

    // The rows of every registered menu (OldenEra.Modding.Common.ModMenu): its title with the version, the master switch,
    // then its switches and choices (a dropdown), then its reset.
    List<Row> Rows()
    {
        var rows = new List<Row>
        {
            // First, when the mods work at all (fogone 07.11): always, only in a single player game, never.
            new TitleSpec(RowPrefix + "All_Title", ModMenu.ModeTitleKey),
            new ChoiceSpec(RowPrefix + "All_Mode", ModMenu.ModeKey, ModMenu.ModeItem),
        };
        // The mod manager's catalog is not here: it has a window of its own, the Mods button in the main menu (ModsWindow, #13).
        foreach (var menu in ModMenu.All)
        {
            var id = RowPrefix + menu.Guid;
            rows.Add(new TitleSpec(id + "_Title", menu.HeadingKey));
            // Off for this version of the game: its title says so, no switches.
            if (menu.IsOff)
                continue;
            rows.Add(new SwitchSpec(id + "_Enabled", ModMenu.EnabledKey, () => menu.Enabled.Value, v => menu.Enabled.Value = v));
            foreach (var item in menu.Items)
                rows.Add(item switch
                {
                    ToggleItem toggle => new SwitchSpec($"{id}_{item.Key}", item.LabelKey, () => toggle.Value.Value, v => toggle.Value.Value = v),
                    ChoiceItem choice => new ChoiceSpec($"{id}_{item.Key}", item.LabelKey, choice),
                    _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
                });
            // The mod's own reset (the game's Reset button is hidden on our tab): every entry back to its default.
            rows.Add(new ButtonSpec(id + "_Defaults", DefaultsKey, () =>
            {
                menu.Enabled.BoxedValue = menu.Enabled.DefaultValue;
                foreach (var item in menu.Items)
                    item.Entry.BoxedValue = item.Entry.DefaultValue;
                refreshRows = true;
                Log.LogInfo($"{menu.Guid}: defaults");
            }));
        }
        return rows;
    }

    // Set by a choice row: the other options' rows are refreshed on the next frame, not inside the click.
    bool refreshRows;

    internal void RefreshIfNeeded()
    {
        if (!refreshRows)
            return;
        refreshRows = false;
        try { FillRows(); }
        catch (Exception e) { Logs.Report("refresh", e); }
    }

    void FillRows()
    {
        if (modsList == null)
            return;
        foreach (var row in Rows())
            switch (row)
            {
                case TitleSpec title:
                    TitleRow(modsList, title.Name, title.Label);
                    break;
                case ButtonSpec button:
                    ButtonRow(modsList, button);
                    break;
                case ChoiceSpec choice:
                    ChoiceRow(modsList, choice);
                    break;
                case SwitchSpec toggle:
                    SwitchRow(switchTemplate, modsList, toggle);
                    break;
            }
        FixLayout(toTop: false);
    }

    // A button: a copy of the General section's button row, "Сбросить" on the button.
    void ButtonRow(Transform list, ButtonSpec spec)
    {
        if (buttonTemplate == null)
            return;
        var row = list.Find(spec.Name);
        if (row == null)
        {
            row = UnityEngine.Object.Instantiate(buttonTemplate.gameObject, list, false).transform;
            row.name = spec.Name;
        }
        SetKey(row.GetComponentInChildren<BhSettingsElement>(true)?.header ?? row.GetComponentInChildren<GameLocalizedText>(true), spec.Label);
        row.gameObject.SetActive(true);
        row.SetAsLastSibling();
        var button = row.GetComponentInChildren<BhSettingsButton>(true);
        var buttonKey = spec.ButtonKey ?? ResetKey;
        // A row with nothing to do (the catalog's "the latest"): its text only.
        if (button.bhButton != null)
            button.bhButton.gameObject.SetActive(spec.Click != null);
        if (spec.Click == null)
            return;
        button.Init(spec.Click, buttonKey);
        if (button.bhButton != null)
            foreach (var text in button.bhButton.GetComponentsInChildren<TMPro.TMP_Text>(true))
                SetKey(text, buttonKey);
    }


    // A choice: a copy of the General section's dropdown row (the city camera mode), our options in it.
    void ChoiceRow(Transform list, ChoiceSpec spec)
    {
        if (dropdownTemplate == null)
            return;
        var item = spec.Item;
        var row = list.Find(spec.Name);
        var created = row == null;
        if (created)
        {
            row = UnityEngine.Object.Instantiate(dropdownTemplate.gameObject, list, false).transform;
            row.name = spec.Name;
        }
        SetKey(row.GetComponentInChildren<BhSettingsElement>(true)?.header ?? row.GetComponentInChildren<GameLocalizedText>(true), spec.Label);
        row.gameObject.SetActive(true);
        row.SetAsLastSibling();
        var dropdown = row.GetComponentInChildren<BhLocalizedDropdown>(true);
        var index = Math.Max(0, Array.FindIndex(item.Options, o => Equals(o, item.Entry.BoxedValue)));
        Action<int> changed = i =>
        {
            if (i < 0 || i >= item.Options.Length)
                return;
            item.Entry.BoxedValue = item.Options[i];
            Log.LogInfo($"{spec.Name} = {item.Options[i]}");
        };
        // The values are the option numbers; the localized dropdown shows the text of "<prefix><value>": our keys
        // <label key>.0 .. .N (ModMenu makes them from the texts written by name).
        var values = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int>(item.Options.Length);
        for (var i = 0; i < item.Options.Length; i++)
            values[i] = i;
        dropdown.Init(changed, new Il2CppSystem.Array(values.Pointer), index, false, item.LabelKey + ".", EDropdownType.Text);
        Describe(row, spec.Label);
        if (logTree.Value || created)
            Logs.Once($"choice {spec.Name}: index {index}, label '{dropdown.bhDropdown?.label?.text}'");
    }

    // A title: a copy of the General section's own title row.
    void TitleRow(Transform list, string name, string text)
    {
        var title = list.Find(name);
        if (title == null)
        {
            var source = rowTemplate.parent.GetComponentInChildren<BhSettingsTitle>(true);
            if (source == null)
                return;
            title = UnityEngine.Object.Instantiate(source.gameObject, list, false).transform;
            title.name = name;
        }
        // Every fill: a reloaded plugin may bring a new version or label.
        SetKey(title.GetComponent<BhSettingsTitle>().title, text);
        title.gameObject.SetActive(true);
        title.SetAsLastSibling();
    }

    // A copy of the game's row with our label and a switch bound to the row; on later fills only the value is
    // refreshed (the entry may have been changed in the config file or by another row).
    void SwitchRow(Transform template, Transform list, SwitchSpec spec)
    {
        var row = list.Find(spec.Name);
        var created = row == null;
        if (created)
        {
            row = UnityEngine.Object.Instantiate(template.gameObject, list, false).transform;
            row.name = spec.Name;
        }
        SetKey(row.GetComponentInChildren<BhSettingsElement>(true)?.header ?? row.GetComponentInChildren<GameLocalizedText>(true), spec.Label);
        row.gameObject.SetActive(true);
        row.SetAsLastSibling();
        Describe(row, spec.Label);
        var checkbox = row.GetComponentInChildren<BhSettingsCheckbox>(true);
        Action<bool> changed = value =>
        {
            spec.Set(value);
            if (spec.Get() != value) // a chosen option switched off stays on
                refreshRows = true;
            Log.LogInfo($"{spec.Name} = {value}");
        };
        // Init animates the knob a step from where it stands: on a new row (copied from a switch that is off, so the
        // step lands right; without Init the switch ignores clicks) and when the shown state is not the value, else
        // the knob walks off with every redraw. The click action is renewed every time (the row's spec may be new).
        var shown = checkbox.bhCheckbox?.SwitchedOn(); // the switch's own value
        if (created || shown != spec.Get())
            checkbox.Init(changed, spec.Get());
        else
            checkbox.SwitchCallback = changed;
    }

    // ---------- descriptions ----------

    // A row's description (its label key + ".desc" in the mod's lang), on hovering the row, as the game's own text
    // tooltip: its name as the header, the description as the text, lines wrapped, kept on screen by the game's
    // positioner (GameNames.Tooltips.ShowTexts: BhTooltips.wbt on 0.82.7, as a hovered unit buff shows its name and
    // description). The game's settings rows have no description of their own; their hover only lights the frame,
    // and its exit shares code with other classes (no patch), so the row under the mouse is looked at every frame
    // (as the research plugin's frame rate disc does). Not BhBtnTooltipShow: the buttons' tooltip is one line (06.11).
    readonly Dictionary<IntPtr, (RectTransform rect, string label)> described = new();

    void Describe(Transform row, string labelKey)
    {
        if (!Texts.Has(labelKey + ".desc"))
            return;
        var host = row.GetComponentInChildren<BhSettingsElement>(true) is { } element ? element.transform : row;
        if (host.GetComponent<RectTransform>() is { } rect)
            described[rect.Pointer] = (rect, labelKey);
    }

    IntPtr describedRow;

    void ShowHoveredDescription()
    {
        (RectTransform rect, string label)? hovered = null;
        if (modsList != null && modsList.gameObject.activeInHierarchy)
            foreach (var (rect, label) in described.Values)
                if (rect != null && Scene.Hovered(rect))
                {
                    hovered = (rect, label);
                    break;
                }
        var pointer = hovered?.rect.Pointer ?? IntPtr.Zero;
        if (pointer == describedRow)
            return;
        if (describedRow != IntPtr.Zero)
        {
            GameNames.Tooltips?.Hide();
            NarrowText(null);
        }
        describedRow = pointer;
        if (hovered is not var (shownRect, shownLabel))
            return;
        var description = Texts.Get(shownLabel + ".desc");
        GameNames.Tooltips?.ShowTexts(Texts.Get(shownLabel), description, null, shownRect, null, ESnapDirection.Top, false);
        NarrowText(description);
    }

    // The description as a column (fogone 06.11): the tooltip takes the width of its text, one long line; the text that
    // shows the description gets a width of ColumnWidth (a LayoutElement over the text's own preferred width), so it
    // wraps there. No line breaks put into the text: the game's tooltip shows them as characters. The width is taken
    // off again when our tooltip goes, so the game's own tooltips keep theirs.
    const float ColumnWidth = 420f;
    // The text's layout element and what it had before (the game's own one, or ours added: then off again).
    LayoutElement narrowed;
    (bool enabled, int priority, float width, bool wrap, bool added) narrowedWas;

    void NarrowText(string description)
    {
        if (narrowed != null)
        {
            narrowed.enabled = narrowedWas.enabled && !narrowedWas.added;
            narrowed.layoutPriority = narrowedWas.priority;
            narrowed.preferredWidth = narrowedWas.width;
            if (narrowed.GetComponent<TMPro.TMP_Text>() is { } was)
                was.enableWordWrapping = narrowedWas.wrap;
        }
        narrowed = null;
        if (description == null || GameNames.Tooltips is not { } tooltips)
            return;
        var start = description.Length > 24 ? description.Substring(0, 24) : description;
        foreach (var text in tooltips.GetComponentsInChildren<TMPro.TMP_Text>(false))
        {
            if (text.text == null || !text.text.Contains(start))
                continue;
            var added = text.GetComponent<LayoutElement>() == null;
            var element = added ? text.gameObject.AddComponent<LayoutElement>() : text.GetComponent<LayoutElement>();
            narrowedWas = (element.enabled, element.layoutPriority, element.preferredWidth, text.enableWordWrapping, added);
            text.enableWordWrapping = true;
            element.enabled = true;
            element.layoutPriority = 2;
            element.preferredWidth = ColumnWidth;
            narrowed = element;
            for (var t = text.transform; t != null && t != tooltips.transform; t = t.parent)
                if (t.GetComponent<RectTransform>() is { } rect)
                    LayoutRebuilder.MarkLayoutForRebuild(rect);
            break;
        }
    }
    // Debug: the object tree under the settings list, to see how rows are built.
    readonly HashSet<string> dumped = new();






    int drawnRevision = -1;

    // The game switched its language (ModEvents.LanguageChanged: our texts are in its new table already). Its texts
    // showing our keys follow by themselves; the tab and the rows are set again for those we wrote without one.
    void OnLanguageChanged(Hex.UI.ELang language)
    {
        if (modsTab != null)
            SetKey(modsTab.text, TabKey);
        refreshRows = true;
    }

    // Every frame: our section gives way to a game section shown, rows are redrawn after a choice or when a plugin
    // was reloaded (its menu replaced), and the debug log after a tab click.
    void Tick()
    {
        if (modsList != null && drawnRevision != ModMenu.Revision)
        {
            drawnRevision = ModMenu.Revision;
            // Rows of menus that are gone: hidden; the others are shown again by FillRows.
            for (var i = 0; i < modsList.childCount; i++)
                if (modsList.GetChild(i).name.StartsWith(RowPrefix, StringComparison.Ordinal))
                    modsList.GetChild(i).gameObject.SetActive(false);
            refreshRows = true;
        }
        RefreshIfNeeded();
        WatchSection();
        ShowHoveredDescription();
        ShowVersion();
        ShowModsWindow();
    }

    // Our tab and section out of the game's arrays; ModBase destroys the objects themselves.
    protected override void OnUnload()
    {
        HideVersion();
        HideModsWindow();
        var section = modsList != null ? modsList.GetComponentInParent<BhSettingsSection>(true) : null;
        var view = section != null ? Scene.Find<SettingsSectionsView>() : null;
        if (view != null)
            view.sections = Without(view.sections, section);
        var screen = Scene.Find<ScSettings>();
        if (screen != null && modsTab != null)
            screen.tabs = Without(screen.tabs, modsTab);
        ShowScreenButtons(true);
        modsList = rowTemplate = switchTemplate = buttonTemplate = dropdownTemplate = null;
        modsTab = null;
        instance = null;
    }

    static Il2CppReferenceArray<T> Without<T>(Il2CppReferenceArray<T> array, T item) where T : Il2CppObjectBase
    {
        var kept = Enumerable.Range(0, array.Length).Select(i => array[i]).Where(x => x == null || x.Pointer != item.Pointer).ToList();
        var result = new Il2CppReferenceArray<T>(kept.Count);
        for (var i = 0; i < kept.Count; i++)
            result[i] = kept[i];
        return result;
    }
}
