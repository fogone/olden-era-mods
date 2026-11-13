using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Nobirds.OldenEra.Modding.Common;

// The encounter of a hero and a map squad (dkw.zki on 0.82.7): what it would do (MeetingOutcome), asked with the same
// questions the game's encounter asks, through the generated names (the searches are in search/GameEncounter.cs); and
// the Diplomacy points its check counts on the way (DiplomacyPoints).
public static partial class Game
{
    extension(GameHero hero)
    {
        // The hero's army's strength, times the hero's factor (fgd.blwd(true)).
        internal int HeroStrength => hero.ArmyStrength(true);
        // The game's Diplomacy check (ffd.bloq).
        internal bool DiplomacyJoins(GameSquad squad) => GameNames.JoinsByDiplomacy(hero, squad);
        // The hero's stats carry enableDiplomacy.
        internal bool HasDiplomacy => hero.HeroStats.Any(stat => stat.enableDiplomacy);

        // What the hero's stepping onto the squad would do; null when not known (no hero, squad or side). The game
        // decides it in its encounter, static void (hero, squad) (dkw.zki on 0.82.7), and acts at once: it locks the
        // pair, opens a window or starts the battle. So it is not called: this asks the same questions in the same
        // order, with the same members of the game (each found among the encounter's own calls and reads), and only reads.
        //
        // dkw.zki on 0.82.7, its code by offset (hero = fgd, squad = frc; `research.txt`: code dkw.zki):
        //   +0066..+007F  hero, squad, hero's entity (fgd.cqer) null -> nothing
        //   +0085  hero entity.bmdt(squad entity: frc.crom)     the meeting check; false -> return, nothing happens
        //   +009E  squad.mh()  / hero.blwd(true)                 strengths (ints, as floats)
        //   +00C3  weak = 0.5 >= squad / hero                    the constant from the code (GameNames.WeakShare)
        //   +00F9  joins = ffd.bloq(hero, squad)                 the Diplomacy check (ffd.blop without the text)
        //   +010F  ai = side.crmt (fqu+0xB8)                     the side's AI, null for a person
        //   +0118  person = side.bnyp()                          the windows open only for a person
        //   +0147..+0177  lh.bvdw && person -> +02F4: battle (dkw.zkj)
        //   +01A1  avoid = squad data.isEscape (+0x47)           without it the squad never joins nor flees
        //   +01CC  noFlee = side.crmh.cqyh (fqu+0x58, fmr+0x37)
        //          join  = avoid && ai == null && joins
        //          flee  = avoid && ai == null && weak && !noFlee
        //   +01E0  join -> +0272: isCampaignFreeDiplomacy (+0x49) ? dkw.zkh (joins at once)
        //                         : lock Diplomacy (fhu.bmer 0x80), person -> hire window (efh.bcve)
        //   +01F4  flee -> +0209: lock WeakSquadWarning (0x20), person -> "weak squad" window (efh.bcup)
        //   else   +02F4: battle (dkw.zkj)
        //
        // The members it calls before acting only read, as far as their code shows (05.11.2026, `code` in the research
        // console): no writes to objects, only class initialisation; bloq/blop build the explanation string and return.
        internal EncounterOutcome? MeetingOutcome(GameSquad squad)
        {
            if (hero == null || squad == null)
                return null;
            // +0085: the meeting check; false -> nothing happens.
            if (hero.Entity is not { } heroEntity || squad.Entity is not { } squadEntity || !heroEntity.Meets(squadEntity))
                return EncounterOutcome.Nothing;
            if (hero.SideOwner is not { } side)
                return null;
            // +009E..+00C3: weak when the squad's strength is at most the share of the hero's.
            var weak = GameNames.WeakShare >= (float)squad.SquadStrength() / hero.HeroStrength;
            // +00F9: the Diplomacy check.
            var joins = hero.DiplomacyJoins(squad);
            // +010F, +0118: an AI's hero never gets the offers; a person's hero.
            var byPerson = side.IsPerson();
            var noAi = !side.HasAi;
            // +0147: the game-wide flag, a person's hero always fights.
            if (GameNames.AlwaysBattle && byPerson)
                return EncounterOutcome.Battle;
            // +01A1, +01CC: the squad may avoid the battle at all; the side's "no flee offers".
            var avoid = squad.MayAvoidBattle;
            if (avoid && noAi && joins)
                return EncounterOutcome.Joins;   // +0272: the hire window, or at once when isCampaignFreeDiplomacy
            if (avoid && noAi && weak && !side.NoFleeOffers)
                return EncounterOutcome.Flees;   // +0209: the "weak squad" window
            return EncounterOutcome.Battle;      // +02F4
        }
    }

    extension(GameSquad squad)
    {
        // DataSquad.isEscape: the squad may avoid the battle, by joining or fleeing.
        internal bool MayAvoidBattle => squad.SquadData is { } d && d.isEscape;
    }

    extension(GameSide side)
    {
        internal bool HasAi => side.Ai != null;
        // Set in the side's state: no squad offers to flee.
        internal bool NoFleeOffers => side.State is { } state && state.NoFleeOffers;
    }
}

// The Diplomacy points of a hero and a squad, as the game's explained Diplomacy check (ffd.bloo on 0.82.10) counts and
// writes them: its explanation ends with the line "hero points: {0}, squad points: {1}" (the hidden string a.yxa) when
// it counted them. Since 0.82.10 the check works the final points out inline (the squad's base times its reaction's
// factor, the hero's base plus the hero's stats and level, times the army ratio's and the hero's factors), so they are
// read from that line, the game's own numbers. Where the check answers without counting (a squad that always or never
// joins, no Diplomacy), none.
//   var (joins, reason, points) = DiplomacyPoints.Check(hero, squad);
public static class DiplomacyPoints
{
    // The two whole numbers of the explanation's last line.
    static readonly Regex pointsLine = new Regex(@"(-?\d+)\D+(-?\d+)\s*$");

    // The game's explained check (GameHero.WouldJoin) and the points it counted, if it did.
    internal static (bool joins, string reason, (int hero, int squad)? points) Check(GameHero hero, GameSquad squad)
    {
        var (joins, reason) = hero.WouldJoin(squad);
        var last = reason?.TrimEnd().Split('\n').LastOrDefault();
        var match = last != null ? pointsLine.Match(last) : null;
        return (joins, reason, match is { Success: true }
            ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
            : null);
    }
}
