# Writing a mod

A guide for writing a BepInEx 6 (IL2CPP) mod for Heroes of Might and Magic: Olden Era on this repository's framework.
The worked example is `mod-skip-intro/`, the smallest mod. Read it next to this page.

**Mods of your own, in a repository of your own:** start from the template
[olden-era-mods-template](https://github.com/fogone/olden-era-mods-template): two example mods on the SDK from nuget.org
(`Nobirds.OldenEra.Modding.Sdk`), a release script and a catalog of your own (`oe-catalog`,
`Nobirds.OldenEra.Modding.Catalog`), and its README as the step-by-step guide. This page is the framework's reference
and how the mods of this repository are made.

Related: [building.md](building.md) (building, releases, pull requests), [process.md](process.md) (the work cycle, the
research console), [game-names.md](game-names.md) (the generated names), [catalog.md](catalog.md) (the mod catalog),
[game-internals.md](game-internals.md) (what is known about the game's code).

## 1. What you need

- The game, with **BepInEx `6.0.0-be.788` (IL2CPP)** in its folder, **started once to the main menu**: BepInEx then
  makes the wrappers of the game's code in `<game>\BepInEx\interop` (`Hex.dll` holds the game's code). After a game
  patch, start it once again.
- The **.NET SDK 10**. Plugins target `net6.0` and compile against `<game>\dotnet\*.dll`, `BepInEx\core` and
  `BepInEx\interop`: the SDK of the mods (`sdk/Nobirds.OldenEra.Modding.Sdk`) finds the game (Game Pass; elsewhere set
  `OLDENERA_GAME`) and sets all of it up. Nothing of the game is downloaded.
- PowerShell for the build scripts (`build.ps1 -Dev`, `-Install`); they pass `-GameDir`, the Game Pass folder by default.
The game's code is obfuscated and its names change with every patch. The library (`OldenEraCommon.dll`) holds the game's
names for one game version and is released for each: a mod that uses only its public API keeps working after a patch
once the library for the new version is installed. A mod that works on the game's obfuscated classes itself (section 4)
is built for **one** game version and stays off on another one (see ModBase below).

## 2. The smallest mod, step by step

Folder `mod-<id>/`, as in `mod-skip-intro/`:

```
mod-skip-intro/
  OldenEraSkipIntro.csproj
  Plugin.cs
  build.ps1                  copy it, change the DLL's name
  lang/english.json
  lang/russian.json
```

### The csproj

The mod's csproj says what the mod is; the SDK (`sdk/Nobirds.OldenEra.Modding.Sdk/Sdk/Sdk.props` and `Sdk.targets`)
does the rest. In this repository it is imported by path; a mod in a repository of its own takes the package:
`<Project Sdk="Nobirds.OldenEra.Modding.Sdk/0.1.1">` (its README: `sdk/Nobirds.OldenEra.Modding.Sdk/README.md`).

```xml
<Project>
  <Import Project="../sdk/Nobirds.OldenEra.Modding.Sdk/Sdk/Sdk.props" />
  <PropertyGroup>
    <AssemblyName>OldenEraSkipIntro</AssemblyName>
    <RootNamespace>Nobirds.OldenEra.Modding.SkipIntro</RootNamespace>
    <ModId>nobirds.oldenera.skipintro</ModId>          <!-- the BepInEx GUID -->
    <ModName>Olden Era Skip Intro</ModName>
    <Version>0.1.1</Version>
    <ModTags>ui;qol</ModTags>                          <!-- the catalog's tags -->
    <ModRequires>nobirds.oldenera.core</ModRequires>   <!-- only a mod that listens to the core's events -->
  </PropertyGroup>
  <Import Project="../sdk/Nobirds.OldenEra.Modding.Sdk/Sdk/Sdk.targets" />
</Project>
```

- The SDK references the game's runtime, BepInEx and its wrappers of the game's code, and the library (this
  repository's `mod-common` project, `OeLibraryProject` in `Directory.Build.props`; the package's copy elsewhere). The
  library (`ModBase`, `ModMenu`, `Texts`, `ModEvents`, `Ticker`, and `mod-common/shared`: the helpers, `Game*.cs` and the
  game's names) is installed once, in `BepInEx\plugins`, and shared by all mods.
- It embeds `lang\*.json` as `<root namespace>.lang.<language>.json` (`Texts.Load` reads them), declares the compiler's
  attributes the .NET 6 runtime keeps internal, and writes `ModInfo` (`Id`, `Name`, `Version`) for `[BepInPlugin]` and
  the `Mod*` properties into the assembly (`AssemblyMetadata`). `ModAuthor` and `ModSource` come from `Directory.Build.props`.
- `dotnet build -t:ModPackage` makes the mod's archive `bin\Release\net6.0\<ModId>-<Version>.zip`: `manifest.json`
  (what the mod is, its names and descriptions from the lang files, the game version it was built on, its files),
  `plugins\<the DLL>`, and `README.md`, `icon.png`, `screenshots\` when the mod has them.
- Only a mod that works on the game's obfuscated classes itself adds `<Compile Include="..\mod-common\shared\GameTypes.g.cs"
  Link="shared\GameTypes.g.cs" />` and its assembly name to the library's `InternalsVisibleTo` (section 4).
- `Directory.Build.targets` in the root makes `GameNames.g.cs` again before the library's build when the game or the
  searches changed (section 4) and writes the mods' version (`version.txt`) into the assemblies.
### The plugin

```csharp
using BepInEx;
using HarmonyLib;
using Nobirds.OldenEra.Modding.Common;

namespace Nobirds.OldenEra.Modding.SkipIntro;

[BepInPlugin(ModInfo.Id, ModInfo.Name, ModInfo.Version)]   // from the csproj
public class Plugin : ModBase
{
    static Switch skipVideos;
    static Plugin instance;

    protected override void OnLoad()
    {
        instance = this;
        skipVideos = Menu.Toggle("SkipVideos", true);  // a switch in the Mods tab, on by default
        if (Harmony.PatchEach(new[] { GameTargets.IntroVideos },
                prefix: new HarmonyMethod(typeof(Plugin), nameof(IntroPrefix))).Count == 0)
            Logs.Warn("the game's intro step not found: the videos play");
    }

    static bool IntroPrefix(GameIntroStep __instance)
    {
        if (skipVideos?.On != true)
            return true;                               // the switch is off: the game runs its own code
        __instance.Done = true;                        // what the video player's callback would set
        return false;                                  // and the step does not start the player
    }
}
```

- The GUID (`ModId`) is `nobirds.oldenera.<id>`. **`<id>` (the last part of the GUID) names the mod's texts**; the config file is
  `BepInEx\config\<GUID>.cfg`.
- Use `OnLoad`, never `Load` (it is sealed in `ModBase`). Patch through `Harmony` from the base class, never
  `new Harmony`: the base class unpatches it all on unload.
- Read a switch where the feature works (`skipVideos.On`), not once in `OnLoad`: `On` is false while the whole mod is
  switched off or the mods do not work now (section 3).
- Patch methods are static and take the game's own types: `__instance`, `__result`, `__args`, the game's parameters.

### The texts

Every string the player sees is a key in `lang/<language>.json`, never a literal in code. The file is in the game's own
format; `<language>` is a `Hex.UI.ELang` name (`english`, `russian`, `german`, `zhcn`, ...). A language without a file
falls back to English, then Russian.

```json
{
"tokens": [
{"sid":"oe.skipintro.title","text":"Skip intro"},
{"sid":"oe.skipintro.SkipVideos","text":"Skip the videos on start"},
{"sid":"oe.skipintro.SkipVideos.desc","text":"The studios' logos and the trailer are not shown ..."}
]
}
```

- `oe.<id>.title`: the mod's name in the Mods tab. Required.
- `oe.<id>.description`: one or two sentences on what the mod does. It is the mod's description in the Mods window and in
  the catalog (`publish-mods.ps1` passes the lang folder to `tools/catalog --lang`, which copies `title` and `description`
  of every language into the mod's meta file). Give it in every language file; `<id>` here is the last part of the plugin GUID.
- `oe.<id>.<Key>`: the label of the switch made with `Menu.Toggle("<Key>", ...)`; `oe.<id>.<Key>.desc` its hover text.
- For `Menu.Choose`: `oe.<id>.<Key>` the label and `oe.<id>.<Key>.<EnumValueName>` each option.
- The game's font has no `≈`.
- Keep the game's own words for its terms (its texts are in `Core.zip`, `Lang/<language>/texts`).

### Build and try

```powershell
.\mod-skip-intro\build.ps1 -Dev      # into <game>\BepInEx\devplugins; the game may run
```

Section 5 has the rest. The mod's lines are in `<game>\BepInEx\LogOutput.log`, under its plugin name.

## 3. The framework

### ModBase (`mod-common/ModBase.cs`)

Your plugin derives from it. On `Load` it:

- makes `Harmony` (named by the GUID), `Logs` (a `ModLog`: `Info`, `Warn`, `Error`, `Once`, `Report`, `Guard`) and
  `Menu`;
- loads your `lang/*.json` (`Texts.Load`);
- **checks the game version**: the generated `GameNames.g.cs` compiled into your DLL carries the game version it was
  made for (`GameNames.GameVersion`). If `Application.version` differs, `OnLoad` is not called, the log says why, the
  main menu shows a notice, and the mod's row in the Mods tab says it is off. (`NeedsGameNames => false` skips the check;
  only the research plugin does that.)
- calls `OnLoad`. If it throws, the mod unloads at once: half loaded is worse than off.

On `Unload` (hot reload, section 5) it calls `OnUnload`, removes the Harmony patches, ticks, event handlers and the
menu, and destroys scene objects whose names start with a prefix from `ObjectPrefixes`.

Rules that follow:

- Whatever you changed in the game's own objects, put back in `OnUnload`.
- Name the scene objects you create with a prefix and return it from `ObjectPrefixes` (`new[] { "OE_Counter" }`).
- **Do not write your own `MonoBehaviour`s**: a reloaded build cannot register the class a second time. Use `OnTick`.
- `HasMenu => false` for a mod with no switches (the core).
- `DebugFlag("Name", "description")` is a developer flag in the config's `[Debug]` section, not in the menu.

### ModMenu (`mod-common/ModMenu.cs`)

The mod's entry in the **Mods** tab of the game's settings (drawn by `mod-settings`). `Menu` already has a master switch
for the mod; you add rows. Values live in the mod's config file, section `[Menu]`.

```csharp
Switch highlight = Menu.Toggle("Highlight", true);                    // highlight.On
Choice<MarkersShown> shown = Menu.Choose("Shown", MarkersShown.Alt);  // shown.Value(MarkersShown.Never)
enum MarkersShown { Always, Alt, Never }                              // the options, in this order
```

`Choice<T>.Value(whenOff)` gives the chosen value, or `whenOff` while the mod is switched off.

**"When the mods work"** is the first row of the tab: single player only, single player and hotseat (the default), or
never (`ModMenu.Mode`, file `BepInEx\config\OldenEra.Mods.cfg`). Online games and replays are never included, so the
mods give no advantage there. `Switch.On` and `Choice.Value` already obey it. A feature built outside them should check
`ModMenu.ModsWork`. The answer "is it single player" comes from the core; without the core the game counts as single
player.

### Texts (`mod-common/Texts.cs`)

`Texts` puts your keys into the game's own localization table, so the game's widgets show them by key and switch them
with the game's language.

- `Texts.Get("oe.myid.Key")`: your text in the game's current language, for places where you set a string yourself
  (a tooltip line, a label you draw). Unknown key: the key itself comes back.
- `Texts.GetIn(language, key)`, `Texts.Has(key)`, `Texts.Add(key, russian, english)` for a text made in code.
- The game's own text of its key: `Game.Translate(key)` (shared code).

### The core's events (`ModEvents` in `mod-common/Events.cs`; `mod-core/`)

Patching the same game method from several mods is fragile. So **`mod-core`** (`nobirds.oldenera.core`) patches the game
once for all mods and raises typed events. A mod subscribes:

```csharp
protected override void OnLoad()
{
    On(ModEvents.MapLoaded, _ => { checksLeft = 5; });   // a state event: a mod loaded later gets the last one at once
    On(ModEvents.SquadTooltipFilled, fill => Logs.Guard("tooltip", () => Show(fill)));
    OnTick(Tick);                                        // every frame, on the main thread
}
```

- Events (see `ModEvents`): `MapLoaded`, `MapClosed`, `HeroSelected`, `LanguageChanged`, `AltInfo`,
  `SquadTooltipFilling`/`Filled`, `TooltipTexts`, `SkullsFilled`, `DwellingTooltipFilling`/`Filled`, `EncounterStarting`,
  `Encounter`, `TooltipShown`/`Hidden`/`LateUpdate`/`Place`, `ForecastBuilt`, `OtherForecast`, `ForecastShown`,
  `CounterDecided`. Each has its own context class in `Events.cs` and `shared/EventTypes.cs`.
- A handler's exception goes to your mod's log, once per handler. A switched-off mod gets no events.
- **Such a mod needs the core installed.** Without it the events never come; `On` says so once in the log. In the
  catalog the mod lists `nobirds.oldenera.core` as required (section 6). A mod that only patches the game itself
  (like skip-intro) needs nothing.
- To add an event, patch the game in `mod-core/Plugin.cs` and add a `GameEvent<T>` to `ModEvents`.
  `OldenEraCommon.dll` changes need the game restarted.

`mod-fixes/Plugin.cs` is a short example of an event plus a tick; `mod-map-tooltip/Plugin.cs` of several events.

### UI helpers (`mod-common/shared`)

A window or a panel of a mod is best made from the game's own widgets, so it looks and behaves like the game. These
helpers are in the library; each file starts with a short usage example. `mod-settings/ModsWindow*.cs` (the
Mods window, a copy of the game's Load screen) uses all of them.

- `ModScreen` — a screen of your own in the game's screen registry (issue #24): `Create(name, sortingOrder)` makes it
  (its canvas is `Content`), `Open`/`Hide`, `Escape` (hides by default) and `Closed`; the game puts it on its stack of
  open screens, gives it the UI camera, the input and Escape, and takes the clicks from the screen below. It registers
  again after a mode change emptied the registry. `Destroy` it in `OnUnload`. Prefer it to a window of your own.
- `MainMenu` — a button of your own in the game's main menu: `AddButton(name, labelKey, click, view => view.someButton)`
  copies the menu's Load button under the given one (below its sub-menu), labels it by your key and clicks it as the
  game's own top-level buttons (selection cleared, an open sub-menu closed). A new main menu (after a game) has none:
  add it again when the returned button is gone; `Remove` it in `OnUnload`.
- `ScreenCopy` — a game screen copied as a window of your own: `Make` copies it with the screen's own scripts taken
  off and switches on what the hidden screen keeps off (raycasters, scrolling, controls, dropdowns, layouts);
  `ModScreen.Adopt` makes the built copy a screen of the game's registry; `DrawOnTop` puts a part over the rest.
- `SettingsTab` — a tab of your own in the game's settings: `Add(name, labelKey)` (null until the settings
  screen is loaded) copies a section with a settings type of its own and a tab of that type into the screen's arrays,
  so the game switches, shows and hides it as its own; `List` takes your rows, `NewTitle` makes a title from the
  game's row factory, `SwitchRow`/`ChoiceRow`/`ButtonRow` are templates of the General section's rows; `Shown` is
  called when the game shows it (Apply and Reset are hidden there). `Remove` it in `OnUnload`.
- `SaveRow` — a row of the game's save list for data of yours: `Make(parent, name)` instantiates the row prefab the
  Load screen's list makes its rows of, with its own script (the game lays it out and plays its hover and selected
  looks); `Name`, `Icon`, `Tags` (and `TagsUnderName`), `Info` (the three lines on the right), `Selected`, `Check` /
  `NoCheck`, `OnClick`. `SaveRow.Spacing` is the list's gap between rows.
- `GameBoxes` — the game's message box (`Confirm`: a question with two buttons) and input box (`Ask`: a line of text),
  one call each with callbacks; `AnyShown` tells whether one is on screen.
- `Pictures`, `PictureCache` — PNG or JPG files loaded once into textures; shown filling a frame (`Cover`, cut) or whole
  in an area (`FitInside`), the aspect kept.
- `GameUi` — the Load screen's filter (`SetUpFilter`, `Ticked`), a tag chip (`ShowChip`), a dropdown's options
  (`SetDropdownTexts`), a check box (`Check`), through the game's own methods.
- `UiLayout` — `NewRect`, `Stretch`, `Size`, `HRow`, `Widen`, `Clear`, `Hide`, `Clickable` for building UI next to
  the game's widgets (`using static Nobirds.OldenEra.Modding.Common.UiLayout;`).

## 4. Using the game's code

**The main rule: use the game's own functions, do not rewrite its logic.** Damage, danger, joining, fleeing, visits,
widgets, sprites, fonts and texts come from the game: find its method and call it. Write your own only when the game
has none, and say so. First look at how the game does the same thing in a similar place.

### The generated names

- A search finds a member of the game by **signatures and the game's code**, not by its obfuscated name. The searches
  are in `mod-common/shared/search/*.cs`.
- At build time `tools/game-names` runs them over the game's files (no running game) and writes
  `mod-common/shared/GameNames.g.cs`, which is in git: type aliases (`GameIntroStep = ccj`), extension members with
  readable names (`introStep.Done`), patch targets (`GameTargets.IntroVideos`), constants, and the game version.
  `Directory.Build.targets` runs it when the game's files, the searches or the pins are newer than the file.
- Zero or several candidates stop the build with `GN0002` and the candidates listed. Choose one in
  `mod-common/shared/GameNames.pins.txt` (`GameHero.Data = fgd.cwtl  # why`). `GN0001`: BepInEx's wrappers are older
  than the game; start the game once.
- Obfuscated names appear only in comments, with the version: "`ccj.qrx` on 0.82.7".
- Mod code uses only the generated names and the readable wrappers in `mod-common/shared/Game*.cs` (`Game`,
  `GameBattle`, `GameTooltip`, `GameVisits`, `GameEncounter`, `GameSettingsUI`). No searching by obfuscated name in a mod.
- After a game patch, `git diff` on `GameNames.g.cs` shows what was renamed.
- **Public and internal.** A generated name is public when it shows no obfuscated class: `row.ShowIcon(sprite)` on the
  readable `BhSessionInfoElement` stays the same on the next version (the library is made again for it). One that shows
  an obfuscated class (`GameHero`, `GameSaveRowPresenter`: the aliases of `GameTypes.g.cs`) is internal, and so are the
  handwritten members that show them. The library's public members never show one: wrap it (`SaveRow` hides the
  row's presenter).
- **The plugins that work on the obfuscated classes** (the core, the research console, the fixes, the skip intro, the
  battle counter, the squad tooltip, the visited buildings, the map badges) compile `GameTypes.g.cs`, which stamps them
  with the game version (`AssemblyMetadata("Nobirds.OldenEra.GameVersion")`), and see the library's internals
  (`InternalsVisibleTo` in `OldenEraCommon.csproj`). They are released again with every patch; the others are not.

### Adding a search: `mod-common/shared/search/GameIntro.cs`

```csharp
internal static partial class Game
{
    // The video player's Play(ClipInfo[], bool, Action): found by its readable signature.
    static readonly Lookup<MethodInfo> playVideos = Lookup<MethodInfo>.Among("play videos (ClipInfo[], bool, Action)",
        () => Types.Methods().Static().Named("Play").Returns(typeof(void))
            .Takes(typeof(Il2CppReferenceArray<Hex.Videoplayer.ClipInfo>), typeof(bool), typeof(Il2CppSystem.Action)),
        Members.Name, onlyOne: true);

    // The start's step that calls it: a void M() of a class that has an EResualt method, whose code calls Play.
    static readonly Lookup<MethodInfo> introVideos = Lookup<MethodInfo>.Among("intro videos step",
        () => playVideos.Value is { } play
            ? Types.Where(t => t.Methods().Instance().Returns(typeof(Hex.InitScene.EResualt)).Takes().Any())
                .Methods().Instance().Returns(typeof(void)).Takes()
                .Where(m => GameCode.Calls(m).Any(c => GameCode.At(c.target).Contains(play)))
            : null,
        Members.Name, onlyOne: true).GenerateTarget("IntroVideos");        // -> GameTargets.IntroVideos

    // The class: a type alias GameIntroStep.
    static readonly Lookup<Type> introStep = Lookup<Type>.Among("intro videos step class",
        () => introVideos.Value is { } run ? new[] { run.DeclaringType } : null, t => t.Name).Generate("GameIntroStep");

    // Its one own bool property: an extension `Done`, writable.
    static readonly Lookup<PropertyInfo> introDone = Lookup<PropertyInfo>.Among("intro videos step done",
        () => introStep.Value is { } step ? Properties(step).Where(p => p.DeclaringType == step && p.PropertyType == typeof(bool)) : null,
        Name, onlyOne: true).Generate("Done", writable: true);
}
```

- `Lookup<T>.Among(what, candidates, describe, onlyOne)`: `what` names it in the log; `onlyOne` makes several candidates
  mean "not found" (the generator stops on them).
- Finish with `.Generate("Name")` (a type alias, a property or method as an extension, a static in `GameNames`),
  `.GenerateTarget("Name")` (a method for `GameTargets`), `.GenerateAll("Name")` (a list) or `.GenerateConstant("Name")`
  (a number read from the game's code). `writable: true` makes a property settable; properties are read-only otherwise.
- Helpers: `Types`, `Types.Methods().Static()/Instance().Named().Returns().Takes()`, `Properties(type)`, `GameCode.Calls`,
  `GameCode.At`, `DistinctCode()`. Simple cases: `GameProperty<TOwner, TValue>`, `GameMethod<TOwner, TResult>`,
  `GameAction<TOwner>` (in `search/Members.cs`).
- Search code in `search/` never uses the generated names; the rest of `shared/` is written on top of them.
- Put a readable wrapper over the generated names in a `Game*.cs` file in `shared/` (an `extension(T x) { ... }` block,
  C# 14).
- **Patch with `PatchEach` / `PatchOne`** (`shared/Patches.cs`): each native address once; a method whose machine code is
  shared with others is skipped (and the log says so), because the patch would run for all of them.
  `Harmony.PatchOne(method, "what it is", prefix: ..., postfix: ...)` returns false and logs when the method is
  missing. Never use Il2CppInterop's `XrefScanner` on arbitrary methods: it crashed the game twice.

### The research console (`mod-research`)

A development plugin: a console on `127.0.0.1:8766` (key in `BepInEx\research-token.txt`, sent as the
`X-Research-Token` header) and the file `BepInEx\research.txt`. It finds out what a game method does without a rebuild.
`eval` reads values and calls methods of live objects, `members` lists a type, `code` shows a method's instructions
with the names of its calls, `callers` finds who calls a method, `tree` and `click` look at and press the live UI,
`shot` writes a screenshot. `eval` with a method call really runs the game's code, so use it with care. Commands and
the order that worked: [process.md](process.md), "Researching game code".

## 5. Build, run, iterate

| Command | What it does |
|---|---|
| `.\mod-x\build.ps1` | build only: `bin\Release\net6.0\<Dll>.dll` |
| `.\mod-x\build.ps1 -Dev` | build and copy into `<game>\BepInEx\devplugins`; **the game may run** |
| `.\mod-x\build.ps1 -Install` | build and copy into `<game>\BepInEx\plugins`; **refuses while the game runs** |
| `.\mod-x\build.ps1 -Uninstall` | remove it from `plugins` |
| `.\install.ps1` | build and install all mods (the list is in the script; add yours there) |

- **Hot reload.** `mod-devloader` (itself in `plugins`) loads the DLLs in `devplugins` and reloads one when its file
  changes: `DEV unloaded <dll>` and `DEV loaded <name> <version>` appear in the log after a second or two. It works only
  for mods on `ModBase`. The first `-Dev` of a mod that was in `plugins` must be done with the game closed (it moves
  the DLL). Old assemblies stay in memory; that is fine for a session.
- **Not reloadable:** `OldenEraCommon.dll` (`ModBase`, `ModMenu`, `Texts`, `ModEvents`, `Ticker`) and `mod-devloader`.
  Install them with `-Install` and restart the game. The shared code (`mod-common/shared/`) is in the library too: a
  change to it needs a restart; a mod's own code reloads.
- **Logs:** `<game>\BepInEx\LogOutput.log` (BepInEx) and
  `%USERPROFILE%\AppData\LocalLow\Unfrozen\HeroesOldenEra\Player.log` (Unity). Wait for `Chainloader startup complete`.
  What the library's own log (`Olden Era Library`) collects (what each lookup found, `Patch: ... not patched`) is there too.
- Start the game from Game Pass: `Start-Process "shell:AppsFolder\HoodedHorse.HeroesofMightandMagicOldenEra_znaey1dw2bdpr!Game"`.
- After a game patch: start the game once to the main menu, then rebuild. The build makes the names for the new
  version; fix the searches that stopped it, check `git diff`.

## 6. Publishing

- A new mod joins the list in three scripts: `install.ps1` (`$parts`), `release.ps1` (folder to DLL name), and
  `publish-mods.ps1` (`$catalogParts`: the DLL path). Its id, name, version, tags and requirements are read from its
  csproj (`ModId`, `ModName`, `Version`, `ModTags`, `ModRequires`).
- `.\publish-mods.ps1 -Mods <id>` builds the mod and writes its catalog files into `release\catalog-preview`; nothing is
  published. `-Publish` makes a GitHub release `<id>-v<version>` (the mod's package, `-t:ModPackage`, is the asset) and a pull request with
  `catalog\`. A version is published once: change the mod and **raise its `Version` in the csproj** to publish again.
- Tags: `ModTags` in the csproj, `;` between, for the catalog (`map`, `battle`, `tooltip`, `ui`, `fix`,
  `qol`, `library`, `info`, `squads`, `buildings`; the Mods window filters by them, the names of the known ones are the `oe.manager.tag.<id>` keys of
  `mod-manager/lang`, so a new tag needs those keys in every language file).
- Screenshots: put PNG or JPG pictures (up to 2 MB each) in the mod's `mod-<id>/screenshots/` folder. They are listed in
  the catalog sorted by file name and served from the repository's `main` branch. An optional
  `screenshots/captions.txt` gives English captions, one line `<file name>=<caption>` (a `|` in a caption is replaced).
  A mod without that folder keeps the screenshots its meta file already has.
- The catalog's icon (`catalog/icon.png`) is added to the root file by the script; a mod needs no icon of its own.
- The catalog, its files and the mod manager: [catalog.md](catalog.md). Releases of the whole set and the rules for
  versions: [building.md](building.md).
- The repository is public. `main` changes only through a pull request that passes `tools/check-public.ps1` (no
  personal paths, names, secrets, game files or built files). Install the hook with `.\tools\install-hooks.ps1` to run
  the same check before a push.
