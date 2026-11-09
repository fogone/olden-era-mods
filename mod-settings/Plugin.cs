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
using Nobirds.OldenEra.Modding.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Settings;

// A "Моды" tab in the game's settings: the menus our plugins register with Nobirds.OldenEra.Modding.Common.ModMenu
// (a title with the version, the master switch, switches, choices as one row per option).
// The switches are copies of the game's own settings rows; flipping one writes the entry, which the plugin reads
// when it needs it.
[BepInPlugin("nobirds.oldenera.settings", "Olden Era Mod Settings", "0.17.7")]
public partial class Plugin : ModBase
{
    const string RowPrefix = "OE_Mod_", TabKey = "oe.settings.tab", DefaultsKey = "oe.settings.defaults", ResetKey = "oe.settings.reset";
    const string SectionName = "OE_ModsSection";
    protected override bool HasMenu => false;

    protected override string[] ObjectPrefixes => new[] { RowPrefix, SectionName, TableName, ModsButtonName, WindowName };

    protected override void OnLoad()
    {
        try
        {
            OnTick(Tick);
            On(ModEvents.LanguageChanged, language => Logs.Guard("language", () => OnLanguageChanged(language)));
        }
        catch (Exception e)
        {
            Log.LogError($"Setup failed, plugin disabled: {e}");
        }
    }

    // The "Mods" tab of the game's settings (SettingsTab): made once the settings screen is loaded, again if the screen
    // was loaded anew; filled every time the game shows it (the values may have been changed in the config files).
    SettingsTab settingsTab;
    bool tabFailed;
    Transform switchTemplate => settingsTab?.SwitchRow;

    void AddSettingsTab()
    {
        if (settingsTab != null && settingsTab.Tab == null)
            settingsTab = null;
        if (settingsTab != null || tabFailed || !SettingsTab.ScreenLoaded)
            return;
        settingsTab = SettingsTab.Add(SectionName, TabKey);
        if (settingsTab == null)
        {
            tabFailed = true;
            Log.LogWarning("Mods tab: the settings screen has no General section to copy rows from");
            return;
        }
        settingsTab.Shown = () => Logs.Guard("tab shown", FillRows);
        FillRows();
        Log.LogInfo($"Mods tab added, {Rows().Count} rows");
    }
    // A game text showing one of our localization keys (it follows a language change), or a text of ours.
    static void SetKey(Component text, string key) => GameUi.Label(text, key);

    static void SetText(Component text, string value) => GameUi.Text(text, value);
    // One row of the tab, by its kind: a title, a switch with how to read and write its value, a choice, a button. Name:
    // the row object's name; Label: a localization key.
    abstract record Row(string Name, string Label);
    sealed record TitleSpec(string Name, string Label) : Row(Name, Label);
    sealed record SwitchSpec(string Name, string Label, Func<bool> Get, Action<bool> Set) : Row(Name, Label);
    sealed record ChoiceSpec(string Name, string Label, ChoiceItem Item) : Row(Name, Label);
    // A button row; its button's label (ButtonKey, the Reset text by default), no button when Click is null.
    sealed record ButtonSpec(string Name, string Label, Action Click, string ButtonKey = null) : Row(Name, Label);

    // The rows of every registered menu (Nobirds.OldenEra.Modding.Common.ModMenu): its title with the version, the master switch,
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
        if (settingsTab?.List is not { } modsList)
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
        settingsTab.Relayout();
    }

    // A button: a copy of the General section's button row, "Сбросить" on the button.
    void ButtonRow(Transform list, ButtonSpec spec)
    {
        if (settingsTab?.ButtonRow is not { } buttonTemplate)
            return;
        var row = list.Find(spec.Name);
        if (row == null)
        {
            row = UnityEngine.Object.Instantiate(buttonTemplate.gameObject, list, false).transform;
            row.name = spec.Name;
        }
        SetKey(row.GetComponentInChildren<BhSettingsElement>(true)?.header ?? GameUi.LocalizedText(row), spec.Label);
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
        if (settingsTab?.ChoiceRow is not { } dropdownTemplate)
            return;
        var item = spec.Item;
        var row = list.Find(spec.Name);
        var created = row == null;
        if (created)
        {
            row = UnityEngine.Object.Instantiate(dropdownTemplate.gameObject, list, false).transform;
            row.name = spec.Name;
        }
        SetKey(row.GetComponentInChildren<BhSettingsElement>(true)?.header ?? GameUi.LocalizedText(row), spec.Label);
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
        if (created)
            Logs.Once($"choice {spec.Name}: index {index}, label '{dropdown.bhDropdown?.label?.text}'");
    }

    // A title: from the game's row factory (SettingsTab.NewTitle).
    void TitleRow(Transform list, string name, string text)
    {
        var title = list.Find(name);
        if (title == null)
        {
            title = settingsTab.NewTitle(name)?.transform;
            if (title == null)
                return;
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
        SetKey(row.GetComponentInChildren<BhSettingsElement>(true)?.header ?? GameUi.LocalizedText(row), spec.Label);
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
        if (settingsTab?.List is { } modsList && modsList.gameObject.activeInHierarchy)
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
            GameUi.HideTooltip();
            NarrowText(null);
        }
        describedRow = pointer;
        if (hovered is not var (shownRect, shownLabel))
            return;
        var description = Texts.Get(shownLabel + ".desc");
        GameUi.ShowTooltip(Texts.Get(shownLabel), description, shownRect, ESnapDirection.Top);
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






    int drawnRevision = -1;

    // The game switched its language (ModEvents.LanguageChanged: our texts are in its new table already). Its texts
    // showing our keys follow by themselves; the tab and the rows are set again for those we wrote without one.
    void OnLanguageChanged(Hex.UI.ELang language)
    {
        if (settingsTab?.Tab is { } tab && tab != null)
            GameUi.Label(tab.text, TabKey);
        refreshRows = true;
    }

    // Every frame: the settings tab made once the screen is loaded, its rows redrawn after a choice or when a plugin was
    // reloaded (its menu replaced).
    void Tick()
    {
        AddSettingsTab();
        if (settingsTab?.List is { } modsList && drawnRevision != ModMenu.Revision)
        {
            drawnRevision = ModMenu.Revision;
            // Rows of menus that are gone: hidden; the others are shown again by FillRows.
            for (var i = 0; i < modsList.childCount; i++)
                if (modsList.GetChild(i).name.StartsWith(RowPrefix, StringComparison.Ordinal))
                    modsList.GetChild(i).gameObject.SetActive(false);
            refreshRows = true;
        }
        RefreshIfNeeded();
        ShowHoveredDescription();
        ShowVersion();
        ShowModsWindow();
    }

    protected override void OnUnload()
    {
        HideVersion();
        HideModsWindow();
        settingsTab?.Remove();
        settingsTab = null;
    }
}