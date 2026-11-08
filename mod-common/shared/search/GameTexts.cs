using System;
using System.Linq;
using System.Reflection;
using Hex.UI;

namespace OldenEra.Modding.Common;

// The game's texts: its localization service and its "text of a key" (the searches; Game.Translate uses them).
internal static partial class Game
{
    // The localization service (cav on 0.82.7), held statically by the game's texts facade (iv.buvy): the one static
    // property of a class with the language (ELang) and the texts' dictionary, the table Texts puts our texts into.
    static readonly Lookup<PropertyInfo> localization = Lookup<PropertyInfo>.Among("localization service",
        () => Types.SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Static))
            .Where(p => p.PropertyType is { IsClass: true } s && s != typeof(string) &&
                        Properties(s).Any(q => q.PropertyType == typeof(ELang)) &&
                        Properties(s).Any(q => q.PropertyType.IsGenericType && q.PropertyType.Name.StartsWith("Dictionary"))),
        Name, onlyOne: true).Generate("Localization", "GameLocalization");

    // The text of a key in the game's language (cav.qmk on 0.82.7): the service's string M(string) the facade's
    // static string M(string) functions (iv.ihi and its clones, what the game's UI calls) all go to.
    static readonly Lookup<MethodInfo> translate = Lookup<MethodInfo>.Among("text of a key",
        () => localization.Value is { } holder
            ? holder.DeclaringType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.ReturnType == typeof(string) && ParamTypes(m).SequenceEqual(new[] { typeof(string) }))
                .SelectMany(m => GameCode.Calls(m).SelectMany(c => GameCode.At(c.target)))
                .Where(m => !m.IsStatic && m.DeclaringType == holder.PropertyType && m.ReturnType == typeof(string) &&
                            ParamTypes(m).SequenceEqual(new[] { typeof(string) }))
                .DistinctCode()
            : null,
        Members.Name, onlyOne: true).Generate("Translate");

    // A UI text that follows the language (cpz on 0.82.7: its textSid key, its TMP text) is one of the service's
    // listeners (List<is> cav.ccdb): when the language changes, the service calls each one's "language changed", the
    // listener interface's one method (is.ihb), and the text looks its key up again. The text's own version of it (cpz.ihb).
    static readonly Lookup<MethodInfo> languageChanged = Lookup<MethodInfo>.Among("localized text: language changed",
        () => localization.Value is { } holder &&
              Properties(holder.PropertyType).Select(p => p.PropertyType)
                  .FirstOrDefault(t => t.IsGenericType && t.Name.StartsWith("List") && !t.GetGenericArguments()[0].IsValueType)
                  ?.GetGenericArguments()[0] is { } listener &&
              listener.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                  .Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0).ToList() is { Count: 1 } own
            ? Types.Where(t => typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(t) && t.GetProperty("textSid")?.PropertyType == typeof(string))
                .Select(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.ReturnType == typeof(void) && m.GetParameters().Length == 0)
                    .NamedInGame(own[0].NameInGame()).FirstOrDefault())
                .Where(m => m != null)
            : null,
        Members.Name, onlyOne: true).GenerateTarget("LanguageChanged");

    // That UI text's class (cpz on 0.82.7), for setting a text's key (textSid) on the game's own UI.
    static readonly Lookup<Type> localizedText = Lookup<Type>.Among("localized text",
        () => languageChanged.Value is { } m ? new[] { m.DeclaringType } : null, t => t.Name).Generate("GameLocalizedText");
}
