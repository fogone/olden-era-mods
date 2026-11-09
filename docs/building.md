# Building the mods

How to build the mods from this repository and how to make a release. Players do not need this: they install a ready
release (see the [README](../README.md)).

## Why the mods are built with the game at hand

The game's code is obfuscated: the names of its classes and methods change with every patch. So the mods are built for
one version of the game (now **0.82.07**), against the game's own files:
- the assemblies in the game's folder (`<game>\dotnet`, `BepInEx\core`, `BepInEx\interop`: the wrappers BepInEx makes for
  the game's code on its first start);
- `GameAssembly.dll` and the game's metadata: the generator (`tools/game-names`, [game-names.md](game-names.md)) finds the
  names the mods need in them, by the game's signatures and code, and writes `mod-common/shared/GameNames.g.cs`.

These files are not in the repository and are not on GitHub's servers, so the mods are built on a computer with the game,
and releases are made there too. A mod built for another version of the game does not switch on: a notice in the main
menu and a line in the Mods tab say so.

## What is needed

- The game, with **BepInEx 6 (IL2CPP), build `6.0.0-be.788`**, started once to the main menu (see the README).
- The **.NET SDK 10** ([dotnet.microsoft.com](https://dotnet.microsoft.com/download)). The plugins target `net6.0` and
  are built against the assemblies in the game's folder, without NuGet.
- PowerShell (Windows PowerShell 5.1 does). If it refuses to run the scripts: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.

## Building and installing everything

```powershell
.\install.ps1                                   # the Game Pass folder
.\install.ps1 -GameDir "D:\Games\Olden Era"     # another one: the folder with HeroesOldenEra.exe
.\install.ps1 -Uninstall                        # take them all out again
```

`install.ps1` checks that BepInEx is there and the game is closed. Then it builds the common library, the core, the Mods
tab and every mod, and puts them into `BepInEx\plugins`.

After a game patch: start the game once to the main menu (BepInEx makes its wrappers again), close it and run
`.\install.ps1` again. The build makes the game's names for the new version by itself.

## Building piece by piece

Each mod has its own `build.ps1`; the game's folder defaults to Game Pass
(`C:\XboxGames\Heroes of Might and Magic- Olden Era\Content`), another one is passed with `-GameDir`.

```powershell
.\mod-common\build.ps1 -Install      # the common library into BepInEx\plugins (game closed)
.\mod-core\build.ps1 -Dev            # a mod into BepInEx\devplugins (the game may run)
.\mod-squad-tooltip\build.ps1        # build only
```

- **`-Install`**: into `BepInEx\plugins`, only while the game is closed.
- **`-Dev`**: into `BepInEx\devplugins`. The dev loader (`mod-devloader`, itself in `BepInEx\plugins`) loads the mods from
  there and reloads one when its DLL changes, with the game running. The common library cannot be updated this way: it
  goes in with `-Install` only, and the mods built against a changed library need it installed first.
- **`-Uninstall`**: out of `BepInEx\plugins`.

The build makes the game's names again by itself when the game was updated or the searches changed. When a search finds
no member of the game, or several, the build stops with error `GN0002` and the candidates listed; an ambiguity is pinned
in `mod-common/shared/GameNames.pins.txt`. When BepInEx's wrappers are older than the game (a patch, the game not started
since), the build stops with error `GN0001`: start the game once to the main menu.

## Making a release

```powershell
.\release.ps1             # release\olden-era-mods-<version>-for-game-<game version>.zip, nothing published
.\release.ps1 -Publish    # and a GitHub release with it (gh, logged in), tagged v<version>
```

`release.ps1` builds what a player needs (the common library, the core, the Mods tab and the mods; not the dev loader nor
the research tools) and packs it laid out like the game's folder: `BepInEx/plugins/…`, with `OldenEraMods.txt` (the
game's version, the mods' versions, how to install).

### Versions

- The set of mods has one version, in `version.txt` (semantic: `major.minor.patch`, from `0.1.0`). The build writes it
  into every assembly (`AssemblyMetadata("ModsVersion")`, `Directory.Build.targets`); the main menu shows it next to the
  game's version ("0.82.07 · mods 0.1.0"), with the game's version the set is built for in the mods table.
- A version of the mods is for one version of the game. A new version of the game gets a new version of the mods: raise
  `version.txt`, build, release. One version is never released twice: `release.ps1 -Publish` refuses a tag that exists.
- Each mod keeps its own version in its csproj (`Version`, which `[BepInPlugin]` takes through `ModInfo`) (shown in the Mods tab): it says which build of that one
  mod is loaded, the release says which set.

## Publishing into the catalog (the mod manager, issue #10)

`publish-mods.ps1` publishes the mods one by one, for the mod manager: each mod its own GitHub release (tag
`<mod>-v<version>`, its DLL the asset) and its meta file in `catalog/mods/<id>.json`, listed in `catalog/catalog.json`;
the manager's own package holds the manager, the library and its patcher. The meta files are written by `tools/catalog`
with the manager's own models (`mod-manager/shared/Catalog.cs`), each file with its sha256.

- `.\publish-mods.ps1` builds every mod and writes the catalog into `release\catalog-preview`; nothing is published.
- `.\publish-mods.ps1 -Mods battle-counter` only that mod.
- `.\publish-mods.ps1 -Mods manager -MetaOnly core,visited` also the description of `core` and `visited` (texts, tags,
  screenshots; `tools/catalog meta`), neither built nor released: their released versions stay. For a description
  change of a mod whose code did not change, while the shared code compiled into it did.
- `.\publish-mods.ps1 -Publish` creates the releases (`gh`) and puts `catalog\` on a branch of its own with a pull
  request, merged once the check passed (see "Changes to the repository" below).

What goes into a mod's catalog entry (`tools/catalog add`, see [catalog.md](catalog.md)):
- **Name and description** in every language: from the mod's `lang/<language>.json`, keys `oe.<id>.title` and
  `oe.<id>.description`.
- **Tags**: the mod's `ModTags` in its csproj (`map`, `battle`, `tooltip`, `ui`, `fix`,
  `qol`, `library`).
- **Author and source**: `fogone` and the repository's address.
- **Icon**: the catalog's own, `catalog/icon.png` (drawn by `catalog/make-icon.ps1`), written into the root file.
- **Screenshots**: the PNG and JPG files of `mod-<id>/screenshots/` (up to 2 MB each), served from `main`, with English
  captions from `screenshots/captions.txt` (`<file name>=<caption>` a line). No folder: the meta file keeps its screenshots.
- **Requirements**: the csproj's `ModRequires` (`nobirds.oldenera.core` for the mods that listen to the core's events).

The mods go into the catalog as **packages** (the SDK's `ModPackage`: one zip per release, the catalog's `packages`,
see catalog.md), read only by managers from 0.5; the manager's package is special: the manager, the library, the Mods
tab and the patcher go in one release of files (the managers before 0.5 update themselves through it) (the DLLs and
`olden-era-mod-manager-<version>-for-game-<game version>.zip` for the first install, unpacked into the game's folder).

How `-Publish` goes: for each mod, `gh release create` if its tag does not exist yet; then all `catalog\` changes go to
a new branch `catalog/<mods>-<time>` cut from `origin/main`, a pull request is opened, `gh pr checks --watch` waits for
the check, and when it passed the pull request is merged by rebase (the commit keeps its author) and the branch is
deleted; when it did not, the pull request stays open and the script warns. The player's manager reads the catalog from
`main`, so a release is seen once its pull request is merged. Your own branch is switched back to and updated.

A version is published once. Run again on another game version with the same DLL, the release only gets that game
version added (it works there too); with another DLL it stops: raise the mod's `Version` in its csproj.

## The SDK on nuget.org

The mods' SDK (`sdk/Nobirds.OldenEra.Modding.Sdk`, with the library inside) is built here, where the game is, and pushed
to nuget.org by a workflow that nuget.org trusts (Trusted Publishing: no API key is kept anywhere).

- `.\publish-sdk.ps1` builds the library and the package (`sdk\Nobirds.OldenEra.Modding.Sdk\bin\Release\*.nupkg`).
- `.\publish-sdk.ps1 -Publish` also makes the GitHub release `sdk-v<version>` with the package and runs the workflow
  `.github/workflows/publish-sdk.yml`, which takes the package from that release, gets a key for an hour from nuget.org
  (`NuGet/login`) and pushes it. A version is published once: raise `<Version>` in the SDK's csproj.
- Once, on nuget.org: the account's **Trusted Publishing** page, a policy with owner `fogone`, repository
  `olden-era-mods`, workflow file `publish-sdk.yml`; and in the repository's secrets, `NUGET_USER`: the nuget.org
  profile name (not the e-mail).

## How it is built

- **`mod-common/`**: the common library `OldenEraCommon.dll`, one for every version of the game: `ModBase` (a mod's frame:
  log, loading and unloading, the version check), `ModMenu` (a mod's menu), `Texts` (the mods' texts in the game's
  localization), `ModEvents` (the core's events), `Ticker`, the wrong-version notice.
- **`mod-common/shared/`**: the library's shared code (compiled into `OldenEraCommon.dll`): `search/` (the searches for the game's members, for the
  generator), `GameNames.g.cs` (the generated names; `GameTypes.g.cs`, the obfuscated classes' names, also compiled by the plugins that work on them), `Game*.cs` (readable wrappers over them: `Game.MeetingOutcome`,
  `Game.VisitFor`, `Game.Translate`…), `MapMarkers`, `Badges`, `BadgeIcons` (badges).
- **`mod-*/`**: the mods. The strings seen in the game are in their `lang/<language>.json`, one file per language of the game (`Hex.UI.ELang` names: `english`, `russian`, `german`, `zhcn`…; the library's own in `mod-common/lang`). A language without a file falls back to English. The texts keep the game's own words for its terms (taken from its texts in `Core.zip`, `Lang/<language>/texts`).
- **`tools/game-names`**: the game's names generator; **`tools/typedump`**, **`tools/disasm`**: looking into the game's
  code without starting it.

More: [process.md](process.md) (the work cycle, debugging, the research console), [game-names.md](game-names.md) (the
generated names), [mod-framework.md](mod-framework.md) (the mods' frame and the core's events),
[game-internals.md](game-internals.md) (what is known about the game's code and data).

## Rules the mods keep

- **Decisions and numbers are the game's.** Damage, danger, joining, fleeing, visits: its own functions. A mod writes
  its own only when no function of the game was found.
- **Hidden numbers are off by default.** The game does not show exact counts, Diplomacy points or the armies' ratio; the
  mods do not either, until it is switched on in their settings.
- **Visible strings go through the game's localization.** The Mods tab and the tooltips switch with the game's language.

## Changes to the repository: pull requests

The repository is public. `main` changes only through a pull request, and only once the check passed
(`.github/workflows/check.yml` runs `tools/check-public.ps1`): what the pull request adds must hold nothing personal
(a machine's paths, people's names, private places), no secrets (tokens, keys, passwords), no e-mail addresses but the
authors', no game files nor built ones, and its commits must be by the authors.
- The private words are not written in the repository: the check takes them from the repository's secret
  `PRIVATE_PATTERNS` and, in a checkout, from the untracked `private-patterns.txt` in the root (one regular expression
  a line).
- `.\tools\install-hooks.ps1` installs a pre-push hook in a checkout that runs the same check on what a push sends.
- `.\tools\check-public.ps1 -Base origin/main` checks the commits not on `main` yet by hand.
