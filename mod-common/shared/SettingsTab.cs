using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Hex.Settings.UI;
using Hex.UI.GeneralKit;
using BhSettingsButton = Hex.Settings.UI.BhSettingsButton;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.Common;

// A tab of our own in the game's settings screen (issue #24), switched by the game itself. The screen keeps its tabs
// and its sections in arrays (ScSettings.tabs, SettingsSectionsView.sections) and switches by a section's settings type
// (SettingsSectionsView.ShowSection, a search of the sections by type): ours is a copy of a section with a type of its
// own, put in those arrays, with a copy of a tab of that type. The screen gives the tab its click when it opens, shows
// and hides the section as its own. Titles come from the game's row factory (the section's prefabs, as the hotkeys
// section makes its titles); switch, choice and button rows have no prefab: copies of the General section's rows.
//
//   tab = SettingsTab.Add("OE_ModsTab", "oe.settings.tab");   // null until the settings screen is loaded
//   tab.Shown = () => Fill(tab.List);
//   var title = tab.NewTitle("OE_Title");   var row = Instantiate(tab.SwitchRow, tab.List)...
//   tab.Remove();   // on unload
public sealed class SettingsTab
{
    // Our sections' types: values the game does not declare, one per tab.
    const int FirstType = 100;
    static readonly List<SettingsTab> all = new();
    static bool patched;

    public readonly string Name;
    public readonly ESettingsType Type;
    public BhSettingsTab Tab { get; private set; }
    public BhSettingsSection Section { get; private set; }
    // The section's list of rows (the copied section's own rows hidden).
    public Transform List { get; private set; }
    // Templates of the General section's rows, kept hidden in the section: a switch that is off, a choice (dropdown), a
    // button. Copies of them go into List.
    public Transform SwitchRow { get; private set; }
    public Transform ChoiceRow { get; private set; }
    public Transform ButtonRow { get; private set; }
    // Called after the game showed the section (its tab clicked, or the screen opened on it).
    public Action Shown;

    ScSettings screen;
    SettingsSectionsView view;

    SettingsTab(string name, ESettingsType type)
    {
        Name = name;
        Type = type;
    }

    // The tab with its label (a localization key) after the game's tabs; null while the settings screen is not loaded.
    // The library patches the game's switch once, for every tab of every mod.
    public static SettingsTab Add(string name, string labelKey)
    {
        if (Screen is not { } screen ||
            screen.sectionsController?.sectionsView is not { } view || view == null || screen.tabs == null || screen.tabs.Length == 0 ||
            view.sections.FirstOrDefault(s => s != null && s.TryCast<BhGeneralSection>() != null)?.TryCast<BhGeneralSection>() is not { } general)
            return null;
        Patch();
        var type = (ESettingsType)(FirstType + Enumerable.Range(0, all.Count + 1).First(i => all.All(t => t.Type != (ESettingsType)(FirstType + i))));
        var tab = new SettingsTab(name, type) { screen = screen, view = view };
        if (!tab.Build(general, labelKey))
            return null;
        all.Add(tab);
        return tab;
    }

    // The game's settings screen, from its screen registry; null while it is not loaded.
    static ScSettings Screen => GameNames.ScreenRegistry()?.Screen("ScSettings")?.TryCast<ScSettings>() is { } screen && screen != null ? screen : null;

    public static bool ScreenLoaded => Screen != null;

    static void Patch()
    {
        if (patched)
            return;
        patched = true;
        var harmony = new Harmony("nobirds.oldenera.common.settings-tab");
        harmony.PatchOne(GameTargets.SettingsShowSection, "the settings sections' switch", postfix: new HarmonyMethod(typeof(SettingsTab), nameof(SwitchedPostfix)));
        harmony.PatchOne(AccessTools.Method(typeof(BhSettingsSection), nameof(BhSettingsSection.Start)), "a settings section's start",
            postfix: new HarmonyMethod(typeof(SettingsTab), nameof(StartPostfix)));
    }

    bool Build(BhGeneralSection general, string labelKey)
    {
        // The smallest of the game's sections, copied: its rows hidden, ours in its list.
        var source = view.sections.FirstOrDefault(s => s != null && s.TryCast<BhSoundSection>() != null) ?? (BhSettingsSection)general;
        var rows = general.GetComponentsInChildren<BhSettingsCheckbox>(true);
        if (rows.Length == 0)
            return false;
        // A row of the General section's list: the ancestor that is one item of the list.
        var row = rows[0].transform;
        while (row.parent != null && row.parent.GetComponentsInChildren<BhSettingsCheckbox>(true).Length < 3)
            row = row.parent;
        var listName = row.parent.name;

        var copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent, false);
        copy.name = Name;
        copy.SetActive(false);
        Section = copy.GetComponent<BhSettingsSection>();
        Section.SectionTypeStored = Type;
        List = copy.GetComponentsInChildren<VerticalLayoutGroup>(true).FirstOrDefault(g => g.name == listName)?.transform;
        if (List == null)
        {
            UnityEngine.Object.DestroyImmediate(copy);
            return false;
        }
        for (var i = 0; i < List.childCount; i++)
            List.GetChild(i).gameObject.SetActive(false);

        // The templates. A switch's copy moves its knob a fixed step from where the template's stands when switched on:
        // the template is a switch that is off.
        var off = rows.FirstOrDefault(r => r.bhCheckbox != null && !r.bhCheckbox.SwitchedOn()) ?? rows[0];
        SwitchRow = Template(off.transform, row.parent, "Switch");
        ChoiceRow = Template((general.cityCameraMode ?? general.GetComponentInChildren<BhLocalizedDropdown>(true))?.transform, row.parent, "Choice");
        ButtonRow = Template((general.emoWheelSettingsButton ?? general.GetComponentInChildren<BhSettingsButton>(true))?.transform, row.parent, "Button");
        view.sections = Append(view.sections, Section);

        var last = screen.tabs[screen.tabs.Length - 1];
        Tab = UnityEngine.Object.Instantiate(last.gameObject, last.transform.parent, false).GetComponent<BhSettingsTab>();
        Tab.name = Name + "_Tab";
        Tab.sectionType = Type;
        // The screen gives every tab its click when it opens (and takes it back when it hides): an open screen has
        // given it already, so ours gets the same now.
        Tab.ClickAction = screen.IsOpen ? last.ClickAction : null;
        GameUi.Label(Tab.text, labelKey);
        Tab.ShowSelected(false);   // the copy may carry its source's selected look
        screen.tabs = Append(screen.tabs, Tab);
        return true;
    }

    // A hidden copy of the list item holding `part`, kept in our section.
    Transform Template(Transform part, Transform list, string what)
    {
        while (part != null && part.parent != list)
            part = part.parent;
        if (part == null)
            return null;
        var copy = UnityEngine.Object.Instantiate(part.gameObject, Section.transform, false).transform;
        copy.name = $"{Name}_{what}Template";
        copy.gameObject.SetActive(false);
        return copy;
    }

    // A title row from the game's factory (the settings elements' prefabs the hotkeys section makes its titles of: only
    // the sections that build rows have them, the copied one has none), in the list; its text is set through `title`.
    public BhSettingsTitle NewTitle(string name)
    {
        if (view.sections.Select(s => s != null ? s.prefabs : null).FirstOrDefault(p => p != null && p.titlePrefab != null)?.titlePrefab is not { } prefab)
            return null;
        var title = UnityEngine.Object.Instantiate(prefab.gameObject, List, false).GetComponent<BhSettingsTitle>();
        title.name = name;
        return title;
    }

    public bool IsShown => view != null && view.ShownSection is { } shown && shown != null && Section != null && shown.Pointer == Section.Pointer;

    // Out of the screen's arrays and destroyed.
    public void Remove()
    {
        all.Remove(this);
        if (view != null && Section != null)
            view.sections = Without(view.sections, Section);
        if (screen != null && Tab != null)
            screen.tabs = Without(screen.tabs, Tab);
        ShowScreenButtons(screen, true);
        if (Tab != null)
            UnityEngine.Object.DestroyImmediate(Tab.gameObject);
        if (Section != null)
            UnityEngine.Object.DestroyImmediate(Section.gameObject);
        Tab = null;
        Section = null;
        List = SwitchRow = ChoiceRow = ButtonRow = null;
    }

    // ---------- the game's switch ----------

    // A copied section's Start (its class's, the copied Sound section's) puts back its own type: ours again, or the
    // switch no longer finds it.
    static void StartPostfix(BhSettingsSection __instance)
    {
        foreach (var tab in all)
            if (tab.Section != null && tab.Section.Pointer == __instance.Pointer)
                tab.Section.SectionTypeStored = tab.Type;
    }

    // After the game switched sections: the screen's Apply and Reset (they work on the game's settings) only on its own
    // tabs; ours laid out and told.
    static void SwitchedPostfix(SettingsSectionsView __instance)
    {
        try
        {
            var ours = all.FirstOrDefault(t => t.view != null && t.view.Pointer == __instance.Pointer && t.IsShown);
            if (all.FirstOrDefault(t => t.view != null && t.view.Pointer == __instance.Pointer) is { } any)
                ShowScreenButtons(any.screen, ours == null);
            if (ours == null)
                return;
            ours.ToTop();
            ours.Shown?.Invoke();
        }
        catch (Exception e)
        {
            Game.Log?.Invoke($"SettingsTab: switch: {e}");
        }
    }

    // The copied list keeps a zero height and a scroll far out until its layout is rebuilt: rebuilt, scrolled to the top.
    void ToTop()
    {
        if (List == null)
            return;
        var rect = List.GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        rect.anchoredPosition = Vector2.zero;
        if (List.GetComponentInParent<ScrollRect>(true) is { } scroll)
        {
            scroll.velocity = Vector2.zero;
            scroll.verticalNormalizedPosition = 1f;
        }
    }

    // The list laid out again after rows changed, the scroll kept.
    public void Relayout()
    {
        if (List != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(List.GetComponent<RectTransform>());
    }

    static void ShowScreenButtons(ScSettings screen, bool show)
    {
        if (screen == null)
            return;
        screen.applyButton?.gameObject.SetActive(show);
        screen.resetButton?.gameObject.SetActive(show);
    }

    static Il2CppReferenceArray<T> Append<T>(Il2CppReferenceArray<T> array, T item) where T : Il2CppObjectBase
    {
        var result = new Il2CppReferenceArray<T>(array.Length + 1);
        for (var i = 0; i < array.Length; i++)
            result[i] = array[i];
        result[array.Length] = item;
        return result;
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
