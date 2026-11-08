using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;

namespace OldenEra.Modding.Common;

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
        Mode = common.Bind("Mods", "Mode", ModsMode.SinglePlayer, "Когда моды работают: Always — всегда, SinglePlayer — только в одиночной игре, Off — никогда");
        var values = Enum.GetValues(typeof(ModsMode)).Cast<object>().ToArray();
        for (var i = 0; i < values.Length; i++)
            Texts.Alias(ChoiceItem.OptionKeyOf(ModeKey, i), $"{ModeKey}.{values[i]}");
        ModeItem = new ChoiceItem("Mode", ModeKey, Mode, values);
    }

    // When the mods work at all (fogone 07.11): always, only in a single player game (the default: the mods are not for
    // multiplayer), or never. A mod that does not work is off as by its own switch, which keeps its value. Lives in the
    // library's own config file (BepInEx\config\OldenEra.Mods.cfg), not a plugin's: the mods obey it with or without the
    // Mods tab, which shows it first (ModeItem, texts in the library's lang files).
    public enum ModsMode { Always, SinglePlayer, Off }
    public const string ModeKey = "oe.common.mode", ModeTitleKey = "oe.common.mode.title";
    public static ConfigEntry<ModsMode> Mode { get; }
    public static ChoiceItem ModeItem { get; }

    // Whether the game being played is not a single player one (online, hotseat, a replay): the core answers it from the
    // game's session (Game.SinglePlayer), once per session; nobody answers it without the core (then: single player).
    public static Func<bool> NotSinglePlayer { get; set; }

    // The mods work now, by the mode.
    public static bool ModsWork => Mode.Value switch
    {
        ModsMode.Always => true,
        ModsMode.SinglePlayer => NotSinglePlayer?.Invoke() != true,
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

// The catalog's rows in the Mods window (issue #10, #13: a "Mods" button in the main menu): the mod manager fills them
// (a mod to install, update, switch off or remove, the manager's own update), mod-settings draws them. Each row: its
// name (the row object's), its text (a localization key, composed per language by the manager) and its buttons (each
// its label's key and what it does). The window's own buttons (Check, Update all) are Actions.
public sealed record CatalogButton(string Key, Action Click);

// A row of the mods table: its name (a localization key composed per language), its version, a note on its state
// (a key, or null), its switch (null: none; the value and what flipping it does), one action button (Install / Update)
// and Remove (null: none).
public sealed record CatalogRow(string Name, string TitleKey, string Version, string NoteKey, bool? Enabled, Action<bool> SetEnabled,
    CatalogButton Action, Action Remove);

public static class ModCatalog
{
    public static IReadOnlyList<CatalogRow> Rows { get; private set; } = Array.Empty<CatalogRow>();
    // The window's heading (a localization key); null: no window (no manager).
    public static string TitleKey { get; private set; }
    // The catalog's state under the heading (a localization key composed by the manager).
    public static string StatusKey { get; set; }
    // The window's buttons at the bottom, besides Close (each null when not available now).
    public static CatalogButton Check { get; set; }
    public static CatalogButton UpdateAll { get; set; }
    // The mod manager's version (it sets it on load): the mods' version shown next to the game's in the main menu.
    public static string ManagerVersion { get; set; }

    public static void Set(string titleKey, IReadOnlyList<CatalogRow> rows)
    {
        TitleKey = titleKey;
        Rows = rows ?? Array.Empty<CatalogRow>();
        ModMenu.Changed();
    }

    public static void Clear() => Set(null, null);
}