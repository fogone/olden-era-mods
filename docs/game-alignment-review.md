# Review: where the mods go against the game's code (04.11.2026)

A review of all the mods' code against the rule "use the game's functions, do not rewrite them and do not fight them". An example of the right way is `mod-map-tooltip` 0.7: the game's function that computes the tooltip position (`wdp`) is intercepted and its result is adjusted.

Marked: **exact** — visible in the code; **guess** — an assumption about which game function to look for.

## 1. We patch the game's result after the fact (the main one)

| Mod | What we do | What to look for in the game |
|---|---|---|
| visited | ~~`HideGameVisitedText`: the "Already visited" line was found by its text and hidden~~ — **done in 0.23**: the tooltip text setter `BhTooltip.vzp` is intercepted, the text of the `tooltipVisited` key is replaced with an empty string, as the game does for an unvisited one | — |
| visited | ~~`Describe`: "visited" from `visitorsList` by going through the hero's int properties~~ — **done in 0.24**: "for everyone / for the player" — the game's check `bnpc(side)` (alias `VisitedBy`); "for the hero" has no game check — a list of visitors by hero id (alias `VisitId`) | the match of `VisitId` with the game's id on a second hero is not checked |
| visited | ~~`PaintOutline`: every frame the game's outline palette `foi.cwwo` was rewritten~~ — **done in 0.25**: the passing of the outline colors to the shader `bei.lkv` is intercepted, the color of the "unvisited" tag is changed only for the duration of its call (the old way left our color in the palette after reloads) | — |
| squad | ~~`WouldFlee`: the flee rule by parsing machine code at offsets~~ — **done in 1.18**: joining and fleeing — `MeetingOutcome`, the game's encounter checks `dkw.zki` in its order, read only, the game's members are aliases found among the calls and reads of `zki`; the log cross-checks with what the game did | the game has no separate predicate "what will happen at an encounter": the decision and the actions are in one function |
| squad | ~~`ShowInSkulls` after the whole tooltip is filled~~ — **done in 1.19**: a postfix on the game's widget fill `BhSkulls.umn(squad)`; the rows are turned on by the game itself on the next fill. The widget has no "will join / will flee" mode (only skulls or "difficulty hidden" per the setting, the caption by the key `tooltipOpponent_<level>`), and there are no short texts of our own for this in the localization — the caption and the badge are ours | — |
| squad | ~~`pointsPattern`: Diplomacy numbers by a regex from the English text~~ — **done in 1.20**: the points come from the game's functions `ffd.blou` (squad) and `ffd.blow` (hero) with its `DiplomacyInfoConfig`, found from the code of the check `ffd.blop`; cross-checked with the game's explanation (16887 / 32300). **0.82.10**: the game no longer has functions for the final points (the check `ffd.bloo` works them out inline: the squad's base times its reaction's factor, the hero's base plus stats and level, times the army ratio's and the hero's factors), so the numbers are read again from the check's own explanation, its last line "hero points: {0}, squad points: {1}" (any two whole numbers there, not the English words) | — |
| counter | `SetIntFields` (Plugin.cs:269, 823): for `counter_before_attack` we write the damage and the killed into the game's forecast object at raw offsets. **Exact** | how the game itself accounts for "counter before the strike" (**guess**) |
| counter | the forecast is run up to ~14 times per hover with a temporarily changed `fullStacks` (Plugin.cs:278–347). **Exact** | a parameter or overload of the forecast builder |
| counter | `ApplyCounter`/`Overhang` (Plugin.cs:552–652): we copy the row's elements, measure the text and set the paddings ourselves. **Exact** | the panel method that fills the forecast row — append through it |
| common | ~~`MapMarkers.Face`: every frame we rotate, shift and scale every badge~~ — **analyzed 05.11**: the game's screen markers (`BhQuestMarkersUI` = `CanvasDetectionUI`, template `efq`) are UI, placed every frame at the object's screen point (`cpu.uci`: the map camera, `WorldToViewportPoint`, off screen — none) divided by the canvas scale. But the canvas is drawn with order 1000, over the HUD (119–126) and tooltips (118), — not suitable for badges over objects. The badges stay in the world at `quest_point`; `Face` now recomputes them only when the camera has moved, the badges have changed, or a part of the map was turned on/off (squad 1.21.1, visited 0.25.2) | — |
| common | ~~`MapMarkers.Scouted`: fog of war computed ourselves from the array~~ — **done 05.11**: the game's check `DataFogOfWar.bkin(cell)` (`Game.InFog`); for a squad the cell is its `DataSquad.node`, for buildings no cell of their own was found — the number is computed from the world point by the same numbering (checked against the squads' `node`) | the cell number for buildings |
| settings | ~~the "Mods" tab: every frame we spread the sections apart (`KeepApart`), show our own ourselves~~ — **done in 0.7**: the game itself switches sections (`SettingsSectionsView.ktn`, looks up by type `+0x44` through `ktt`); our section lost its type because the `Start` of the "Sound" copy set Sound on the first show. The type is restored after `Start`, manual switching and `KeepApart` are removed | — |

## 2. Load

- ~~visited: `ScanMarkers`~~ — measured 05.11 (`perf`): the pass takes no more than 1.5 ms; the badges are rotated only when the camera moves.
- ~~squad: `ScanSquads`~~ — measured 05.11: about 5 ms per second; the solution is a read (`MeetingOutcome`), not a run.
- ~~`Game.HiddenSkulls`: `FindObjectsOfTypeAll` on every call while there is no template~~ — 05.11: no more than once per 2 s.
- `Badges.Extent`: reading the atlas pixels (`ReadPixels`/`GetPixel`), once per sprite. **Exact**
- ~~`Texts.Fill` every frame~~ — 05.11: once per 30 frames, immediately only on new texts (common library, installed with the game closed).
- counter: on every hover the FORECAST/ALT log lines are built, going through all the fields. **Exact**
- ~~settings: interception of the section's void methods without checking merged addresses~~ — 0.7.1: `PatchEach`.

## 3. Obfuscated names without aliases

- ~~visited, Outline.cs~~ — rewritten on `bei.lkv` (0.25), obfuscated names only in comments.
- ~~settings~~ — 0.7.1: the localization probe is removed, the rest are aliases in `mod-settings/Aliases.cs` (`GameProperty`/`GameMethod`/`GameAction`).
- counter: the counter decision, the forecast builder, `CountersLeft`, buffs are found by signature right in the mod. In Trace.cs: `bjpa`, `bewy`, `ezi`…
- ~~squad: the flee rule (`FleeRule`), `escapeCandidates`, `"gwa" or "igq"`~~ — removed in 1.18–1.19.1
- ~~The mods' aliases in their own `Aliases.cs`~~ — 05.11: moved to the common `Game` (`GameTooltip.cs`, `GameVisits.cs`, `GameEncounter.cs`, `GameSettingsUI.cs`), typed `GameProperty`/`GameMethod`/`GameAction`. The old parts of `Game.cs` are still on `this` extensions and `Lookup` — to be converted as we go.

## 4. Debugging that runs by default

- ~~settings: `ProbeLocalization()` in `OnLoad`~~ — removed in 0.7.1.
- ~~squad: `Discover()` on every start scans all types for the debug `EscapeProbe`.~~ — removed in 1.19.1
- counter: `AlternativeForecasts`, `Fields`, OUTCOME — a research build (left for now at fogone's request).
- A lot of switched-off debugging in production files: ~~squad ~300 lines~~ (removed in 1.19.1), counter Trace.cs, ~~visited `ProbeOutline`~~ (removed).
