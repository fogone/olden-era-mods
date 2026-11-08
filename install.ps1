# Builds every mod a player needs and puts it into the game's BepInEx\plugins: the common library, the core and the mods.
# The game must be closed. Needs BepInEx 6 (IL2CPP) in the game's folder, started once (README.md, "Installing").
#   .\install.ps1                                   -> the Game Pass folder
#   .\install.ps1 -GameDir "D:\Games\Olden Era"     -> another one (the folder with HeroesOldenEra.exe)
#   .\install.ps1 -Uninstall                        -> takes them all out of BepInEx\plugins
param([string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content", [switch]$Uninstall)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path (Join-Path $GameDir 'HeroesOldenEra.exe'))) { throw "No HeroesOldenEra.exe in ${GameDir}: pass the game's folder with -GameDir" }
if (-not (Test-Path (Join-Path $GameDir 'BepInEx\interop\Hex.dll'))) {
    throw "BepInEx is not set up in $GameDir yet: put BepInEx 6 (IL2CPP) there and start the game once to the main menu"
}
if (Get-Process HeroesOldenEra -ErrorAction SilentlyContinue) { throw "Close the game first" }

# The library first: the mods are built against it.
$parts = 'mod-common', 'mod-core', 'mod-settings', 'mod-squad-tooltip', 'mod-visited', 'mod-map-badges', 'mod-battle-counter', 'mod-map-tooltip', 'mod-fixes', 'mod-skip-intro'
foreach ($part in $parts) {
    "== $part"
    if ($Uninstall) { & (Join-Path $PSScriptRoot "$part\build.ps1") -Uninstall -GameDir $GameDir }
    else { & (Join-Path $PSScriptRoot "$part\build.ps1") -Install -GameDir $GameDir | Select-Object -Last 1 }
}
if (-not $Uninstall) { "Done: start the game, the mods are in its settings, tab ""Mods""." }
