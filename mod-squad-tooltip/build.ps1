# Builds the squad tooltip plugin with the per-user .NET SDK.
#   .\build.ps1             -> builds bin\Release\net6.0\OldenEraSquadTooltip.dll
#   .\build.ps1 -Install    -> builds and copies it into <game>\BepInEx\plugins
#   .\build.ps1 -Dev        -> builds and copies it into <game>\BepInEx\devplugins, the game may run (mod-devloader reloads it)
#   .\build.ps1 -Uninstall  -> removes it from BepInEx\plugins
param([switch]$Install, [switch]$Dev, [switch]$Uninstall,
      [string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content")

$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }   # a .NET SDK installed for all users, on PATH
$pluginPath = Join-Path $GameDir 'BepInEx\plugins\OldenEraSquadTooltip.dll'
$devPath = Join-Path (Join-Path $GameDir 'BepInEx\devplugins') (Split-Path $pluginPath -Leaf)

if ($Uninstall) {
    if (Test-Path $pluginPath) { Remove-Item $pluginPath -Confirm:$false; "Removed $pluginPath" } else { "Not installed" }
    return
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
& $dotnet build (Join-Path $PSScriptRoot 'OldenEraSquadTooltip.csproj') -c Release -nologo -v:minimal "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

$dll = Join-Path $PSScriptRoot 'bin\Release\net6.0\OldenEraSquadTooltip.dll'
"Built $dll"

if ($Install) {
    if (Get-Process HeroesOldenEra -ErrorAction SilentlyContinue) { throw "Close the game before installing" }
    Copy-Item $dll $pluginPath -Force
    "Installed to $pluginPath"
}

if ($Install -and (Test-Path $devPath)) {
    Remove-Item $devPath -Confirm:$false
    "Removed the dev copy $devPath"
}
if ($Dev) {
    if (Test-Path $pluginPath) {
        if (Get-Process HeroesOldenEra -ErrorAction SilentlyContinue) { throw "$pluginPath is loaded by the game: close it once to move the plugin to devplugins" }
        Remove-Item $pluginPath -Confirm:$false
        "Removed $pluginPath (now loaded from devplugins)"
    }
    New-Item -ItemType Directory -Force (Split-Path $devPath) | Out-Null
    Copy-Item $dll $devPath -Force
    "Copied to $devPath"
}
