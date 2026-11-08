using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.GameHub.UICommon;
using Hex.UI;
using Hex.UI.GeneralKit;

namespace OldenEra.Modding.Common;

// The game's widgets the Mods window (mod-settings) is built from (issue #14): its message and input boxes, opened with
// the game's own options objects, its tabs, buttons and check marks. Used through the generated names (GameNames.g.cs).
// The comments name each member as it is on 0.82.7.
internal static partial class Game
{
    // The properties of a game class of one type, by the place of their field in the object (the order the game's code
    // fills them in); `count` of them or none (the class changed: the build stops on it).
    static List<PropertyInfo> FieldsInOrder(Type owner, Type valueType, int count)
    {
        var found = Properties(owner).Where(p => p.DeclaringType == owner && p.PropertyType == valueType)
            .Select(p => (p, field: GameCode.FieldOf(p))).Where(x => x.field != null)
            .OrderBy(x => x.field.Value.offset).Select(x => x.p).ToList();
        return found.Count == count ? found : null;
    }

    static Lookup<PropertyInfo> NthField(string what, Func<Type> owner, Type valueType, int index, int count) =>
        NthField(what, owner, () => valueType, index, count);

    // The same with a value type found by another lookup (asked only when this one is).
    static Lookup<PropertyInfo> NthField(string what, Func<Type> owner, Func<Type> valueType, int index, int count) =>
        Lookup<PropertyInfo>.Among(what, () => owner() is { } t && valueType() is { } v && FieldsInOrder(t, v, count) is { } all ? new[] { all[index] } : null,
            Name, onlyOne: true);

    // The one parameter's type of a static Open(options) of a game screen.
    static IEnumerable<Type> OpenOptions(Type screen) =>
        screen.Methods().Static().Named("Open").Where(m => m.GetParameters().Length == 1).Select(m => m.GetParameters()[0].ParameterType).Distinct();

    // ---------- the input box (ScInputBox.Open(dck)) ----------

    // Its options (dck): the game fills them with translated texts (ctl.vcm, cif.rev, cii.rfn): +0x10 the caption, +0x18
    // the placeholder (passwordbox_placeholder, enter_by_code_placeholder), +0x20 the confirm button's text, +0x28 the
    // cancel button's, +0x38 a text; +0x40 the Action OnBtnCancel calls, +0x48 the Action<string> OnBtnOk calls with the
    // text typed.
    static readonly Lookup<Type> inputBoxOptions = Lookup<Type>.Among("input box options",
        () => OpenOptions(typeof(ScInputBox)), t => t.Name, onlyOne: true).Generate("GameInputBoxOptions");

    static Type InputBoxOptions => inputBoxOptions.Value;

    static readonly Lookup<PropertyInfo> inputBoxCaption = NthField("input box caption", () => InputBoxOptions, typeof(string), 0, 5).Generate("Caption", writable: true);
    static readonly Lookup<PropertyInfo> inputBoxPlaceholder = NthField("input box placeholder", () => InputBoxOptions, typeof(string), 1, 5).Generate("Placeholder", writable: true);
    static readonly Lookup<PropertyInfo> inputBoxConfirm = NthField("input box confirm text", () => InputBoxOptions, typeof(string), 2, 5).Generate("ConfirmText", writable: true);
    static readonly Lookup<PropertyInfo> inputBoxCancel = NthField("input box cancel text", () => InputBoxOptions, typeof(string), 3, 5).Generate("CancelText", writable: true);
    static readonly Lookup<PropertyInfo> inputBoxOnCancel = NthField("input box on cancel", () => InputBoxOptions, typeof(Il2CppSystem.Action), 0, 1).Generate("OnCancel", writable: true);
    static readonly Lookup<PropertyInfo> inputBoxOnConfirm = NthField("input box on confirm", () => InputBoxOptions, typeof(Il2CppSystem.Action<string>), 0, 1).Generate("OnConfirm", writable: true);

    // ---------- the message box (ScMessageBox.Open(dcl)) ----------

    // Its options (dcl): +0x10 the caption, +0x18 the text (settings_messagebox_title_discard / _label_discard in
    // SettingsSectionsController.OnResetButton), +0x20 a notification; +0x30 the confirm button, +0x38 the cancel button
    // (OnBtnCancel calls the action of the one at +0x38).
    static readonly Lookup<Type> messageBoxOptions = Lookup<Type>.Among("message box options",
        () => OpenOptions(typeof(ScMessageBox)), t => t.Name, onlyOne: true).Generate("GameMessageBoxOptions");

    static Type MessageBoxOptions => messageBoxOptions.Value;

    // A button of the box (dcm): its text (+0x10), what it does (+0x18) and a flag (+0x20).
    static readonly Lookup<Type> messageBoxButton = Lookup<Type>.Among("message box button",
        () => MessageBoxOptions is { } options
            ? Properties(options).Select(p => p.PropertyType).Where(t => t.IsClass && t.Assembly == options.Assembly && t != options).Distinct()
            : null, t => t.Name, onlyOne: true).Generate("GameMessageBoxButton");

    static readonly Lookup<PropertyInfo> messageBoxCaption = NthField("message box caption", () => MessageBoxOptions, typeof(string), 0, 3).Generate("Caption", writable: true);
    static readonly Lookup<PropertyInfo> messageBoxText = NthField("message box text", () => MessageBoxOptions, typeof(string), 1, 3).Generate("Text", writable: true);
    static readonly Lookup<PropertyInfo> messageBoxConfirm = NthField("message box confirm button", () => MessageBoxOptions, () => messageBoxButton.Value, 0, 2).Generate("Confirm", writable: true);
    static readonly Lookup<PropertyInfo> messageBoxCancel = NthField("message box cancel button", () => MessageBoxOptions, () => messageBoxButton.Value, 1, 2).Generate("Cancel", writable: true);
    static readonly Lookup<PropertyInfo> messageButtonLabel = NthField("message box button label", () => messageBoxButton.Value, typeof(string), 0, 1).Generate("Label", writable: true);
    static readonly Lookup<PropertyInfo> messageButtonClick = NthField("message box button click", () => messageBoxButton.Value, typeof(Il2CppSystem.Action), 0, 1).Generate("Click", writable: true);

    // ---------- tabs (BhTab, the game hub's: the profile's, the leaderboard's) ----------

    static int? FieldOffset(Type owner, string property) => GameCode.FieldOf(owner.GetProperty(property))?.offset;

    // The void M(bool)s of a widget whose code touches all these fields of it.
    static IEnumerable<MethodInfo> BoolSetters(Type owner, params string[] fields) =>
        fields.Select(f => FieldOffset(owner, f)).ToList() is var offsets && offsets.All(o => o != null)
            ? owner.Methods().Instance().Returns(typeof(void)).Takes(typeof(bool))
                .Where(m => GameCode.Offsets(m) is var read && offsets.All(o => read.Contains(o.Value))).DistinctCode()
            : null;

    // Its selected look (BhTab.jiv): the selected background on, the other off, the label's colour; only on a tab that
    // is available.
    static readonly Lookup<MethodInfo> tabShowSelected = Lookup<MethodInfo>.Among("hub tab selected look",
        () => BoolSetters(typeof(BhTab), nameof(BhTab.selectedFon)), Members.Name, onlyOne: true).Generate("ShowSelected");

    // Available or not (BhTab.jiu): its flag (+0x90) and the not selected / unavailable backgrounds.
    static readonly Lookup<MethodInfo> tabAvailable = Lookup<MethodInfo>.Among("hub tab available",
        () => BoolSetters(typeof(BhTab), nameof(BhTab.unavailableFon)), Members.Name, onlyOne: true).Generate("SetAvailable");

    // What a click does (BhTab.ccmi, Action<cia>: OnClick calls it with the tab, then plays the click sound).
    static readonly Lookup<PropertyInfo> tabClick = Lookup<PropertyInfo>.Among("hub tab click",
        () => Properties(typeof(BhTab)).Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(Il2CppSystem.Action<>)),
        Name, onlyOne: true).Generate("Click", writable: true);

    // The type the click is called with (cia).
    static readonly Lookup<Type> tabClickArgument = Lookup<Type>.Among("hub tab click argument",
        () => tabClick.Value is { } p ? new[] { p.PropertyType.GetGenericArguments()[0] } : null, t => t.Name).Generate("GameTabItem");

    // ---------- the Load screen's tabs and tag chips (Hex.UI.SaveLoad.ScLoad) ----------

    // A tab's data for BhSimpleTabs.Init(List<dcs>, int) (dcs): its label (+0x10) and what choosing it does (+0x18; Select
    // calls it).
    static readonly Lookup<Type> simpleTabData = Lookup<Type>.Among("simple tab data",
        () => typeof(Hex.UI.Elements.BhSimpleTabs).Methods().Named("Init").Where(m => m.GetParameters().Length == 2)
            .Select(m => m.GetParameters()[0].ParameterType).Where(t => t.IsGenericType).Select(t => t.GetGenericArguments()[0]).Distinct(),
        t => t.Name, onlyOne: true).Generate("GameSimpleTabData");

    static readonly Lookup<PropertyInfo> simpleTabLabel = NthField("simple tab label", () => simpleTabData.Value, typeof(string), 0, 1).Generate("Label", writable: true);
    static readonly Lookup<PropertyInfo> simpleTabClick = NthField("simple tab click", () => simpleTabData.Value, typeof(Il2CppSystem.Action), 0, 1).Generate("Click", writable: true);

    // A tag chip of the save list (BhTag): its text, the chip sized to it (yeo), and its colour (yeq).
    static readonly Lookup<MethodInfo> tagText = Lookup<MethodInfo>.Among("tag chip text",
        () => typeof(BhTag).Methods().Instance().Returns(typeof(void)).Takes(typeof(string)).DistinctCode(), Members.Name, onlyOne: true).Generate("ShowText");

    static readonly Lookup<MethodInfo> tagColor = Lookup<MethodInfo>.Among("tag chip colour",
        () => typeof(BhTag).Methods().Instance().Returns(typeof(void)).Takes(typeof(UnityEngine.Color)).DistinctCode(), Members.Name, onlyOne: true).Generate("ShowColor");

    // ---------- the save list's filter (BhFilter: a funnel button opening a list of check marks) ----------

    // Its options set up (BhFilter.uxi(string[], Action)): an element per name (a localization key the game translates),
    // the action called when one is ticked or unticked.
    static readonly Lookup<MethodInfo> filterInit = Lookup<MethodInfo>.Among("save filter set up",
        () => typeof(Hex.UI.SaveLoad.BhFilter).Methods().Instance().Returns(typeof(void))
            .Takes(typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray), typeof(Il2CppSystem.Action)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("SetUp");

    // Its elements (BhFilter.cfax), in the order of the names.
    static readonly Lookup<PropertyInfo> filterElements = Lookup<PropertyInfo>.Among("save filter elements",
        () => Properties(typeof(Hex.UI.SaveLoad.BhFilter)).Where(p => p.PropertyType == typeof(Il2CppSystem.Collections.Generic.List<Hex.UI.SaveLoad.BhFilterElement>)),
        Name, onlyOne: true).Generate("Elements");

    // Whether an element is ticked (BhFilterElement.uxo, its flag +0x48).
    static readonly GameMethod<Hex.UI.SaveLoad.BhFilterElement, bool> filterElementOn =
        new GameMethod<Hex.UI.SaveLoad.BhFilterElement, bool>("save filter element ticked", m => !m.IsStatic && m.GetParameters().Length == 0).Generate("Ticked");

    // ---------- buttons and check marks ----------

    // A button available or greyed out (BhGenericButton.uhb): the active and grey looks, and its flag (+0xA8).
    static readonly Lookup<MethodInfo> buttonAvailable = Lookup<MethodInfo>.Among("button available",
        () => BoolSetters(typeof(BhGenericButton), nameof(BhGenericButton.itemGray), nameof(BhGenericButton.itemActive)), Members.Name, onlyOne: true)
        .Generate("SetAvailable");

    // A check mark (the save list filter's BhToggle) set without its callback (BhToggle.yes): the toggle's value and
    // the mark shown or hidden.
    static readonly Lookup<MethodInfo> toggleShowValue = Lookup<MethodInfo>.Among("check mark show value",
        () => BoolSetters(typeof(BhToggle), nameof(BhToggle.toggle), nameof(BhToggle.checkmark)), Members.Name, onlyOne: true)
        .Generate("ShowValue");
}
