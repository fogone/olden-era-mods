# Discord post draft (Unfrozen, Olden Era mods channel)

A draft, not posted. The channel's rule is one mod, one thread, updates in the same thread.

**The plan:** one thread for the mod manager first (it is how every other mod is installed and updated, and it lists
them), then a thread of its own for each mod once it has screenshots, each linking back to the manager's thread.

---

## Thread 1

**Title:** `Olden Era Mod Manager: install, update and remove mods in game (+ tooltips, map badges, battle forecast, skip intro)`

**Tags (pick from the channel's):** tools / utility, UI, quality of life

**First message:**

```markdown
# Olden Era Mod Manager
A **Mods** button in the main menu: a window to install, update, switch off and remove mods, from an open catalog on GitHub. No unzipping by hand after the first install; mods switch on and off without restarting the game. Nothing is installed or updated without your click.

The catalog has these mods (all optional, all free, MIT):
- **Squad tooltip**: a map squad's tooltip says "Want to join" or "Want to flee" instead of the battle difficulty, when the game itself would have it join or flee.
- **Visited buildings**: a building's tooltip shows visited / available and how often it can be visited, its guards with the fight's danger; unvisited objects get an outline.
- **Map badges**: badges over the map for the selected hero: unvisited buildings, mines, pickups, dwellings, guards' danger, squads that would join or flee. Always, while Alt is held, or never.
- **Battle counter**: the damage forecast as the attack plays: counterattack before the strike, the strike, the counterattack after, Double Strike's second hit.
- **Map tooltip**: a map object's tooltip no longer covers the object or the HUD panels.
- **Game fixes**: dwelling growth lost after loading a save (fixed by the developers in 0.82.08; the fix is for 0.82.07).
- **Skip intro**: no studio logos and trailer on start.

**What it changes:** the tooltips, badges and forecast only show what the game itself already decides (whether a squad joins, whether a defender strikes back, whether a building was visited): the mods ask the game, they do not calculate anything their own way. They do not change rules, AI or balance. Game fixes and Skip intro are the only ones that change something, each behind a switch.

**Single player only.** By default every mod is off in online games, hotseat and replays (Settings → Mods → "When the mods work"). Please do not use them to get an advantage over other players. Hidden numbers (exact counts, Diplomacy points) are off by default.

**Tested on:** game version 0.82.07 (Game Pass), BepInEx 6.0.0-be.788 (IL2CPP).

**Install**
1. Unpack **BepInEx 6, IL2CPP x64, build 6.0.0-be.788** into the game folder (the one with `HeroesOldenEra.exe`): <https://builds.bepinex.dev/projects/bepinex_be>
2. Start the game once, wait for the main menu (the first start takes about a minute), close it.
3. Unpack `olden-era-mod-manager-<version>-for-game-0.82.07.zip` from the latest manager release into the game folder: <https://github.com/fogone/olden-era-mods/releases>
4. Start the game: main menu → **Mods** → Install the ones you want. Their options are in Settings → Mods.

**Uninstall:** remove the mods in the Mods window, then delete `OldenEra*.dll` from `BepInEx\plugins` and `BepInEx\patchers` and the folder `BepInEx\oldenera-mods`. To remove BepInEx too, delete its files from the game folder.

**Known issues**
- Each build is for one game version. After a game patch the mods stay off (the main menu says so) until a build for the new version is in the catalog.
- Does not work with the Nexus "BepInEx 6 with version-independent deobfuscation" or any `UnhollowerDeobfuscationRegex` in `BepInEx.cfg`.
- Tested on Game Pass only; Steam should be the same game version but is not tested.
- Game fixes and Skip intro are in English and Russian only (the game's other languages show them in English); the rest is in all 16 of the game's languages.

Source, guide and issues: <https://github.com/fogone/olden-era-mods>
```

Attachments: 2–4 screenshots (see below).

---

## Later threads, one per mod

Same shape, shorter: title with the mod's name and what it does (for example
`Squad tooltip: "Want to join" / "Want to flee" instead of the battle difficulty`), what it changes, tested on,
"install it from the Mod Manager (link to thread 1)", known issues, screenshots.
