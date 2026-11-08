# Code review, round 2 (05.11.2026)

fogone: the usual "is the code good" review plus these points: elegance (1), reuse (2), fighting the game (3), consistency (4), typing (5). Five reviewers by area: the common library and the core; the common code (`shared/`, the searches) and the name generator; Squad tooltip and Map tooltip; Visited buildings and Mods; Battle counter. About 150 findings, grouped by theme below. The most serious ones were re-checked against the code. The full lists are in the reviewers' reports (in the session notes); what is listed here is what we take on, in order.

## The main point for each area
- **Battle counter is a research bench with a feature inside.** About two thirds of the code is debugging of the search for the game's decision (the `Trace.cs` tracing on hard-wired obfuscated 0.82.7 names, swords, OUTCOME, ALT, "doubts"), and part of it runs on every hover: 6-9 extra calls of the forecast builder. The feature itself is small: on the game's decision, compute the forecast with swapped roles and append it to the line.
- **The name generator is half done.** The plugins still look for hook targets while the game runs (each plugin anew, with its own code table); refinements do not reach the dependent searches; some of the flagged searches do not show candidates.
- **Map badges and danger are copied across mods** (Visited buildings, Squad tooltip), and the copies have already drifted apart: intervals, the delay after Alt, error handling, badge drawing, each one with its own hidden copy of the skulls.
- **Squad tooltip keeps the game's logic to itself**: the encounter breakdown (`MeetingOutcome`) with its own enum instead of the core's `EncounterOutcome`, its own hooks on Diplomacy points.
- **The common library pushes toward the untyped**: events carry wrappers over `Il2CppObjectBase`, mods do `.Raw.AsX()`; errors and the lifecycle go through global statics.
- **Mods fights the game's UI**: the language is polled every frame and everything is redrawn 8 times, the dropdown is recreated on every show, the game's text component is used by the obfuscated name `cpz`.

## Order of work
### A. Bugs (first) — 05.11 items 1-9 fixed, except part of item 9 (generator: hook targets are in C); check in the game: 2 ("strike after counter" on a squad that counters first)
1. Visited buildings: with no hero selected the fog is not checked — badges over everything, including in the fog; the guard is counted as foreign. (`Markers.cs` `ScanMarkers`) — checked.
2. Battle counter: "strike after counter" rewrites the numbers of the game's forecast before its decision and with the switch off. (`StrikeAfterCounter` from `OnForecastBuilt`) — checked.
3. Battle counter: the debugging watch on units for 1800 frames reads them through raw pointers — after the battle that is freed memory.
4. The "error already reported" sets (`GameEvent`, `Ticker`) are not cleared on unload: after a hot reload a failed handler no longer writes to the log. — checked.
5. `Texts.CurrentLanguage` has no check for the localization service being absent — an exception before it appears.
6. Squad tooltip: the power ratio (`lastPowers`) is not reset — the next squad may show someone else's; a false `MISMATCH` in the log when the verdict is unknown.
7. Visited buildings: a unit dwelling with no hire badge gets the guard swords at the card size (0.8).
8. Squad tooltip with "hide difficulty" sets the "enemy" sign even with the danger badges off.
9. Generator: a refinement does not reach the dependent searches (hero data → army, model → scene objects, the squad storage) — correct only because the first candidate happened to match the pinned one; about 18 searches flagged `OnlyOne` do not show candidates (should be `Among`); `DistinctCode` glues together all methods with an unknown address; the tool does not check that the BepInEx wrappers were rebuilt after a patch.

### B. Battle counter — put in order — done 05.11 (0.33: 327 lines instead of ~1300 with the tracing; check in battle)
Remove the research layer (`Trace.cs`, swords, OUTCOME/ALT/STATE, "doubts", the walk over fields through pointers); the result is a record (`damage from–to`, `killed from–to`, `before the strike`) instead of the string `"!д–д|у"` with parsing; one forecast → details table instead of six `IntPtr` dictionaries, cleanup per battle; state in an instance, `Logs.Guard`; write only on the game's decision. Expected ~250 lines instead of ~750.

### C. Name generator — finish — done 06.11: hook targets and `WeakShare` come from the generator (`GameTargets`), the in-game searches at load are gone; 27 types with names, the generator does not let an obfuscated type through into the readable names; names are generated already as needed (`ObjectData`, `SquadData`, `HeroStats`, `SquadOwners`, `SquadStrength()`), the wrappers are removed; dead code removed (`ReadEncounter`, the target accessor methods, `RipTarget`, `ClassNameAt`, `Values`, `GameProperty.Get/Set`, `GameMethod.Call`, `Members.Call`); generator problems are build errors `GN0002`. And (06.11): the forecast numbers are those integer fields of the forecast that the game's forecast panel reads (`BhHealDamageForecastPanel.Show`), not just the first four; `CountersLeft` removed — the mods do not need it; the console tools (disassembler, virtual slots, hidden strings) are in `mod-research/CodeResearch.cs`, the unused `Dump` removed. Section closed
Hook targets and constants (`WeakShare`) from the generator, not by searching in the game (`ReadEncounter` and the code table leave the plugins); aliases for the obfuscated types that leaked through (`bak`, `bye`, `eon`, `fhf`, `fmr`, `fno`) and a ban on such types in the output; generated names right away with the needed names — without renaming wrappers (`ObjectData = Data`, `SquadStrength = Strength()`…); remove dead code (`GameProperty.Get/Set`, `GameMethod.Call`, `Values`, `RipTarget`…); `CountersLeft` and the forecast numbers by offsets from the game's code, not by the order of properties; generator problems as build errors.

### D. Common services instead of copies (06.11: the badge schedule — `MapMarkers.Update/Prepare` for both mods, a 30-frame interval and a delay after show for both; the danger badge — `MapMarkers.Danger`; the Visited buildings badges are drawn by `Badges.Draw` with the frame colour, the copy of the drawing code removed. 06.11 these too: `Scene.Find/Shown/UiCamera/OnScreen/Tree/Items`, the sprite cache in a single pass `Game.SpriteNamed/SpriteMatching`)
The map badge schedule (the walk, the delay after show, warm-up, errors) in `MapMarkers`; one danger icon and one hidden copy of the skulls; drawing the Visited buildings badges through `Badges`; scene search, the screen rectangle, the sprite cache, walking Il2Cpp collections, the tree dump — into the common code.

### E. Mod framework (06.11: access to the event wrappers is typed — `fill.Squad?.Typed` and the like, no `.Raw` in the mods; errors of handlers and ticks go to the log of the mod itself; a disabled mod gets no events; "hero selected" and "map loaded" are remembered and handed to a late subscriber, the manual repeats in the mods removed; a failed `OnLoad` is rolled back; the "no core" warning is one, in `ModBase.On`. Debug flags — `ModBase.DebugFlag(key, description)` (the `[Debug]` section, outside the menu), the Squad tooltip "badge instead of skulls" moved into the menu. We do not make events on real types in the library itself: the library is one for all game versions, while the hero and squad types are obfuscated and exist only in the plugin's generated names; the typed entry is `.Typed` in the plugin's common code. Section closed)
Events with the game's real types (step 5 of `docs/game-names.md`); state events (selected hero, map) are handed to a late subscriber too — the manual `if (Game.SelectedHero != null) Prepare()` goes away; the mod's main switch applies to the handlers too; errors go to the log of the mod itself, without `ModEvents.Log`; settings outside the menu (`Menu.Hidden`, `Menu.Debug`); `OnLoad` with an error — rollback.

### F. Do not fight the game (06.11: the encounter breakdown — `Game.MeetingOutcome` in the common code on the core's `EncounterOutcome`, Diplomacy points — `DiplomacyPoints` in the common code; the core learns "joined at once" from the game's method `dkw.zkh`, a person — from the game's `IsPerson()`; the text of a key — by the game's function `Game.Translate` (`cav.qmk`), reading the table from the library removed; language change — the `LanguageChanged` event from the game's listeners (`cpz.ihb`): our texts are put into the new table before the redraw, Mods is redrawn once; the guard row — flag `true`, as the game itself passes it (`BhTooltipHire.wae`, `cwx.weo`), the trial fills removed. Left: the counter rules from the data (the assassin's debuff, "counters first") — check in battle whether the game's decision accounts for them, and then remove)
`MeetingOutcome` — into the common code/core, with `EncounterOutcome`; Diplomacy points — by names or a core event; translating a key — by the game's function, not a table; language change — by a game event; the guard row flag — find what the game passes; `ByPerson` — the game's `IsPerson()`; classifying an encounter — by the game's methods, not "everything else = joining"; the counter rules from the data — remove after checking.

### G. Typing, small items (06.11: languages — `ELang` in `Texts` and the `LanguageChanged` event; the Mods rows — records by kind (`TitleSpec`, `SwitchSpec`, `ChoiceSpec`, `ButtonSpec`); visit rules — `VisitRule`/`VisitReset` (`Game.VisitFor`); squad badges — `Sign` records in the Map badges mod; `MenuItem` — `ToggleItem`/`ChoiceItem`; choice options in `lang` by value name (`.Strobe`), the numbered keys for the game's dropdown are made by the menu itself (`Texts.Alias`). Section closed)
Languages — `ELang`; the Mods rows — records; visit rules — an enum; the Squad tooltip badge keys — records, as in Visited buildings; `MenuItem` — subclasses; choice option keys in `lang` — by name, not by number.

### H. Cleanup (06.11: stale comments in the plugins — "search by signatures at startup", "18 loaders", `mod-console`; `foreach` over a single name; 31 unneeded `using` by the IDE0005 analyzer; in `docs/process.md` the sections on debugging and the game's names rewritten to match the present (the `[Debug]` flags, logs, generated names) instead of `LogDecisions` and `Aliases.cs`; the Map tooltip description — and from the interface panels too; the Battle counter line in CLAUDE.md. Section closed)
Stale comments (signatures at startup, 18 loaders, `CounterDecisionPostfix`), unused `using` and locals, literal constants, descriptions in `lang` (the map tooltip leaves the HUD panels too; the flee badge), the CLAUDE.md line about the Battle counter version.

---

# Code review (05.11.2026)

Three reviewers: the common code (`mod-common`), Squad tooltip and Visited buildings, the other mods (Map tooltip, Mods, Research, the loader, Battle counter). fogone's criteria:
1. beauty and elegance;
2. reuse;
3. attempts to fight the game;
4. consistency of approaches;
5. typing;
6. reflection only behind neat strictly typed accessors.

Below the findings are grouped by theme. The number in parentheses is the criterion. Battle counter (`mod-battle-counter`) is a separate list at the end; fogone 05.11: we put it in order too.

## General impression
The foundation is good: `Lookup` with a log, the `Members` chain, `PatchEach`, finding the game's members by the encounter code (`GameEncounter.cs`), `GameSettingsUI.cs` as a model of the alias layer. The mods mostly do not fight the game: skulls, garrison, outline and "visited" are taken from it. The main troubles:
- weak typing of the common API: `extension(object …)`, `object` in the plugins;
- the old part of `Game.cs` is written in an early style (bare `Invoke`, manual `BindingFlags`, search by trial calls);
- the scaffolding (log, errors, hooks) is copied into every plugin.

## 1. Safety and reliability (important)
- ~~**The `mod-research` console is unprotected**~~ — research 0.6: a key in `research-token.txt` + a header, refusal on `Origin` and a foreign `Host`, a body limit, a thread per request. (`Console.cs`) Any page in a browser can send `fetch('http://127.0.0.1:8766/?c=eval …')`, and `eval` calls the game's methods. There is no `Host`/`Origin` check, no body limit, `int.Parse` without protection, one thread. Needed: a token (the file `BepInEx\research-token.txt`, a parameter or a header), refusal on a foreign `Host` or any `Origin`, a body limit, `TryParse`.
- ~~**`Lookup` silently returns null**~~ — 05.11: `OnlyOne()` instead of `Single`/`First`, comments fixed; hooks through `PatchOne` (not found — to the log, saying what is turned off), Battle counter turns off without unit data. An explicit `Game.Require` — with typing (step 3)., while the comments promise "throws if not exactly one" (`Single`, `First`) (4, 3). So the check `_ = Game.UnitDataProperty; // throws when missing` protects nothing, and `harmony.Patch(null, …)` in Squad tooltip and Visited buildings will throw in the middle of `OnLoad`, when some of the hooks are already installed. Needed: `.OnlyOne()` everywhere; being required — explicit (`Game.Require(...)`: false and a log line saying what was not found).
- ~~**The loader** (`mod-devloader`)~~ — 05.11: does not load a new copy on top of one that failed to unload (installed with the game closed). It was: if the old plugin did not unload (not a `ModBase`), the new one is loaded anyway, and the hooks are installed twice.
- **`Perf`** reaches into the private field `Ticker.actions` by string; a public typed hook in `Ticker` is needed. (The swallowing of errors in `Perf` was fixed in research 0.5.6.)

## 2. Typing (5) and reflection behind accessors (6)
- **`extension(object hero/squad/mapObject/entity/side)`**: `squad.HeroArmy`, `"x".IsPerson` compile, `Game.SelectedHero`, `Squads()`, `MapObjects()` return `object`. The game's type is known only at run time, but the wrapper can be made strict: `readonly record struct MapHero(Il2CppObjectBase Raw)`, `MapSquad`, `MapObject`, `SideOwner`, `MapEntity`. The cast (`CastTo`) once at creation, `extension(MapHero hero)` blocks, comparison by pointer in the wrappers' equality.
- **The accessors do not cover everything, so they are bypassed** (`Members.cs`). Missing: static properties (`GameStatic<T>`), properties of an obfuscated type (`RuntimeProperty<TValue>(Func<Type> owner, …)`), a signature by types (`takes: typeof(DataParty)`), typed arguments (`Call(TOwner, TArg)`). `Lookup` with `where T : class` forces `Lookup<object>` for the number 0.5 and `Tuple`.
- **Bare `Invoke`/`GetValue` in the aliases** of `Game.cs`, `GameBattle.cs` (skulls, garrison, fog, tooltip texts), with no null check and no cast of the target. Move to `GameMethod`/`GameProperty`.
- **Bare reflection in the plugins**: the Diplomacy icon through `method.Invoke` over the sprite loaders (Squad tooltip), `typeof(DataObject).GetProperties()` (Visited buildings), the obfuscated `cpz`/`textSid` right in the Mods code, searching for the settings section's methods in the plugin, the Alt mode through `t.Name == "BhPathDrawer"` in `MapMarkers`, walking Il2Cpp dictionaries through `GetMethod("GetEnumerator")` twice (`Game.cs`, `Texts.cs`).
- **Hooks with `object[] __args`** and casts in place; Harmony supports typed `__0`, `__1`, `__result`.
- Small items: `Positioner` returns `IntPtr`; the field tuple `(name, offset, isStatic, type)` and comparing the type by the string `"System.Boolean"`; `Options` in `ModMenu` is `object[]`.

## 3. Fighting the game and guessing (3)
- **The Diplomacy check is looked up in two ways**: the old one goes over all `bool(…, …, out string)` and picks the one whose string contains `"squad points"`. That is guessing by text, while the exact one already exists (`explainedCheck` from the encounter code). `SquadType` is derived from "the last candidate".
- **Trial calls to choose a method**: the share that joins (`JoinShare`: "value in (0, 1]"), the unit portrait (`UnitPortrait`: "sprite name == sid"), the flag for showing the garrison count ("digits only"). Find from the game's code who calls them in the hire window, the unit card, the squad tooltip fill.
- **The Diplomacy icon is guessed** by keys and by the substring "diplomacy". Find where the game's skills window gets it from.
- **Mods overwrites the texts after a language change** 8 times over a few seconds. Find the game's method that redraws localized texts, or a language-change event.
- **Map tooltip** adjusts the offset for up to 5 frames, compensating for the game having "pushed" the tooltip onto the screen. Find the positioner's function that limits the place to the screen.
- **Alt mode**: its own state machine over key presses instead of the game's flag (`BhPathDrawer.ShowMovePointsTooltip`) — remove the fallback path.
- **The cell number from a world point** — its own formula; look for the map's "world → cell" function.
- **"How many will join"** — its own formula `floor(count × share)`; the hire window computes this itself.
- **The squad behaviour line** (`container.behaviorSquad`) — the mod writes into the game's line; if the game shows something there itself, the text is overwritten. Make a line of our own next to it, like `OE_VisitRow` in Visited buildings.
- `HasDiplomacy` — its own prefilter before the game's check; remove it or explain it.

## 4. Reuse (2) and consistency (4)
- ~~**The plugin scaffolding in every mod**~~ — 05.11: all mods on `ModLog` from `ModBase` (`Once`, `Report`, `Guard`), their own `ReportError`/`LogOnce` removed. It was: `logger = Log; Game.Log = …`, their own `ReportError`/`LogOnce` with different deduplication and log level, dozens of `try { … } catch { ReportError }`. Move into `ModBase`: `Game.Log` in `Load()`, `LogOnce`, `ReportOnce(where, e)`, `Guard(where, action)`, `DebugFlag(name)`.
- **Hooks in three ways**: bare `harmony.Patch` (+ `AccessTools.Method` with no null check), manual address filtering (a copy of `PatchEach` in Squad tooltip), `PatchEach`. Everything — through `PatchEach` or a helper "find or say so and switch off".
- **Two languages of member search**: the `Members` chain and manual `GetMethods(BindingFlags…)`/`ParamTypes(m).SequenceEqual`; `Name()` in `Game` and in `Members`; inconsistent descriptors in `Lookup`. Bring it all to `Members`.
- **"Find once" implemented by hand half a dozen times** (the `…Searched` flags, their own retries every 2/5/10 s). `Lookup.Retrying(seconds)` is needed and, if the trials stay, `Lookup.FirstAnswering`.
- **Copies**: the badge drawing in Visited buildings is a copy of `Badges`; the sprite loaders (the Diplomacy icon and `UnitPortrait`); the danger badge (Squad tooltip and Visited buildings); scene object search (`Shown<T>` in Map tooltip, `FindObjectsOfTypeAll(...).FirstOrDefault(scene.IsValid)` in Mods and `FpsOverlay`); the `RectTransform` screen rectangle and `UiCamera` (Map tooltip and `FpsOverlay`); `Append`/`Without` for Il2Cpp arrays; `ClassPointer(Type)` three times in `GameCode`; the "will join" verdict formula three times in Squad tooltip.
- **The selected hero in two ways**: Squad tooltip catches it by hooking the cursor colour, badges and Visited buildings use `Game.SelectedHero` from the HUD. There must be one source.
- **The mechanics around the game's members live in the plugin**: `MeetingOutcome`, peeking at the Diplomacy points — move into `GameEncounter` (`hero.MeetingOutcome(squad)`, `hero.WouldJoinWithPoints(squad)`).
- **Efficiency**: `CastTo` does `GetMethod`+`MakeGenericMethod` on every call; `OwnType` is a linear search by name; `ObjectModel` goes over all GameObject properties every time; `Settings` looks for the root on every read; the log describes the squad (walking the storage) on every tooltip fill.
- Miscellaneous: `ShowIcon` in the config bypassing the menu; the Visited buildings text keys as strings in place; the game's sprite names as strings across plugins (`Icon_Escape` twice); the Visited buildings badges are encoded as strings `"unit:<sid>:dim:<n>"` and parsed back.

## 5. Beauty (1), small items
- `Hud()` in Map tooltip: broken indentation; sides as strings instead of an `enum`; `Over` mixes roles.
- Mods: `BuildModsTab` of 67 lines and 9 scattered static fields (begs for a `ModsTab` class); `Row` with "which field is not null" instead of row kinds.
- Research: `Execute` is a 120-line `switch`; a stale comment about `mod-console`; `FileHelp[..40]`.
- `MapMarkers.cs`: broken formatting (a comment glued to `return false;`, `}        catch`).
- Leftovers: `DumpHierarchy`, `foreach (var name in new[] { RowName })`, unused `using`, stale versions and references to the removed `Aliases.cs` in the documentation, the description of the squad badges ("amber with an arrow" — now the game's flee badge).

## Battle counter (from 05.11 — also in progress)
- The game's forecast is read as "the first four `Int32` fields in order" and overwritten through `Marshal.WriteInt32` (3, 5, 6).
- For the computation the squad's creature count (`fullStacks`) is temporarily changed and someone else's method is called (3).
- The counter line is built by copying the elements of the game's line and measuring the width by hand (3).
- Its own signature scans in `OnLoad` and `Trace.cs` instead of aliases; its own parsing of `E8 rel32` instead of `GameCode.Calls`; `CountersLeft` "three properties before fullStacks"; six `IntPtr → …` dictionaries for one entity; the result as a string with the protocol `"!"` and `"|"`; `object[] __args` (2, 5, 6).

## Order of work
1. **Safety**: a token and a `Host` check for the console, a body limit; explicit requiredness of aliases instead of "silently null"; the loader does not load a second time.
2. **Mod infrastructure** (`docs/mod-framework.md`): events with context, services, log, lifecycle; hooks — in the core, not in every mod.
3. **Typing**: the wrappers `MapHero`/`MapSquad`/`MapObject`/`SideOwner`/`MapEntity`, extending the accessors (`GameStatic`, `RuntimeProperty`, signature by types), `Game.cs` and the plugins on top of them, typed hook parameters.
4. **Guessing → the game's code**: the Diplomacy check through `explainedCheck`, the share and portrait by the game's calls, Alt mode by the game's flag, the Diplomacy icon, the garrison flag.
5. **Copies → common code**: `Badges`, sprite loaders, the danger badge, scene object search, `OnScreen`, Il2Cpp arrays.
6. **The rest of "against the game"**: the Mods texts after a language change, the Map tooltip adjustment, "how many will join", the squad behaviour line.
6a. **Battle counter**: aliases instead of its own scans, a typed forecast instead of "the first four ints", computation without temporarily editing the squad, the counter line through the game's function, one dictionary per forecast — by the same steps 2-5.
7. Small items and beauty — along the way.
