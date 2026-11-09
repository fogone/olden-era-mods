using Hex.UI;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.Manager;

// The catalog's tags (ModMeta.Tags): the name of a known tag in the game's language (oe.manager.tag.<id>), the id for an unknown one.
public static class Tags
{
    public static string TagName(string id) => Texts.Has(KeyOf(id)) ? Texts.Get(KeyOf(id)) : id;

    // The same in a given language (the window's tag keys are composed for every language).
    public static string TagNameIn(ELang language, string id) => Texts.Has(KeyOf(id)) ? Texts.GetIn(language, KeyOf(id)) : id;

    static string KeyOf(string id) => $"oe.manager.tag.{id}";
}
