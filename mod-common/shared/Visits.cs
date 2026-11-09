using System.Collections.Generic;
using System.Linq;
using Hex.Configs;
using Hex.Session.Data;

namespace Nobirds.OldenEra.Modding.Common;

// How often a map building may be visited (its config's visitType) and when its visitors are forgotten (visitorsResetType).
public enum VisitRule { Once, PerHero, PerSide, Unlimited }
public enum VisitReset { None, Week, Day }

// A building's visit rule and whether the selected hero (or their player) has used it: Visited null when the building
// has no limit for this period (nothing to mark).
public sealed record VisitState(VisitRule Rule, VisitReset Reset, bool? Visited);

public static partial class Game
{
    extension(GameMapObject mapObject)
    {
        // The visit rule of a map building (an event bank: ObjEventBank with its ObjEventBankConfig) and whether the hero
        // already used it; null when the object has no visit rule.
        // - Once for all, once per player: the game's own check, the one its tooltip writes "Уже посещено" by
        //   (mapObject.VisitedBy). Once for all is also used up when anyone visited it: the visitors list has an
        //   enemy's side too, the game's check asks only about ours.
        // - Once per hero, and a period's visitors of an unlimited building: the game has no such check (its tooltip says
        //   nothing then), so the building's visitors list is read: the heroes who used it, by their visit id
        //   (hero.VisitId; the list of a visited per-hero building held the hero's id, 04.11.2026).
        internal VisitState VisitFor(DataObject data, GameHero hero)
        {
            var bank = data.TryCast<ObjEventBank>();
            var config = data.Configs.Select(c => c.TryCast<ObjEventBankConfig>()).FirstOrDefault(c => c != null);
            if (bank == null || config == null)
                return null;
            var visitors = bank.visitorsList != null ? Scene.Items(bank.visitorsList).ToList() : new List<int>();
            var heroId = hero?.VisitId;
            var side = hero?.HeroSide;
            bool HeroVisited() => heroId is { } id && visitors.Contains(id);
            bool? PlayerVisited() => mapObject != null && hero?.SideOwner is { } owner ? mapObject.VisitedBy(owner) : null;
            var rule = config.visitType switch
            {
                "OneTime" => VisitRule.Once,
                "EachHeroOneTime" => VisitRule.PerHero,
                "EachSideOneTime" => VisitRule.PerSide,
                _ => VisitRule.Unlimited,
            };
            var reset = config.visitorsResetType switch
            {
                "OnStartWeek" => VisitReset.Week,
                "OnStartDay" => VisitReset.Day,
                _ => VisitReset.None,
            };
            bool? visited = rule switch
            {
                VisitRule.Once => visitors.Count > 0 || PlayerVisited() == true,
                VisitRule.PerHero => HeroVisited(),
                VisitRule.PerSide => PlayerVisited() ?? (side != null && visitors.Contains(side.index)),
                // An unlimited building that resets still keeps this period's visitors.
                _ => reset != VisitReset.None ? HeroVisited() : null,
            };
            return new VisitState(rule, reset, visited);
        }
    }
}
