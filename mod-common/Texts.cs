using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BepInEx.Logging;
using Hex.UI;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Logger = BepInEx.Logging.Logger;

namespace OldenEra.Modding.Common;

// Our texts in the game's own localization table, so the game's UI shows them by key (a row's label, its hover
// description, a dropdown's options) and switches them with the game's language.
//
// A plugin keeps its texts as the game does (Lang/<language>/texts/*.json): lang/<language>.json files in the game's
// format {"tokens":[{"sid":"...","text":"..."}]}, <language> being a Hex.UI.ELang name (russian, english, ...),
// embedded in its DLL (csproj: <EmbeddedResource Include="lang\*.json" />). ModBase loads them; a key missing in the
// game's language is taken from English, then Russian. Our keys start with "oe.".
//
// The table is the game's localization service: a type with the current language (ELang) and a dictionary
// key -> entry (key, text, args), found by that shape, not by its obfuscated names (iv.buvy: cav, cax on 0.82.7).
// The keys are put in again when the language (and with it the table) changes.
public static class Texts
{
    const ELang Fallback = ELang.english, Second = ELang.russian;
    // language -> key -> text
    static readonly Dictionary<ELang, Dictionary<string, string>> texts = new();
    static ManualLogSource log;
    static bool searched, started, dirty;
    static PropertyInfo serviceProperty, languageProperty, tableProperty;
    static ConstructorInfo entryConstructor;
    static PropertyInfo entryKey, entryText, entryArgs;
    static IntPtr filledTable;
    static ELang? filledLanguage;

    // The lang/<language>.json resources of an assembly (a plugin's own texts).
    public static void Load(Assembly assembly)
    {
        log ??= Logger.CreateLogSource("Olden Era Texts");
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
        {
            // "<root namespace>.lang.<language>.json"
            var parts = name.Split('.');
            if (parts.Length < 3 || !parts[^3].Equals("lang", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Enum.TryParse<ELang>(parts[^2], true, out var language))
            {
                log.LogWarning($"{assembly.GetName().Name} {name}: no such language of the game");
                continue;
            }
            try
            {
                using var stream = assembly.GetManifestResourceStream(name);
                using var json = JsonDocument.Parse(stream, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                var count = 0;
                foreach (var token in json.RootElement.GetProperty("tokens").EnumerateArray())
                {
                    Set(language, token.GetProperty("sid").GetString(), token.GetProperty("text").GetString());
                    count++;
                }
                log.LogInfo($"{assembly.GetName().Name}: {count} texts in {language}");
            }
            catch (Exception e)
            {
                log.LogError($"{assembly.GetName().Name} {name}: {e.Message}");
            }
        }
        Changed();
    }

    // The library's own texts (its lang/<language>.json: "Mod enabled", the wrong-version notice), loaded once.
    static bool ownLoaded;

    public static void LoadOwn()
    {
        if (ownLoaded)
            return;
        ownLoaded = true;
        Load(typeof(Texts).Assembly);
    }

    // A text made for every language there are texts in, from that language's texts (a mod's heading: its title in
    // that language and its version).
    public static string Compose(string key, Func<ELang, string> make)
    {
        foreach (var language in texts.Keys.ToList())
            Set(language, key, make(language));
        Changed();
        return key;
    }

    // One text in code, for the languages we write (English also stands for the languages we do not).
    public static string Add(string key, string russian, string english)
    {
        Set(Second, key, russian);
        Set(Fallback, key, english ?? russian);
        Changed();
        return key;
    }

    // Another key with the texts of `sameAs`, in every language that has them (a dropdown's option looked up by number,
    // written by name: ModMenu.Choose).
    public static void Alias(string key, string sameAs)
    {
        foreach (var (language, table) in texts.ToList())
            if (table.TryGetValue(sameAs, out var text))
                Set(language, key, text);
        Changed();
    }

    static void Set(ELang language, string key, string text)
    {
        if (string.IsNullOrEmpty(key))
            return;
        if (!texts.TryGetValue(language, out var table))
            texts[language] = table = new Dictionary<string, string>();
        table[key] = text ?? "";
    }

    static void Changed()
    {
        dirty = true;
        if (!started)
        {
            started = true;
            Ticker.Add(typeof(Texts), Fill);
        }
    }

    public static bool Has(string key) => texts.Values.Any(t => t.ContainsKey(key));

    // Our text in the game's language now (for places the game does not look up by key); the key when unknown.
    public static string Get(string key) => Lookup(CurrentLanguage(), key) ?? key;

    // Our text in a given language (with the same fallbacks); the key when unknown.
    public static string GetIn(ELang language, string key) => Lookup(language, key) ?? key;

    // (The game's own text of its key: Game.Translate in a plugin's shared code, the game's function.)

    // The game is switching its language (mod-core, when it starts telling its texts): our texts into its new table now,
    // before its texts look their keys up again. Returns the new language when it is another one than our texts
    // were in, else null. Between switches the table is looked at every 30 frames (the first table, a table loaded some
    // other way).
    public static ELang? Refresh()
    {
        var before = filledLanguage;
        nextFillCheck = 0;
        Fill();
        return before != null && filledLanguage != before ? filledLanguage : null;
    }

    static string Lookup(ELang? language, string key) =>
        new[] { language, Fallback, Second }.Where(l => l != null).Select(l => texts.TryGetValue(l.Value, out var t) && t.TryGetValue(key, out var s) ? s : null)
            .FirstOrDefault(s => s != null);

    // Null until the game's localization service is there.
    // The game's language now (English while the game's localization is not there yet).
    public static ELang Language => CurrentLanguage() ?? Fallback;

    static ELang? CurrentLanguage() => Find() && Service() is { } service && languageProperty.GetValue(service) is ELang language ? language : null;

    static object Service() => serviceProperty.GetValue(null);

    // When our texts changed, or (looked at every 30 frames) the table is not the one filled (another language loaded):
    // fill it.
    static int nextFillCheck;

    static void Fill()
    {
        if (!dirty && UnityEngine.Time.frameCount < nextFillCheck)
            return;
        nextFillCheck = UnityEngine.Time.frameCount + 30;
        if (!Find())
            return;
        var service = Service();
        if (service == null || tableProperty.GetValue(service) is not Il2CppObjectBase table)
            return;
        var language = languageProperty.GetValue(service) is ELang now ? now : (ELang?)null;
        if (!dirty && table.Pointer == filledTable && language == filledLanguage)
            return;
        var setItem = table.GetType().GetProperty("Item");
        var keys = texts.Values.SelectMany(t => t.Keys).Distinct().ToList();
        foreach (var key in keys)
        {
            var entry = entryConstructor.Invoke(Array.Empty<object>());
            entryKey.SetValue(entry, key);
            entryText.SetValue(entry, Lookup(language, key));
            entryArgs?.SetValue(entry, new Il2CppStringArray(0));
            setItem.SetValue(table, entry, new object[] { key });
        }
        filledTable = table.Pointer;
        filledLanguage = language;
        dirty = false;
        log.LogInfo($"{keys.Count} texts put into the game's localization ({language})");
    }

    static bool Find()
    {
        if (searched)
            return serviceProperty != null;
        searched = true;
        log ??= Logger.CreateLogSource("Olden Era Texts");
        try
        {
            var game = typeof(Hex.UI.ELang).Assembly;
            Type[] types;
            try { types = game.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            const BindingFlags instance = BindingFlags.Public | BindingFlags.Instance;
            // The entry: exactly two string properties and a string array.
            bool IsEntry(Type t) => t.GetConstructor(Type.EmptyTypes) != null &&
                                    t.GetProperties(instance).Count(p => p.PropertyType == typeof(string) && !p.Name.StartsWith("_")) == 2 &&
                                    t.GetProperties(instance).Any(p => p.PropertyType == typeof(Il2CppStringArray));
            foreach (var type in types)
            {
                var language = type.GetProperties(instance).FirstOrDefault(p => p.PropertyType == typeof(Hex.UI.ELang) && !p.Name.StartsWith("_"));
                if (language == null)
                    continue;
                var table = type.GetProperties(instance).FirstOrDefault(p => p.PropertyType.IsGenericType &&
                    p.PropertyType.GetGenericTypeDefinition() == typeof(Il2CppSystem.Collections.Generic.Dictionary<,>) &&
                    p.PropertyType.GetGenericArguments()[0] == typeof(string) && IsEntry(p.PropertyType.GetGenericArguments()[1]));
                if (table == null)
                    continue;
                var holder = types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static)).FirstOrDefault(p => p.PropertyType == type);
                if (holder == null)
                    continue;
                var entryType = table.PropertyType.GetGenericArguments()[1];
                var strings = entryType.GetProperties(instance).Where(p => p.PropertyType == typeof(string) && !p.Name.StartsWith("_")).ToList();
                serviceProperty = holder;
                languageProperty = language;
                tableProperty = table;
                entryConstructor = entryType.GetConstructor(Type.EmptyTypes);
                // In declaration order: the key, then the text (seen in the table on 0.82.7: ccdi = key, ccdj = text).
                entryKey = strings[0];
                entryText = strings[1];
                entryArgs = entryType.GetProperties(instance).FirstOrDefault(p => p.PropertyType == typeof(Il2CppStringArray));
                log.LogInfo($"Localization: {holder.DeclaringType?.Name}.{holder.Name} ({type.Name}): {language.Name}, {table.Name} of {entryType.Name}({entryKey.Name}, {entryText.Name})");
                return true;
            }
            log.LogWarning("The game's localization table not found: our texts show as keys");
        }
        catch (Exception e)
        {
            log.LogError($"localization: {e}");
        }
        return false;
    }
}
