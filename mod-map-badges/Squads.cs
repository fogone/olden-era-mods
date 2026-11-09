using System;
using System.Collections.Generic;
using System.Linq;
using Nobirds.OldenEra.Modding.Common;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.MapBadges;

// Badges over squads, for the hero selected in the HUD: the Diplomacy icon over a squad that would join, the game's
// escape icon over one that would offer to flee (the game's encounter decides: Game.MeetingOutcome, as the squad
// tooltip says it), else a skull in the colour of the fight's danger. No numbers. Only scouted squads.
//
// The squads: the map session (feh.me on 0.82.7) holds a store (fre) with them by id; a squad's view holder (fno)
// has its scene GameObject. Both through Game (Game.Squads, squad.SquadModel), the hero from the HUD (Game.SelectedHero).
public partial class Plugin
{
    const string SquadBadgesName = "OE_SquadBadge";
    readonly MapMarkers squadBadges = new(SquadBadgesName);
    Sprite fleeIcon;

    const float SquadBadgeSize = 0.45f;
    const float SquadBadgeLift = -0.1f;  // from the quest anchor, the badge's centre a little below it

    bool SquadsOn => showSquads.On || showSquadDanger.On;

    void ScanSquads()
    {
        var squads = Game.Squads();
        var hero = Game.SelectedHero;
        if (squads == null || hero == null)
        {
            squadBadges.Hide();
            return;
        }
        var fog = hero.HeroSide?.fogData;
        var hasDiplomacy = hero.HasDiplomacy;
        var dangerHidden = Game.DangerHidden;
        var wanted = new Dictionary<IntPtr, Marker>();
        foreach (var squad in squads)
        {
            if (squad == null || squad.SquadData is { released: true })
                continue;
            if (squad.SquadModel is not { } model || model == null)
                continue;
            // The squad's own cell (DataSquad.node), as the game asks its fog.
            if (fog != null && !(squad.SquadData is { } squadData ? MapMarkers.Scouted(fog, squadData.node) : MapMarkers.Scouted(fog, model.transform.position)))
                continue;
            // With the game's setting "hide the battle danger": the same sign over every squad (with the danger badges on),
            // nothing about the outcome.
            if (dangerHidden)
            {
                if (showSquadDanger.On && EnemyIcon() is { } enemy)
                    wanted[model.Pointer] = new Marker(model, new[] { enemy }, SquadBadgeSize, SquadBadgeLift);
                continue;
            }
            MarkerIcon? icon = null;
            // As the game's encounter decides; without it, the Diplomacy check alone.
            var encounter = showSquads.On ? Logs.Guard("meeting outcome", () => hero.MeetingOutcome(squad)) : null;
            var joins = encounter is { } e ? e == EncounterOutcome.Joins : showSquads.On && hasDiplomacy && hero.WouldJoin(squad).joins;
            if (joins && Game.DiplomacyIcon is { } diplomacy)
                icon = Picture("join", diplomacy, Color.white);
            else if (encounter == EncounterOutcome.Flees)
                icon = Game.EscapeIcon is { } escape ? Picture("flee:game", escape, Color.white) : new MarkerIcon("flee", FleeIcon());
            else if (showSquadDanger.On && DangerSkull(squad) is { } skull)
                icon = Picture(skull.Key, skull.Sprite, skull.Color);
            if (icon != null)
                wanted[model.Pointer] = new Marker(model, new[] { icon.Value }, SquadBadgeSize, SquadBadgeLift);
        }
        squadBadges.Set(wanted);
    }

    // The battle danger the squad tooltip's skulls show, as one skull in the danger's colour. All done by the game's
    // skulls widget (BhSkulls): int M(squad) (umn on 0.82.7, skulls.ShowDangerOfSquad) fills the widget for the squad
    // against the selected hero's army, as the squad's tooltip shows it, and returns the danger level; M(int) -> Color
    // (umq) gives its colour (MapMarkers.Danger).
    readonly HashSet<int> dangerChecks = new();

    MarkerIcon? DangerSkull(GameSquad squad)
    {
        if (Game.HiddenSkulls(SkullsName) is not { } skulls)
            return null;
        var level = skulls.ShowDangerOfSquad(squad);
        // Check, once per level: what the game wrote into the widget for it (the caption, how many skulls lit).
        if (dangerChecks.Add(level))
        {
            var lit = 0;
            for (var i = 0; skulls.skulls != null && i < skulls.skulls.Count; i++)
                lit += skulls.skulls[i] != null && skulls.skulls[i].sprite == skulls.skullActive ? 1 : 0;
            Logs.Info($"Danger skull: level {level} -> '{skulls.opponent?.text}', {lit} skulls lit");
        }
        return MapMarkers.Danger(skulls, level);
    }

    // The sign of a squad when the danger is hidden: the picture of the game's own "danger hidden" row of the skulls
    // widget (BhSkulls.groupHidden), else a plain skull in white.
    MarkerIcon? EnemyIcon()
    {
        if (Game.HiddenSkulls(SkullsName) is not { } skulls)
            return null;
        var hidden = skulls.groupHidden != null ? skulls.groupHidden.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.sprite != null)?.sprite : null;
        Logs.Once($"Danger hidden: squads marked with {(hidden != null ? $"the game's '{hidden.name}'" : "a white skull")}");
        return hidden != null ? Picture("enemy:game", hidden, Color.white) : skulls.skullActive != null ? Picture("enemy", skulls.skullActive, Color.white) : null;
    }

    // Runs away, while the game's escape icon is not loaded: an arrow to the right out of a doorway.
    Sprite FleeIcon() => fleeIcon != null ? fleeIcon : fleeIcon = Badges.Draw(Badges.Amber, (x, y) =>
        Math.Min(Badges.Min(Badges.Segment(x, y, 18, 32, 46, 32), Badges.Segment(x, y, 46, 32, 36, 42), Badges.Segment(x, y, 46, 32, 36, 22)),
                 Badges.Min(Badges.Segment(x, y, 16, 18, 16, 46), Badges.Segment(x, y, 16, 46, 26, 46), Badges.Segment(x, y, 16, 18, 26, 18))));
}
