using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Nobirds.OldenEra.Modding.Common;
using Hex.Session.Data;
using Hex.UI;
using Il2CppInterop.Runtime.InteropTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nobirds.OldenEra.Modding.SquadTooltip;

// When the hovered map squad would join the selected hero or offer to flee (the game's encounter decides it:
// Game.MeetingOutcome), the tooltip's battle difficulty block says so: "Хотят присоединиться" with the Diplomacy skill
// icon, or "Хотят сбежать" with the game's escape icon, in place of the skulls. Otherwise the tooltip is untouched.
// Exact numbers (diplomacy points, creatures joining, power ratio) are off by default so the tooltip doesn't reveal
// what the game hides. The game's members are its generated names (GameNames.g.cs, made at build time).
[BepInPlugin("nobirds.oldenera.squadtooltip", "Olden Era Squad Tooltip", "1.31.1")]
public partial class Plugin : ModBase
{
    const string IconName = "OE_DiplomacyIcon";
    // What the tooltip writes, in the game's language (lang/*.json, through the game's localization table: Texts).
    const string JoinKey = "oe.squadtooltip.text.Join", FleeKey = "oe.squadtooltip.text.Flee", PointsKey = "oe.squadtooltip.text.Points",
                 JoinCountKey = "oe.squadtooltip.text.JoinCount", HeroStrongerKey = "oe.squadtooltip.text.HeroStronger", SquadStrongerKey = "oe.squadtooltip.text.SquadStronger";

    Switch showJoin, showFlee, showIcon, showDetails;

    (int hero, int squad)? lastPowers;
    bool lineIsOurs;


    // Skull groups we hid in the last tooltip, restored before the game fills the next one.
    readonly List<GameObject> hiddenSkullGroups = new();

    protected override string[] ObjectPrefixes => new[] { IconName };

    protected override void OnLoad()
    {
        // The "Моды" tab of the game's settings (mod-settings plugin).
        var menu = Menu;
        showJoin = menu.Toggle("ShowJoin", true);
        showFlee = menu.Toggle("ShowFlee", true);
        showIcon = menu.Toggle("ShowIcon", true);
        // The numbers the game hides, all under one switch, each shown where the game has it: the Diplomacy points
        // (when its check counted them), how many would join (when they join), the armies' ratio (unless the danger is hidden).
        showDetails = menu.Toggle("ShowDetails", false);

        var harmony = Harmony;
        // The game's events (mod-core): the tooltip, its skulls, the encounter.
        On(ModEvents.SquadTooltipFilling, fill => Logs.Guard("restore", () =>
        {
            lastPowers = null;   // the skulls fill of this tooltip sets them again; a tooltip without one shows none
            RestoreSkulls(fill.Tooltip.container.skulls);
        }));
        On(ModEvents.SkullsFilled, fill => Logs.Guard("skulls", () => OnSkullsFilled(fill)));
        On(ModEvents.SquadTooltipFilled, fill => Logs.Guard("tooltip", () => OnTooltipFilled(fill)));
        On(ModEvents.HeroSelected, _ => Logs.Guard("prepare", Prepare));
        DiplomacyPoints.Watch(harmony);
        WatchEncounters();
    }

    // ---------- patches ----------

    // The skulls widget of the squad's tooltip, right after the game has filled it for the squad (ModEvents.SkullsFilled:
    // BhSkulls.umn on 0.82.7 shows the skull row or the "danger hidden" row, as the settings say, colours the skulls and
    // writes the difficulty by its key tooltipOpponent_<level>). The widget has no state for "joins" or "flees", so that
    // is put in over what the game has just written: its caption, and the Diplomacy / escape icon in the row's place.
    // The game's next fill shows its own rows again; the tooltip's filling hides our icon.
    void OnSkullsFilled(SkullsFill fill)
    {
        // Only the tooltip's widget for a squad: not a hidden copy the map badges fill (mod-map-badges).
        if (!fill.InSquadTooltip || fill.Squad is not { } squadRef)
            return;
        lastPowers = fill.HeroPower is { } h && fill.SquadPower is { } s ? (h, s) : null;
        var skulls = fill.Skulls;
        if (squadRef.Typed is not { } squad)
            return;
        if (Game.SelectedHero is not { } hero)
            return;
        // The game has just shown its rows itself: only our icon goes.
        hiddenSkullGroups.Clear();
        if (skulls.groupDefault != null && skulls.groupDefault.transform.parent.Find(IconName) is { } icon)
            icon.gameObject.SetActive(false);
        // The game's setting "hide the battle danger": its "danger hidden" row stays, nothing about the outcome is told.
        if (Game.DangerHidden)
            return;
        var encounter = OutcomeNow(hero, squad);
        var joins = encounter is { } e ? e == EncounterOutcome.Joins : hero.HasDiplomacy && hero.WouldJoin(squad).joins;
        if (joins && showJoin.On)
            ShowJoinInSkulls(skulls);
        else if (encounter == EncounterOutcome.Flees && showFlee.On)
            ShowFleeInSkulls(skulls);
    }

    // What the game's encounter would do (Game.MeetingOutcome), once per squad and frame: the skulls and the tooltip line
    // ask for the same squad in one fill. Null when not known.
    (IntPtr squad, int frame, EncounterOutcome? outcome) lastOutcome;

    EncounterOutcome? OutcomeNow(GameHero hero, GameSquad squad)
    {
        var pointer = squad?.Pointer ?? IntPtr.Zero;
        if (pointer != IntPtr.Zero && lastOutcome.squad == pointer && lastOutcome.frame == Time.frameCount)
            return lastOutcome.outcome;
        EncounterOutcome? outcome = null;
        Logs.Guard("meeting outcome", () => outcome = hero?.MeetingOutcome(squad));
        lastOutcome = (pointer, Time.frameCount, outcome);
        return outcome;
    }

    // The squad tooltip filled by the game (ModEvents.SquadTooltipFilled): our line under it.
    void OnTooltipFilled(SquadTooltipFill fill)
    {
        var line = fill.Tooltip.container.behaviorSquad;
        if (line == null)
            return;
        var squad = fill.Squad?.Typed;
        var hero = squad != null ? fill.Hero?.Typed : null;
        // The verdict as the game's encounter decides it (the skulls show it, OnSkullsFilled); the game's Diplomacy check
        // gives its explanation and the points it counted.
        var encounter = OutcomeNow(hero, squad);
        var check = hero != null && hero.HasDiplomacy ? DiplomacyPoints.Check(hero, squad) : ((bool joins, string reason, (int hero, int squad)? points)?)null;
        var joins = encounter is { } e ? e == EncounterOutcome.Joins : check?.joins == true;
        var join = check is var (_, reason, _) ? (joins, reason) : joins ? (true, null) : ((bool, string)?)null;
        if (squad != null)
            JournalTooltip(squad, encounter, check?.joins);
        UpdateLine(line, squad != null ? BuildText(hero, squad, join, check?.points) : null);
    }

    // ---------- tooltip content ----------

    // Ready on the map before the first tooltip (a hero selected, ModEvents.HeroSelected): the first-time searches the
    // tooltip needs — the icons (the Diplomacy one by the loaded sprites' names), the hero's stats, the encounter's members —
    // made now and not on the first hover (05.11: 291 ms of the first squad tooltip were them).
    void Prepare()
    {
        if (Game.SelectedHero is { } hero)
        {
            _ = DiplomacyIcon();
            _ = Game.EscapeIcon;
            _ = hero.HasDiplomacy;
        }
    }

    // Difficulty text -> join text; skull row (or "difficulty hidden" row) -> Diplomacy icon in the same place.
    void ShowJoinInSkulls(BhSkulls skulls) => ShowInSkulls(skulls, Texts.Get(JoinKey), showIcon.On ? DiplomacyIcon() : null);

    // Difficulty text -> flee text; the skulls -> the game's escape icon, the one the squad's map badge shows.
    void ShowFleeInSkulls(BhSkulls skulls) => ShowInSkulls(skulls, Texts.Get(FleeKey), showIcon.On ? Game.EscapeIcon : null);

    void ShowInSkulls(BhSkulls skulls, string text, Sprite icon)
    {
        if (skulls == null)
            return;
        if (text.Length > 0 && skulls.opponent != null)
            skulls.opponent.text = text;

        if (icon == null)
            return;

        var row = skulls.groupDefault != null && skulls.groupDefault.activeSelf ? skulls.groupDefault : skulls.groupHidden;
        if (row == null)
            return;
        var size = RowHeight(skulls.groupDefault);

        var iconObject = row.transform.parent.Find(IconName)?.gameObject ?? CreateIcon(row.transform.parent);
        iconObject.GetComponent<Image>().sprite = icon;
        var rect = iconObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        var element = iconObject.GetComponent<LayoutElement>();
        element.preferredWidth = element.minWidth = size;
        element.preferredHeight = element.minHeight = size;
        iconObject.transform.SetSiblingIndex(row.transform.GetSiblingIndex());
        iconObject.SetActive(true);

        foreach (var group in new[] { skulls.groupDefault, skulls.groupHidden })
        {
            if (group != null && group.activeSelf)
            {
                group.SetActive(false);
                hiddenSkullGroups.Add(group);
            }
        }
    }

    void RestoreSkulls(BhSkulls skulls)
    {
        foreach (var group in hiddenSkullGroups)
            if (group != null)
                group.SetActive(true);
        hiddenSkullGroups.Clear();

        var icon = skulls != null && skulls.groupDefault != null ? skulls.groupDefault.transform.parent.Find(IconName) : null;
        if (icon != null)
            icon.gameObject.SetActive(false);
    }

    static float RowHeight(GameObject skullRow)
    {
        if (skullRow != null)
        {
            for (var i = 0; i < skullRow.transform.childCount; i++)
            {
                var rect = skullRow.transform.GetChild(i).GetComponent<RectTransform>();
                if (rect != null && rect.rect.height > 1f)
                    return rect.rect.height;
            }
        }
        return 32f;
    }

    static GameObject CreateIcon(Transform parent)
    {
        var iconObject = new GameObject(IconName);
        iconObject.AddComponent<RectTransform>();
        iconObject.transform.SetParent(parent, false);
        iconObject.AddComponent<LayoutElement>();
        var image = iconObject.AddComponent<Image>();
        image.preserveAspect = true;
        image.raycastTarget = false;
        return iconObject;
    }

    void UpdateLine(TextMeshProUGUI line, string text)
    {
        if (text == null)
        {
            if (lineIsOurs)
                line.gameObject.SetActive(false);
            lineIsOurs = false;
            return;
        }
        line.text = text;
        line.gameObject.SetActive(true);
        lineIsOurs = true;
    }

    string BuildText(GameHero hero, GameSquad squad, (bool joins, string reason)? join, (int hero, int squad)? points)
    {
        var lines = new List<string>();

        if (join is var (joins, _))
        {
            if (showDetails.On && points is var (heroPoints, squadPoints))
                lines.Add(string.Format(Texts.Get(PointsKey), heroPoints, squadPoints));
            if (joins && showDetails.On && JoinCount(hero, squad) is var (joining, total, share))
                lines.Add(string.Format(Texts.Get(JoinCountKey), joining, total, share));
        }

        if (showDetails.On && !Game.DangerHidden && lastPowers is var (heroPower, squadPower) && heroPower > 0 && squadPower > 0)
        {
            var format = Texts.Get(heroPower >= squadPower ? HeroStrongerKey : SquadStrongerKey);
            lines.Add(string.Format(format, heroPower >= squadPower ? (double)heroPower / squadPower : (double)squadPower / heroPower));
        }

        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    // ---------- sprites ----------

    // The game's Diplomacy skill icon: among the loaded sprites, the shortest name mentioning diplomacy (the base icon,
    // not a level's); none of the game's sprite loaders gives it (05.11: all answered placeholders). The skills window
    // loads it; until then the skulls stay (Game.SpriteMatching looks again at most every ten seconds).
    Sprite DiplomacyIcon()
    {
        var icon = Game.DiplomacyIcon;
        Logs.Once(icon != null ? $"Diplomacy icon: {icon.name}" : "Diplomacy icon not loaded yet (the hero's skills window loads it); the skulls stay till then");
        return icon;
    }


    // ---------- diplomacy ----------

    // Creatures that would join: every stack of the squad times the join share (the game's, hero.JoinShare), rounded down.
    static (int joining, int total, float share)? JoinCount(GameHero hero, GameSquad squad)
    {
        var share = hero.JoinShare(squad);   // the game's (ffd.blor)
        var stacks = SquadStacks(squad);
        if (stacks == null)
            return null;
        int joining = 0, total = 0;
        foreach (var count in stacks)
        {
            total += count;
            joining += (int)Math.Floor(count * share);
        }
        return total > 0 ? (joining, total, share) : null;
    }

    static List<int> SquadStacks(GameSquad squad)
    {
        if (squad.SquadData is { } data && data.units != null && data.units.Count > 0)
        {
            var stacks = new List<int>();
            foreach (var unit in data.units)
                if (unit != null && unit.amount > 0)
                    stacks.Add(unit.amount);
            if (stacks.Count > 0)
                return stacks;
        }
        return SquadUnits(squad)?.Select(u => u.stacks).ToList();
    }

    // The squad's creatures through its hero-like owners' armies, when its DataSquad has none.
    static List<Unit> SquadUnits(GameSquad squad)
    {
        foreach (var owner in squad.SquadOwners)
        {
            if (owner?.HeroArmy is not { } party || party.units == null || party.units.Count == 0)
                continue;
            var units = new List<Unit>();
            foreach (var unit in party.units)
                if (unit != null && unit.stacks > 0)
                    units.Add(unit);
            return units;
        }
        return null;
    }

    // ---------- helpers ----------

}
