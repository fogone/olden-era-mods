using System;
using System.Collections.Generic;
using System.Linq;
using Hex.Session.Data;
using OldenEra.Modding.Common;
using UnityEngine;

namespace OldenEra.Modding.MapBadges;

// Badges over buildings: the amber "available" one over every building with a visit rule that the selected hero can
// still use (with its period icons too, if chosen), the gem over mines not the hero's side's (green over its own), a
// small one over what can be picked up (resources, artifacts, chests), over unit dwellings their unit's card, and
// beside anything guarded the guard's danger skull (or the swords). Objects in the fog of war of the selected hero's
// side (Side.fogData) get none.
//
// The map objects: the map session (feh.me on 0.82.7) holds a store (fqm) with all of them by id; an object's model
// holder (bye) has its scene GameObject. Both through Game (Game.MapObjects, mapObject.ObjectModel).
public partial class Plugin
{
    const string BuildingBadgesName = "OE_MapBadge";
    readonly MapMarkers buildingBadges = new(BuildingBadgesName);
    string lastBuildingStats;

    const float BadgeLift = 0.7f;   // above the quest anchor, in world units
    const float BadgeSize = 0.4f;   // one icon, world units
    const float DwellingSize = 0.8f;
    const float DwellingCornerSize = 0.24f;  // the guard swords on a dwelling's card
    const float CornerSize = 0.24f;          // the danger skull over a building's sign (0.45 of the sign was small, fogone 05.11)
    const float PickupLift = 0.3f;
    const float PickupSize = 0.25f;

    bool BuildingsOn => (showBuildings.On || showPickups.On || showDwellings.On || showOwnMines.On) && Camera.main != null;

    // What a badge over a map object shows: one of our drawn badges, the guard's danger skull for the selected hero, or
    // a dwelling's unit card (its unit, dimmed when nobody is left to hire, how many when shown). The key names the look
    // (MapMarkers remakes a badge only when it changes).
    abstract record Sign(string Key);
    sealed record BadgeSign(Badge Badge) : Sign(Badge.ToString());
    sealed record DangerSign(int Level) : Sign($"danger:{Level}");
    sealed record HireSign(string Sid, bool Dim, int? Count) : Sign($"unit:{Sid}:{(Dim ? "dim" : "")}:{Count}");

    // A pickup still on the map and its icon: resources (gem), an artifact (sword), a chest.
    static Badge? PickupIcon(DataObject data) =>
        data.released ? null : data.TryCast<ObjRes>() != null ? Badge.Res : data.TryCast<ObjItem>() != null ? Badge.Item : data.TryCast<ObjChest>() != null ? Badge.Chest : null;

    static bool IsMine(DataObject data) => data.TryCast<ObjResMine>() != null || data.TryCast<ObjMagicMine>() != null;

    // A unit dwelling: the portrait of its unit, dimmed when nobody is left to hire; how many, with its own switch.
    List<Sign> DwellingIcons(ObjHire hire, Side side)
    {
        var icons = new List<Sign>();
        var sets = hire.assortmentData?.unitSets;
        if (showDwellings.On && sets != null && sets.Count > 0 && sets[0].sids is { Count: > 0 } sids)
        {
            var amount = 0;
            for (var i = 0; i < sets.Count; i++)
                amount += sets[i] != null ? Math.Max(sets[i].currentAmount, 0) : 0;
            // The count shares the corner with the guard swords: while a guard stands there, nobody is hired anyway.
            var guarded = hire.garnisonParty?.units is { Count: > 0 } && hire.ownerSide != side.index;
            icons.Add(new HireSign(sids[0], amount <= 0, showDwellingCount.On && !guarded ? amount : null));
        }
        return icons;
    }

    void ScanBuildings()
    {
        var hero = Game.SelectedHero;
        // Only for a selected hero: whose side is visiting, whose fog hides what, whose guards are not ours.
        if (Game.MapObjects() is not { } mapObjects || hero?.HeroSide is not { } side)
        {
            buildingBadges.Hide();
            return;
        }
        var all = buildingIcons.Value(BuildingIcons.Sign) == BuildingIcons.All;
        var wanted = new Dictionary<IntPtr, Marker>();
        int objects = 0, buildings = 0, dwellings = 0, pickups = 0, noModel = 0, fogged = 0;
        foreach (var mapObject in mapObjects)
        {
            objects++;
            if (mapObject?.ObjectData is not { } data)
                continue;
            var icons = new List<Sign>();
            var pickup = false;
            var dwelling = false;
            if (data.TryCast<ObjHire>() is { } hire)
            {
                icons = DwellingIcons(hire, side);
                dwelling = icons.Count > 0;   // a dwelling's card; with no unit to hire shown, its guard sign is a plain badge
                dwellings++;
            }
            else if (showBuildings.On && mapObject.VisitFor(data, hero) is { Visited: false } visit)
            {
                icons = (all ? BadgeIcons.PeriodOf(visit).Prepend(Badge.Available) : new[] { Badge.Available }).Select(b => (Sign)new BadgeSign(b)).ToList();
                buildings++;
            }
            // A mine is not visited but captured: marked while it is not the selected hero's side's.
            else if (showBuildings.On && IsMine(data) && data.ownerSide != side.index)
            {
                icons = new List<Sign> { new BadgeSign(Badge.Res) };
                buildings++;
            }
            // A mine of ours: the gem in green, captured (with its own switch).
            else if (showOwnMines.On && IsMine(data) && data.ownerSide == side.index)
                icons = new List<Sign> { new BadgeSign(Badge.OwnRes) };
            else if (showPickups.On && PickupIcon(data) is { } kind)
            {
                icons = new List<Sign> { new BadgeSign(kind) };
                pickup = true;
                pickups++;
            }
            // Anything a guard stands at and that is not ours (a dwelling, a bank, a mine): the guard sign beside.
            // As a skull in the colour of the fight's danger for the selected hero, the swords when it is unknown; with
            // the game's setting "hide the battle danger" only the swords: guarded, no danger told.
            var guard = data.garnisonParty?.units;
            if (showBuildings.On && !pickup && !data.released && guard != null && guard.Count > 0 && data.ownerSide != side.index)
                icons.Add(showGuardDanger.On && !Game.DangerHidden && GuardDanger(data.garnisonParty) is { } level ? new DangerSign(level) : new BadgeSign(Badge.Guard));
            if (icons.Count == 0)
                continue;
            if (mapObject.ObjectModel is not { } model)
            {
                noModel++;
                continue;
            }
            // Only what the selected hero's side has scouted: the fog of war covers the rest, nothing is given away.
            if (side.fogData is { } fog && !MapMarkers.Scouted(fog, model.transform.position))
            {
                fogged++;
                continue;
            }
            wanted[model.Pointer] = new Marker(model, icons.Select(IconOf).ToList(), pickup ? PickupSize : dwelling ? DwellingSize : BadgeSize,
                                               pickup ? PickupLift : BadgeLift, dwelling ? DwellingCornerSize : CornerSize, overMesh: pickup);
        }
        var stats = $"Buildings: objects {objects}, unvisited buildings {buildings}, dwellings {dwellings}, pickups {pickups} (no model {noModel}, in the fog {fogged})";
        if (stats != lastBuildingStats)
            Logs.Info(lastBuildingStats = stats);
        buildingBadges.Set(wanted);
    }

    // The danger level of fighting the guard with the selected hero's army, by the game's skulls widget (int M(DataParty),
    // umo on 0.82.7: skulls.ShowDangerOfParty, as the dwelling tooltip shows its guard); null when the widget is not there.
    int? GuardDanger(DataParty guard) =>
        guard != null && Game.HiddenSkulls(SkullsName) is { } skulls ? Logs.Guard("guard danger", () => (int?)skulls.ShowDangerOfParty(guard)) : null;

    // The badge of a sign: our drawing, the guard's danger skull (the game's crossed swords when the danger is not
    // known, our drawing until the game has them loaded), or the game's unit card with the unit's portrait.
    MarkerIcon IconOf(Sign sign)
    {
        switch (sign)
        {
            case DangerSign danger when Game.HiddenSkulls(SkullsName) is { } skulls:
                return MapMarkers.Danger(skulls, danger.Level, corner: true);
            case DangerSign or BadgeSign { Badge: Badge.Guard }:
                return Game.SpriteNamed("Icon_CounterAttack_MouseOver") is { } swords
                    ? new MarkerIcon("guard:game", swords, Color.white, corner: true)
                    : new MarkerIcon(nameof(Badge.Guard), BadgeIcons.Icon(Badge.Guard), Color.white, corner: true);
            case HireSign hire:
                var (back, frame) = UnitCard();
                return new MarkerIcon(hire.Key, Game.UnitPortrait(hire.Sid) ?? BadgeIcons.Icon(Badge.Available), hire.Dim ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white,
                                      back, frame, caption: hire.Count?.ToString());
            case BadgeSign badge:
                return new MarkerIcon(badge.Key, BadgeIcons.Icon(badge.Badge), Color.white);
            default:
                throw new ArgumentOutOfRangeException(nameof(sign), sign, null);
        }
    }

    // The game's small unit card (BhTooltipUnit): its back and frame sprites, by name.
    static (Sprite back, Sprite frame) UnitCard() => (Game.SpriteNamed("Frame_UnitLittle_Back"), Game.SpriteNamed("Frame_UnitLittle_Top"));
}
