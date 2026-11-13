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

## Principles of the SDK

What the framework (and a mod on it) does, and in what order, whatever is added: a menu row, a text, a picture, a
faction, a model. Check a design against these before writing it.

1. **The game's own functions, not its logic rewritten.** A number, a decision, a widget's filling, a sprite, a font,
   a text is taken from the game: find the method that does it and call it. Look first at how the game itself does
   the same thing in a similar place. Own code only where the game has no such function, and say so.
2. **Into the game's mechanisms, not around them.** Find how the game itself defines and loads the thing (its data
   files, its tables of texts and pictures, its registry of screens, its menu, its caches) and add ours through the
   same mechanism, so the game treats it as its own. A faction: how the game defines its own factions, then a new one
   added the same way. Copying the game's live objects, or hiding the game's thing and drawing ours over it, only when
   there is no mechanism, said so in the code.
3. **The resources of a mod from the mod's folder, through the game's loaders.** The game loads its data and assets
   by path (its data roots, `Resources`, its sprite tables, its localization). The framework joins those loaders once,
   so a path can also name a mod's file, and every place of the game that loads by that path takes the mod's resource;
   not a hook in each place that shows it.
   **Which loader:** look at what the game's code itself calls before deciding. A call into the engine (Unity's
   `Resources.Load`) may sit under a resource manager of the game's with logic of its own (paths, caches, pools,
   fallbacks); join the game's abstraction of finding a resource, not the engine function under it, nor the place
   above it that shows the result. Follow the calls from a place that loads (who calls whom, what it caches) to the
   layer that answers "where is the resource of this path", and join that layer.
4. **A hook only where no mechanism exists**, at the narrowest point the game passes through, with a comment why.
   Hooks are installed once for everyone (the core) and mods subscribe to its events.
5. **Mods ask the framework, not the game.** A mod adds a menu row, a tab, a screen, a text, a picture, a faction
   through the framework's API or as data; it does not reach into the game's objects. A mod of data alone needs no code.
6. **Game names are generated, not written.** Obfuscated names change with every patch: code is written on the
   generated names (`GameNames.g.cs`) and readable aliases (`Game*.cs`), not on obfuscated names or reflection.
7. **Next to other mods.** Whatever a mod adds goes on top of the untouched game and of the other mods: lists are added
   to (`Add\`), not replaced; nothing of the game's files is changed or copied out.
8. **Every visible string through localization**, and hidden numbers of the game shown only when a setting asks.

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

### Game data

A mod can bring the game's own data: files under `data\` of the project, laid out as the game's `Core.zip`
(`data\DB\fractions\7_myfaction.json`, `data\DB\units\units_logics\...`; unpack `Core.zip` from
`<game>\HeroesOldenEra_Data\StreamingAssets` to see the formats). `-t:ModPackage` packs them into `data\<id>.zip`; the
mod manager mounts it as one of the game's data roots when the game starts, after the game's own data:

- an entry with a new `id` is added (a new unit, hero, city, faction);
- an entry with an `id` the game has replaces it whole: copy the whole entry, then change it;
- a single-object file (`DB/data.json`, the key/value lists like `DB/city_music_sounds.json`) is merged field by field:
  write only the fields you change, an array whole. To add to an array, write the addition instead (`Add\` below): two
  mods that both write the whole array would drop each other's entries.

The game reads its data once, at the start: switching such a mod on or off, or updating it, counts from the next
start. Data changes the game's rules, so such a mod is for single player and hotseat only; a save made with its
content needs it installed.

What the game does not read from a mod's data the library reads, so a mod of data alone needs no code
(`<ModCode>false</ModCode>` in the csproj: the package without the DLL):

| In `data\` | What it does |
|---|---|
| `Lang\<language>\**\*.json` | the mod's texts, in the game's format (`{"tokens":[{"sid":"...","text":"..."}]}`, as `Core.zip`'s `Lang\english\texts\*.json`); `<language>` is the game's (`english`, `russian`, ...). A JSON field that names a text key (`"name": "myfaction_name"`) shows it |
| `Sprites\<folder>\<name>.png` | a picture among the game's, under `<name>` in the game's table of that Resources folder: `Sprites\icons\units\hex_portraits\myfaction_knight.png` is the portrait of the unit `myfaction_knight`, `Sprites\icons\fractions\fraction_myfaction.png` the icon a faction's `"icon": "fraction_myfaction"` names |
| `Lobby\<faction>.json` | the faction in the lobby: `{"like": "human"}`, its lobby pictures those of that faction |
| `Lobby\<faction>\<picture>.png` | ... except these: `icon`, `slotFon`, `statisticFon`, `versusFon`, `bigIcon`, `card` |
| `Add\<path>.json` | what the mod adds to the game's single-object file `<path>`: `Add\DB\data.json` with `{"fractions": ["myfaction"]}` adds to that list. An object is added to field by field, an array gets the elements at its end; an element already there (the same value, an object with the same `key`, `sid` or `id`) is replaced. Every mod's additions go into one data root of the library's, after all the others |
| `Copy\<faction>.json` | the faction's parts copied from a faction of the game, below |
| `Bundles\<bundle>` | an AssetBundle of the mod's (models, sounds...), built with the game's Unity version (6000.0.66): the game's resource manager loads it as its own, and what is under `Assets/Resources/` in it the game finds at its path. Its materials are put on the game's shaders of the same names. Below |
| `Models\<path>.json` | a model of the game's kind made from a model of the mod's bundles, found by the game at `<path>`. Below |

The folders of the game's pictures are the Resources paths in `<game>\HeroesOldenEra_Data\globalgamemanagers`
(`icons/units/hex_portraits/angel`, `icons/heroes/...`, `icons/cities_buildings/...`). In code the same is
`Sprites.Add(folder, sprite)` / `Sprites.FromPng(bytes, name)` and `Factions.AddLobby(id, like, pictures)`.

#### A new faction

A faction is many entries in many files: its units and their views, heroes and their specializations, its city and
the city's map object, dwellings, laws, the faction skill with its sub-skills and roll tables, sub-classes, and about
650 map squads (the map generator looks for those of the city's faction). Their ids are not all made from the
faction's id (`skill_faction_humans`, `humans_might_skills_table`), so a faction mod copies them from a faction of the
game instead of writing them: `data\Copy\myfaction.json`

```json
{
  "like": "human",
  "words": { "humans": "myfaction" },
  "files": [
    "DB/units/units_logics/humans/", "DB/units/units_views/humans/", "DB/heroes/humans/",
    "DB/heroes_specializations/specializations_human.json",
    "DB/heroes_skills/skills/skills.json", "DB/heroes_skills/sub_skills/sub_skills.json",
    "DB/heroes_skills/skills_by_level_tables/", "DB/heroes_sub_classes/sub_classes_human.json",
    "DB/fractions_laws/fractions_laws_table_human.json",
    "DB/objects_logic/cities/human_city.json", "DB/objects_logic/hires/barracks.json",
    "DB/map/objects/4_interactables.json", "DB/squads/squads_human/"
  ],
  "names": { "human_name": "myfaction_name" }
}
```

- From the files given (a path ending in `/` is a folder: every file under it), the entries of the faction are copied:
  those whose `fraction` (or `faction`) is `like`, or whose id has a word of `words` (words are the parts between
  `_`; `like` is always one), and the entries with the id of one of those (a unit's view has the unit's id).
- A copy's id has the words replaced (`human_hero_1` → `myfaction_hero_1`, `skill_faction_humans` →
  `skill_faction_myfaction`); an id without such a word gets the faction's id in front (`esquire` → `myfaction_esquire`).
  `names` gives an id (or any string) its new value instead.
- In the copies, every string that is the id of a copied entry becomes the copy's id (a hero's start squad, its
  specialization and skill table, a city's units), `like` becomes the faction's id, and a string of `names` its value.
  The rest (text keys, pictures, ids not copied) stays: the copies show the game's pictures and texts. The texts the
  game names by an entry's id (a hero's name is the text of its id, its motto `<id>_motto`) are put in under the
  copy's id too, until the mod's `Lang` has its own; so are the pictures it names by an id (a unit's portrait), until
  the mod's `Sprites` has its own.
- An entry the mod's own `DB` has (the same id as a copy's, in the same folder: a unit's view of the mod's is not its
  logic) is not copied: the mod's wins. Refer to copies by their ids
  (`fraction_law_myfaction_3` in the faction's `fractionLawsLines`).
- The copies are written into the mod's data folder (`~oe-copies.zip`) at the start: the game takes some of a
  faction's entries only from the data root that has the faction (the random hero of the lobby is one of its heroes
  from that root).

Besides the copies, a new faction is its `DB\fractions\` entry (its name, icon, laws, city names; written by the mod),
`Add\DB\data.json` with it in `fractions` and `availableFractions`, `Add\DB\city_music_sounds.json` and
`city_ambient_sounds.json` with its key (shaped as the game's files: `{"array": {"data": [{"key": "myfaction", "val":
{...}}]}}`), its texts, pictures and lobby entry (#56). `DB/biomes_info.json` maps one
faction to each biome; adding the faction to a biome of the game (`{"key": "Grass", ...}`) would replace that biome's
faction, so leave it out.

#### Models

The game takes a unit's models by path through its resource manager (`Hex.ResManager`): the map model is
`units/` + the view's `mapMesh`, the battle model and the model in the hire and unit windows `units/` + its `mesh`.
The manager looks a path up first among the assets of the AssetBundles it loaded (`assets/resources/<path>`), then in
the game's `Resources`. The game ships no bundles; a mod's `Bundles\` are loaded by that manager the moment the game
shows its first frame, so the mod's assets are found the same way as the game's (#62): a bundle built from
`Assets/Resources/units/myfaction/goblin.prefab` is the view's `"mesh": "myfaction/goblin"`.

**A prefab of the game's kind.** The game reads its own components on the prefab, so the mod's prefab carries the same
ones, laid out as the game's. A battle unit (checked 12.11.2026 with a goblin in a battle and in the hire window):

```
goblin                 UnitViewHelper (projectileSpawnPoint, projectileEndPoint, shieldPoint, shieldScale,
                       meshRenderer, idleVFX), PlayableDirector, BhBattleTimelineHelper (director; its
                       startAttackTimelines and impactAttackTimelines by ability, may be empty), BhParticleAccelerator
  Scale_roll_pos       the first child: the game sets its scale to the view's "scale" (and turns the model, a turn on
                       it stays: a model facing another way than the game's is turned here)
    goblin_model       Animator (an override of the game's battle controller), UnitAnimationEvents (the sounds the
                       clips call), the skinned mesh
```

Plain attacks need no timeline; the times of an attack, of being hit and of dying are the view's (`attackTimeBefore`,
`attackTimeAfter`, `takeDamageTime`, `deathTimeOffset`) and should match the model's clips.

**The game's components by readable names.** A bundle refers to a component by its script: assembly, namespace, class.
Some of the game's classes have names the obfuscator changes with every patch, so a mod's bundle names them in
namespace `OldenEra` by what they are, and the library puts the running game's names in when it takes the bundle
(`BundleNames`, once, into `BepInEx\cache\oldenera-mod-assets`); the bundle then works on every game version the
library knows:

| In the bundle (assembly `Hex.dll`) | What it is | The game's class on 0.82.08 |
|---|---|---|
| `OldenEra.UnitViewHelper` | a battle unit's points, skinned mesh, idle effects | `emp` |
| `OldenEra.UnitAnimationEvents` | the receiver of a unit model's animation events (sounds) | `cf` |

The classes the game names readably (`Hex.Session.Battle.BhBattleTimelineHelper`, `BhParticleAccelerator`) are
referred to as they are. In a Unity project they are stub scripts of those names with the game's serialized fields,
in an assembly definition named `Hex`.

`Models\<path>.json` makes a model of the game's kind from a mesh without the game's scripts, by copying a prefab of
the game and putting the mod's animated model into it:

```json
{ "like": "units/Dungeon/1Prefabs/troglodyte_map", "model": "goblin/goblin", "scale": 1 }
```

It is what the map model of the first test used; a prefab of the game's kind in the bundle is the way.

A building on the map (a town, a dwelling) is a prefab of the game with its scripts and points too: the map object's
`"prefs": ["interactive/<name>"]` is loaded from `objects/interactive/<name>`. `"part"` names the part of the game's
prefab the mod's model replaces (default: the animated one); the model is put as big as that part, its base centred on
the part's, and its materials on a shader the game does not have are drawn with the part's material (the game's
`Hex/Lit`) and their own texture:

```json
Models\objects\interactive\myfaction_city.json
{ "like": "objects/interactive/dungeon_city", "model": "myfaction_town", "part": "dungeon_city_map" }
```

with the map object copied from the game's town and its `prefs` renamed (`Copy\<faction>.json`, `"names":
{"interactive/dungeon_city": "interactive/myfaction_city"}`). Checked 13.11.2026 on 0.82.10 with a town building of
Golden-Era's 3D town: in place, at the town's size, drawn with the game's material. The model must be a whole one: that
building, a relief made for the town's camera, falls into shards seen from above.

A model made from a picture: an image-to-3D tool (TRELLIS.2, Hunyuan3D, Meshy...) gives a `.glb`, `oe-bundle model`
(`tools/bundle/README.md`) makes it a bundle's prefab without Unity. Checked 13.11.2026 on 0.82.10: the Stronghold
town of the HoMM3 map (its sprite redrawn large by Gemini, made a model by TRELLIS.2: 95,000 triangles, a 2048 texture)
stands on the map in the prototype's towns. A named part's model stands upright in the prefab: the game's buildings are
made lying (Z up) and turned by their part, a model of Unity's kind is not.

**Without Unity: `oe-bundle`** (`tools/bundle`). From a model prefab of a bundle (an animated skinned mesh) it writes a
unit's battle and map prefabs of the game's kind into the bundle, the game's components by the readable names:

```
oe-bundle units goblin.bundle Bundles\goblin goblin.json
```

```json
{
  "model": "goblin/goblin.prefab",
  "battle": { "path": "units/myfaction/goblin", "turn": 60, "projectileSpawn": "RightHand", "projectileEnd": "Spine1", "shield": "Spine1" },
  "map":    { "path": "units/myfaction/goblin_map", "turn": 195, "scale": 0.96, "mirror": true, "questMarker": 1.55 }
}
```

The map prefab of the game (troglodyte): root with `Hex.Session.World.BhSquadViewHelper` (`questMarkerPoint`),
`Scale_roll_pos` (turned 195°, mirrored, its scale the prefab's own: the map does not use the view's `scale`) with the
model, `FX_empty`, `quest_marker_point`. Checked 12.11.2026: a goblin from a Golden-Era bundle on the map and in a
battle from the tool's prefabs, no `Models\` file. The tool's README has the fields.

A bundle carries its own copies of the shaders its materials use, and a copy of the game's `Hex/Lit` made outside the
game is a stand-in (one pass: no fog of war, no shadows): the library puts the bundle's materials on the game's shader
of the same name. Use lower-case paths: the table's keys are (a mixed-case path was not tried).

#### Town scenes

A town is a scene: a session loads the game's city scenes with it and keeps each in the town's scene registry by its
`EScene` (the city's `sceneName` parsed); opening the town activates the registry's entry. A mod's town scene is a scene
bundle among its `Bundles\` (built from `Assets/.../<scene>.unity`; the game's resource manager loads it like the
others), and the city names it by its name:

```json
{ "id": "myfaction_city", "sceneName": "myfaction_city_scene", ... }
```

The library gives each mod scene an `EScene` number of its own (from 256: Unity takes a number for a build index, so the
scene keeps its name), writes the number into the cities naming the scene, and when a session's town registry is there
loads the scene by its name and adds it to the registry as the game adds its own (the game's entry, `chs(EScene, Scene)`,
taking the scene's roots).

What the scene holds is the game's town (`docs/game-internals.md`): the buildings' manager with a slot per building of
the city's data and a building per level, and the town camera with its controller. `oe-bundle city` (`tools/bundle`)
makes them of a scene's objects: which object is shown at which level of which slot, which camera is the town's and
what it frames; the slots' component is named `OldenEra.BuildingSlot`, the game's name put in by the library. Then the
game does the rest: it shows the levels built, opens a building's windows on a click on it, and draws its town
interface over the camera. Checked 13.11.2026 on the Stronghold prototype in Golden-Era's scene: the town opens with the
game's interface, a click on the town hall opens the building tree, a market built there appears in the scene.

A town modelled in 3D needs no scene built in Unity either: its buildings and surroundings made as prefabs of a model
bundle, `oe-bundle city` puts them into a scene (`models`, `tools/bundle/README.md`) and its camera is made a
perspective one. Checked 13.11.2026 with Golden-Era's 3D Stronghold (its buildings modelled from the HoMM3 art, a
terrain, a sky, effects): the town opens in 3D with the game's interface, a click on the town hall opens the building
tree, fortifications built there appear after the game's building effect.

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
