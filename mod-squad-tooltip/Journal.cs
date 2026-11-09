using System.Collections.Generic;
using System.Linq;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.SquadTooltip;

// The decision journal: one line per squad in BepInEx/LogOutput.log, always on, nothing on screen.
//   SQUAD     <type>#<id> <map|guard?> escape=<DataSquad.isEscape> tooltip=<what the tooltip says> dipl=<the Diplomacy check alone>
//             when the hovered squad's verdict changes;
//   ENCOUNTER <type>#<id> <map|guard?> escape=... hero=<person|ai> tooltip=<the last verdict shown for it> game=<what the game did>
//             when a hero really steps onto a squad (ModEvents.EncounterStarting / Encounter, mod-core), and
//             "MISMATCH" at the end when the two differ.
// "map" is a squad of the map's squad store (Game.Squads), "guard?" one not in it (a building's guard, as far as known).
public partial class Plugin
{
    // The verdict the tooltip last showed per squad: null when it could not tell.
    readonly Dictionary<System.IntPtr, EncounterOutcome?> shownVerdicts = new();
    readonly Dictionary<System.IntPtr, string> journalLines = new();

    // Described while the encounter starts: after a battle the squad is off the map already.
    readonly Dictionary<EncounterSeen, string> encounterSquads = new();

    void WatchEncounters()
    {
        On(ModEvents.EncounterStarting, seen => Logs.Guard("journal", () =>
            encounterSquads[seen] = $"{DescribeSquad(seen.Squad.Typed)} hero={(seen.ByPerson ? "person" : "ai")}"));
        On(ModEvents.Encounter, seen => Logs.Guard("journal", () =>
        {
            var squad = encounterSquads.TryGetValue(seen, out var described) ? described : "?";
            encounterSquads.Remove(seen);
            var hovered = shownVerdicts.TryGetValue(seen.Squad.Pointer, out var shown);
            // A mismatch only against a verdict the tooltip had (not one it could not tell).
            var mismatch = shown is { } verdict && verdict != seen.Outcome ? " MISMATCH" : "";
            Log.LogInfo($"ENCOUNTER {squad} tooltip={(hovered ? Verdict(shown) : "not hovered")} game={seen.Outcome}{mismatch}");
        }));
    }

    void JournalTooltip(GameSquad squad, EncounterOutcome? encounter, bool? diplomacyJoins)
    {
        if (squad is not { } o)
            return;
        shownVerdicts[o.Pointer] = encounter;
        var line = $"SQUAD {DescribeSquad(squad)} tooltip={Verdict(encounter)} dipl={diplomacyJoins?.ToString() ?? "-"}";
        if (journalLines.TryGetValue(o.Pointer, out var last) && last == line)
            return;
        journalLines[o.Pointer] = line;
        Log.LogInfo(line);
    }

    static string Verdict(EncounterOutcome? outcome) => outcome?.ToString() ?? "unknown";

    static string DescribeSquad(GameSquad squad)
    {
        var data = squad?.SquadData;
        if (data == null)
            return "?";
        var pointer = squad.Pointer;
        var onMap = Game.Squads()?.Any(s => s?.Pointer == pointer) == true;
        return $"{data.configSid}#{data.id} {(onMap ? "map" : "guard?")} escape={data.isEscape}";
    }
}
