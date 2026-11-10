# Olden Era — what is known about the game's code and data

**Versions.** 0.81.4.0 — everything was found and verified on it. 0.82.7.0 — current as of 31.10.2026, the obfuscated names changed.
Markers: **[0.81.4]** — a name or offset from that version; **[verified]** — confirmed in the game; **[inference]** — not verified.

## Data (Core.zip)
- The whole game database is JSON in `StreamingAssets/Core.zip`: about 8000 files, wrapped as `{ "array": [...] }`.
- Texts: `Lang/<language>/texts/*.json`, format `{"tokens":[{"sid","text"}]}`. The hero format is in `hero-data-schema.md`.
- **Diplomacy.** `DB/diplomacy_info.json`:
  - `squadUnitsPoints` and `heroUnitsPoints` by level;
  - `reactionTypeModifiers = [0, 4, 2, 1, 0.5, 0]` indexed by `ESquadReactionType` (Aggressive, Negative, Common, Friendly, Peaceful, Docile);
  - `valueChancePairs`.
- **Map scenarios.** `StreamingAssets/maps/<map>.json` are separate files, not in Core.zip:
  - quests and triggers: `conditions`/`actions`, about 50 conditions and 88 actions;
  - dialog call: `{"a":"Dialog","p":["<id>"]}`;
  - the dialogs themselves: `DB/dialogs/dialogs/**`.
- **Data roots** — [verified on 0.82.08, 10.11.2026]. The game keeps a static list of mod folders (`GameNames.DataRoots` = `bkp.byxl`, each `bko` {enabled, name, folder}; built by `bkp.nbv`/`htm` with one root, `core` = StreamingAssets, usually before the plugins load). For each root it mounts the folder's `*.zip` (top level, not sorted; the key is `folder/path in the zip`, a later archive replaces the same path) and loads the database root after root:
  - id-keyed collections (about 90: heroes, units, cities, items, map objects...): a new id is added, an id already there is replaced whole (no field merge); within a root files go in path order;
  - single-object files (`DB/data.json`, `diplomacy_info.json`, `battle_balance.json`): merged field by field (`JsonUtility.FromJsonOverwrite`), an array replaced whole;
  - Lang texts are read from a fixed path under the game's data folder, not per root.
  The library's `DataRoots.Add` appends a root (and again after the builders); the manager does it at the chainloader for every enabled mod with `data\*.zip`.
- **A new faction** — [verified on 0.82.08, 10.11.2026, a local copy of the human faction under a new id]: the faction, its city (`sceneName` of a built-in scene), units, heroes and `DB/data.json` (`fractions`, `availableFractions`) as data; in code, a `FractionLobbyAsset` for the new id in `SoFractions.dict_` and `fractions` (the HUD reads it unchecked) and the units' sprites under their ids in the sprite registry (`coi`). The lobby, the random map (the player got the new city), the town, hiring, battle, save and load worked. Missing data then: hero texts, `sub_classes` of the faction, city and battle sound sets, a building looked up by faction (`PortalSummoning`). Details: issue #56. Since 10.11 the library does the code part from a data mod's archive (`ModContent`): `Lobby/<id>.json` → `Factions` (the lobby entry), `Sprites/<folder>/<name>.png` → `Sprites` (PNG through `IconData.jvk`, into the table of `coi.cdwo` that holds the folder's own sprites: for `icons/units/hex_portraits` and `icons/fractions` that is one table of about 3200 sprites), `Lang/<language>/**.json` → `Texts`; checked 10.11 with a data-only copy: the lobby list shows its name from the mod's texts and its icon from its PNG.
- **Data additions and map squads** — [verified on 0.82.08, 10.11.2026]: a root after the mods' with `DB/data.json` (the faction lists), `DB/city_music_sounds.json` and `DB/city_ambient_sounds.json` holding only the lists added to is merged by the game as any root. The map generator looks for squads of the city's faction (`[SessionData] squad config not found ... faction: cove` without them); the 656 squads of `DB/squads/squads_human/` copied under `squad_cove_...` with the faction and units renamed are placed (`Create new random squad: squad_cove_...`). A hero's name is the text of its id (`<id>`, `<id>_motto`, `<id>_description`); a city's name is one of its faction's `cityNames` keys (`ObjCity.cityName`). `DB/biomes_info.json` maps one faction per biome.
- **Squads.** `DB/squads/**` — composition. The squad config has no flee flag: it lives in the runtime data (`DataSquad.isEscape`).

## Squad tooltip on the map (UI)
- `Hex.UI.Tooltips.BhTooltipSquad`. Fill method: `void (ESquadTooltipType, Il2CppSystem.Object)` — `vof` [0.81.4], `weh` [0.82.7].
- `container : TooltipSquadContainer`. Inside: `behaviorSquad` (TMP, the game keeps it disabled — we take it over), `skulls : Hex.UI.BhSkulls`, `unitClickTip` and others.
- `BhSkulls`:
  - `opponent` — the difficulty text ("Easy fight"…);
  - `groupDefault` and `groupHidden` — the skull rows;
  - method `int (int heroPower, int squadPower)` — the difficulty level (`twy` [0.81.4]).
- For a squad, the tooltip target is the squad's map object (`fem` [0.81.4] / `fgy` [0.82.7]). Inside it is the squad (`fom` / `frc`), and inside the squad — `DataSquad`.

## Types (obfuscated)
| Role | 0.81.4 | 0.82.7 | How to recognize |
|---|---|---|---|
| hero on the map | `fdr` | `fgd` | has the property `Hex.Session.Logic.Hero` |
| squad | `fom` | `frc` | has the property `DataSquad` |
| squad on the map (tooltip target) | `fem` | `fgy` | has a property of the "squad" type and the methods `CursorColor (hero, int)` |
| player side | `fod` | `fqu` | a property of the hero; the type has `Side` and a back reference to the hero |
| Diplomacy utility | `fcr` (`DiplomacyUtility`) | `ffd` | static class with the join check |

## Joining (Diplomacy) — [verified on 0.81.4 and 0.82.7]
- **The check.** `static bool (hero, squad, out string reason)` — `fcr.bkes` / `ffd.blop`. There is a second clone with the same signature, but it returns an encrypted token. The real check is recognized by its `reason` containing `squad points`.
- **Reason texts:** "You can take squad units / You need more points…, hero points: N, squad points: M", "This squad is Aggressive", "Skill Diplomacy is not learned".
- **Whether the hero has Diplomacy:** `HeroStat.enableDiplomacy` in at least one of the hero's stat sets. There are 6: base, per level, bonus, total.
- **Share of those who join:** `float (squad, hero)` in the same utility, a value in (0,1] (`bkeu` [0.81.4]). The count is taken from `DataSquad.units[].amount`. The number matched the hire window [verified].
- **The Diplomacy icon.** The game's sprite loaders given the key `skill_diplomacy` return placeholders (`other_0`, `template_default_icon`). The real `skill_diplomacy` sprite is taken from the already loaded ones (`Resources.FindObjectsOfTypeAll`).
- **The hero under the cursor** is remembered by the plugin from a prefix on `CursorColor (hero, int)` of the squad's map object.

## Squad fleeing — [verified on 0.81.4 and 0.82.7]
**The encounter chain** (found by a stack scan; names 0.81.4 / 0.82.7):
1. `cfo.Update` / `cgl.Update`;
2. the hero enters the tile (`fdn.bkiw` [0.81.4]; on 0.82.7 — via `ffs.blsq` → `ffq.Start` → `ffx.blsg`);
3. `fom.bmto(hero)` → … → `fom.bmtr(hero)` / `frc.boee` → `fhf.bmdu` → `frc.boef` → `frc.boeh(hero)`;
4. the decision `dju.ypk(hero, squad)` / `dkw.zki(hero, squad)` (clone `dkw.lkv`), `static void (hero, squad)`;
5. it opens the flee window `eec.bbyz` / `efh.bcup(squad, hero)`, the hire window `eec.bbzo` / `efh.bcve(squad, hero)`, or starts a battle `dju.ypl` / `dkw.zkj(hero, squad)`.

**The decision** [read from machine code on both versions; the offsets matched]:
```
if !heroEntity(hero+0x58).gate(squadEntity(squad+0x60)) → exit      // fet.fol / fhf.bmdt
ratio = (float)squad.power() / (float)hero.power(true)               // lky,bkmf / mh,blwd
fleeByPower = ratio <= 0.5                                           // the constant 0.5 was read from the game's memory
join = diplomacy(hero, squad)                                        // fcr.bket / ffd.bloq
side = hero+0x18;  aiCtl = side+0xB8;  flagsB = side+0x58
if lh.static[0] && side.isHuman() → battle                           // new in 0.82.7, the flag is usually 0
if aiCtl != null → neither joining nor fleeing
flagA = DataSquad.isEscape (byte +0x47 of squad+0x10)
join' = flagA && join;   flee' = flagA && fleeByPower && !flagsB[+0x37]
join' → hire window (if DataSquad+0x49 — another path, dkw.zkh); else flee' → flee window (human only); else battle
```
- Fields on 0.82.7 (the `fields` console command): `aiCtl` = `fqu.crmt` (`fzh`, which has `DifficultyAi`), `flagsB` = `fqu.crmh` (`fmr`), `+0x37` = `fmr.cqyh`, `lh.static[0]` = `lh.bvdw`, `+0x49` = `DataSquad.isCampaignFreeDiplomacy`. `ffd.bloq` is a wrapper over `ffd.blop` without the explanation text. The mod repeats this decision by reading only: `MeetingOutcome` in `mod-squad-tooltip/Plugin.cs`.
- `isEscape` is set on all 427 squads of the map in the 05.11 save, including building guards (the unicorns by the die gave up). So it cannot be used to tell guards apart.
- Squad power = the number in the skulls. Hero power `power(true)` is 1–3 % greater than the number in the skulls.
- Verified on the hmeliks (`squad_nature_one_unit_t2_2`) and the parasites (`squad_demon_three_units_t1_1`) on 0.81.4, and on the aggressive demons `squad_demon_one_unit_t1_2` (ratio 0.027) on 0.82.7.
- **How the plugin finds this without names (v1.6).** Among the `static void (hero, squad)` methods it picks the one that calls the squad power `int()`, the hero power `int(bool)` and the Diplomacy check `static bool (hero, squad)`. Then it decodes it through Iced (`BepInEx\core\Iced.dll`) and tracks the registers. The arguments are `rcx`/`rdx`. `mov reg,[reg+X]` extends the path (`hero+18+58`), byte reads give flags. The call arguments give the paths of the gate and the side. The threshold is the first `movss xmm,[rip]`. If anything is not recognized, "Want to flee" is turned off, and the log says what exactly was not found.
- `fom.bmtx(hero, node) → bool` — a check of the 8 neighboring tiles, unrelated to the flee decision [inference].
- The native addresses of the methods change on every launch, so they cannot be saved between sessions.

## Battle (for the "counter" mod) — an analysis of the 0.82.7 metadata, not verified in the game
See `archive/mod-ideas-until-2026-11-06.md` §1 and her `retaliation_methods.md`. The main points:
- damage preview: `Hex.Session.Battle.BhHealDamageForecastPanel.Show(exv)`;
- unit plate: `BhBattleUnitInfoView` (`isCounter`, `healDamageForecast`);
- counter limit: `Hex.Configs.UnitStat.numCounters / alwaysCounter / disableCounterOnCrit`;
- damage calculation: the obfuscated `exw` and `ezz` with clones.

### Found by the `mod-battle-counter` plugin on 0.82.7
- **Damage forecast** — `static exv (DamageUiParams, bool)`, three clones (`exw.bjfj/isw/bkg`). The order of the int fields of `exv`: damage min, damage max, kills min, kills max. The plugin computes the counter with the same method with the roles swapped (casting the defender's `CounterAttack` ability) and with `UnitData.fullStacks` temporarily replaced.
- **Counters left in the round** — `UnitData.cpma` (+0x28) **[verified]**: 1 → 0 after a counter, 1 again in a new round. The plugin finds it by position: the int three properties before `fullStacks` (`cpma int, cpmb int, cpmc float, fullStacks`). `UnitData.cply` (+0x20) is not counters: 1 → 0 after the squad's turn.
- The counter is executed by the common "apply ability" action (`ezi`, method `bjpa(float)`), queued in the unit's queue by `ezq.bjql(ezj)`. `ejn.bewy` only creates the cast object.
- **Data:** `counter_before_attack` (counter before the hit) — on `elf_tracker*`, `minos_upg`, `locust_upg`, in day magic and in the hero skill "Formation" ("Строй"). `numCounters` of the archeresses = 1. `add_attack_after_counter_*` — on the locusts, squires, the avatar of war and others.
- The `BhHealDamageForecastPanel` panel: the `HpPanel` (fist) and `SquadsPanel` (skull) rows are laid out by hand, each with its own `HorizontalLayoutGroup`/`BhRangePanel`; the background is `HpPanel/ShadowPanel`. In copies of a row the layout must be turned off, otherwise the number slides onto the icon.
- **Number of creatures** — `UnitData.fullStacks + 1` **[verified]**: `fullStacks` does not count the top creature, which has its own health (`cpme`, +2 ints after `fullStacks`). For 2 vampires `fullStacks` = 1. The "kills" forecast is capped by the full count.
- **The swords on the plate are the game's own counter check [verified]**: `BhBattleUnitInfoView.counterAttackShowAnim` (`UiAnimatorPrime`, the `CounterAttack` object). The in-battle hover handler `epm.bhdr(FieldObject, evz, ESideCastType)` gives the plate the forecast through `epm.bhdi(Unit, DamageUiParams)` → `plate.bhhn(exv)`, then, if the defender will counter, `epm.bhdj(DamageUiParams)` → `plate.bjee()` → `In()`. Cursor leaving: `epm.bhdo()` → `plate.bjef()` → `Out()`. The order: `BhHealDamageForecastPanel.Show` before `In()`. The swords take into account the counters left and attacks across a tile (`attackType_ "range"`: druids, gravediggers, trogloditess, tricksters), but **not** the death of the whole squad (for the phoenix the swords lit up on dying stacks). `isCounter` on the plate is always active — not a sign.
- **Attack types** (`attackType_`): melee, shoot, range (hits from a tile one away), end_move_attack, jump, move, cast. Reaching along a line — the `attack_reach_*` templates (minotaur alt, dragons, giant frog). The counter is `melee` for almost everyone, `shoot` for one unit.
- **Counter prohibitions in the data**: buffs with `disablers` (`ability` counter_attack/all) — sleep, fetters, trap, the medusa's petrification, the unicorn's blinding; the assassin's debuff `assassin_debuff` is applied by his hit (`damageDealer.buff`). A unit's buffs are `ele` in the container `elg` (`Unit.cwnv`, lists cmgb/cmgc/cmgd).
- Area attacks: the forecast is built for each unit hit, in one frame. The first in the frame is not necessarily the one who will counter (the minotaur's hit along a line); who will counter is told by the decision below.
- **The "defender will counter" decision [verified on 0.82.7, v0.26]**: `eqe.bhqj(Unit attacker, Unit defender, eji ability, Hex, List) : bool`, an instance method. It is called by the hover handler step `epm.bhdj(DamageUiParams)` (and the clone `epm.bpq`), and only on `true` does `plate.bjee()` → the swords `In()` follow. It takes into account the counters left and attacks across a tile. For a line hit the decision is made for each unit hit separately (the first in the line, killed entirely, is `false`, the second is `true`). `eqe.doc/lzk/mmb` also have a signature with the same types, so the plugin takes the one that the method `void (DamageUiParams)` calls directly (a search for `E8 rel32` in its code).
- Mass patches on the methods of the `UiAnimator` library (the `Dispose` coroutines and others) crash the game: merged functions.

## Settings screen — [verified on 0.82.7]
- The screen `Hex.Settings.UI.ScSettings`: `tabs` (an array of `BhSettingsTab`), `sectionsController.sectionsView.sections` (an array of `BhSettingsSection`), static `Hide()`, `OnBackButton()`. The sections are **not** inside the screen; to find them — `Resources.FindObjectsOfTypeAll` with `scene.IsValid()`.
- Section types `ESettingsType`: General=0, Control=1, Sound=2, Video=3, Hotkeys=4, Gameplay=5, Multiplayer=6, Visibility=7. The sections Control, Gameplay, Multiplayer are absent. The game uses the type only to find a section by it: "Mods" has a value of its own, 100 (`SettingsTab`).
- Sections: `BhGeneralSection` ("General"), `BhSoundSection`, `BhVideoSection`, `BhVisibilitySection` (highlight, `outliningDropdown`, `highlightInteractiveObjs*`), `BhHotkeysSection`. Their `Start` runs at game launch; `Show(SettingsData)` is not called when a tab is opened.
- Elements: `BhSettingsCheckbox.Init(Action<bool>, bool)`, `BhSettingsSlider.Init(Action<float>, float)`, `BhLocalizedDropdown.Init(...)`, `BhSettingsTitle`. The row label is `BhSettingsElement.header` (`cpz`: `textSid`, `HardSetText`). Tab: `BhSettingsTab.sectionType`, the click action `bwrb` (supplied by the screen when it opens), selection `kqf(bool)`.

## Localization — [verified on 0.82.7]
- Game texts: `Lang/<language>/texts/*.json` in `Core.zip`, `{"tokens":[{"sid","text"}]}`. The language is `ELang` (russian=0, english=1, …).
- The in-memory table: the service `iv.buvy` of type `cav`: `cccy` — the current `ELang`, `cccz` — `Dictionary<string, cax>`; `cax`: `ccdi` the key, `ccdj` the text, `ccdk` the arguments. Found by shape (`Texts.Find` in `mod-common`), our keys are put into the same place.
- The game's settings rows have no descriptions.

## Map objects — [verified on 0.82.7]
- The map session `feh.me` (a MonoBehaviour with a static instance) → the storage `fqm.crlf` = `Dictionary<int, fql>` of all objects; squads are separate, `fre.crox` = `Dictionary<int, frc>`.
- Object `fql`: `crkp` — `DataObject`, `crko` (`bye`) — the model holder (a GameObject in one of the properties, `ModelOf` goes through all of them), `crku` (`fgv`) — the view, `bmbs()` → position (throws if there is no model), `crkq` (`eco`) — its squad (the guard).
- Squad `frc`: `crob` — `DataSquad`, `croh` (`fno`) — the model holder, GameObject `crcj`.
- The models lie in sections `Section Root: x, y`; the game disables invisible sections as a whole.
- Models have a quest-mark anchor: a child `quest_point` / `Quest_point`, for squads `quest_marker_point`. The quest mark is a particle prefab (`quest_mark_silver_01`, `fx_quest_mark_gold_01`, `BhMapEffectsController`) on this anchor. `BhQuestMarkersUI` — on-screen arrows to quests, not marks.
- `DataObject.ownerSide` uses the same numbering as `Side.index` (verified: a mine's badge changes on capture). `-1` — no one's.
- Data types: `ObjEventBank` (buildings with a visit), `ObjHire` (unit dwelling), `ObjResMine` / `ObjMagicMine` (mines, the sawmill — `mine_wood`), `ObjRes` / `ObjItem` / `ObjChest` (pickups, `released` after pickup).
- Object guard: `DataObject.garnisonParty.units`.
- Unit dwelling `ObjHire`: `assortmentData.unitSets[]` — `sids` (base, upgraded, alternative), `currentAmount` (how many to hire), `weeklyIncrement`.

## Fog of war — [verified on 0.82.7]
- `Side.fogData` (`DataFogOfWar`): `visibilityMap` bool[`sizeX*sizeZ`], `sizeX`, `sizeZ`; `true` — explored.
- World tile: `x = floor(world.x + sizeX/2)`, `z = floor(world.z + sizeZ/2)`, index `x + z*sizeX` (the world origin is at the center of the map). `DataObject.sideScoutings` is not the fog.

## Building tooltips — [verified on 0.82.7]
- The common tooltip `BhTooltipSquad`: fill `weh(ESquadTooltipType, Object)`; `container.garnison` — the garrison row `BhTooltipGarnison`, filled by `unj(eco squad, bool)`; `true` — counts as the game's ranges ("5-9", "100-999").
- The unit dwelling has its own tooltip `BhTooltipHire` (`wae(fpq)`): the unit card `unit`, the skulls `skulls`, the `garnison` row the game leaves hidden.
- Danger skulls `BhSkulls` (verified in the game 04.11.2026):
  - `umn(frc)` fills the widget for a map squad against the army of the selected hero and returns the danger level. This is what the squad tooltip does.
  - `umo(DataParty)` does the same for an enemy army, for example a building's guard. This is how the unit dwelling tooltip (`BhTooltipHire.wae`) shows its guard. In the dryads' dwelling the result matched the game's display: "Easy fight", level 1, one skull lit.
  - `ump(int,int)` — the level from two powers; `umq(int)` — the level's color; `skullActive` / `skullDisabled` — the sprites `Icon_SquadDanger` / `Icon_SquadDangerGray`; `opponent` — the difficulty label.
  - There is no external "fill the skulls" method: the widget fills itself. To get the level without redrawing an open tooltip, the plugins call the method on their own hidden copy of the widget.
  - In the squad tooltip shown for a guarded building (`BhTooltipSquad`), the game does not enable the skulls widget; `mod-visited` enables it and fills it with `umo(guard)`.

## Sprites, fonts, input — [verified on 0.82.7]
- Unit portrait: the game's static `Sprite(string sid)` (`efr.bdbb`), the sprite name = sid. Unit card: background `Frame_UnitLittle_Back`, frame `Frame_UnitLittle_Top`.
- The game's badges by name (`Game.SpriteNamed`): `Icon_Escape` (flee), `Icon_CounterAttack_MouseOver` (counter swords), `Icon_SquadDanger`, `skull_icon`, etc. — loaded together with the interface.
- Movement points at a path: the on-screen tooltip layer, font `LT-Regular`, material `LT-Regular Material`.
- Extra-info mode (Alt): `Hex.Session.World.BhPathDrawer.ShowMovePointsTooltip` (static, a readable name) — `true` exactly while the key is held. The game's input is the new Input System; the service `cbd` (`cbf.ccej` statically), its dictionary `ccee` (`EInputAction → EInputState`) contains the action only on the frame of press (`Down`) and release (`Up`). `TryGetValue` of this dictionary is stripped in the game ("Method unstripping failed") — iterate instead.
- Outline palette: static `foi.cwwo` (Color[18], read every frame), `foi.crem` — tag → palette index (Interactable=0, Visited=1, …).
- The images of the game's badges: `Icon_Escape` (the boot) has no backing, the boot itself is dark; the `Icon_SquadDanger` skull image has almost no margins, the boot has a margin of about 20 %. The size on the map is aligned to the opaque part (`Badges.Extent`).

## Map object outline: colors — [verified on 0.82.7, 04.11.2026]
- The outline tags are `Hex.Render.SelectionOutlineTag` (`Interactable` 0 — not visited, `Visited` 1, `Self`, `Ally`, `Neutral`, `Enemy`). The color number for a tag is chosen in the game's settings (`foi.crem`), the palette is 18 colors (`foi.cwwo`).
- The colors go to the shader every frame through `bei.lkv(CommandBuffer, int[] color number by tag, Color[] palette)` (clones `cfk`, `dhb`; inside — `CommandBuffer.SetGlobalVectorArray`). Found by searching for the callers of `SetGlobalVectorArray` (`callers` in `mod-research`). `BhOutlineRenderer.lkj` draws the outlined meshes, it does not set the colors itself.
- Writing into the palette itself is a bad idea: on hot reloads the "original" color was remembered already changed and stayed in the palette until the game restarted.
- `cwr.wdp` (the book page positioner) is merged with `Il2CppSystem.Exception.StripFileInfo` — do not hook it.

## The "Already visited" line in an object's tooltip — [verified on 0.82.7, 04.11.2026]
- `BhTooltipSquad.weh(ESquadTooltipType, Object)` creates, by tooltip type, through the factory `cxc` a filler (a descendant of `cxa`, method `wdx(Object)`): `cwt` — the event bank (`fpk`), `cwx` — other map objects (`fql`), `cwy` (`fqb`), `cwv` (hero, `fgd`), `cwz` (squad), and so on.
- `fql` — the map object logic (hero, side, outline flags…); `fpk : fql` — the event bank (`OnStartDay`, `OnStartWeek`, rewards); `fgd` — the hero logic; `fqu` — the side.
- The line: `cwt.wdx` calls the virtual `fpk.bnpc(fqu side) → bool` (slot `klass+0x238`) — **the game's "visited" check**; if yes — `iv.ihi(a.qfz())`, otherwise `a.cw()`. `a` is `<PrivateImplementationDetails>.a`, static strings: `qfz` = `"tooltipVisited"`, `cw` = `""`. `iv.ihi(String)` — the key translation (four more `static String(String)` methods of `iv` nearby). The texts go to `BhTooltip.vzp(BasicTooltip, crv)` (`BasicTooltip`: `Header`, `Content`, `SubHeader`, `Extra`, `localizationIsNeeded`).
- **What `bnpc` means** (verified in the game 04.11.2026, by spying on the calls during filling): `fql.bnpc(fqu)` — a virtual "visited by the side" check, declared on the map object logic `fql`, with its own versions in `fpk` (event bank) and `fpm`. The game calls it with the local player's side (`dcz.me.yup()` → `egy.cwkq`; this is the same `fqu` object as in the selected hero's `fgd.cqej`). The rainbow nest (visited) → `True`, the "Already visited" line in `SubHeader`.
- **"Once per each hero" buildings (`EachHeroOneTime`) are not marked by the game in the tooltip at all** — neither by the line nor by `bnpc` (on the altar of night visited by this hero, `bnpc` = `False`, no line). A visit by a hero is stored only in `ObjEventBank.visitorsList` — the hero id: for the verified hero `fgd.blsy()` = 0 = the only int property of `Hex.Session.Logic.Hero` (`cwfa`), in the altar's list — `[0]` (a match on a second hero is not verified). `fpk.bnph(fgd)` (reads `blsy` and the side id) is `False` on all such buildings — the meaning is unknown.
- `mod-visited` 0.23 intercepts `vzp` for the time of filling and returns an empty field instead of the `tooltipVisited` text. The key's text in the current language is `Game.Translate(key)`: the game function `cav.qmk` of the localization service `iv.buvy` (called by `iv.ihi` and its clones `krr`, `izh`, `lcw`, `jci`).
- The analysis went like this: `CALLS`/`BYTES` from the log → `tools/disasm`; the `a.*` keys — by a call through reflection; the virtual slot's method — by reading `MethodInfo` from `klass+offset+8` and `il2cpp_method_get_name`.

## Object tooltip on the map: screen position — [verified on 0.82.7]
- The tooltip manager `Hex.UI.Tooltips.BhTooltips` (`wbu(fql, Camera, GameObject, float, bool)` and neighbors — one per object type) calls `BhTooltip.Show(Camera, GameObject, float)` (a readable name; float = 0.2). The map tooltip is `BhTooltipSquad` "HERO / CITY(Clone)".
- `BhTooltip` has `thisRect_` — a zero-size holder in the center of the screen, the visible panel is `tooltipRect_`; the `mod-visited` badges hang on the tooltip root (`transform`).
- The tooltip is positioned by the helper `cxg` (`BhTooltip.cfsf`): the camera, the rectangles, `wgf(Vector3)` — every frame receives the object's point in the world (the model position, at ground level) and places the tooltip above it by itself: the panel center above the point, the bottom 140 px above the point at a screen height of 2160, clamped at the top edge (the panel top 84 px from the edge). `fkj(Vector3)` is a second such method. Replacing the point in a `wgf` prefix is not advisable: `wgf` is called from all sides, and the position is computed by `wdp` (below).
- Analysis of the machine code of `wgf` (04.11.2026): `vp = camera(+0x18).WorldToViewportPoint(point)`; `p = (vp.x·W − W/2, vp.y·H − H/2)`, where W×H is the canvas `rect` (`+0x30`); `p = wdp(p)` (a virtual call, slot `0x188`; `wdp` adds to y the `sizeDelta.y` of the rectangle `+0x20`) — **this is the game's "tooltip position for a point" function**; then `rect(+0x20).position = canvas.TransformPoint(p.x, p.y − pivot.y·h)` and `bbk.lbn(rect, canvas, bool)` — keeping it within the screen. `fkj` is the same but also shifts x by a constant. `wdp` is virtual: the map tooltip positioner has its own descendant class `cwn` with its own `wdp` (also `cwr` — the book pages); hooking only the base `cxg.wdp` does not fire. None of the three is merged with anything. `mod-map-tooltip` 0.7 hooks all three (postfix) and adds a shift to the game's position. The game creates a new positioner on every show.
- For interface elements the game has its own "tooltip to the side" mode: `BhTooltip.Show(RectTransform, ESnapDirection, float, bool)` (through `BhTooltips.wcc/wcd/...`, the components `BhBtnTooltipShow`, `UbTooltipComponent` and others).
- `BhTooltip.vzy(RectTransform, ESnapDirection, float)` (anchoring to an interface element from the desired side; `ESnapDirection`: Top 0, Down 1, Left 2, Right 3, None 4) has no effect for a tooltip over a world object: it only remembers the target, and `UpdatePosition` does not use it in this mode.
- The `cxg` methods sharing one address: `egr`, `wge`, `lz`, `lac`, `nmg` (and the common stub `wdq`) — hook one per address, otherwise the game crashes.
- Under Il2Cpp: `GetWorldCorners(Vector3[])` fills a copy of the array — compute the corners through `rect.rect` and `TransformPoint`; `x as RectTransform` for an Il2Cpp object is always null — use `TryCast<RectTransform>()`.
- HUD panels: `Hex.Session.Hud.BhResElement` (elements of the resource panel), `BhDayPanel`/`BhDayPanelV2`, `BhSideHeroesPanel`, `BhSideCitiesPanel`; `BhResPanel` and `BhHeroHUDPanel` are full-screen screens (`BhScreen`).
