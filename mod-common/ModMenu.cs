using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;

namespace Nobirds.OldenEra.Modding.Common;

// A plugin's entry in the "Моды" tab of the game's settings (drawn by the mod-settings plugin): a title with the
// plugin's version, a master switch and the plugin's own switches and choices, in the order they are declared.
// Values live in the plugin's config file, section [Menu]; a plugin works the same without mod-settings.
//
// Texts are keys in the plugin's lang/<language>.json (see Texts), made from the plugin's id (the last part of its
// GUID, "visited") and the entry's key:
//   oe.visited.title                     the mod's name
//   oe.visited.ShowBadges                a switch's label;  oe.visited.ShowBadges.desc  its hover description
//   oe.visited.Highlight.Strobe          a choice's option, by the enum value's name
//
//   var menu = Menu;                                   // ModBase makes it
//   showBadges = menu.Toggle("ShowBadges", true);
//   if (showBadges.On) ...                             // off while the whole mod is switched off
public sealed class ModMenu
{
    const string Section = "Menu";
    public const string EnabledKey = "oe.common.enabled";
    const string OffHeadingKey = "oe.common.offHeading";
    static readonly List<ModMenu> menus = new();

    static ModMenu()
    {
        Texts.LoadOwn();   // "Mod enabled" and its description, the off heading: the library's lang files
        var common = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, "OldenEra.Mods.cfg"), true);
        Mode = common.Bind("Mods", "Mode", ModsMode.Hotseat, "When the mods work: SinglePlayer, Hotseat (single player and hotseat), Off. Never online.");
        var values = Enum.GetValues(typeof(ModsMode)).Cast<object>().ToArray();
        for (var i = 0; i < values.Length; i++)
            Texts.Alias(ChoiceItem.OptionKeyOf(ModeKey, i), $"{ModeKey}.{values[i]}");
        ModeItem = new ChoiceItem("Mode", ModeKey, Mode, values);
    }

    // When the mods work at all: only in a single player game, in a single player game and hotseat (the default; all
    // the players at this computer see the same), or never; never online nor in a replay (fogone 08.11: no "always", the
    // mods must give no advantage in a multiplayer game). A mod that does not work is off as by its own switch, which
    // keeps its value. Lives in the library's own config file (BepInEx\config\OldenEra.Mods.cfg), not a plugin's: the
    // mods obey it with or without the Mods tab, which shows it first (ModeItem, texts in the library's lang files).
    // A config with the old "Always" is not read and gets the default.
    public enum ModsMode { SinglePlayer, Hotseat, Off }
    public const string ModeKey = "oe.common.mode", ModeTitleKey = "oe.common.mode.title";
    public static ConfigEntry<ModsMode> Mode { get; }
    public static ChoiceItem ModeItem { get; }

    // Whether the game being played is not a single player one (online, hotseat, a replay): the core answers it from the
    // game's session (Game.SinglePlayer), once per session; nobody answers it without the core (then: single player).
    public static Func<bool> NotSinglePlayer { get; set; }

    // Whether it is a hotseat game (played here, more than one side played by people at this computer; Game.Hotseat):
    // the core answers it too (from 0.7.3); without an answer, not hotseat.
    public static Func<bool> HotseatGame { get; set; }

    // The mods work now, by the mode.
    public static bool ModsWork => Mode.Value switch
    {
        ModsMode.SinglePlayer => NotSinglePlayer?.Invoke() != true,
        ModsMode.Hotseat => NotSinglePlayer?.Invoke() != true || HotseatGame?.Invoke() == true,
        _ => false,
    };

    // Every plugin's menu, by the mod's name in the game's language: the same order however the plugins loaded or
    // were reloaded (the dev loader loads them in the order their files changed).
    public static IReadOnlyList<ModMenu> All =>
        menus.OrderBy(m => Texts.Get($"oe.{m.Id}.title"), StringComparer.CurrentCultureIgnoreCase).ThenBy(m => m.Guid).ToList();

    // Bumped whenever a menu is added or removed (a plugin reloaded by the dev loader), or the catalog's rows change:
    // mod-settings redraws.
    public static int Revision { get; private set; }

    public static void Changed() => Revision++;

    public string Id { get; }
    public string Version { get; }
    public string Guid { get; }
    // The title row's key: the mod's name and version.
    public string HeadingKey { get; }
    public ConfigFile Config { get; }
    public ConfigEntry<bool> Enabled { get; }
    public IReadOnlyList<MenuItem> Items => items;
    readonly List<MenuItem> items = new();

    // On: its own switch, and the mods work now (Mode).
    public bool IsEnabled => Enabled.Value && ModsWork;

    // Off for this version of the game (built for another one: ModBase): the Mods tab shows its title saying so and no
    // switches (fogone 06.11). The main menu's notice (VersionNotice) is for when the Mods tab itself is off.
    public bool IsOff { get; private set; }
    // The game's version it was built for, when it is off for this one.
    public string BuiltFor { get; private set; }

    public static void Off(BasePlugin plugin, string builtFor)
    {
        var menu = For(plugin);
        menu.IsOff = true;
        menu.BuiltFor = builtFor;
        var title = $"oe.{menu.Id}.title";
        Texts.Compose(menu.HeadingKey, language => string.Format(Texts.GetIn(language, OffHeadingKey), Texts.GetIn(language, title), menu.Version, builtFor));
        Revision++;
    }

    ModMenu(BasePlugin plugin)
    {
        var info = MetadataHelper.GetMetadata(plugin);
        Version = info?.Version?.ToString() ?? "";
        Guid = info?.GUID ?? plugin.GetType().FullName;
        Id = Guid.Split('.').Last();
        Config = plugin.Config;
        var title = $"oe.{Id}.title";
        HeadingKey = Texts.Compose($"oe.{Id}.heading", language => $"{Texts.GetIn(language, title)} · {Version}");
        Enabled = Config.Bind(Section, "Enabled", true, "Мод включён");
    }

    static string RussianOf(string key) => Texts.GetIn(Hex.UI.ELang.russian, key);

    // The plugin's menu; one per plugin, made once (ModBase). A reloaded build gets a new menu in the old one's place.
    public static ModMenu For(BasePlugin plugin)
    {
        var menu = menus.FirstOrDefault(m => m.Config == plugin.Config);
        if (menu != null)
            return menu;
        menu = new ModMenu(plugin);
        var index = menus.FindIndex(m => m.Guid == menu.Guid);
        if (index >= 0)
            menus[index] = menu;
        else
            menus.Add(menu);
        Revision++;
        return menu;
    }

    public static void Remove(BasePlugin plugin)
    {
        if (menus.RemoveAll(m => m.Config == plugin.Config) > 0)
            Revision++;
    }

    // An on/off switch.
    public Switch Toggle(string key, bool defaultValue)
    {
        var labelKey = LabelKeyOf(key);
        var entry = Config.Bind(Section, key, defaultValue, RussianOf(labelKey));
        items.Add(new ToggleItem(key, labelKey, entry));
        return new Switch(this, entry);
    }

    // One of several options (a dropdown). The option texts are written by the enum value's name, <label key>.<Name>
    // (oe.visited.Highlight.Strobe); the game's dropdown looks them up by number, so <label key>.0 .. .N are made from
    // them here, in the order of the enum's values.
    public Choice<T> Choose<T>(string key, T defaultValue) where T : struct, Enum
    {
        var labelKey = LabelKeyOf(key);
        var values = Enum.GetValues(typeof(T)).Cast<object>().ToArray();
        for (var i = 0; i < values.Length; i++)
            Texts.Alias(ChoiceItem.OptionKeyOf(labelKey, i), $"{labelKey}.{values[i]}");
        var entry = Config.Bind(Section, key, defaultValue, RussianOf(labelKey));
        items.Add(new ChoiceItem(key, labelKey, entry, values));
        return new Choice<T>(this, entry);
    }

    string LabelKeyOf(string key) => $"oe.{Id}.{key}";
}

// One row of a menu: a switch (ToggleItem) or a choice of options (ChoiceItem); its texts by its label key.
public abstract class MenuItem
{
    public string Key { get; }
    public string LabelKey { get; }
    public string DescriptionKey => LabelKey + ".desc";
    public abstract ConfigEntryBase Entry { get; }

    protected MenuItem(string key, string labelKey)
    {
        Key = key;
        LabelKey = labelKey;
    }
}

public sealed class ToggleItem : MenuItem
{
    public ConfigEntry<bool> Value { get; }
    public override ConfigEntryBase Entry => Value;

    internal ToggleItem(string key, string labelKey, ConfigEntry<bool> value) : base(key, labelKey) => Value = value;
}

// A choice: its options are the enum's values, in order; the dropdown's texts are OptionKey(index).
public sealed class ChoiceItem : MenuItem
{
    public override ConfigEntryBase Entry { get; }
    public object[] Options { get; }

    public string OptionKey(int index) => OptionKeyOf(LabelKey, index);
    internal static string OptionKeyOf(string labelKey, int index) => $"{labelKey}.{index}";

    internal ChoiceItem(string key, string labelKey, ConfigEntryBase entry, object[] options) : base(key, labelKey)
    {
        Entry = entry;
        Options = options;
    }
}
public sealed class Switch
{
    readonly ModMenu menu;
    public ConfigEntry<bool> Entry { get; }

    internal Switch(ModMenu menu, ConfigEntry<bool> entry)
    {
        this.menu = menu;
        Entry = entry;
    }

    // On: switched on and the mod is on. Read it where the feature works, not once in Load().
    public bool On => menu.IsEnabled && Entry.Value;
}

public sealed class Choice<T> where T : struct, Enum
{
    readonly ModMenu menu;
    public ConfigEntry<T> Entry { get; }

    internal Choice(ModMenu menu, ConfigEntry<T> entry)
    {
        this.menu = menu;
        Entry = entry;
    }

    // The chosen option; `whenOff` while the mod is off.
    public T Value(T whenOff) => menu.IsEnabled ? Entry.Value : whenOff;
}

// The Mods window's data (issue #10, #13, #14, #6): the mod manager fills it, mod-settings draws it. Every visible text is
// a localization key (the manager composes per-language texts with Texts.Compose); actions are delegates, null while
// not available (no manager, busy, not allowed). The window redraws on ModMenu.Revision.

// A mod as the window shows it, on the Installed tab (Installed != null) and the Catalog tab (every mod of the
// sources, installed or not).
public sealed record ModEntry(
    string Id,
    string NameKey,                         // the mod's name in the game's language
    string DescriptionKey,                  // its description, or null
    string Author,                          // plain (a name)
    string SourceNameKey,                   // the name of the catalog it comes from, or null (installed by hand)
    IReadOnlyList<string> TagKeys,          // the tags' names
    string Installed,                       // the installed version, or null
    string Available,                       // the newest release for this game version, or null
    bool HasUpdate,                         // Available is newer than Installed
    bool? Enabled,                          // installed: on or off; null: no switch (not installed, or others need it)
    bool RequiredByOthers,                  // installed mods need it: no switch, no remove
    IReadOnlyList<string> RequiresKeys,     // the names of the mods it needs
    IReadOnlyList<ModScreenshot> Screenshots,
    string NoteKey,                         // a note on its state (restart needed, removed, no release...), or null
    bool ByHand = false);                   // loaded by BepInEx itself (plugins, devplugins): its switch works, no update nor remove

// A screenshot of a mod: its file on disk once downloaded (checked by sha256, cached), else null; its caption key or null.
public sealed record ModScreenshot(string Path, string CaptionKey);

// A source of the catalog: its name (the root file's name; the URL when it has none), its URL, how many mods it gave,
// a note on its state (read, failed, from the cache), when it was last checked (local time, or null), how often it is
// checked ("start", "daily", "weekly", "manual") and the actions (null when not allowed: the built-in source cannot be
// removed). Remove(true) removes the mods installed from it too; Remove(false) leaves them, without updates.
public sealed record CatalogSource(string TitleKey, string Url, int Mods, string NoteKey, bool BuiltIn, DateTime? LastChecked,
    string CheckEvery, Action Check, Action<string> SetCheckEvery, Action<bool> Remove,
    string IconPath = null,    // the catalog's icon on disk once downloaded (the root file's icon, checked and cached), or null
    int Installed = 0);        // how many installed mods came from it

public static class ModCatalog
{
    // The window's heading (a localization key); null: no window (no manager).
    public static string TitleKey { get; private set; }
    // The outcome of the last thing done, or the catalog's state (a localization key composed by the manager).
    public static string StatusKey { get; set; }
    // A failure to tell the player once, in the game's message box (a localization key composed by the manager): a
    // source that does not read, an action that failed. ProblemRevision counts them, so the window shows each once.
    public static string ProblemKey { get; private set; }
    public static int ProblemRevision { get; private set; }

    public static void Problem(string key)
    {
        ProblemKey = key;
        ProblemRevision++;
        ModMenu.Changed();
    }
    // The mod manager's version (it sets it on load): the mods' version shown next to the game's in the main menu.
    public static string ManagerVersion { get; set; }
    // The manager itself on the Installed tab: its name, version and a note (its own update staged, failed...), its
    // update when there is one (null otherwise).
    public static ModEntry Manager { get; private set; }

    // Every mod: installed ones (Installed != null) and the catalog's.
    public static IReadOnlyList<ModEntry> Mods { get; private set; } = Array.Empty<ModEntry>();
    public static IReadOnlyList<CatalogSource> Sources { get; private set; } = Array.Empty<CatalogSource>();
    // Something runs in the background (reading, downloading): the window shows it and its buttons wait.
    public static bool Busy { get; set; }

    // What the window can do with a mod (by id; null while not available). Update and SetEnabled take several ids
    // (the Installed tab's selection); the manager runs them one after another.
    public static Action<string> Install { get; set; }
    public static Action<IReadOnlyList<string>> Update { get; set; }
    public static Action<IReadOnlyList<string>> Remove { get; set; }
    public static Action<IReadOnlyList<string>, bool> SetEnabled { get; set; }
    public static Action UpdateAll { get; set; }
    // A mod's screenshots downloaded (when the window shows its details): its entry's Screenshots get their Path.
    public static Action<string> LoadScreenshots { get; set; }

    // Sources: check them all now, add one (the text the player typed in the game's input box: a root file's URL, or
    // "owner" / "owner/repo" for GitHub). The manager reads a source before adding it and says how it went in StatusKey.
    public static Action CheckAll { get; set; }
    public static Action<string> AddSource { get; set; }
    public static Action<string> AddGitHubSource { get; set; }

    public static void Set(string titleKey, ModEntry manager, IReadOnlyList<ModEntry> mods, IReadOnlyList<CatalogSource> sources)
    {
        TitleKey = titleKey;
        Manager = manager;
        Mods = mods ?? Array.Empty<ModEntry>();
        Sources = sources ?? Array.Empty<CatalogSource>();
        ModMenu.Changed();
    }

    public static void Clear() => Set(null, null, null, null);
}
