# Game names: a generated file per version (decision of 05.11.2026)

## Why
Right now game members are found by signature at run time (`Lookup`, `Game*.cs`). Downsides:
- **the search can silently get it wrong.** There are doubles (`dkw.zki` / `dkw.lkv`, `ffd.blop` / `ffd.bfj`), and some methods are chosen by trial calls. If after a patch the real member disappears but its double stays, the double gets found;
- **the error is only visible in the game**, from the log and the behavior;
- **everything goes through reflection and `object`**: `GetValue` / `Invoke`, `MapHero` wrappers over `Il2CppObjectBase`.

fogone: a separate build for each game version is fine.

## How it will work
1. **The searches stay** — the same ones as now (by signature, by game code), but they run once per version — **at build time, from the game files, without a running game** (05.11, fogone: "it needs to move into the build"):
   - `tools/game-names` compiles our searches (`mod-common/shared`) and the generator into itself and runs them on the BepInEx wrappers (`BepInEx\interop\*.dll`: in an ordinary program reflection works on them, and `typeof(DataObject)` compares as in the game);
   - machine code, method addresses, field offsets and static classes are taken from `GameAssembly.dll` and `global-metadata.dat` by the LibCpp2IL library, the same one BepInEx uses to make the wrappers (it lives in `BepInEx\core`). The code source is shared between the game and the files — `CodeSource` (`mod-common/shared/CodeSource.cs`): `LiveCode` in the plugins, `FileCode` in the tool. Checked 05.11: the addresses of `DataFogOfWar.bkin` and `BhSkulls.umq` are the same, the code read from the files matched the code from the live game;
   - the game version (`Application.version`, "0.82.07") comes from `HeroesOldenEra_Data\globalgamemanagers`, the only version-like string there besides the Unity version;
   - `Directory.Build.targets` in the root: before building a plugin that compiles `GameNames.g.cs`, the tool runs if any of the inputs is newer than the file (the game files and wrappers after a patch, our searches, the pins); otherwise the step is skipped. About 6 s. If zero or several are found, the build fails with a list of candidates. Without the game files (another machine), the file from git is used;
   - manually: `dotnet run --project tools/game-names -c Release -- [<game folder>] [<file>]`;
   - BepInEx regenerates the wrappers on the first launch of the game after a patch: after an update the game has to be started once up to the main menu, then everything works from the files;
   - `aliases` in the Research console is kept: the same thing on the live game, for cross-checking.
   Clones with the same machine code (`frc.bc` / `frc.mh` / `frc.ofh` — squad strength) are collapsed into one, with the smallest name: the same in the game and from the files.
2. **The command writes `mod-common/shared/GameNames.g.cs`:**
   - types: `global using GameHero = fgd;`, `GameSquad = frc;`, `GameMapObject = fql;`, `GameSide = fqu;` …;
   - members — extension blocks with readable names over the real ones: `extension(GameHero hero) { public GameSide SideOwner => hero.cqej; }`;
   - the game version the file was made for.
2a. **Ambiguity is resolved at build time, not in the game** (fogone: "resolve problems can be fixed at build time, but not at runtime anymore"). The generator does not choose by itself: if a search gives zero or several candidates (doubles, clones with different code, a trial call without a single answer), it stops and prints the candidates with their code and calls. The choice is pinned in a pins file next to the generated file (`mod-common/shared/GameNames.pins.txt`: "`GameHero.Data = fgd.cwtl  # why`"; the key is the owner's alias and the name, for statics `GameNames.Name`), also in git. On the next game version the pin is checked again: if the pinned member is no longer among the candidates, it stops again; a pin that matches nothing also stops it.
2b. **What gets written:** a property — read-only (the mods read the game's state), write only if marked `Generate(name, writable: true)` (our copies of the game's widgets: the "Mods" tab and section); a method — an extension method with the same parameters (`out`/`ref` are preserved); a static — a member of `GameNames`. A method that is in fact a property getter under its own name (`fqu.bnyp` is the getter of `fqu.cwyd`) is written as a property read: C# does not allow calling an accessor directly.
2c. **A search that can find several** is written through `Lookup<T>.Among(what, () => candidates, ...)`: in the game the first is taken (or nothing with `onlyOne`, as with `GameProperty`/`GameMethod`), while the generator sees all of them. `FirstOrDefault` in marked searches was hiding doubles: the first run found two such places (the map object model `fql.crko`/`fql.crla`, the hero data `fgd.cwtl`/`fgd.cwto`), both pinned to what already worked.
3. **The file is in git.** `git diff` after a patch shows what was renamed and what was found differently; a double in place of the real member is visible to the eye.
4. **Our code** (`Game*.cs`, the mods) is written on top of these names with ordinary typed calls: no reflection, no `object`, no wrappers. The extensions hang on the game's own objects.
5. **On load** the mod checks the game version against the file. If it does not match — a log entry and the mod does not turn on.
6. **Events** (`ModEvents`) receive the game's real types instead of `MapHero` / `MapSquad` / `MapObject` (wrappers remain only where the game has no type of its own).

The cost: after a game patch — `aliases`, looking through `git diff`, rebuilding everything (and the common DLL) and installing with the game closed.

## Order
1. ~~Registry of searches~~ — 05.11: `Lookup.Generate(name, property type name)`; the hero, squad, map object, side and their data are marked.
2. ~~The `aliases` command and file generation~~ — 05.11: `mod-common/shared/GameNames.g.cs` (4 types, 6 properties on 0.82.07), built into all mods. The first run showed a search error: the hero's "army wrapper" is the readable `Hex.Session.Data.Hero`, not an obfuscated class; the mark was fixed.
2½. ~~Methods, statics, stopping on several candidates, the pins file~~ — 05.11: 4 types, 27 properties, 17 methods (6 static), 2 pins. Marked: the map object, hero, side, squad and their data, the HUD hero panel, fog, settings, skulls, tooltips, the encounter and everything it reads, the settings screen.
3. ~~Version check on load~~ — 05.11: `ModBase` compares the mod assembly's `GameNames.GameVersion` with `Application.version`; if it does not match — an error in the log, the mod does not turn on. Except Research (`NeedsGameNames => false`): it is the one that makes the names for a new version.
3½. Build check — 12.11 (#68): one version can be several builds with other obfuscated names (the Steam build of 0.82.08 is not the Game Pass one: `Localization: iv.buwi … ccdj` there, `iv.buvv … cccw` here; `cqp.ujd()` missing). `GameNames.GameBuild` is the start of the SHA-256 of `global-metadata.dat` the names were made from (`GameBuild.Of`, `mod-common/shared/search/GameBuild.cs`); `ModBase` compares it with the running game's and keeps the mod off on another build. Names for a Steam build need that build's files.
4. Moving `Game*.cs` and the mods to the generated names in parts; reflection stays only in the generator and Research.
   - **Two sides of the common code** (05.11): `mod-common/shared/search/` — the searches (`Lookup`, `Members`, `GameCode`, `CodeSource`, `search/Game*.cs`), compiled by both the generator and the plugins; this side never uses the generated names. The rest of `shared/` (`Game.cs`, `GameEncounter.cs`, `GameVisits.cs`, `MapMarkers.cs` …) is written on top of the names, only the plugins compile it. Otherwise a new mark and its use would not build in a single change.
   - ~~Settings screen~~ — the Settings mod writes `section.SectionTypeStored`, `tab.ClickAction`, `checkbox.SwitchedOn()`, `SwitchCallback` directly.
   - ~~Hero, squad, map object, map storages, fog, settings, skulls, guard row, the encounter (`MeetingOutcome`)~~ — 05.11: the mods get `GameHero`, `GameSquad`, `GameMapObject` instead of `object`; the "member found" checks (`…Known`, `Discover`) are removed.
   - ~~Lists~~ (`Lookup.GenerateAll`): hero stats (`hero.Stats`, 6 paths), the squad's "owners" (`squad.Owners`), the model's scene objects (`Model.SceneObjects`), the object's configs (`data.Configs`) — `GameNames.Each` takes whatever is there.
   - ~~Trial calls~~: the Diplomacy check with an explanation — `JoinsByDiplomacyExplained` (from the encounter code); the join share — `GameNames.JoinShare` = `ffd.blor`, called by the encounter's "join immediately" (`dkw.zkh`); unit portraits — `GameNames.UnitPortrait` = `efr.bdbb` (a pin); the Diplomacy icon — among the loaded sprites (the loaders do not give it). `VisitedBy` for a subclass is the wrapper itself: Il2CppInterop calls the version of the object's class (`il2cpp_object_get_virtual_method`, checked in `Hex.dll`). The Alt mode — `BhPathDrawer.ShowMovePointsTooltip` directly.
   - ~~Positioner and tooltip texts~~ — 05.11: `tooltip.Positioner` (`GameTooltipPositioner`), `tooltip.SetTexts(texts, GameTooltipOptions)`.
   - ~~Battle counter~~ — 05.11: `unit.Data`, `unit.LogicConfig`, `unit.Abilities()?.Ability(type)`, `ability.LogicConfig`, `ability.CastConfigs`/`BuffConfigs` (lists through holder methods), `unit.Buffs?.Lists`, `passive.BuffDatas`, `UnitData.CountersLeft`, the forecast numbers `forecast.DamageMin…KilledMax` (instead of the "first four ints" through IL2CPP), `GameNames.BuildForecast` (`exw.bkg`, clones pinned), the strike parameters `new DamageUiParams(new GameCaster(…), new GameCastSource(…), new GameCastTarget(…), …)`. Reflection remains in the debug trace (`Trace.cs`, behind a flag).
   - Still on reflection: `Texts.cs` in the common library.
5. Events on real types.
