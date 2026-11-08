# Heroes of Might and Magic: Olden Era mods

[BepInEx](https://github.com/BepInEx/BepInEx) plugins for **Heroes of Might and Magic: Olden Era**: tooltips and map
badges that tell what the game knows but does not show. Everything the mods say is the game's own decision: whether a
squad would join, whether a defender would strike back, whether a building was visited. The mods do not work it out
their own way; they ask the game.

## Please read

- **These mods are not official.** They are made by a player and have nothing to do with the game's developers or
  publisher.
- **The mods do not change how the game plays.** They only tell what the game itself already knows: they are for
  reference and information. The exceptions are **Game fixes**, which fixes the game's own bugs the way the game does
  it where it works (each fix has its own switch), and **Skip intro**, which skips the videos on start.
- **Not for multiplayer.** The author is strictly against using these mods, or anything made from them, to get an
  advantage over other players in a multiplayer game. **In an online game and in a replay the mods never work**: they
  change and show nothing, whatever the settings. In the game's settings, tab **Mods**, "When the mods work" at the
  very top: in a single player game and hotseat (the default; all the players at this computer see the same), only in a
  single player game, or never.

## Installing

**1. BepInEx.** Download **BepInEx 6, IL2CPP, x64, build `6.0.0-be.788`**
(the file `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788+….zip` from [builds.bepinex.dev](https://builds.bepinex.dev/projects/bepinex_be))
and unpack it into the game's folder, the one with `HeroesOldenEra.exe`:
- Game Pass: `C:\XboxGames\Heroes of Might and Magic- Olden Era\Content`;
- Steam: `…\steamapps\common\<the game's folder>`.

The mods need BepInEx's own default config. They do not work with the redistribution "BepInEx 6 (with
version-independent deobfuscation)" from Nexus Mods, nor with any `UnhollowerDeobfuscationRegex` in
`BepInEx\config\BepInEx.cfg`: that setting renames the game's (and Unity's) short names, and the mods do not load.
With it set, empty the value, delete the folder `BepInEx\interop` and start the game again.

**2. First start.** Start the game and wait for the main menu. The first time, BepInEx takes about a minute to make its
wrappers for the game's code. Then close the game.

**3. The mod manager.** From [Releases](../../releases), take the latest **Olden Era Mod Manager** release and its zip
`olden-era-mod-manager-<version>-for-game-<game version>.zip`, and unpack it into the game's folder (it holds
`BepInEx\plugins` and `BepInEx\patchers`).

**4. The mods.** Start the game. In the main menu, **Mods** (under Additional) opens the mods window: the mods of the
catalog, each with its version and state, **Install**, **Update**, a switch and a cross to remove it. Install the ones
you want; they start working at once, without a restart. The core the mods need comes with the first of them. The
manager updates itself the same way (at the next start). Nothing is installed or updated without your click.

The mods' options are in the game's settings, tab **Mods**, in the game's language, with a description of every option
on hover. "mods …" next to the game's version in the main menu means everything is in place; hover it for a table of
the mods and their state.

**Where the mods come from.** The manager reads the catalog of this repository (`catalog/`) over HTTPS and checks every
downloaded file against its SHA-256 in the catalog. More catalogs can be added in the Mods window (by URL, or from
GitHub by `owner` or `owner/repo`; kept in `BepInEx\oldenera-mods\sources.json`); nobody reviews them, add only ones you
trust.

**Removing:** remove the mods in the mods window, then delete `OldenEra*.dll` from `BepInEx\plugins` and
`BepInEx\patchers`, and the folder `BepInEx\oldenera-mods`.

**After a game patch:** the mods are built for one version of the game. On a new version they stay off and say so in
the main menu; the catalog offers the builds for the new version when they are out.

## The mods

| Mod | What it does |
|---|---|
| **Squad tooltip** | In a map squad's tooltip: "Want to join" with the Diplomacy icon, or "Want to flee" with the game's escape icon, in place of the battle difficulty. Optionally, the hidden Diplomacy numbers. |
| **Visited buildings** | In a building's tooltip: visited or available, and how often it can be visited; the building's guards with the fight's danger; a striking outline on unvisited objects. |
| **Map badges** | Badges over the map for the selected hero: unvisited buildings, mines, pickups, unit dwellings, the guards' danger, "would join / would flee" and the danger of squads. Always, with Alt, or never. |
| **Battle counter** | The battle's damage forecast as the attack happens, left to right: a counterattack before the strike, the strike, a counterattack after it, Double Strike's second strike, each for the health left after the hits before it; optionally the stacks' health under each hit. |
| **Map tooltip** | A map object's tooltip does not cover the object itself or the interface panels. |
| **Game fixes** | Fixes of the game's own bugs, each with a switch: the weekly growth from unit dwellings captured before a save, lost after loading it. |
| **Skip intro** | No studio logos and trailer when the game starts: straight on to the main menu. |
| **Mod manager** | The Mods window in the main menu: installs, updates, switches off and removes the mods from the catalog. The Mods tab in the game's settings for all the mods' options, with when they work at all (single player and hotseat, single player, never; never online); the mods' version in the main menu. |

What each mod and each option does: **[player's guide](docs/mods.md)**.

The hidden numbers the game does not show (exact counts, Diplomacy points, the armies' ratio) are off by default.

## Building

To build the mods yourself or to work on them: **[docs/building.md](docs/building.md)** (building, releases, publishing a mod into the catalog with `publish-mods.ps1`). Writing a mod of your own: **[docs/writing-a-mod.md](docs/writing-a-mod.md)**; the catalog's format and hosting a catalog: **[docs/catalog.md](docs/catalog.md)**. Tasks and ideas are in the
repository's [Issues](../../issues).
Changes come as pull requests; each is checked for anything personal, secrets and game files before it can be merged
(`tools/check-public.ps1`).

## License

The code is under the [MIT license](LICENSE). Heroes of Might and Magic: Olden Era, its names, code and data belong to their owners; nothing of the game is in this repository or in the releases (BepInEx makes the interop for the game on the player's machine).
