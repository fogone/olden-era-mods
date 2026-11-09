namespace Nobirds.OldenEra.Modding.Common;

// Visits of map objects (the searches are in search/GameVisits.cs). mapObject.VisitedBy(side wrapper) is the game's own
// check (fql.bnpc): the interop calls the version of the object's own class (il2cpp_object_get_virtual_method).
public static partial class Game
{
    extension(GameHero hero)
    {
        // The hero's id in the visitors lists (Hero.cwfa).
        internal int? VisitId => hero.Logic?.VisitId;
    }
}
