using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hex.InputSys;
using Hex.Settings.UI;
using Hex.UI;
using Hex.UI.GeneralKit;

namespace OldenEra.Modding.Common;

// The game's screen registry (issue #24): every screen (a BhScreen) is registered under its object's name, opened and
// hidden by name; the registry keeps the stack of open screens, gives the top one the input and the UI camera. A screen
// of ours registers there too (ModScreen). The searches start from what the game's own screens call: the settings
// screen's static Open/Hide, the message box's one-time init. The comments name each member as it is on 0.82.7.
internal static partial class Game
{
    static IEnumerable<MethodInfo> CalledBy(MethodInfo method) => GameCode.Calls(method).SelectMany(c => GameCode.At(c.target));

    static IEnumerable<MethodInfo> CalledBy(IEnumerable<MethodInfo> methods) => methods.SelectMany(CalledBy);

    // The settings screen's static Open(Action) (ScSettings.Open) and Hide() (ScSettings.Hide): registry calls by name.
    static IEnumerable<MethodInfo> SettingsOpen => typeof(ScSettings).Methods().Static().Named("Open").Takes(typeof(Il2CppSystem.Action));
    static IEnumerable<MethodInfo> SettingsHide => typeof(ScSettings).Methods().Static().Named("Hide").Takes();

    // The registry's instance (cqp.ujg, it makes one if there is none): what the settings screen's Open asks for.
    static readonly Lookup<MethodInfo> screenRegistry = Lookup<MethodInfo>.Among("screen registry instance",
        () => CalledBy(SettingsOpen).Where(m => m.IsStatic && m.GetParameters().Length == 0 && m.ReturnType.IsClass && m.ReturnType != typeof(string))
            .DistinctCode(),
        Members.Name, onlyOne: true).Generate("ScreenRegistry");

    static Type ScreenRegistryType => screenRegistry.Value?.ReturnType;

    // The registry's class (cqp).
    static readonly Lookup<Type> screenRegistryType = Lookup<Type>.Among("screen registry class",
        () => ScreenRegistryType is { } t ? new[] { t } : null, t => t.Name).Generate("GameScreenRegistry");

    static IEnumerable<MethodInfo> RegistryMethods => ScreenRegistryType is { } t ? t.Methods().Instance() : Enumerable.Empty<MethodInfo>();

    // Open a screen by name (cqp.ujm): BhScreen M(string) the settings screen's Open calls.
    static readonly Lookup<MethodInfo> openScreenByName = Lookup<MethodInfo>.Among("open screen by name",
        () => CalledBy(SettingsOpen).Where(m => m.DeclaringType == ScreenRegistryType && !m.IsStatic && m.ReturnType == typeof(BhScreen))
            .Takes(typeof(string)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("Open");

    // Open a screen (cqp.ujn), the one the open by name calls: its Show, onto the stack, the "opened" event.
    static readonly Lookup<MethodInfo> openScreen = Lookup<MethodInfo>.Among("open screen",
        () => openScreenByName.Value is { } byName
            ? CalledBy(byName).Where(m => m.DeclaringType == ScreenRegistryType && m.ReturnType == typeof(BhScreen)).Takes(typeof(BhScreen)).DistinctCode()
            : null,
        Members.Name, onlyOne: true).Generate("OpenScreen");

    // A registered screen by name, null when there is none (cqp.ujk, it logs the miss), the one the open by name calls.
    static readonly Lookup<MethodInfo> screenByName = Lookup<MethodInfo>.Among("screen by name",
        () => openScreenByName.Value is { } byName
            ? CalledBy(byName).Where(m => m.DeclaringType == ScreenRegistryType && m.ReturnType == typeof(BhScreen)).Takes(typeof(string)).DistinctCode()
            : null,
        Members.Name, onlyOne: true).Generate("Screen");

    // Hide a screen by name (cqp.ujr): its Hide, off the stack, the screen below gets the clicks back.
    static readonly Lookup<MethodInfo> hideScreenByName = Lookup<MethodInfo>.Among("hide screen by name",
        () => CalledBy(SettingsHide).Where(m => m.DeclaringType == ScreenRegistryType && !m.IsStatic).Returns(typeof(void)).Takes(typeof(string)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("Hide");

    // The registered screens by their objects' names (cqp.ceoz) and the stack of open ones, the top one last (cepa).
    static readonly Lookup<PropertyInfo> registeredScreens = Lookup<PropertyInfo>.Among("registered screens",
        () => ScreenRegistryType is { } t ? Properties(t).Where(p => p.PropertyType == typeof(Il2CppSystem.Collections.Generic.Dictionary<string, BhScreen>)) : null,
        Name, onlyOne: true).Generate("Screens");

    static readonly Lookup<PropertyInfo> openScreens = Lookup<PropertyInfo>.Among("open screens",
        () => ScreenRegistryType is { } t ? Properties(t).Where(p => p.PropertyType == typeof(Il2CppSystem.Collections.Generic.List<BhScreen>)) : null,
        Name, onlyOne: true).Generate("OpenScreens");

    // Register a screen under its object's name if the name is free, and put it in the hidden state (cqp.efg; BhScreen's
    // Awake does the same): void M(BhScreen) reading the screens (+0x10) and the screen's name. Its clones have their
    // own code (ekm, oal, ujj): pinned.
    static readonly Lookup<MethodInfo> registerScreen = Lookup<MethodInfo>.Among("register screen",
        () => registeredScreens.Value is { } screens && openScreens.Value is { } stack &&
              GameCode.FieldOf(screens)?.offset is { } screensAt && GameCode.FieldOf(stack)?.offset is { } stackAt
            ? RegistryMethods.Returns(typeof(void)).Takes(typeof(BhScreen))
                .Where(m => GameCode.Offsets(m) is var read && read.Contains(screensAt) && !read.Contains(stackAt) &&
                            CalledBy(m).Any(c => c.Name == "get_name"))
                .DistinctCode()
            : null,
        Members.Name, onlyOne: true).Generate("Register");

    // The registry's Init (cqp.Init): the scene loaders call it after loading a mode's scenes, it runs every registered
    // screen's one-time init; the registry was emptied before (cqp.Clear, on leaving a mode), so ours register again.
    static readonly Lookup<MethodInfo> screenRegistryInit = Lookup<MethodInfo>.Among("screen registry init",
        () => RegistryMethods.Named("Init").Takes().Returns(typeof(void)),
        Members.Name, onlyOne: true).GenerateTarget("ScreenRegistryInit");

    // ---------- a screen ----------

    // The message box's one-time init (ScMessageBox.hbq): the base one, then its keys bound (the one of its own void M()s
    // binding keys).
    static IEnumerable<MethodInfo> MessageBoxInit =>
        typeof(ScMessageBox).Methods().Instance().Returns(typeof(void)).Takes().Where(m => m.DeclaringType == typeof(ScMessageBox) &&
            CalledBy(m).Any(c => !c.IsStatic && ParamTypes(c).SequenceEqual(new[] { typeof(EInputAction), typeof(EInputState), typeof(Il2CppSystem.Action) })));

    // A screen's one-time init (BhScreen.hbq): makes its input receiver; what the message box's own one calls first.
    static readonly Lookup<MethodInfo> screenSetUp = Lookup<MethodInfo>.Among("screen one-time init",
        () => CalledBy(MessageBoxInit).Where(m => m.DeclaringType == typeof(BhScreen) && !m.IsStatic).Returns(typeof(void)).Takes().DistinctCode(),
        Members.Name, onlyOne: true).Generate("SetUpOnce");

    // A screen's input receiver (BhScreen.cekt, a cbe): its key bindings, on the game's input stack while the screen is
    // open.
    static readonly Lookup<PropertyInfo> screenInput = Lookup<PropertyInfo>.Among("screen input",
        () => DeclaredProperties(typeof(BhScreen)).Where(p => !p.Name.EndsWith("k__BackingField") &&
            p.PropertyType.Methods().Instance().Returns(typeof(void)).Takes(typeof(EInputAction), typeof(EInputState), typeof(Il2CppSystem.Action)).Any()),
        Name, onlyOne: true).Generate("Input");

    static readonly Lookup<Type> inputReceiver = Lookup<Type>.Among("input receiver",
        () => screenInput.Value is { } p ? new[] { p.PropertyType } : null, t => t.Name).Generate("GameInputReceiver");

    // Bind a key (cbe.qpo): what the message box's init binds Enter and Escape with.
    static readonly Lookup<MethodInfo> bindKey = Lookup<MethodInfo>.Among("bind key",
        () => CalledBy(MessageBoxInit).Where(m => m.DeclaringType == inputReceiver.Value)
            .Takes(typeof(EInputAction), typeof(EInputState), typeof(Il2CppSystem.Action)).DistinctCode(),
        Members.Name, onlyOne: true).Generate("Bind");

    // Whether the screen is open, on the stack (BhScreen.cekq): the bool the registry's open checks first, besides
    // allowedMultipleOpens and SetFocused (settings of the screen).
    static readonly Lookup<PropertyInfo> screenIsOpen = Lookup<PropertyInfo>.Among("screen is open",
        () => openScreen.Value is { } open && GameCode.Offsets(open) is var read
            ? DeclaredProperties(typeof(BhScreen)).Where(p => p.PropertyType == typeof(bool) && !p.Name.EndsWith("k__BackingField") &&
                p.Name != nameof(BhScreen.allowedMultipleOpens) && p.Name != nameof(BhScreen.SetFocused) && GameCode.FieldOf(p)?.offset is { } at && read.Contains(at))
            : null,
        Name, onlyOne: true).Generate("IsOpen");

    // What to call once when the screen hides (BhScreen.cekl: its Hide calls it and clears it).
    static readonly Lookup<PropertyInfo> screenWhenClosed = Lookup<PropertyInfo>.Among("screen when closed",
        () => DeclaredProperties(typeof(BhScreen)).Where(p => p.PropertyType == typeof(Il2CppSystem.Action) && !p.Name.EndsWith("k__BackingField")),
        Name, onlyOne: true).Generate("WhenClosed", writable: true);
}
