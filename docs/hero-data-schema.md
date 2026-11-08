# Olden Era — hero data format

Source: `Core.zip` from the installed Xbox version **0.81.4.0** (taken 2026-10-21), checked against the OldenEraExplorer code and the `modding_guide` from Golden Era.
Field statistics were counted over all 177 files in `DB/heroes/**`.

Marks: **[fact]** — visible in the game's data; **[inference]** — follows from the data or from someone else's code, not checked in the game.

---

## 1. Where things are (inside `Core.zip`)

| What | Path | Referenced via |
|---|---|---|
| Heroes | `DB/heroes/<folder>/<id>.json` (`humans`, `necros`, `demons`, `dungeon`, `nature`, `unfrozen`, `campaign`, `campaign_tutorial`, `custom_maps`) | — |
| Specializations | `DB/heroes_specializations/specializations_<faction>.json` | `hero.specialization` |
| Skill progression tables | `DB/heroes_skills/skills_by_level_tables/<faction>_<might\|magic>_skills_table.json` | `hero.skillsRollVariant` |
| Skills / sub-nodes | `DB/heroes_skills/skills/skills.json`, `.../sub_skills/sub_skills.json` | `startSkills[].sid` |
| Subclasses | `DB/heroes_sub_classes/sub_classes_<faction>.json` | not referenced from the hero directly (see §5) |
| Experience curves | `DB/heroes_exp/exp_variants_table.json` | `hero.expVariant` |
| Stat limits | `DB/hero_stats_limits.json` | — |
| Factions | `DB/fractions/<n>_<id>.json` | `hero.fraction` |
| Global lists | `DB/data.json` (`bannedHeroes`, `campaignHeroes`, `availableFractions`) | — |
| Texts | `Lang/<language>/texts/heroInfo.json`, `heroSkills.json` | by keys (§4) |
| Portraits, models | **not in Core.zip** — Unity asset bundles (`resources.assets`, `sharedassets*.assets`) | `hero.icon`, `hero.mesh` |

**There is no hero registration.** Neither the faction nor `data.json` lists heroes. The game (like the Explorer) scans `DB/heroes/**/*.json` in full. **[inference]**
`data.json → campaignHeroes` removes a hero from the regular pool, `bannedHeroes` — bans one. **[fact: the lists exist; the behavior is an inference from the names]**

---

## 2. The hero file

All DB files share one wrapper: `{ "array": [ {...} ] }`. One hero per file, the file name = `id`. **[fact]**
JSON allows tabs and trailing spaces; there are no comments in the files.

### Full example (`DB/heroes/humans/human_hero_6.json`, only repeats shortened)

```json
{
  "array": [
    {
      "id": "human_hero_6",
      "mesh": "knight",
      "mounts": [ "horse" ],
      "icon": "hero_human_6_heretic_avis",
      "fraction": "human",
      "nativeBiome": "Grass",
      "classType": "might",
      "skillsRollVariant": "humans_might_skills_table",
      "costGold": 2500,
      "startLevel": 1,
      "attacksTimesBefore": [0.5],
      "startSquad": [
        { "sid": "esquire",     "min": 14, "max": 20 },
        { "sid": "crossbowman", "min": 10, "max": 14 },
        { "sid": "griffin",     "min": 5,  "max": 7 }
      ],
      "startSquadAlt": [ { "sid": "esquire", "min": 1, "max": 1 } ],
      "specialization": "human_hero_6_specialization",
      "stats": {
        "viewRadius": 6, "statsNum": 1, "magicCastsPerRound": 1,
        "enableTactics": true, "tacticsPlacementSize": 3, "enableHeroNativeBiome": true,
        "offence": 2, "defence": 3, "spellPower": 1, "intelligence": 1, "luck": 0, "moral": 0
      },
      "statsRolls": [
        { "levelFrom": 1,  "rollChances": [ {"v":0,"c":35}, {"v":1,"c":35}, {"v":2,"c":10}, {"v":3,"c":20} ] },
        { "levelFrom": 24, "rollChances": [ {"v":0,"c":25}, {"v":1,"c":25}, {"v":2,"c":25}, {"v":3,"c":25} ] }
      ],
      "startSkills": [
        { "sid": "skill_faction_humans", "skillLevel": 1 },
        { "sid": "skill_trainer",        "skillLevel": 1 }
      ],
      "startMagics": [
        { "sidConfig": "primal_17_magic_groundsight", "level": 1, "isLearned": true }
      ]
    }
  ]
}
```

### Top-level fields

"Present in" — how many of the 177 heroes have the field.

| Field | Type | Present in | Meaning |
|---|---|---|---|
| `id` | string | 177 | Unique ID. Also the name key in the localization. A duplicate ID crashes loading (Golden Era). |
| `fraction` | string | 177 | `human`, `undead`, `nature`, `demon`, `dungeon`, `unfrozen`. Exactly **fraction**, not faction. |
| `classType` | string | 177 | `might` \| `magic` |
| `mesh` | string | 177 | The 3D model on the map: `knight`, `human_mage`, `death_knight`, `death_warrior`, `druid`, `druid_warrior`, `dungeon_mage`, `dungeon_warrior`, `hive_mage`, `hive_warrior`, `unfrozen_mage`, `unfrozen_warrior` |
| `mounts` | string[] | 177 | Mounts (`DB/mounts`) |
| `icon` | string | 177 | The portrait sprite key, for example `hero_human_1_ister`. The large portrait = `icon + "_large"`. |
| `nativeBiome` | string | 177 | `Grass`, `Deathland`, `Autumn`, `Lava`, `Dirt`, `Snow` |
| `skillsRollVariant` | string | 177 | The ID of the skill chance table on level-up |
| `costGold` | int | 177 | Hiring price (standard 2500) |
| `startLevel` | int | 177 | Starting level |
| `specialization` | string | 177 | The specialization ID (§3) |
| `stats` | object | 177 | Stats (below) |
| `startSquad` | `{sid,min,max}[]` | 177 | The starting army: the unit ID and the size range |
| `startSquadAlt` | `{sid,min,max}[]` | 177 | The alternative army (usually 1 unit; probably for hiring as not the first hero **[inference]**) |
| `startSkills` | `{sid,skillLevel}[]` | 177 | Starting skills. For regular heroes `skill_faction_<faction>` comes first. |
| `startMagics` | `{sidConfig,level,isLearned}[]` | 177 | Starting spells (may be empty) |
| `statsRolls` | object[] | 176 | Stat gain chances on level-up (below) |
| `attacksTimesBefore` | float[] | 119 | Attack timing in the battle animation **[inference]**; always `[0.5]` |
| `description`, `motto`, `name` | string | 64 / 64 / 35 | Override of the localization keys. Only on campaign heroes. |
| `expVariant` | string | 5 | The experience curve (`exp_standard`, `exp_standard_campaign`, `exp_low`, …). Without the field — the default. |

### `stats`

| Key | Type | Meaning | Usually |
|---|---|---|---|
| `offence` | int | Attack | might 2, magic 0–1 |
| `defence` | int | Defense | |
| `spellPower` | int | Spell power | |
| `intelligence` | int | Knowledge | |
| `luck`, `moral` | int | Luck / Morale, limit −5…+5 (`hero_stats_limits.json`) | 0 |
| `viewRadius` | int | View radius | 6 |
| `statsNum` | int | How many stats grow per level **[inference]** | 1 |
| `magicCastsPerRound` | int | Spells per battle round | 1 |
| `enableTactics` | bool | The tactics phase | true |
| `tacticsPlacementSize` | int | The depth of the placement zone | 3 |
| `enableHeroNativeBiome` | bool | The native biome bonus | true |
| `movementBonus` | int | Movement modifier (on 6 campaign heroes, −90) | — |

### `statsRolls`

```json
{ "levelFrom": 1, "rollChances": [ { "v": 0, "c": 35 }, ... ] }
```

`v` — the stat index, `c` — the weight. The order of indices, apparently, is 0 = offence, 1 = defence, 2 = spellPower, 3 = intelligence: for might heroes the weights are shifted to 0/1. **[inference]** A block applies from the level `levelFrom` until the next block.

---

## 3. Specialization

`DB/heroes_specializations/specializations_<faction>.json`, an array of records. One record per hero, `<heroId>_specialization`. **[fact]**

```json
{
  "id": "human_hero_1_specialization",
  "name": "human_hero_1_spec_name",
  "desc": "human_hero_1_spec_description",
  "icon": "human_hero_1_specialization_icon",
  "bonuses": [
    { "activationLevel": 8, "type": "heroStat", "parameters": [ "movementPerBonus", "0.05" ],
      "upgrade": { "increment": 0.05, "levelStep": 8 } },
    { "type": "heroStat", "parameters": [ "roadPerBonus", "0.33333334" ] },
    { "type": "heroBattleAbility", "parameters": [ "skill_warrior_ability", "0" ] },
    { "type": "heroBattleAbility", "battleType": "forCity", "receiverRole": "attacker",
      "parameters": [ "attack_siege_ability", "0" ] },
    { "type": "heroStat", "parameters": [ "magicSchoolSet", "neutral", "1", "0" ] },
    { "activationLevel": 4, "type": "heroStat", "parameters": [ "magicSchoolSet", "neutral", "2", "0" ] }
  ]
}
```

### Bonus fields

| Field | Meaning |
|---|---|
| `type` | The bonus category |
| `parameters` | A string array: `[0]` — what we change, then the arguments. Numbers are also written as strings. |
| `activationLevel` | From which hero level the bonus turns on |
| `upgrade` | `{increment, levelStep}`: every `levelStep` levels the value grows by `increment` |
| `receivers`, `receiverRole`, `receiverAllegiance`, `battleType` | Filters: who it applies to and in which battle (`forCity` + `attacker` — in a siege) |

### The most frequent `type / parameters[0]` combinations

Over all the factions' specializations.

| type | parameters[0] | times |
|---|---|---|
| `heroStat` | `magicSchoolSet` | 525 |
| `heroBattleAbility` | `attack_siege_ability`, `skill_warrior_ability`, `skill_mage_ability` | 117 / 59 / 57 |
| `battleSubskillBonus` | `hero_ability` | 75 |
| `unitStat` | `offence`, `defence`, `speed`, `initiative`, `hpPerc`, `modifierSet`, `hp`, `moral`, `critChanceModifier`, `damageMax` … | 66 / 66 / 38 / 37 / 33 / 14 / … |
| `heroStat` | `spellPowerSidSet`, `heroResPercentSet`, `movementPerBonus`, `statsNum`, `expPerBonus`, `roadPerBonus`, `viewRadius`, `luck`, `moral`, `diplomacyEfficiencyPerBonus`, `landscapePenaltyPerBonus` … | 44 / 10 / 8 / 8 / 4 / … |
| `heroMagicReplace` | `<old spell>`, `<new>` | — |
| `sideRes` | `gold`, `crystals` | 3 / 3 |
| `cityUnitsIncrement` | `<unit sid>` | 3 |

A specialization is assembled from this vocabulary. New bonus types cannot be added from JSON: they have to be implemented in code, that is, a BepInEx plugin has to be written. **[inference]**

---

## 4. Localization

File format: `{ "tokens": [ { "sid": "...", "text": "..." } ] }`. The `{0}` arguments are taken from `Lang/args/*.json` (`{"tokensArgs":[{"sid","args":[...]}]}`).

| What | Key | File |
|---|---|---|
| Hero name | `<heroId>` | `heroInfo.json` |
| Motto | `<heroId>_motto` | `heroInfo.json` |
| Biography | `<heroId>_description` | `heroInfo.json` |
| Specialization name | the value of `spec.name` (`<heroId>_spec_name`) | `heroInfo.json` |
| Specialization description | the value of `spec.desc` (`<heroId>_spec_description`) | `heroInfo.json` |
| Subclass | `sub_class_<faction>_<class>_<n>_name` / `_desc` | `heroSkills.json` |

There are 17 languages. A new hero needs tokens at least in `english` and `russian`; what the game will show in other languages has not been checked.

---

## 5. Related entities (briefly)

- **Skill table** (`skillsRollVariant`): `{ id, defaultList: [ { levels: [1..50], rollChances: [ {sid, chance} ] } ] }`. A hero can be given a table of his own: this is how the campaign heroes' tables are built (`campaign_hero_1_skills_table`).
- **Skill**: `{ id, name, desc, skillType, parametersPerLevel: [ {icon, name, desc, bonuses[], subSkills[]} ] }`. The index in the array = level − 1.
- **Subclass**: `{ id, icon, name, desc, faction, classType, activationConditions: [ {skillSid, skillLevel, subSkillSids} ], bonuses[] }`. The subclass field is called **faction**, while the hero's is **fraction**. A subclass is not tied to a specific hero: it opens to any hero of its faction and class when he gains the required skills.
- The skill sub-node IDs (`sub_skill_*`) must be globally unique, otherwise maps and sessions do not load (Golden Era).

---

## 6. New hero checklist (data only, no plugin)

1. `DB/heroes/<folder>/<new_id>.json`: a copy of a neighboring hero with a new `id`.
2. `specialization` → a new record `<new_id>_specialization` in a separate file `DB/heroes_specializations/<own>.json`. It is better not to overwrite someone else's file, to avoid conflicting with patches. **[inference]**
3. `icon`: at the first step we take an **existing** portrait key. Custom art requires a sprite resolver plugin, as in Golden Era, or substituting someone else's sprite through CAM.
4. `mesh` and `mounts` — only from the existing ones.
5. The tokens `<new_id>`, `_motto`, `_description`, `_spec_name`, `_spec_description` in `Lang/english` and `Lang/russian`.
6. Packaging: a separate zip in `StreamingAssets` with the same folder structure and **only new and changed files**. We do not touch `Core.zip` itself. **[inference from the developers' guide as retold in other sources; not checked in the game]**
7. There must be no other extra `.zip` files in `StreamingAssets`: the game loads them all, and duplicate IDs crash loading.

### Open questions — to check in the game

- Does the game pick up an overlay zip in the **Xbox version**: all the mods found were tested on Steam.
- Does a new hero get into the tavern or the starting choice without edits to `data.json`.
- Does a changed set of heroes break multiplayer (desync).
- The exact order of the `v` indices in `statsRolls`.
