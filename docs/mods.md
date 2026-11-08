# The mods: player's guide

The mods are not official, and they are not for multiplayer: they only tell what the game already knows, and the
author is strictly against using them to get an advantage over other players (see the [README](../README.md)).

Every mod is set up in the game's settings, tab **Mods**. At the very top, **All mods**, "When the mods work":
- in a single player game and hotseat (the default): hotseat is more than one person playing at this computer, all of
  them seeing the same;
- in a single player game: not in a hotseat game either;
- never: the main menu says "mods … off" next to the game's version.

In an online game and in a replay the mods never work, whatever is chosen: they must give no advantage over other
players. The game says which it is: how its session is played (here, online, a replay) and the map's hotseat sides.

While the mods do not work, every mod is off as by its own switch, and its own switches keep their values. (In the
files: `BepInEx\config\OldenEra.Mods.cfg`, `[Mods] Mode` = `Hotseat`, `SinglePlayer` or `Off`.)

Each mod has:
- a "Mod enabled" switch: a switched-off mod changes nothing in the game;
- its options: hover an option to see its description;
- a Reset button: puts this mod's options back to their defaults.

In the main menu, next to the game's version, stands the mods' version ("0.82.07 · mods 0.1.0"). Hover it for a table of
all the mods: version and state (a green tick: on, a grey cross: off).

The mods show only what the game decided, and only for the **selected hero**. On the map, only what is **scouted**: there
are no badges in the fog of war. The numbers the game hides (exact counts, Diplomacy points, the armies' ratio) are off
by default.

## Squad tooltip

Hover a squad on the map: the tooltip says what happens if the selected hero steps onto it. The game decides: the mod
asks the same questions as the game's encounter, in the same order, and only reads the answers.

| Option | Default | What it does |
|---|---|---|
| "Want to join" with the Diplomacy icon | on | When the squad would join the selected hero, "Want to join" and the Diplomacy icon replace the battle difficulty. |
| "Want to flee" | on | When the squad would offer to flee, "Want to flee" replaces the battle difficulty and the game's escape icon replaces the skulls. |
| An icon instead of the difficulty skulls | on | With "join" and "flee", the Diplomacy icon or the escape icon in place of the skulls. Off: only the words. |
| Diplomacy details where there are any | off | The hidden numbers where there are any: the hero's and the squad's Diplomacy points, how many creatures would join, how many times the hero's army is stronger or weaker. With the game's setting "hide the battle danger" the ratio is not shown. |

With the game's setting "hide the battle danger" the mod says nothing about the outcome either: the game's "danger
hidden" line stays.

## Visited buildings

Buildings on the map remember who visited them (once, per hero, per player, reset weekly or daily), but the game's
tooltip often does not say so. The mod shows it in the building's tooltip.

| Option | Default | What it does |
|---|---|---|
| Visit badges by the building tooltip | on | Left of the tooltip: visited or available (a green tick / an amber "!") and how often it can be visited. |
| Caption "visited · weekly" | on | A line in the tooltip: visited or available and the visit rule in words. |
| Hide the game's "Already visited" line | on | The game's line repeats the visit badge, it can go. |
| Unvisited highlight | strobe | How the outline of unvisited objects stands out: as the game does, brighter, pulsing, flashes, strobe. The outline's colour is in the game's settings, Visibility. |
| Guards in the tooltip of a guarded building | on | Who guards a unit dwelling or another guarded place and how dangerous the fight is, as in a squad's tooltip, without walking up. Counts in the game's ranges. |

The game itself checks "visited" for all and per player. The game has no check "per hero", so the mod reads the
building's list of visitors. A building "once for all" that an enemy has visited counts as visited.

## Map badges

Badges over objects and squads for the selected hero, only over what is scouted. All of this mod's badges are in one
menu.

| Option | Default | What it does |
|---|---|---|
| Show the badges | while the alt info key is held | Always; only while the game's alt info key is held (Alt unless rebound), along with the game's own extra info; or never. |
| Over unvisited buildings and others' mines | on | An amber badge over a building the selected hero can still visit, and over mines that are not yours. On a guarded one, in the corner, a skull in the colour of the fight's danger, or swords. |
| A building's badge | the sign only | The sign only, or the sign and how often the building can be visited. |
| Buildings' guard danger | on | In the corner of a guarded building's badge, a skull in the colour of the fight's danger for the selected hero. Off: crossed swords, just "guarded". |
| "Captured" over your mines | on | A green gem badge over your side's mines. |
| Over pickups | on | A small badge over resources (gem), artifacts (sword) and chests still lying on the map. |
| Unit portraits over dwellings | on | The portrait of the unit the dwelling hires: bright while there is someone to hire, dim when nobody is left. |
| How many to hire, on the dwelling's portrait | off | How many creatures can be hired now, in the portrait's corner. Not shown while it is guarded. |
| Join and flee badges over squads | on | The Diplomacy icon over a squad that would join, the escape icon over one that would offer to flee. No numbers. |
| Danger skull over squads | on | Over a squad that would neither join nor flee, a skull in the colour of the fight's danger for the selected hero. |

The danger (the skull's colour) is worked out by the game's own skulls widget, the one in its tooltips. With the game's
setting "hide the battle danger": swords only over guards, one common sign over squads, no outcome.

## Battle counter

In battle, hovering an attack shows the game's forecast: damage and how many creatures die. The mod shows the whole
attack in the order it happens, left to right, as the game will play it:
- a counterattack before the strike (a defender that answers first);
- the strike;
- a counterattack after it (a defender that answers twice, like the fauns with their law, has both);
- Double Strike's second strike.

What happens is the game's own decisions (whether the defender answers, first, twice; whether the attacker strikes
again). Each hit's numbers are the game's own forecast for the health both stacks have left at that moment, the
wounded top creature too.

The forecast becomes a table, a grid:
- the first column is the game's own numbers, holding the first hit (amber when it is the defender's counter);
- each next hit is a column: the crossed swords and amber numbers for a counter, the row's own icon and white numbers
  for a strike;
- each column's icons stand on one vertical, its numbers centred in it.

| Option | Default | What it does |
|---|---|---|
| Counterattack damage and losses in the forecast | on | The defender's counters in the damage and losses rows, only when the game decided there will be a counter. Off: not shown, but the strikes after a counter are still worked out for what it leaves. |
| Double Strike's second strike | a column | Off; added to the strike (the strike's numbers say both strikes); or a column of its own after the counter. |
| The stacks' health in the forecast | off | One more row: under each hit, the health of the one it hits at that moment, in white. Still whole: "One and all", the top creature's and the whole stack's health ("350 · 12350"); "Smart", a lone creature's own, a stack's whole health with the top creature's before it when the hit kills fewer than two. Already hit: the range of the whole stack's health left. |

The mod leaves out what is random (luck, a crit that cancels the counter).

## Map tooltip

| Option | Default | What it does |
|---|---|---|
| Tooltip does not cover the object | off | When a map object's tooltip covers the object itself, it moves to a free place: above, right, left or below, off the interface panels too. The game places it; the mod only shifts it. |

## Game fixes

Fixes of the game's own bugs. A fix does what the game itself does where it works, with the game's own functions.

| Option | Default | What it does |
|---|---|---|
| Dwellings' growth after a load | on | A unit dwelling on the map adds weekly growth of its unit in its owner's towns (the menhirs: +2 root iriads). After loading a save the game forgets that growth for every dwelling captured before the save. The fix gives it back right after the map opens, the way the game does on a capture. For every side. The game fixed it itself in 0.82.08; the fix is for 0.82.07 (Game Pass got the update later). |

## Skip intro

| Option | Default | What it does |
|---|---|---|
| Skip the videos on start | on | The studios' logos and the trailer are not shown when the game starts: it goes straight on to the main menu. The game's own start step is told its videos are over. Takes effect at the next start. |

## The mods window

**Mods** in the main menu (under Additional) opens the mod manager's window: a table of the catalog's mods, its
version, its state, and per mod a switch, **Install** or **Update**, and a cross to remove it; at the bottom
**Check** (read the catalog again), **Update all** (when there are two updates or more) and **Close**.
- Install, update, switch off and remove work at once when the mod can unload (all ours can); a mod the others need
  (the core) has no switch nor cross, it says "other mods need it".
- The manager's own update comes at the next start; if the new version does not start, the old one comes back by
  itself.
- Nothing is installed or updated without a click.

## When a mod did not switch on

The mods are built for one version of the game. After a game patch, a mod built for the old version does not switch
on. Then:
- the main menu says at the top which mods are off and which version they were built for;
- in the Mods tab such a mod has "off: built for the game's version …" in place of its switches;
- in the mods table on the version it is marked with an amber "!".

Such a mod needs a build for the new version of the game: the mods window offers it as an update when it is in the catalog.

## For development

**Research** shows the frame rate in a circle at the top left (option "Frame rate in the screen's corner", off by
default). Hover the circle for the frame statistics and our mods' time. The rest of Research is a console for looking
into the game's code (see [process.md](process.md), in Russian).
