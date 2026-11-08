using OldenEra.Modding.Common;

namespace OldenEra.Modding.Manager;

// The catalog's tags (ModMeta.Tags): the name of a known tag in the game's language (oe.manager.tag.<id>), the id for an unknown one.
public static class Tags
{
    public static string TagName(string id)
    {
        var key = $"oe.manager.tag.{id}";
        return Texts.Has(key) ? Texts.Get(key) : id;
    }
}
