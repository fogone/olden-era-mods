using System;
using System.Collections.Generic;
using System.Linq;
using Hex.UI.GeneralKit;
using Hex.UI.SaveLoad;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Events;

namespace Nobirds.OldenEra.Modding.Common;

// Small widgets of the game set up with our data through the game's own methods (GameWidgets.cs): the Load screen's
// filter (a funnel opening check marks), a tag chip, a dropdown's options, a check box.
//
//   GameUi.SetUpFilter(filter, tagKeys, () => redraw = true);   var ticked = GameUi.Ticked(filter, tagKeys);
//   GameUi.ShowChip(chip, Texts.Get(key), back, labelColor);
//   GameUi.SetDropdownTexts(dropdown, new[] { "A", "B" }, chosen: 0);
//   GameUi.Check(toggle, isOn, value => ...);
public static class GameUi
{
    // The filter's check marks, one per name (localization keys: the game translates them); `changed` when one is
    // ticked or unticked. Setting it up unticks them all.
    public static void SetUpFilter(BhFilter filter, IReadOnlyList<string> keys, Action changed) =>
        filter.SetUp(new Il2CppStringArray(keys.ToArray()), (Il2CppSystem.Action)(changed ?? (() => { })));

    // The names ticked in a filter set up with `keys` (its elements are in their order).
    public static HashSet<string> Ticked(BhFilter filter, IReadOnlyList<string> keys)
    {
        var ticked = new HashSet<string>();
        if (filter == null || filter.Elements is not { } elements)
            return ticked;
        for (var i = 0; i < elements.Count && i < keys.Count; i++)
            if (elements[i] != null && elements[i].Ticked())
                ticked.Add(keys[i]);
        return ticked;
    }

    // A tag chip with a text (the game sizes the chip to it) in a colour, its text in another.
    public static void ShowChip(BhTag chip, string text, Color back, Color label)
    {
        chip.ShowText(text);
        chip.ShowColor(back);
        if (chip.label != null)
            chip.label.color = label;
    }

    // A dropdown's options and the one chosen, by the game's own Init (it closes a list the player has open).
    public static void SetDropdownTexts(BhDropdown dropdown, IEnumerable<string> texts, int chosen) =>
        dropdown.Init(new Il2CppSystem.Array(new Il2CppStringArray(texts.ToArray()).Pointer), chosen, EDropdownType.Text);

    // A check box showing a value (without calling back) and what a click on it does.
    public static void Check(BhToggle toggle, bool value, Action<bool> changed)
    {
        toggle.ShowValue(value);
        toggle.toggle.onValueChanged.AddListener((UnityAction<bool>)changed);
    }

    // A text of the game showing our localization key, as its own texts do: it follows a language change.
    public static void Label(Component text, string key)
    {
        if (text == null)
            return;
        var value = Texts.Get(key);
        if (text.GetComponent<GameLocalizedText>() is { } localized)
        {
            localized.textSid = "";
            try { localized.HardSetText(value); }   // its own text element, the one it draws from
            catch (Exception) { }                   // a copy not awake yet has none: the text below is enough
            localized.textSid = key;
        }
        if (text.GetComponent<TMPro.TMP_Text>() is { } tmp)
            tmp.text = value;
    }

    // A text of the game showing a text of ours that is no localization key (a number, a name): its key cleared, or the
    // game puts its own text back.
    public static void Text(Component text, string value)
    {
        if (text == null)
            return;
        if (text.GetComponent<GameLocalizedText>() is { } localized)
        {
            localized.textSid = "";
            try { localized.HardSetText(value); }
            catch (Exception) { }
        }
        if (text.GetComponent<TMPro.TMP_Text>() is { } tmp)
            tmp.text = value;
    }

    // The game's localized text (its text component that follows the language) on or under an object; null if none.
    public static Component LocalizedText(Component under) =>
        under == null ? null : under.GetComponent<GameLocalizedText>() ?? under.GetComponentInChildren<GameLocalizedText>(true);

    // The game's text tooltip (a header and a text, as a unit buff's) over a rectangle, kept on screen by the game.
    public static void ShowTooltip(string header, string text, RectTransform over, Hex.UI.Tooltips.ESnapDirection side) =>
        GameNames.Tooltips?.ShowTexts(header, text, null, over, null, side, false);

    public static void HideTooltip() => GameNames.Tooltips?.Hide();

    // The tooltip's positioner (the object that places it, the one ModEvents.TooltipPlace names), to match the two.
    public static Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase Positioner(Hex.UI.Tooltips.BhTooltip tooltip) => tooltip?.Positioner;

    // The Load screen's tabs (BhSimpleTabs) labelled and wired by the game's own Init: a label and a click each, the one
    // selected. The game labels as many tabs as it has: add copies of a tab to `tabs` first for more.
    public static void SetTabs(Hex.UI.Elements.BhSimpleTabs tabs, IReadOnlyList<(string label, Action click)> items, int selected)
    {
        var data = new Il2CppSystem.Collections.Generic.List<GameSimpleTabData>();
        foreach (var (label, click) in items)
        {
            var item = new GameSimpleTabData();
            item.Label = label;
            item.Click = (Il2CppSystem.Action)click;
            data.Add(item);
        }
        tabs.Init(data, selected);
    }
}