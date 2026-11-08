using System;
using System.Collections.Generic;
using System.Linq;
using Hex.UI.GeneralKit;
using Hex.UI.SaveLoad;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.Events;

namespace OldenEra.Modding.Common;

// Small widgets of the game set up with our data through the game's own methods (GameWidgets.cs): the Load screen's
// filter (a funnel opening check marks), a tag chip, a dropdown's options, a check box.
//
//   GameUi.SetUpFilter(filter, tagKeys, () => redraw = true);   var ticked = GameUi.Ticked(filter, tagKeys);
//   GameUi.ShowChip(chip, Texts.Get(key), back, labelColor);
//   GameUi.SetDropdownTexts(dropdown, new[] { "A", "B" }, chosen: 0);
//   GameUi.Check(toggle, isOn, value => ...);
internal static class GameUi
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
}
