using BepInEx;
using OldenEra.Modding.Common;
using UnityEngine;

namespace OldenEra.Modding.MapBadges;

// Badges over the map, for the hero selected in the HUD, in one menu: over buildings (still to visit, mines, pickups,
// unit dwellings, the guard's danger: Buildings.cs) and over squads (would join, would flee, the fight's danger:
// Squads.cs). Only over what the hero's side has scouted. The decisions are the game's, through the shared code
// (Game.VisitFor, Game.MeetingOutcome, the skulls widget); drawn and scheduled by MapMarkers (mod-common).
[BepInPlugin("nobirds.oldenera.mapbadges", "Olden Era Map Badges", "0.1.2")]
public partial class Plugin : ModBase
{
    Choice<MarkersShown> shown;
    Switch showBuildings, showGuardDanger, showPickups, showDwellings, showDwellingCount, showOwnMines, showSquads, showSquadDanger;
    Choice<BuildingIcons> buildingIcons;

    public enum BuildingIcons { Sign, All }

    // A hidden copy of the game's skulls widget: the danger levels and their colours are its (filling a shown one would
    // repaint an open tooltip).
    const string SkullsName = "OE_BadgeSkulls";

    protected override string[] ObjectPrefixes => new[] { BuildingBadgesName, SquadBadgesName, SkullsName };

    protected override void OnLoad()
    {
        Game.Log = Logs.Info;
        // The "Моды" tab of the game's settings (mod-settings plugin).
        var menu = Menu;
        shown = menu.Choose("Shown", MarkersShown.Alt);
        showBuildings = menu.Toggle("ShowBuildings", true);
        buildingIcons = menu.Choose("BuildingIcons", BuildingIcons.Sign);
        showGuardDanger = menu.Toggle("ShowGuardDanger", true);
        showOwnMines = menu.Toggle("ShowOwnMines", true);
        showPickups = menu.Toggle("ShowPickups", true);
        showDwellings = menu.Toggle("ShowDwellings", true);
        showDwellingCount = menu.Toggle("ShowDwellingCount", false);
        showSquads = menu.Toggle("ShowSquads", true);
        showSquadDanger = menu.Toggle("ShowSquadDanger", true);
        // Ready before they are first shown: on a hero selected (MapMarkers.Prepare: made and drawn invisibly).
        On(ModEvents.HeroSelected, _ => Logs.Guard("prepare", Prepare));
        OnTick(UpdateBadges);
    }

    MarkersShown When => shown.Value(MarkersShown.Always);

    void UpdateBadges()
    {
        buildingBadges.Update(BuildingsOn, When, ScanBuildings, Logs);
        squadBadges.Update(SquadsOn, When, ScanSquads, Logs);
    }

    void Prepare()
    {
        if (Game.SelectedHero is { } hero)
        {
            _ = Game.DiplomacyIcon;
            _ = Game.EscapeIcon;
            _ = hero.HasDiplomacy;
        }
        buildingBadges.Prepare(BuildingsOn, When, ScanBuildings, Logs);
        squadBadges.Prepare(SquadsOn, When, ScanSquads, Logs);
    }

    // A game picture without a card, sized by its visible part: the skull, the boot and the Diplomacy icon have
    // different margins in their sprites and would look of different sizes at the same icon size.
    static MarkerIcon Picture(string key, Sprite sprite, Color color) => new(new MarkerIcon(key, sprite, color), 1f / Badges.Extent(sprite));
}
