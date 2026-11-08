using System;
using System.Collections.Generic;
using Hex.Session.Data;
using Hex.UI;
using Hex.UI.Tooltips;
using Il2CppInterop.Runtime.InteropTypes;

namespace OldenEra.Modding.Common;

// The events of the game our mods listen to, each with its context: a mod subscribes (ModBase.On) and gets what
// happened, typed, without patching the game itself. The core plugin (mod-core) patches the game once for all of them
// and raises the events; without it the events do not come (ModEvents.SourceReady says so).
//
//   protected override void OnLoad() => On(ModEvents.SquadTooltipFilled, OnSquadTooltip);
//   void OnSquadTooltip(SquadTooltipFill fill) { if (fill.Squad is { } squad) ... }
//
// This file is in OldenEraCommon.dll, loaded once: a change here needs the game restarted. The core and the mods
// reload as before.

// ---------- the game's objects, typed ----------
// The game's own types of these are obfuscated (fgd, frc, fql, fqu, fgy on 0.82.7) and found at run time; these wrap
// them so that a hero is not taken for a squad. Equal when they wrap the same object of the game.

public readonly record struct MapHero(Il2CppObjectBase Raw)
{
    public IntPtr Pointer => Raw?.Pointer ?? IntPtr.Zero;
    public bool Equals(MapHero other) => Pointer == other.Pointer;
    public override int GetHashCode() => Pointer.GetHashCode();
}

public readonly record struct MapSquad(Il2CppObjectBase Raw)
{
    public IntPtr Pointer => Raw?.Pointer ?? IntPtr.Zero;
    public bool Equals(MapSquad other) => Pointer == other.Pointer;
    public override int GetHashCode() => Pointer.GetHashCode();
}

// A building, a mine, a dwelling, a pickup: the map object's wrapper of the game (fql and its subclasses).
public readonly record struct MapObject(Il2CppObjectBase Raw)
{
    public IntPtr Pointer => Raw?.Pointer ?? IntPtr.Zero;
    public bool Equals(MapObject other) => Pointer == other.Pointer;
    public override int GetHashCode() => Pointer.GetHashCode();
}

// ---------- contexts ----------

// A map session: loaded, or closed.
public sealed class MapSession
{
    public DateTime At { get; init; }
}

// The hero selected in the HUD now; null when none.
public sealed class HeroSelection
{
    public MapHero? Hero { get; init; }
}

// The squad tooltip (BhTooltipSquad), filled by the game for a squad on the map or for a map object (its guard): Kind
// says which. Filling: before the game fills it; Filled: after.
public sealed class SquadTooltipFill
{
    public BhTooltipSquad Tooltip { get; init; }
    public ESquadTooltipType Kind { get; init; }
    public Il2CppObjectBase Target { get; init; }   // what the game fills it for, as the game passes it
    public MapSquad? Squad { get; init; }           // when it is a squad
    public MapObject? MapObject { get; init; }      // when it is a map object
    public MapHero? Hero { get; init; }             // the hero selected in the HUD
}

// The texts a tooltip is about to show (BasicTooltip: header, sub-header, content, extra). A handler may change them:
// the tooltip then shows the changed ones.
public sealed class TooltipTexts
{
    public BhTooltip Tooltip { get; init; }
    public string Header { get; set; }
    public string SubHeader { get; set; }
    public string Content { get; set; }
    public string Extra { get; set; }
}

// The danger skulls widget (BhSkulls), filled by the game for a squad or for an army (a building's guard), with the
// danger level it worked out.
public sealed class SkullsFill
{
    public BhSkulls Skulls { get; init; }
    public MapSquad? Squad { get; init; }
    public DataParty Party { get; init; }
    public int Level { get; init; }
    public int? HeroPower { get; init; }            // the strengths the game compared for the level (BhSkulls.ump)
    public int? SquadPower { get; init; }
    public bool InSquadTooltip { get; init; }       // the widget is the squad tooltip's (not a hidden copy of a mod)
}

// A unit dwelling's tooltip (BhTooltipHire), filled by the game for the dwelling.
public sealed class DwellingTooltipFill
{
    public BhTooltipHire Tooltip { get; init; }
    public MapObject Dwelling { get; init; }
}

// What the game did when a hero stepped onto a squad.
public enum EncounterOutcome { Nothing, Flees, Joins, Battle }

public sealed class EncounterSeen
{
    public MapHero Hero { get; init; }
    public MapSquad Squad { get; init; }
    public EncounterOutcome Outcome { get; set; }   // set when the encounter is over (Encounter); Nothing while it starts
    public bool ByPerson { get; init; }             // a person's hero (not the AI's)
}

// A map object's tooltip shown over its object (BhTooltip.Show(Camera, GameObject, float)).
public sealed class TooltipShown
{
    public BhTooltip Tooltip { get; init; }
    public UnityEngine.Camera Camera { get; init; }
    public UnityEngine.GameObject Target { get; init; }
}

// The place the tooltip's positioner gives for the object's point on its canvas (the positioner's wdp on 0.82.7): a
// handler may move it.
public sealed class TooltipPlace
{
    public Il2CppObjectBase Positioner { get; init; }
    public UnityEngine.Vector2 Point { get; init; }
    public UnityEngine.Vector2 Place { get; set; }
}

// A battle forecast object of the game (exv on 0.82.7: the damage and kills it shows on the panel).
public readonly record struct BattleForecast(Il2CppObjectBase Raw)
{
    public IntPtr Pointer => Raw?.Pointer ?? IntPtr.Zero;
    public bool Equals(BattleForecast other) => Pointer == other.Pointer;
    public override int GetHashCode() => Pointer.GetHashCode();
}

// The forecast of an attack, made by one of the game's builders (static (DamageUiParams, bool) on 0.82.7).
public sealed class ForecastBuilt
{
    public System.Reflection.MethodInfo Builder { get; init; }
    public Hex.Session.Battle.DamageUiParams Params { get; init; }
    public bool Flag { get; init; }
    public BattleForecast Forecast { get; init; }
}

// The forecast panel shown with a forecast (BhHealDamageForecastPanel.Show).
public sealed class ForecastShown
{
    public Hex.Session.Battle.BhHealDamageForecastPanel Panel { get; init; }
    public BattleForecast? Forecast { get; init; }
}

// The game's decision whether the defender answers the attack (bool (attacker, defender, ability, hex, list), eqe.bhqj).
public sealed class CounterDecided
{
    // The battle's logic that decided (GameBattleLogic, eqe) and the attack's ability (GameAbility, eji): the rest of the
    // attack's sequence is asked of the same logic (a second strike after the counter).
    public Il2CppObjectBase Battle { get; init; }
    public Il2CppObjectBase Ability { get; init; }
    public Il2CppObjectBase Attacker { get; init; }
    public Il2CppObjectBase Defender { get; init; }
    public bool Counters { get; init; }
}

// ---------- events ----------

// One event: its handlers by owner (a mod), raised by the core. A handler's error is said once in the log and does not
// stop the others.
public sealed class GameEvent<T> : IGameEvent
{
    readonly List<(object owner, Action<T> handler)> handlers = new();
    readonly HashSet<string> reported = new();

    // `remembers`: a state (the hero selected, the map loaded): a handler added later gets the last one at once, so a
    // mod loaded (or reloaded) after it happened starts from it.
    public GameEvent(string name, bool remembers = false)
    {
        Name = name;
        this.remembers = remembers;
        ModEvents.All.Add(this);
    }

    readonly bool remembers;
    bool hasLast;
    T last;

    public string Name { get; }
    public bool Listened => handlers.Count > 0;

    public void Add(object owner, Action<T> handler)
    {
        handlers.Add((owner, handler));
        if (remembers && hasLast)
            Call(owner, handler, last);
    }

    // The state is over (the map closed): a handler added later gets nothing.
    public void Forget() => (hasLast, last) = (false, default);

    // The owner's handlers go, and what was said about them: a reloaded build (the same names) reports its own errors.
    void IGameEvent.RemoveOwner(object owner)
    {
        handlers.RemoveAll(h => h.owner == owner);
        reported.RemoveWhere(key => key.StartsWith(owner.GetType().FullName + ".", StringComparison.Ordinal));
    }

    // Raised by the core.
    public void Raise(T context)
    {
        if (remembers)
            (hasLast, last) = (true, context);
        foreach (var (owner, handler) in handlers.ToArray())
            Call(owner, handler, context);
    }

    // A handler's error goes to its mod's own log, once per handler (ModLog.Report); a switched off mod gets no events.
    void Call(object owner, Action<T> handler, T context)
    {
        if (owner is ModBase { Menu: { IsEnabled: false } })
            return;
        try { handler(context); }
        catch (Exception e)
        {
            if (owner is ModBase mod)
                mod.Logs.Report($"{Name} ({handler.Method.Name})", e);
            else if (reported.Add($"{owner.GetType().FullName}.{handler.Method.Name}"))
                ModEvents.Log?.Invoke($"{Name}: {owner.GetType().FullName}.{handler.Method.Name}: {e}");
        }
    }
}

internal interface IGameEvent
{
    void RemoveOwner(object owner);
}

public static class ModEvents
{
    internal static readonly List<IGameEvent> All = new();

    // Said by the core: which events it raises (a game method not found: that event does not come).
    public static Action<string> Log;
    public static bool SourceReady { get; set; }

    // The map session.
    public static readonly GameEvent<MapSession> MapLoaded = new("map loaded", remembers: true);
    public static readonly GameEvent<MapSession> MapClosed = new("map closed");

    // The HUD's hero (checked every frame; raised when it changes).
    public static readonly GameEvent<HeroSelection> HeroSelected = new("hero selected", remembers: true);

    // The game switched its language (its UI texts are being told, cpz.ihb on 0.82.7): the new language. Our texts are in the new table by then (Texts.Refresh); the game's texts showing our keys
    // look them up again themselves, a mod sets again only the texts it wrote without a key.
    public static readonly GameEvent<Hex.UI.ELang> LanguageChanged = new("language changed");

    // The game's "alt info" mode (the key held): on, off.
    public static readonly GameEvent<bool> AltInfo = new("alt info");

    // The squad tooltip (BhTooltipSquad.weh on 0.82.7).
    public static readonly GameEvent<SquadTooltipFill> SquadTooltipFilling = new("squad tooltip filling");
    public static readonly GameEvent<SquadTooltipFill> SquadTooltipFilled = new("squad tooltip filled");

    // A tooltip's texts before it shows them (BhTooltip.vzp).
    public static readonly GameEvent<TooltipTexts> TooltipTexts = new("tooltip texts");

    // The skulls widget filled (BhSkulls.umn for a squad, umo for an army).
    public static readonly GameEvent<SkullsFill> SkullsFilled = new("skulls filled");

    // The dwelling tooltip (BhTooltipHire.wae).
    public static readonly GameEvent<DwellingTooltipFill> DwellingTooltipFilling = new("dwelling tooltip filling");
    public static readonly GameEvent<DwellingTooltipFill> DwellingTooltipFilled = new("dwelling tooltip filled");

    // A hero steps onto a squad (the game's encounter, dkw.zki): starting (the squad still on the map), and over, with
    // what the game did (the same context).
    public static readonly GameEvent<EncounterSeen> EncounterStarting = new("encounter starting");
    public static readonly GameEvent<EncounterSeen> Encounter = new("encounter");

    // A map object's tooltip: shown over the object, hidden, every frame while shown (LateUpdate), and its place.
    public static readonly GameEvent<TooltipShown> TooltipShown = new("tooltip shown");
    public static readonly GameEvent<BhTooltip> TooltipHidden = new("tooltip hidden");
    public static readonly GameEvent<BhTooltip> TooltipLateUpdate = new("tooltip late update");
    public static readonly GameEvent<TooltipPlace> TooltipPlace = new("tooltip place");

    // The battle's forecasts: of an attack (a builder), of anything else (spells, heals: Forecast only), shown on the
    // panel; and the game's decision whether the defender answers.
    public static readonly GameEvent<ForecastBuilt> ForecastBuilt = new("forecast built");
    public static readonly GameEvent<BattleForecast> OtherForecast = new("other forecast");
    public static readonly GameEvent<ForecastShown> ForecastShown = new("forecast shown");
    public static readonly GameEvent<CounterDecided> CounterDecided = new("counter decided");

    // A mod's handlers out of every event (ModBase on unload).
    public static void RemoveOwner(object owner)
    {
        foreach (var e in All)
            e.RemoveOwner(owner);
    }
}
