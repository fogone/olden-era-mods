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
| `name` | string | no | Name of the catalog. Not used for anything the manager decides. |
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
| `description` | object | no | Same shape as `name`. May be `{}`. |
| `author` | string | no | Shown to the player. |
| `source` | string | no | Link to the repository or page. |
| `tags` | string[] | no | Tag ids, for example `["map","battle","ui","fix","qol","library"]`. The manager shows known tags by name and unknown ones by id. Meta files without `tags` still read. |
| `releases` | array | yes | One entry per version, any order (the manager sorts). |
| `releases[].version` | string | yes | Dotted number such as `0.48.1`. Also the folder name `<id>\<version>\` on the player's disk. |
| `releases[].games` | string[] | yes | Game versions this build works on, exactly as the game reports them (`0.82.07`, with the leading zero). A release is offered only when the list contains the running version. |
| `releases[].bepinex` | string | no | BepInEx build it was made for (`6.0.0-be.788`). Informational; not checked yet. |
| `releases[].requires` | array | no | Other mods needed. `id` is required; `version` is the lowest version or `null`. |
| `releases[].files` | array | yes | Everything the release consists of. |
| `files[].name` | string | yes | Plain file name, saved as `<id>\<version>\<name>`. |
| `files[].url` | string | yes | https URL (or a local `file:` URL for a development source). |
| `files[].sha256` | string | yes | Hex sha256 of the file, any case. |

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
- **Several sources.** A mod id present in several sources is taken from the first source (the built-in one comes
  first); the rest are ignored. Ids compare ignoring case.
- **Versions.** Compared by numbers, part by part, split on `.`, `-` and `+` (`0.48.10` is newer than `0.48.9`; a part
  that is not a number is compared as text). An update is offered when the release is newer than the installed version.
- **Offline.** Root and meta files are cached in `BepInEx\oldenera-mods\.cache`; when a source cannot be fetched, the
  cached copy is used. A source that fails with no cache is skipped.
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

A player adds the source today by editing `BepInEx\config\nobirds.oldenera.manager.cfg`:

```ini
[Catalog]
Sources = https://raw.githubusercontent.com/<owner>/<repo>/HEAD/catalog/catalog.json
```

Several URLs are separated by spaces. The change is read at the next start. An "Add from GitHub" button in the game
that takes `<owner>/<repo>` and builds this URL is planned (issue #6).

## Writing meta files with tools/catalog

`tools/catalog` adds a release to a mod's meta file and the mod to the root file; it computes the sha256 itself:

```
dotnet run --project tools/catalog -- add --catalog catalog --root-url https://raw.githubusercontent.com/me/mods/HEAD/catalog ^
  --id me.oldenera.hello --version 1.0.0 --game 0.82.07 --bepinex 6.0.0-be.788 ^
  --requires nobirds.oldenera.core --lang mod-hello/lang --author me --source https://github.com/me/mods ^
  --file Hello.dll=https://github.com/me/mods/releases/download/hello-v1.0.0/Hello.dll
```

Options: `--catalog <dir>` (holds `catalog.json` and `mods\`), `--root-url`, `--id`, `--version`, `--game`, `--file
<path>=<URL>` (repeatable; the name is the path's file name) are required (`--file` at least once); `--bepinex`,
`--requires id[@version],...`, `--author`, `--source` are optional; `--lang <folder>` takes the mod's names from every
`<language>.json` in it (token `oe.<last part of the id>.title`); `--tags a,b,c` sets the tags (replacing them). The
tool does not write `description` yet; edit the file by hand. Same version with the same files adds the game version to `games`; the same version with other
files is refused. Releases are written newest first, root entries sorted by id.

This repository's own `publish-mods.ps1` wraps it: a GitHub release per mod (tag `<mod>-v<version>`, the DLL as the
asset), meta files under `catalog/`, committed through a pull request (`docs/building.md`, "Publishing into the catalog").

## For players: security

Nobody reviews other sources. A mod is code that runs inside the game with your user's rights. The sha256 only proves
that the file is the one the catalog names, not that it is safe. Add only sources whose author you trust, and read their
repository first. The manager never installs or updates by itself, and refuses anything that is not https.
