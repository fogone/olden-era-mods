# The mod catalog

How the Olden Era mod manager finds mods, and how to publish yours or host a catalog of your own. The format is defined
by the models in `mod-manager/shared/Catalog.cs`; this page describes what the manager does with them.

## Overview

Four levels of JSON, all plain files you can host anywhere over https:

```
catalog.json (root meta file)         lists mods: id + URL of the mod's meta file
  └─ <mod>.json (mod meta file)       name, author, releases
       └─ releases[]                  version, game versions it works on, requirements
            └─ files[]                name, download URL, sha256
```

The player's manager reads the root files of its sources, then every mod's meta file, picks for each mod the newest
release whose `games` contains the running game's version string (for example `"0.82.07"`), and downloads its files.
Nothing is installed or updated without the player's click; installing a mod first installs the mods it requires.

## catalog.json

```json
{
  "schema": 1,
  "name": "My mods",
  "mods": [
    { "id": "me.oldenera.hello", "meta": "https://raw.githubusercontent.com/me/mods/HEAD/catalog/mods/me.oldenera.hello.json" }
  ]
}
```

| Field | Type | Required | Meaning |
|---|---|---|---|
| `schema` | int | no | Format version, `1`. Not checked by the manager yet. |
| `name` | string | yes | Name of the catalog, shown everywhere the source is. A root file without it still works, but the Mods window shows the source by its URL, with a note. `tools/catalog add` refuses to write a root file that has none (`--name`). |
| `icon` | object | no | The catalog's icon, shown with the source in the Mods window: `url` (https) and `sha256` of a PNG or JPG, at most 512 KB (524288 bytes); checked like a screenshot and cached in `.cache\icons\<sha256>.png|jpg`, downloaded in the background when the source is read. Roots without it still read. |
| `mods[].id` | string | yes | The mod's id (see "The mod id"). Must equal `id` inside the meta file, compared ignoring case. |
| `mods[].meta` | string | yes | URL of the mod's meta file (https or a local `file:` URL). An entry without it is skipped. |

## <mod>.json

```json
{
  "schema": 1,
  "id": "me.oldenera.hello",
  "name": { "english": "Hello", "russian": "Привет" },
  "description": { "english": "Says hello." },
  "author": "me",
  "source": "https://github.com/me/mods",
  "tags": ["qol", "ui"],
  "screenshots": [
    { "url": "https://raw.githubusercontent.com/me/mods/HEAD/catalog/shots/hello.png", "sha256": "<64 hex digits>", "caption": { "english": "Hello on the map" } }
  ],
  "releases": [
    {
      "version": "1.0.0",
      "games": ["0.82.07"],
      "bepinex": "6.0.0-be.788",
      "requires": [ { "id": "nobirds.oldenera.core", "version": null } ],
      "files": [
        { "name": "Hello.dll", "url": "https://github.com/me/mods/releases/download/hello-v1.0.0/Hello.dll", "sha256": "<64 hex digits>" }
      ]
    }
  ]
}
```

| Field | Type | Required | Meaning |
|---|---|---|---|
| `schema` | int | no | `1`. Not checked yet. |
| `id` | string | yes | The mod id; equals the entry's `id` in the root file, or the mod is left out. |
| `name` | object | yes | Display name by the game's language name: `english`, `russian`, `brportugese`, `czech`, `french`, `german`, `hungarian`, `italian`, `japanese`, `korean`, `polish`, `spanish`, `turkish`, `ukrainian`, `zhcn`, `zhtw`. The Mods window shows the game's language, English when it is missing, the id when there is no English either. Always give `english`. |
| `description` | object | no | Same shape as `name`: what the mod does, shown in its details and searched by the Mods window's search box. May be `{}`. |
| `author` | string | no | Shown to the player. |
| `source` | string | no | Link to the repository or page. |
| `tags` | string[] | no | Tag ids; the manager knows these by name: `map`, `battle`, `tooltip`, `ui`, `fix`, `qol`, `library`, `info`, `squads`, `buildings`. The manager shows known tags by name (in the game's language) and unknown ones by id; the Mods window filters by them. Meta files without `tags` still read. |
| `screenshots` | array | no | Pictures for the mod's details (see "Screenshots"). Meta files without it still read. |
| `screenshots[].url` | string | yes | https URL of a PNG or JPG. |
| `screenshots[].sha256` | string | yes | Hex sha256 (64 digits) of the file; a screenshot without a valid one is refused. |
| `screenshots[].caption` | object | no | Same shape as `name`; the game's language, English when missing, none when empty. |
| `releases` | array | yes | One entry per version, any order (the manager sorts). |
| `releases[].version` | string | yes | Dotted number such as `0.48.1`. Also the folder name `<id>\<version>\` on the player's disk. |
| `releases[].games` | string[] | yes | Game versions this build works on, exactly as the game reports them (`0.82.07`, with the leading zero). A release is offered only when the list contains the running version. |
| `releases[].bepinex` | string | no | BepInEx build it was made for (`6.0.0-be.788`). Informational; not checked yet. |
| `releases[].requires` | array | no | Other mods needed. `id` is required; `version` is the lowest version or `null`. |
| `releases[].files` | array | yes | Everything the release consists of. |
| `files[].name` | string | yes | Plain file name, saved as `<id>\<version>\<name>`. |
| `files[].url` | string | yes | https URL (or a local `file:` URL for a development source). |
| `files[].sha256` | string | yes | Hex sha256 of the file, any case. |

### Packages (`packages`, manager 0.5 and later)

A release can be a **package**: one archive the mods' SDK makes (`dotnet build -t:ModPackage`,
`sdk/Nobirds.OldenEra.Modding.Sdk/README.md`). It is listed in `packages`, a list of its own with the same fields as
`releases` but `files`, and `archive` in their place:

```json
  "packages": [
    {
      "version": "1.1.0",
      "games": ["0.82.08"],
      "bepinex": "6.0.0-be.788",
      "requires": [ { "id": "nobirds.oldenera.core", "version": null } ],
      "archive": { "name": "me.oldenera.hello-1.1.0.zip", "url": "https://github.com/me/mods/releases/download/hello-v1.1.0/me.oldenera.hello-1.1.0.zip", "sha256": "<64 hex digits>" }
    }
  ]
```

- The archive holds `manifest.json` (what the mod is: `id`, `version`, `name` and `description` by language, `author`,
  `source`, `tags`, `games`, `bepinex`, `requires`, `files`), `plugins\` (the DLLs, plain names), and optionally
  `README.md`, `icon.png`, `screenshots\`, `data\` (the mod's game data archives, `*.zip` laid out as the game's
  `Core.zip`; manager 0.6 and later). The manager installs what is under `plugins\` into `<id>\<version>\` and the
  archives under `data\` into `<id>\<version>\data\`, which it mounts as one of the game's data roots at the start (a
  change to such a mod counts from the next start); a package of data only, with no DLL, is fine. The manifest's `id`
  and `version` must be the mod's and the package's, or the package is refused.
- `tools/catalog add --package <zip>=<url>` writes the entry from the manifest (and the mod's names, descriptions,
  tags, author and source with it).
- Managers before 0.5 read only `releases`: a mod built on the library of `Nobirds.OldenEra.Modding.*` goes into
  `packages`, so they never install it without that library. A manager 0.5 takes the newest of both lists.

"Required" means a mod without it is not offered or not shown properly. The manager refuses outright only a wrong
`id`, a file without a plain `name` or without `sha256` (below); a missing `name` shows the id, a missing `releases` or
`games` means no release for the game.

## What the manager enforces

- **https only.** Every URL (root file, meta file, mod file) must be https or a local file; anything else is refused.
- **Size.** Each fetched file is at most 32 MB (33554432 bytes).
- **sha256.** A file without `sha256` is refused; a file whose hash differs is refused. Hashes are compared ignoring case.
- **Plain names.** A file `name` that is empty, contains characters invalid in a Windows file name (path separators
  included) or contains `..` is refused.
- **All or nothing.** All files of a release are downloaded and checked first, written to `<id>\<version>.download\`
  and moved into place whole. Any failure leaves nothing. An existing folder of the same version is replaced.
- **Game version.** `ReleaseFor` takes the newest release (by version, see below) whose `games` contains the running
  game's version string, compared exactly. No match, no install, no update.
- **Id match.** A mod whose meta file says another `id` than its catalog entry is left out (logged).
- **Several sources.** A mod id present in several sources is taken from the source the installed mod came from (the
  mod's `Source` in the state is its meta file's URL) when that source still lists it; otherwise from the first source
  (the built-in one comes first). Ids compare ignoring case.
- **Updates only from the source installed from.** An installed mod is offered an update only when the chosen meta file
  is the one it was installed from. A mod whose source was removed, or that no longer lists it, gets no updates (it
  stays installed). A mod put in by hand has no source and may update from any.
- **Versions.** Compared by numbers, part by part, split on `.`, `-` and `+` (`0.48.10` is newer than `0.48.9`; a part
  that is not a number is compared as text). An update is offered when the release is newer than the installed version.
- **Offline and not due.** Root and meta files are cached in `BepInEx\oldenera-mods\.cache`; when a source cannot be
  fetched, the cached copy is used. A source that fails with no cache is skipped. A source that is not due for a check
  (see "Sources") is read from the cache only; with no cache it is "not checked".
- **Icon.** The catalog's icon: https, at most 512 KB (524288 bytes), sha256 given and matching, a PNG or a JPG by its first bytes; cached in `.cacheicons`, downloaded in the background when the source is read. A refused one is logged and the source shows without it.
- **Screenshots.** Only https (a local file is not accepted here). At most 2 MB (2097152 bytes). The sha256 must be
  given and must match. The file must be a PNG or a JPG by its first bytes, whatever the URL or extension says. Kept in
  `BepInEx\oldenera-mods\.cache\screens\<sha256>.png|jpg` (written through a temporary file). Downloaded in the
  background when the player opens the mod's details, not when the catalog is read; one already cached is not fetched
  again. A refused or failed one is logged and left out; the others still load.
- **A version is never republished with other files.** This is a convention enforced by `tools/catalog`, not by the
  manager: players who installed `1.0.0` would not be offered a changed `1.0.0`. Raise the version instead. The same
  files may be listed again with one more game version in `games`.
- **Not enforced yet:** `schema`, `bepinex`, and the version in `requires` (a requirement is met by id alone).

## The mod id

The id is the BepInEx plugin GUID (`[BepInPlugin("me.oldenera.hello", ...)]`). The manager loads every file of the
release as an assembly, creates every non-abstract `BasePlugin` that has a `[BepInPlugin]` attribute, and:

- a requirement is met when a mod with that id is loaded by the manager, or `IL2CPPChainloader.Instance.Plugins`
  contains a plugin with that GUID (one loaded from `BepInEx\plugins`);
- a plugin whose GUID is already loaded by BepInEx is skipped with a warning;
- a plugin whose GUID differs from the mod id still loads, with only a warning, but requirements and the "already
  installed" check go by id, so keep them equal.

A release with several files (a plugin and the libraries it uses) lists them all; the plugin must be in one of them.
A plugin that derives from the framework's `ModBase` (`OldenEraCommon.dll`) can unload itself, so switching it on or
off, updating and removing take effect without restarting the game; any other plugin changes at the next start.
Mods that need our core (`nobirds.oldenera.core`) list it in `requires`.

## Hosting your own catalog on GitHub

1. Put the files in your repository under `catalog/`: `catalog/catalog.json` and `catalog/mods/<id>.json`.
2. Publish each build as a GitHub release (for example tag `<mod>-v<version>`) with the DLL as an asset; `files[].url`
   is its `.../releases/download/<tag>/<file>` URL.
3. The catalog's URL is the raw URL of the root file:
   `https://raw.githubusercontent.com/<owner>/<repo>/HEAD/catalog/catalog.json`.
   The `meta` URLs inside can point to the same repository the same way.

The player adds it in the game's Mods window, tab **Sources** (the game warns that nobody reviews other sources):

- **Add by URL** takes the root file's URL. Only https; a URL already in the list is refused. The root file is read
  from the network first (never from the cache); a source that cannot be read is not added. The mods it lists then
  appear in the window.
- **Add from GitHub** takes `owner` or `owner/repo` (a pasted `https://github.com/owner[/repo]` works too, deeper
  paths are cut to owner and repository) and builds
  `https://raw.githubusercontent.com/<owner>/<repo>/HEAD/catalog/catalog.json`; without a repository the name is
  `olden-era-mods`. Owner: letters, digits and `-`, up to 39 characters; repository: letters, digits, `-`, `_`, `.`, up to
  100. Anything else is refused. Then it is added like any URL.

The player's sources can be removed (the built-in one cannot). Removing a source leaves its mods installed; they get no
updates, because a mod updates only from the source it was installed from.

How often each source is checked is set per source: `start` (every start, the default), `daily`, `weekly` or `manual`.
At the game's start a source that is due is read from the network and gets a last-check time; the others are read
from the cache. `daily`: last read on an earlier day; `weekly`: seven or more days ago; `manual`: never by itself; a
source never read is due unless `manual`. Unknown values read as `start`. **Check** reads one source now, **Check all**
every source; neither installs anything.

Sources are stored in `BepInEx\oldenera-mods\sources.json`:

```json
{ "schema": 1,
  "builtIn": { "checkEvery": "daily", "lastChecked": "2026-11-08T10:00:00" },
  "sources": [ { "url": "https://.../catalog.json", "name": "My mods", "added": "2026-11-08", "checkEvery": "weekly", "lastChecked": "2026-11-08T10:00:00" } ] }
```

| Field | Meaning |
|---|---|
| `schema` | `1`. |
| `sources[].url` | The root file's https URL; the key of the source (compared ignoring case). |
| `sources[].name` | The root file's `name` as last read; empty until it was read once. |
| `sources[].added` | The date the player added it (`yyyy-MM-dd`). |
| `sources[].checkEvery` | `start`, `daily`, `weekly` or `manual`; missing reads as `start`. |
| `sources[].lastChecked` | Local time of the last successful read from the network; missing: never. |
| `builtIn` | The built-in source: only its `checkEvery` and `lastChecked` (and the name last read), no `url` (it is fixed in the manager, so it is not in `sources` and cannot be removed). |

A file without `checkEvery` / `lastChecked` reads fine.  When the file does not exist yet, the old config line `[Catalog] Sources` in
`BepInEx\config\nobirds.oldenera.manager.cfg` (URLs separated by spaces) is moved into it once; after that the config
line is ignored. A file that cannot be read is copied to `sources.json.bad` and the list starts empty. Nothing is
installed or updated by a source check: the player asks for each.

## Writing meta files with tools/catalog

`tools/catalog` adds a release to a mod's meta file and the mod to the root file; it computes the sha256 itself:

```
dotnet run --project tools/catalog -- add --catalog catalog --root-url https://raw.githubusercontent.com/me/mods/HEAD/catalog ^
  --id me.oldenera.hello --version 1.0.0 --game 0.82.07 --bepinex 6.0.0-be.788 ^
  --requires nobirds.oldenera.core --lang mod-hello/lang --author me --source https://github.com/me/mods ^
  --name "My mods" --screenshot shots/hello.png=https://raw.githubusercontent.com/me/mods/HEAD/catalog/shots/hello.png^|Hello ^
  --file Hello.dll=https://github.com/me/mods/releases/download/hello-v1.0.0/Hello.dll
```

Options:
- Required: `--catalog <dir>` (holds `catalog.json` and `mods\`), `--root-url`, `--id`, `--version`, `--game`, and
  `--file <path>=<URL>` (repeatable, at least once; the name is the path's file name).
- Release: `--bepinex`, `--requires id[@version],...`.
- Mod meta: `--author`, `--source`, `--tags a,b,c` (replaces the tags: trimmed, lowercased, duplicates and empty ones
  dropped), `--lang <folder>` (takes the mod's `name` and `description` in every language from each `<language>.json`
  in it: tokens `oe.<last part of the id>.title` (or `.name`) and `oe.<last part of the id>.description`; so a mod's
  catalog description is the `oe.<id>.description` key of its lang files), `--screenshot <path>=<url>[|<english
  caption>]` (repeatable; computes the sha256 of the local picture and replaces the mod's screenshots; refused: no
  such file, over 2 MB, not a PNG or JPG, URL not https; the caption is written as `english`, split at the last `|`).
- Root file: `--name <catalog name>` (required while the root file has none; the tool refuses otherwise), `--icon
  <path>=<url>` (the catalog's icon; sha256 computed; refused: no such file, over 512 KB, not a PNG or JPG, URL not
  https).

Same version with the same files adds the game version to `games`; the same version with other files is refused.
Releases are written newest first, root entries sorted by id.

This repository's own `publish-mods.ps1` wraps it: a GitHub release per mod (tag `<mod>-v<version>`, the DLL as the
asset), meta files under `catalog/`, committed through a pull request (`docs/building.md`, "Publishing into the catalog").

## For players: security

Nobody reviews other sources. A mod is code that runs inside the game with your user's rights. The sha256 only proves
that the file is the one the catalog names, not that it is safe. Add only sources whose author you trust, and read their
repository first. The manager never installs or updates by itself, and refuses anything that is not https.
