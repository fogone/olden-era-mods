# Development and debugging process

## Hot reload (without restarting the game)
The main cycle since 03.11.2026. The plugins we are working on live not in `BepInEx\plugins` but in `BepInEx\devplugins`. `mod-devloader` loads them and reloads when the DLL changes.
1. **Edit and build into the game:** `powershell -ExecutionPolicy Bypass -File <mod>\build.ps1 -Dev`. Builds and puts the DLL into `devplugins`, even if the game is open.
2. **Reload — automatic.** After 1–2 seconds `LogOutput.log` shows `DEV unloaded <dll>` and `DEV loaded <name> <version>`. The old version unloads its patches, menu, per-frame subscriptions and objects with the `ObjectPrefixes` prefixes. The new one loads afresh from the file's bytes.
3. **Check** — right in the game; the Mods menu shows the new version in its header.

Conditions and limitations:
- **The mod must be on `ModBase`.** This is a class from `mod-common`: `OnLoad` instead of `Load`, `Harmony` and `Menu` from the base class, `OnTick` instead of your own `MonoBehaviour`s. Created objects are named with a prefix from `ObjectPrefixes`, and whatever the mod changed in game objects it restores in `OnUnload`. Do not create your own `MonoBehaviour`s in a mod: their class cannot be registered in the game a second time.
- **Moving from `plugins` to `devplugins` — once, with the game closed.** `-Dev` itself deletes the copy from `plugins`, and refuses if the game is open.
- **`mod-common` and `mod-devloader` cannot be reloaded**, they are installed only by `-Install` with the game closed. .NET does not unload an assembly from a process, and the plugins bind to the first loaded `OldenEraCommon.dll`. The namespaces are `Nobirds.OldenEra.Modding.*` (since 05.11), the DLL names stayed `OldenEra*.dll`.
  - `OldenEraCommon.dll` holds the framework since 09.11 (#30): `ModBase`, `ModMenu`, `Texts`, `Ticker` and `mod-common\shared\*.cs` (the helpers, the game's names). A change to the shared code needs a restart; a mod's own code reloads.
- **Each reload leaves the old assembly in memory.** That is tens of kilobytes and does not affect a development session.
- **Game crashes, patches of new methods with a risk of merged code, edits of game data** — are checked as before, by restarting.
- **Release:** `build.ps1 -Install` with the game closed puts the mod back into `plugins` and removes the copy from `devplugins`.

## Work cycle with a game restart (installation, new patches, crashes)
1. **Edit the code.** The plugin: `mod-squad-tooltip/Plugin.cs`.
2. **Build.** `powershell -File mod-squad-tooltip/build.ps1` builds `bin/Release/net6.0/OldenEraSquadTooltip.dll` in about a second.
3. **Install.** First check that the game is closed (`Get-Process HeroesOldenEra`), then `build.ps1 -Install` copies the DLL to `<game>\BepInEx\plugins\`. If the game is open, ask the user to close it.
4. **Launch.** Start the game (the command is in `CLAUDE.md`), wait for the line `Chainloader startup complete` in `LogOutput.log`. Then wait 10–20 seconds and check that the process is alive: this catches crashes at load.
5. **Check in the game.** Ask the user to do a specific action: hover over a squad or walk up to it.
6. **Read the log.** Read `LogOutput.log`: the plugin's lines are marked `[...:Olden Era Squad Tooltip]`.
7. **Uninstall.** `build.ps1 -Uninstall` removes the plugin.

**The plugin config** is `<game>\BepInEx\config\nobirds.oldenera.squadtooltip.cfg`. BepInEx creates it on first launch and keeps the user's values. If the defaults changed, the file is deleted and created anew.

## Debugging in plugins
- **`[Debug]` flags** in the plugin config (`ModBase.DebugFlag`, not in the menu, off by default): `LogObjects` of Visited buildings (visit and guard data of the hovered object, lines `OBJECT`/`GUARD`/`FILL`), `LogTree` of Mods (object trees of the settings sections after clicks), `TimeFromStart` of Research (time of our mods since the game started).
- **Logs, always on:** Squad tooltip — `SQUAD` / `ENCOUNTER` / `MISMATCH` (the tooltip's decision against what the game did); Battle counter — `COUNTER` (the game's decision and the counterattack numbers); Map badges — the summary `Buildings: ...`.
- **The `mod-research` console** (below) — values, method code, who calls, fields with offsets, peeking at calls, frame rate.
- **Type members without running the game:** `dotnet run --project tools/typedump -- "<type name or regex>" [assembly, Hex by default]` reads the wrappers from `BepInEx\interop` (properties, methods, enum values).
- **Disassembler.** Bytes from the log (`BYTES ... @0x...`) are disassembled with the `tools/disasm` utility (Iced from `BepInEx\core`, `net10.0`): `dotnet run --project tools/disasm -- <log> <start of the signature, e.g. dkw.zki>`. PowerShell 5.1 does not load this Iced.dll. For an IL2CPP class: the name is at `klass+0x10`, static fields at `klass+0xB8`.
## Game names: how to add a new one
Game members are taken from the generated file `mod-common/shared/GameNames.g.cs` (details in `docs/game-names.md`); nothing is searched for while the game runs:
- a search is written in `mod-common/shared/search/*.cs`: `Lookup<T>.Among("what it is", () => candidates, description, onlyOne: true)`, then `.Generate("Name")` for a property or method (an extension on a game class or a static one in `GameNames`), `.GenerateTarget("Name")` for a hook target (`GameTargets.Name`), `.GenerateConstant("Name")` for a constant from code. Candidates are found by signatures and game code (`Members`, `GameCode.Calls/Offsets/IntReadsThrough`), not by obfuscated names;
- the generator (`tools/game-names`) runs by itself when a plugin is built, when the searches or the game changed. Zero or several candidates is the build error `GN0002`; an ambiguity is pinned in `GameNames.pins.txt`;
- in mod code — only the generated names and clear wrappers over them in `mod-common/shared/Game*.cs` (`Game.VisitFor`, `hero.MeetingOutcome(squad)`, `Game.Translate`); obfuscated names only in comments, with the version: "`cav.qmk` on 0.82.7";
- hooks — `harmony.PatchEach(GameTargets.Name, ...)` / `PatchOne`: each native address once, a method with merged code is not hooked (the lesson of `cxg.wdq`).
## Researching game code (`mod-research`, without rebuilding)
A development plugin (`devplugins`). The commands (`Commands.cs`) run on the game's main thread, in two ways:
- **console** (this computer only, `127.0.0.1`, with a key): the console writes the key at startup to `<game>\BepInEx\research-token.txt`, and it must be sent in the `X-Research-Token` header. Requests without the key, with an `Origin` (from a browser) and with a foreign `Host` are rejected. PowerShell: `$k = Get-Content "<game>\BepInEx\research-token.txt"; curl.exe -s -H "X-Research-Token: $k" "http://127.0.0.1:8766/?c=eval%20hero"`; several lines — `--data-binary @file`. The reply comes immediately;
- **file** `<game>\BepInEx\research.txt`: on saving, its lines are executed, the result goes to `research-out.txt` and to the log (`RESEARCH ...`).
You can write obfuscated names: this is for finding out what a method does. A plugin in `devplugins` is not unloaded if its DLL is deleted: the old version then stays until the game is restarted.

**Live state** (`Eval.cs`): an expression is a chain from the root `hero` (the selected hero), `$name` (a variable), `$` (the last result) or a type name (then its static members); steps `.property`, `.field`, `.method(arguments)`, `[i]`, `.as(Type)`.
- `eval hero.cqei.cwfa` — a value; `inspect $side` — all properties with values (and elements, if it is a list); `methods X` — the same plus method signatures; `set $side hero.cqej` — remember it.
- `members Type` — properties and methods with signatures (like `tools/typedump`, but in the game);
- `code Type.method` — instructions (Iced) with the names of the called methods; `calls Type.method` — only direct calls;
- `callers <regex>` — which game methods call a method with a matching signature (the code of all methods is read), for example `callers ^CommandBuffer\.SetGlobalVectorArray` — this is how the passing of the outline colors `bei.lkv` was found;
- `shared Type.method` — which methods are merged with it (a hook would affect all of them);
- `slot Type 0x238` — the method in a class's virtual slot (code like `call [reg+0x238]`). Only an offset from a real call: on 05.11 `slot fhf 0x0` read the class header as a method and crashed the game. Now `slot` refuses offsets below 0x100 and an address where no known method begins;
- careful: `eval` with a method call (`.method()`) really calls the game's code. Wrappers like `bmdx()` on merged generic methods are call stubs, do not call them;
- `string qfz` — the hidden string `<PrivateImplementationDetails>.a.qfz()` (localization keys and the like); `strings <regex>` — which hidden strings have a matching text (then `callers ^a\.qfz\(` for who uses one);
- `readers cny` — which game methods read the static fields of a class (constants kept in statics: this is how the attack's sequence `eqe.bhqa`/`bhpz` was found from the names of the sequence effects in `cny`);
- `probe Type.method [seconds]` — peek at calls (object, arguments, result), at most 200 lines; it cannot be set on merged code; `unprobe` — remove it.
- **UI** (live Unity objects): `tree <name or path end> [depth]` — the objects under it with components, sizes and texts (a name with spaces is not found: the argument splits on spaces); `click <path end>` — the pointer events of a real click (enter, down, up, click, exit) sent to an active object, e.g. `click OE_Row_nobirds.oldenera.visited/Space/Remove` (this is how the Mods window's buttons were checked on 08.11 without the player); `shot <file.png>` — the game's screen into a file; `objects <Type> [regex]` — the loaded objects of a type whose name, image sprite or path matches; `sprite <name> <file.png>` — a loaded sprite of the game into a PNG (to pick the game's own pictures, e.g. its arrows); `screen make|open|hide|state|drop` — a trial `ModScreen` (issue #24).

The same tools are available from code (`GameCode`, `Probe` in `mod-common/shared`): `GameCode.Dump`, `GameCode.DumpDisassembly`, `GameCode.CallersOf`, `GameCode.VirtualSlot`, `GameCode.HiddenString`, `GameCode.RipTarget`, `Probe.Calls`.

The order that worked (the map tooltip, the "visited" line, the outline): find a candidate by readable types → `code` and `callers` to see what it calls and who calls it → `probe` to peek at what the game passes and receives → hook the function that computes the result and fix its result.

## Toolkit: what to try and in what order
1. **Wrapper metadata.** Types and signatures from `<game>\BepInEx\interop\Hex.dll` are read through Mono.Cecil from `BepInEx\core` right in PowerShell (`Add-Type -Path ...Mono.Cecil.dll`, `ModuleDefinition.ReadModule`). There are no method bodies there, only signatures. Search by readable parameter and return types.
2. **Calling candidates from the plugin on hover.** Call the candidate methods yourself and write the results to the log. This is how the Diplomacy check was found: it explains its decision itself as text in `out string`.
3. **Harmony tracing.** A postfix on candidates: the game calls them itself, the log shows arguments and results. Before patching, filter out identical native addresses: `Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod` → the first 8 bytes of `MethodInfo`.
4. **Stack scan at the moment of an event.** The IL2CPP stack is empty, the names are stripped. `RtlCaptureStackBackTrace` breaks off at the Harmony trampolines. Something else works: walk the stack memory from the current position to `GetCurrentThreadStackLimits.high` and match the values against a table "native start address → method" for all methods of `Hex.dll`, about 60 thousand. This immediately gives the call chain.
5. **Disassembling the machine code of one method.** The code is taken from its start to the start of the next one in the table. In it, `E8`/`E9 rel32` are searched for, and only the targets that fall exactly on the start of a known method are kept. This shows what the method calls. Comparisons and constants are read from the bytes by hand. A constant at a RIP-relative address can be read from the memory of the running game through `ReadProcessMemory`, straight from PowerShell.
6. **Object fields by offsets.** `IL2CPP.il2cpp_object_get_class` / `il2cpp_class_get_fields` / `il2cpp_field_get_offset` / `il2cpp_field_get_name`, see `DumpIl2CppObject`. This is how the byte at `+0x47` turned out to be `DataSquad.isEscape`.

**What not to do:** `XrefScanner.UsedBy` / `XrefScan` on merged or foreign methods crashes the game. Extending tracing to thousands of methods is not worth it either.

## Switches in the game settings' "Mods" tab
The `mod-settings` plugin adds a "Mods" tab to the game settings. The menu of every plugin of ours registered through `ModMenu` ends up in it. `mod-settings` does not need to be changed for this.

**How to connect a plugin to the tab.** Through the common library `mod-common` (`OldenEraCommon.dll`, class `ModMenu`):
```csharp
// csproj: <ProjectReference Include="..\mod-common\OldenEraCommon.csproj" Private="false" />
using Nobirds.OldenEra.Modding.Common;

static Switch showThing;
static Choice<Highlight> highlight;

// in Load(), before any patches:
var menu = ModMenu.For(this, "Mod name in Russian");      // header "Name · version from [BepInPlugin]"
showThing = menu.Toggle("ShowThing", "Row label", true);
highlight = menu.Choose("Highlight", "Highlight", Highlight.Both, "off", "outline", "badge", "both");

// where the feature works — every time, not once at startup:
if (!showThing.On) return;                      // false also when the whole mod is off
var mode = highlight.Value(Highlight.Off);      // Off when the whole mod is off
```
- Every mod automatically has a general switch "Mod enabled" (`menu.Enabled`, `menu.IsEnabled`). `Switch.On` and `Choice.Value(...)` already take it into account.
- Rows go in the order of the `Toggle`/`Choose` calls, mods in the order the plugins load.
- Values are stored in the plugin config, in the `[Menu]` section. Without `mod-settings` the plugin works the same, just without the tab.
- A toggle writes its value to the config immediately, no game restart needed. So `.On` is read at the moment of work, not cached in `Load()`.
- By default hidden numbers (see `CLAUDE.md`) are `false`.
- Debug flags (`[Debug]`) and texts (`[Text]`) are not put into the menu: they stay in the config file.
- A choice is shown as a row per option: the enabled option is the selected one.
- `mod-common` must also be installed into the game (`build.ps1 -Install`); it lies next to the plugins in `BepInEx\plugins`.

**How it works (if it needs fixing after a game patch):** the tab is a `SettingsTab` (`mod-common/shared/SettingsTab.cs`):
- its section is a copy of the "Sound" section, the game's rows hidden and ours added; switch, choice and button rows are copies of the General section's rows, titles come from the game's row factory (`SettingsElementsPrefabs.titlePrefab`, held by the hotkeys section);
- in the copies `cpz.textSid` (the translation key) must be erased, otherwise the game brings back its own text;
- the section and the tab are appended to the game's arrays once the settings screen is loaded, and the game itself switches, shows and hides them: the section is found by its type, a value of its own (100) the game does not declare (`SettingsSectionsView.ShowSection`, `ktn` on 0.82.7, compares numbers). The copy of "Sound" has `Start` set its type to Sound on first display: the type is restored after `Start`;
- the screen hands every tab its click when it opens, ours included; Apply and Reset are hidden while ours is shown (a postfix on the game's switch);
- after copying, the list layout must be rebuilt (`LayoutRebuilder.ForceRebuildLayoutImmediate`), otherwise the height is 0 and the scrolling drifts away.
## Data (JSON) without code
- Unpacking `Core.zip`: the paths are long, use the `\\?\` prefix or a short folder. Paths inside the archive use `/`, no BOM.
- A mod's data goes in its package (`data\` of the project, the SDK packs it into `data\<id>.zip`); the manager mounts it as one of the game's data roots (`DataRoots`, see `game-internals.md` "Data roots"). In the log: `Found 1 archives in '...\<id>\<version>\data'`, `Loaded N entries from config archive ...`, `mod:<id>|configs loaded from folder:...`; Player.log says `<id>: DB/data.json is overriden` for a single-object file.
- An overlay `.zip` in `StreamingAssets` itself also loads (a later archive replaces a file at the same path), but it writes into the game's folder: not for our mods.
