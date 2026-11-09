# Publishes our mods one by one into the catalog (issue #10): each mod its own GitHub release (tag <mod>-v<version>,
# its DLL as the asset) and its meta file in catalog/mods/<id>.json; the mod manager its own package (the manager, the
# library, the patcher). A mod whose version is released already is not released again: when its DLL is the same, the
# running game's version is only added to that release in the catalog (it works on this game version too); when it is
# not, it stops and says to raise the version.
#   .\publish-mods.ps1                         -> builds every mod, writes the catalog into release\catalog-preview, publishes nothing
#   .\publish-mods.ps1 -Mods battle-counter    -> only that one (the folder's name without mod-), still nothing published
#   .\publish-mods.ps1 -Publish                -> the GitHub releases (gh, logged in) and catalog\ in the repository, committed and pushed
#   .\publish-mods.ps1 -Mods manager -MetaOnly core,visited -Publish
#                                              -> also the description (texts, tags, screenshots) of core and visited, neither
#                                                 built nor released: their released versions stay as they are
# The builds need the game's files (BepInEx interop), so they are made here; nothing is installed into the game.
param([string[]]$Mods, [string[]]$MetaOnly, [switch]$Publish, [string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content")

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repo = 'fogone/olden-era-mods'
$bepinex = '6.0.0-be.788'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

# What a player can install: folder -> its DLLs (the first holds the plugin) and what it requires. The library
# (OldenEraCommon) and the Mods tab (OldenEraSettings, where the catalog is shown) go with the manager, not as mods of their own.
$catalogParts = [ordered]@{
    'manager'       = @{ Files = @('mod-manager\bin\Release\net6.0\OldenEraModManager.dll', 'mod-common\bin\Release\net6.0\OldenEraCommon.dll', 'mod-manager-patcher\bin\Release\net6.0\OldenEraModManager.Patcher.dll', 'mod-settings\bin\Release\net6.0\OldenEraSettings.dll'); Builds = @('mod-common', 'mod-manager', 'mod-manager-patcher', 'mod-settings') }
    'core'          = @{ Files = @('mod-core\bin\Release\net6.0\OldenEraCore.dll') }
    'squad-tooltip' = @{ Files = @('mod-squad-tooltip\bin\Release\net6.0\OldenEraSquadTooltip.dll') }
    'visited'       = @{ Files = @('mod-visited\bin\Release\net6.0\OldenEraVisited.dll') }
    'map-badges'    = @{ Files = @('mod-map-badges\bin\Release\net6.0\OldenEraMapBadges.dll') }
    'battle-counter'= @{ Files = @('mod-battle-counter\bin\Release\net6.0\OldenEraBattleCounter.dll') }
    'map-tooltip'   = @{ Files = @('mod-map-tooltip\bin\Release\net6.0\OldenEraMapTooltip.dll') }
    'fixes'         = @{ Files = @('mod-fixes\bin\Release\net6.0\OldenEraFixes.dll') }
    'skip-intro'    = @{ Files = @('mod-skip-intro\bin\Release\net6.0\OldenEraSkipIntro.dll') }
}
foreach ($m in @($Mods) + @($MetaOnly) | Where-Object { $_ }) {
    if (-not $catalogParts.Contains($m)) { throw "No mod $m; one of: $($catalogParts.Keys -join ', ')" }
}
$selected = @(if ($Mods -or $MetaOnly) { @($Mods) + @($MetaOnly) | Where-Object { $_ } } else { $catalogParts.Keys })

# The game's version the builds are for: the generated names'.
$names = Get-Content (Join-Path $root 'mod-common\shared\GameNames.g.cs') -Raw
if ($names -notmatch 'GameVersion = "([^"]+)"') { throw "No GameVersion in GameNames.g.cs" }
$gameVersion = $Matches[1]

$catalog = if ($Publish) { Join-Path $root 'catalog' } else { Join-Path $root 'release\catalog-preview' }
if (-not $Publish -and (Test-Path $catalog)) { Remove-Item $catalog -Recurse -Force }
if (-not $Publish -and (Test-Path (Join-Path $root 'catalog'))) { Copy-Item (Join-Path $root 'catalog') $catalog -Recurse }
$rootUrl = "https://raw.githubusercontent.com/$repo/main/catalog"

& $dotnet build (Join-Path $root 'tools\catalog') -c Release -nologo -v:quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw "tools\catalog does not build" }
$tool = Join-Path $root 'tools\catalog\bin\Release\net10.0\catalog.dll'
# The catalog's icon (catalog\icon.png, drawn by catalog\make-icon.ps1): shown for our mods that have no picture of their own.
$iconArgs = @('--icon', "$(Join-Path $root 'catalog\icon.png')=https://raw.githubusercontent.com/$repo/main/catalog/icon.png")

foreach ($mod in $selected) {
    $part = $catalogParts[$mod]
    $descriptionOnly = $MetaOnly -contains $mod
    "== $mod$(if ($descriptionOnly) { ' (the description only)' })"
    # A mod is published as its package (the SDK's ModPackage: a zip with manifest.json and plugins\, in the catalog's
    # "packages", which managers before 0.5 do not read); the manager as its files (it updates itself through them).
    $asPackage = $mod -ne 'manager'
    $pluginFolder = Join-Path $root "mod-$mod"
    if (-not $descriptionOnly) {
        if ($asPackage) {
            & $dotnet build (Get-ChildItem $pluginFolder -Filter *.csproj | Select-Object -First 1).FullName -c Release -nologo -v:minimal -t:ModPackage "-p:GameDir=$GameDir" | Select-Object -Last 1
            if ($LASTEXITCODE -ne 0) { throw "mod-${mod}: the package does not build" }
        }
        else {
            foreach ($b in $part.Builds) { & (Join-Path $root "$b\build.ps1") -GameDir $GameDir | Select-Object -Last 1 }
        }
    }
    # What the mod is: its csproj's Mod* properties (the SDK writes them into the DLL and [BepInPlugin] takes ModInfo).
    $csproj = Get-ChildItem $pluginFolder -Filter *.csproj | Select-Object -First 1
    $props = (& $dotnet msbuild $csproj.FullName -nologo -getProperty:ModId -getProperty:ModName -getProperty:Version -getProperty:ModTags -getProperty:ModRequires | Out-String | ConvertFrom-Json).Properties
    if (-not $props.ModId) { throw "mod-${mod}: no ModId in $($csproj.Name)" }
    $id = $props.ModId
    $title = $props.ModName
    $version = $props.Version
    $part.Tags = ($props.ModTags -replace ';', ',')
    $part.Requires = ($props.ModRequires -replace ';', ',')
    $tag = "$mod-v$version"
    $files = @($part.Files | ForEach-Object { Join-Path $root $_ })
    $package = Join-Path $pluginFolder "bin\Release\net6.0\$id-$version.zip"

    $released = $false
    if ($Publish) {
        $released = & { $ErrorActionPreference = 'Continue'; gh release view $tag --repo $repo *> $null; $LASTEXITCODE -eq 0 }
    }
    $fileArgs = @()
    foreach ($f in $files) { $fileArgs += '--file'; $fileArgs += "$f=https://github.com/$repo/releases/download/$tag/$(Split-Path $f -Leaf)" }
    $requiresArgs = @(); if ($part.Requires) { $requiresArgs = @('--requires', $part.Requires) }
    $langArgs = @()
    if (Test-Path (Join-Path $pluginFolder 'lang')) { $langArgs = @('--lang', (Join-Path $pluginFolder 'lang')) }
    # Screenshots: every PNG/JPG in the mod's screenshots folder, served from the repository's main branch; the optional
    # screenshots\captions.txt has lines "<file name>=<english caption>". None: the meta file keeps the screenshots it has.
    $shotArgs = @()
    $shotFolder = Join-Path $pluginFolder 'screenshots'
    if (Test-Path $shotFolder) {
        $captions = @{}
        $captionFile = Join-Path $shotFolder 'captions.txt'
        if (Test-Path $captionFile) {
            foreach ($line in [IO.File]::ReadAllLines($captionFile, [Text.Encoding]::UTF8)) {
                $eq = $line.IndexOf('=')
                if ($eq -gt 0) { $captions[$line.Substring(0, $eq).Trim()] = $line.Substring($eq + 1).Trim().Replace('|', ' ').Replace('"', '''') }
            }
        }
        foreach ($shot in (Get-ChildItem $shotFolder -File | Where-Object { $_.Extension -in '.jpg', '.jpeg', '.png' } | Sort-Object Name)) {
            $shotUrl = "https://raw.githubusercontent.com/$repo/main/mod-$mod/screenshots/$($shot.Name)"
            if ($captions.ContainsKey($shot.Name)) { $shotUrl += "|$($captions[$shot.Name])" }
            $shotArgs += '--screenshot'; $shotArgs += "$($shot.FullName)=$shotUrl"
        }
    }
    $tagsArgs = @(); if ($part.Tags) { $tagsArgs = @('--tags', $part.Tags) }
    if ($descriptionOnly) {
        & $dotnet $tool meta --catalog $catalog --root-url $rootUrl --id $id @tagsArgs @iconArgs --name 'Nobirds catalog' `
            --author fogone --source "https://github.com/$repo" @langArgs @shotArgs
        if ($LASTEXITCODE -ne 0) { throw "${mod}: the catalog refused it (see above)" }
        continue
    }
    if ($asPackage) {
        # The package's manifest says the rest: id, version, game, requirements, names, descriptions, tags, author, source.
        & $dotnet $tool add --catalog $catalog --root-url $rootUrl --package "$package=https://github.com/$repo/releases/download/$tag/$(Split-Path $package -Leaf)" `
            --game $gameVersion --bepinex $bepinex @iconArgs --name 'Nobirds catalog' @shotArgs
    }
    else {
        & $dotnet $tool add --catalog $catalog --root-url $rootUrl --id $id --version $version --game $gameVersion --bepinex $bepinex `
            @requiresArgs @tagsArgs @iconArgs --name 'Nobirds catalog' --author fogone --source "https://github.com/$repo" @langArgs @shotArgs @fileArgs
    }
    if ($LASTEXITCODE -ne 0) { throw "${mod}: the catalog refused it (see above)" }

    # The manager's release also holds the first install: a zip to unpack into the game's folder (over BepInEx), the
    # manager, the library and the Mods tab in BepInEx\plugins, its patcher in BepInEx\patchers. Not in the catalog.
    $assets = if ($asPackage) { @($package) } else { $files }
    if ($mod -eq 'manager') {
        Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
        $zipPath = Join-Path $root "release\olden-era-mod-manager-$version-for-game-$gameVersion.zip"
        New-Item -ItemType Directory -Force (Split-Path $zipPath) | Out-Null
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        $zip = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
        try {
            foreach ($f in $files) {
                $leaf = Split-Path $f -Leaf
                $folder = if ($leaf -like '*.Patcher.dll') { 'patchers' } else { 'plugins' }
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f, "BepInEx/$folder/$leaf") | Out-Null
            }
        }
        finally { $zip.Dispose() }
        "First install: $zipPath"
        $assets = @($files) + $zipPath
    }

    if ($Publish -and -not $released) {
        $notes = "$title $version for Heroes of Might and Magic: Olden Era $gameVersion (BepInEx $bepinex). Installed by the mod manager; see the README."
        if ($mod -eq 'manager') { $notes = "$title $version for Heroes of Might and Magic: Olden Era $gameVersion (BepInEx $bepinex). First install: unpack olden-era-mod-manager-$version-for-game-$gameVersion.zip into the game's folder (see the README); later updates come through the mod manager itself." }
        gh release create $tag @assets --repo $repo --title "$title $version (game $gameVersion)" --notes $notes
        if ($LASTEXITCODE -ne 0) { throw "gh release create $tag failed ($LASTEXITCODE)" }
        "Published $tag"
    }
    elseif ($released) { "$tag is released already: the catalog says it works on $gameVersion too" }
}

if ($Publish) {
    # The repository is public: main changes only through a pull request that passed the check (check.yml). The
    # catalog goes on a branch of its own, a pull request for it, merged once the check passed. The player's manager
    # reads the catalog from main, so a release is seen when its pull request is merged.
    # git writes warnings (line endings) to stderr, which Windows PowerShell takes for errors under Stop.
    $branch = "catalog/$($selected -join '-')-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    $message = "Catalog: $($selected -join ', ') for the game $gameVersion"
    & {
        $ErrorActionPreference = 'Continue'
        $here = git -C $root rev-parse --abbrev-ref HEAD
        git -C $root stash push -q -- catalog 2>$null
        git -C $root switch -q -c $branch origin/main 2>$null
        git -C $root stash pop -q 2>$null
        git -C $root add catalog 2>$null
        git -C $root commit -q -m $message 2>$null
        git -C $root push -q -u origin $branch 2>$null
        gh pr create --repo $repo --head $branch --base main --title $message --body "Published by publish-mods.ps1: the catalog's meta files for $($selected -join ', ') (game $gameVersion)."
        Start-Sleep 20   # the check is not reported at once
        gh pr checks $branch --repo $repo --watch --fail-fast
        # A rebase: the commit keeps its author (a squash takes the GitHub profile's name).
        if ($LASTEXITCODE -eq 0) { gh pr merge $branch --repo $repo --rebase --delete-branch }
        else { Write-Warning "the check did not pass: the pull request stays open, see it on GitHub" }
        git -C $root switch -q $here 2>$null
        git -C $root pull -q --ff-only 2>$null
    }
    "The catalog: pull request $branch"
}
else { "The catalog (not published): $catalog" }
