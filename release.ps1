# Builds every mod a player needs and packs them into a zip laid out like the game's folder (BepInEx\plugins\...), for
# GitHub Releases: a player unpacks it into the game's folder where BepInEx is (README.md, "Installing"). The mods are
# built here, where the game is: the build needs the game's files (its BepInEx interop, GameAssembly.dll), which GitHub's
# servers do not have.
#   .\release.ps1                 -> release\olden-era-mods-<version>-for-game-<game version>.zip, nothing published
#   .\release.ps1 -Publish        -> and a GitHub release with it (gh, logged in), tagged v<version>
# The version is the whole set's (version.txt), for one version of the game: a new version of the game gets a new version
# of the mods, a version is never released twice.
#   .\release.ps1 -GameDir "..."  -> the game in another folder
param([string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content", [switch]$Publish)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }   # a .NET SDK installed for all users, on PATH
# What a player needs: the common library, the core, the Mods tab and the mods (not the dev loader nor the research tools).
$parts = [ordered]@{
    'mod-common' = 'OldenEraCommon'; 'mod-core' = 'OldenEraCore'; 'mod-settings' = 'OldenEraSettings';
    'mod-squad-tooltip' = 'OldenEraSquadTooltip'; 'mod-visited' = 'OldenEraVisited'; 'mod-map-badges' = 'OldenEraMapBadges';
    'mod-battle-counter' = 'OldenEraBattleCounter'; 'mod-map-tooltip' = 'OldenEraMapTooltip'; 'mod-fixes' = 'OldenEraFixes'; 'mod-skip-intro' = 'OldenEraSkipIntro'
}

# The game's version the mods are built for: the generated names' (GameNames.g.cs, made from the game's files).
$names = Get-Content (Join-Path $root 'mod-common\shared\GameNames.g.cs') -Raw
if ($names -notmatch 'GameVersion = "([^"]+)"') { throw "No GameVersion in GameNames.g.cs" }
$gameVersion = $Matches[1]
$version = (Get-Content (Join-Path $root 'version.txt') -Raw).Trim()
$name = "olden-era-mods-$version-for-game-$gameVersion"
$tag = "v$version"
if ($Publish) {
    # gh says "release not found" on stderr, which Windows PowerShell takes for an error under Stop: asked with Continue.
    $released = & { $ErrorActionPreference = 'Continue'; gh release view $tag *> $null; $LASTEXITCODE -eq 0 }
    if ($released) { throw "$tag is released already: raise the version in version.txt first" }
}

$stage = Join-Path $env:TEMP "oe-release\$name"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
$plugins = Join-Path $stage 'BepInEx\plugins'
New-Item -ItemType Directory -Force $plugins | Out-Null

$versions = @()
foreach ($part in $parts.Keys) {
    "== $part"
    & (Join-Path $root "$part\build.ps1") -GameDir $GameDir | Select-Object -Last 1
    $dll = Join-Path $root "$part\bin\Release\net6.0\$($parts[$part]).dll"
    Copy-Item $dll $plugins
    # The mod's name and version: its csproj's (ModName, Version).
    $csproj = Get-ChildItem (Join-Path $root $part) -Filter *.csproj | Select-Object -First 1
    $props = (& $dotnet msbuild $csproj.FullName -nologo -getProperty:ModName -getProperty:Version | Out-String | ConvertFrom-Json).Properties
    if ($props.ModName) { $versions += "- $($props.ModName) $($props.Version)" }
}

$notes = @"
Olden Era mods **$version** for Heroes of Might and Magic: Olden Era **$gameVersion** (BepInEx 6 IL2CPP, build 6.0.0-be.788).

**Installing:** BepInEx in the game's folder, started once to the main menu; then unpack this zip into the game's folder
(the one with HeroesOldenEra.exe), so the DLLs land in BepInEx\plugins. The settings are in the game's settings, tab Mods.
See the README.

Built for the game's version ${gameVersion}: on another version the mods stay off and say so in the main menu.

$($versions -join "`n")
"@
# Next to the mods, not in the game's own folder.
Set-Content -Path (Join-Path $plugins 'OldenEraMods.txt') -Value $notes -Encoding UTF8

$out = Join-Path $root 'release'
New-Item -ItemType Directory -Force $out | Out-Null
$zip = Join-Path $out "$name.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# Each file added with a "/" path, as the zip format has them (PowerShell 5.1's Compress-Archive and .NET Framework's
# ZipFile write "\", which some unzippers take as part of a file name).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in Get-ChildItem $stage -Recurse -File) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry)
    }
}
finally { $archive.Dispose() }
"Packed $zip"

if ($Publish) {
    $notesFile = Join-Path $stage 'notes.md'
    Set-Content -Path $notesFile -Value $notes -Encoding UTF8
    gh release create $tag $zip --title "Olden Era mods $version (game $gameVersion)" --notes-file $notesFile
    if ($LASTEXITCODE -ne 0) { throw "gh release create failed ($LASTEXITCODE)" }
    "Published $tag"
}
