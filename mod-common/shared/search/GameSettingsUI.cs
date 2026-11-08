using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.Settings.UI;
using Hex.UI.GeneralKit;

namespace OldenEra.Modding.Common;

// The game's settings screen (sections, tabs, switches). The members are used through the generated names
// (GameNames.g.cs: section.SectionType, view.ShownSection, tab.ClickAction, tab.ShowSelected(bool), checkbox.SwitchedOn(),
// settingsCheckbox.SwitchCallback); the searches behind them are here. The comments name each member as it is on 0.82.7.
internal static partial class Game
{
    // A section's settings type, the one the game's switch finds it by (BhSettingsSection.bwqv; it has no setter: it is
    // written through its backing field _bwqv_k__BackingField, +0x44, SectionTypeStored).
    static readonly GameProperty<BhSettingsSection, ESettingsType> sectionType =
        new GameProperty<BhSettingsSection, ESettingsType>("section type", _ => true).Generate("SectionType");
    static readonly GameProperty<BhSettingsSection, ESettingsType> sectionTypeField =
        new GameProperty<BhSettingsSection, ESettingsType>("section type field", _ => true, backingField: true).Generate("SectionTypeStored", writable: true);

    // The section the sections view shows now (SettingsSectionsView.bwrx).
    static readonly GameProperty<SettingsSectionsView, BhSettingsSection> shownSection =
        new GameProperty<SettingsSectionsView, BhSettingsSection>("shown section", _ => true).Generate("ShownSection");

    // A tab's click action, set by the screen when it opens (BhSettingsTab.bwrb); our tab takes another tab's.
    static readonly GameProperty<BhSettingsTab, Il2CppSystem.Action<ESettingsType>> tabAction =
        new GameProperty<BhSettingsTab, Il2CppSystem.Action<ESettingsType>>("tab click action", _ => true).Generate("ClickAction", writable: true);

    // A tab's selected look on or off: void M(bool) (BhSettingsTab.kqf).
    static readonly GameAction<BhSettingsTab> tabSelected = new GameAction<BhSettingsTab>("tab selected look",
        m => !m.IsStatic && m.GetParameters() is { Length: 1 } ps && ps[0].ParameterType == typeof(bool)).Generate("ShowSelected");

    // Whether a switch is on: the switch's only bool M() (BhCheckbox.xyx).
    static readonly GameMethod<BhCheckbox, bool> checkboxOn =
        new GameMethod<BhCheckbox, bool>("switch is on", m => !m.IsStatic && m.GetParameters().Length == 0).Generate("SwitchedOn");

    // A settings switch's callback with the new value (BhSettingsCheckbox.bwqr); our switches get our own.
    static readonly GameProperty<BhSettingsCheckbox, Il2CppSystem.Action<bool>> checkboxChanged =
        new GameProperty<BhSettingsCheckbox, Il2CppSystem.Action<bool>>("switch callback", _ => true).Generate("SwitchCallback", writable: true);

    // A main menu button's click action (BhNavigationButton.chbk, +0x90: its OnPointerClick calls the action there; it
    // has three Actions); the Mods button, a copy of Options, gets ours.
    static readonly GameProperty<BhNavigationButton, Il2CppSystem.Action> menuButtonClick =
        new GameProperty<BhNavigationButton, Il2CppSystem.Action>("menu button click", _ => true).Generate("MenuClick", writable: true);

    // A section's fills: its Start and its void M(SettingsData) (Show, kpm, kpn on 0.82.7); which one runs for the
    // General section was not known, so all of them.
    static readonly Lookup<List<MethodInfo>> sectionFills = new Lookup<List<MethodInfo>>("settings section fills",
        () => typeof(BhSettingsSection).Methods().Instance().Returns(typeof(void))
            .Where(m => m.Name == "Start" || m.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Hex.Settings.Data.SettingsData) }))
            .ToList() is { Count: > 0 } found ? found : null,
        Members.Names).GenerateTarget("SettingsSectionFills");

}
