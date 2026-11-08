
namespace OldenEra.Modding.Common;

// The events' wrappers of the game's objects (ModEvents, in the common library, which does not know the generated
// names) as the game's own types: fill.Squad?.Typed is the GameSquad, built.Forecast.Typed the GameForecast.
internal static class EventTypes
{
    extension(MapHero hero)
    {
        public GameHero Typed => hero.Raw?.TryCast<GameHero>();
    }

    extension(MapSquad squad)
    {
        public GameSquad Typed => squad.Raw?.TryCast<GameSquad>();
    }

    extension(MapObject mapObject)
    {
        public GameMapObject Typed => mapObject.Raw?.TryCast<GameMapObject>();
    }

    extension(BattleForecast forecast)
    {
        public GameForecast Typed => forecast.Raw?.TryCast<GameForecast>();
    }
}
