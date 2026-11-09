
namespace Nobirds.OldenEra.Modding.Common;

// The events' wrappers of the game's objects (ModEvents, in the common library, which does not know the generated
// names) as the game's own types: fill.Squad?.Typed is the GameSquad, built.Forecast.Typed the GameForecast.
public static class EventTypes
{
    extension(MapHero hero)
    {
        internal GameHero Typed => hero.Raw?.TryCast<GameHero>();
    }

    extension(MapSquad squad)
    {
        internal GameSquad Typed => squad.Raw?.TryCast<GameSquad>();
    }

    extension(MapObject mapObject)
    {
        internal GameMapObject Typed => mapObject.Raw?.TryCast<GameMapObject>();
    }

    extension(BattleForecast forecast)
    {
        internal GameForecast Typed => forecast.Raw?.TryCast<GameForecast>();
    }
}
