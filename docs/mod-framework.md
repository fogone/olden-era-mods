# Mod infrastructure — plan (05.11.2026)

Task (fogone): a mod gets a clear interface — events with convenient context and ready-made ways to do typical things (settings, display, texts). Hook as little as possible ourselves and use the toolkit.

## Current state
Each mod itself:
- installs hooks on game code: the Squad tooltip and Visited buildings both hook the game's squad tooltip filling `weh`, both look for the selected hero, both fill the skulls;
- parses hook arguments (`object[] __args`, casts);
- sets up a log, `ReportError`, `LogOnce`, `Game.Log`;
- looks for scene objects, keeps its own caches, decides itself when to search again.

Ready-made pieces already exist, but scattered: the menu (`ModMenu`), texts (`Texts`), map badges (`MapMarkers`), drawn badges (`Badges`), aliases (`Game*`), hooks (`PatchEach`/`PatchOne`), every frame (`OnTick`).

## How it will be
A mod is a class on `ModBase` that subscribes to events and uses services. Example (the Squad tooltip, how it will look):

```csharp
public class Plugin : ModBase
{
    Switch showJoin;

    protected override void OnLoad()
    {
        showJoin = Menu.Toggle("ShowJoin", true);
        Events.SquadTooltip += OnSquadTooltip;       // squad tooltip filled by the game
        Events.SkullsFilled += OnSkulls;             // skulls widget filled by the game
        Events.MapLoaded += map => Prepare(map);     // map loaded: prepare in advance
    }

    void OnSquadTooltip(SquadTooltip tip)            // tip.Tooltip, tip.Squad (MapSquad), tip.Hero (MapHero?), tip.Outcome
    {
        if (tip.Hero is { } hero && showJoin.On && tip.Outcome == Encounter.Joins)
            tip.Line(Texts.Get(JoinKey));            // a line of our own in the tooltip, the game does not overwrite it
    }
}
```

### Events (with context)
Each event is a typed context, no `object` and no `__args`:

| Event | When | Context |
|---|---|---|
| `MapLoading` / `MapLoaded` / `MapClosed` | the game is loading / has loaded / has closed a map | `Map`: store of squads and objects, the player's side, fog |
| `HeroSelected` | another hero is selected in the HUD | `MapHero` |
| `AltInfo` | the extra-info key is pressed / released | `bool` |
| `SquadTooltip` | the game has filled the squad tooltip | `SquadTooltip`: tooltip, squad, hero, meeting outcome (`MeetingOutcome`), lines for our own text |
| `MapObjectTooltip` | the game has filled a map object tooltip | tooltip, object, `DataObject`, texts (`BasicTooltip`) before display — a line can be removed |
| `DwellingTooltip` | unit dwelling tooltip | tooltip, dwelling, guard |
| `SkullsFilled` | the game has filled the skulls widget | widget, squad or army, level |
| `Encounter` | a hero stepped onto a squad (what the game did) | hero, squad, outcome |
| `Forecast`, `CounterDecision` | strike forecast, counterattack decision | attacker, defender, forecast |
| `Tick` | every frame | — |

The hooks for all events are installed by the infrastructure, one per game method, rather than each mod having its own. If a game method was not found, the event simply does not arrive, and the log says it is disabled.

### Services
- **Settings**: `Menu.Toggle` / `Choose` (exist), plus typed reading and a "changed" event.
- **Texts**: `Texts.Get` (exists), keys in `lang/*.json`.
- **Display**:
  - badges over objects (`MapMarkers`, exists);
  - a line in a tooltip — our own, next to the game's lines (like `OE_VisitRow`);
  - a label or circle over the screen (like the frame rate);
  - game pictures by name and drawn badges (`Badges`).
- **Log**: `Log`, `LogOnce`, `Report(where, e)`, `Guard(where, action)` — one rule for all.
- **Scene**: finding the game's UI objects (`Find.Shown<T>()`), the on-screen rectangle `RectTransform`.
- **Lifecycle**: heavy things (finding game members, sprites, templates) on `MapLoading`, not on first display. This will also remove the stutter of the first badge load.

### Types
Wrappers instead of `object`: `MapHero`, `MapSquad`, `MapObject`, `SideOwner`, `MapEntity`. The game hands them out in event contexts, aliases hang on them (`extension(MapHero hero)`), not on `object`.

## Where it lives
Currently two layers:
- `OldenEraCommon.dll` in `plugins` — loaded once, changes only while the game is closed;
- `shared/*.cs` — compiled into every mod and reloaded with it.

For a hook to be installed once for everyone, there must be a single owner. Proposal:
- **description of events and contexts** — in `OldenEraCommon.dll`. It is the interface and changes rarely;
- **source of events** — a new plugin `mod-core` in `devplugins`: installs the hooks, turns them into events, reloads by hot replacement like the other mods;
- **aliases** (`Game*`) — move to `mod-core`. Mods get ready typed contexts and hardly need the aliases themselves.

Cost: after the event description in the DLL changes, the game must be restarted. Changes in `mod-core` and in the mods are installed by hot replacement.

## How to migrate
One event at a time, mod by mod, always in a working state:
1. ~~`mod-core`, log (`ModLog`: `Once`, `Report`, `Guard`), events, wrappers `MapHero` / `MapSquad` / `MapObject`~~ — 05.11, built.
2. ~~Squad tooltip and Visited buildings on events~~ — 05.11, built: their hooks for the tooltip, texts, skulls, danger level, dwelling tooltip, the hero under the cursor and the encounter are in the core. The Squad tooltip still has its own peeking at Diplomacy points; Visited buildings, the outline colors.
3. ~~Preparing in advance — first-load stutter~~ — 05.11: badges are created hidden on `HeroSelected`, checked.
4. ~~Battle counter on events~~ — 05.11, built: `ForecastBuilt`, `OtherForecast`, `ForecastShown`, `CounterDecided`; finding the builders and the decision are aliases in `GameBattle.cs` (via `GameCode.Calls`, our own `E8` parsing removed). Its rework from the review comes next.
5. ~~Map tooltip on events~~ — 05.11, built: `TooltipShown`, `TooltipHidden`, `TooltipLateUpdate`, `TooltipPlace`. Mods (the Mods tab) and Research — as we go (their hooks concern only themselves).
