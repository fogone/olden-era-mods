# Builds the mod manager preloader patcher with the per-user .NET SDK.
#   .\build.ps1             -> builds bin\Release\net6.0\OldenEraModManager.Patcher.dll
#   .\build.ps1 -Install    -> builds and copies it into <game>\BepInEx\patchers
#   .\build.ps1 -Uninstall  -> removes it from BepInEx\patchers
param([switch]$Install, [switch]$Uninstall,
      [string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content")

$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }   # a .NET SDK installed for all users, on PATH
$pluginPath = Join-Path $GameDir 'BepInEx\patchers\OldenEraModManager.Patcher.dll'

if ($Uninstall) {
    if (Test-Path $pluginPath) { Remove-Item $pluginPath -Confirm:$false; "Removed $pluginPath" } else { "Not installed" }
    return
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
& $dotnet build (Join-Path $PSScriptRoot 'OldenEraModManagerPatcher.csproj') -c Release -nologo -v:minimal "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

$dll = Join-Path $PSScriptRoot 'bin\Release\net6.0\OldenEraModManager.Patcher.dll'
"Built $dll"

if ($Install) {
    if (Get-Process HeroesOldenEra -ErrorAction SilentlyContinue) { throw "Close the game before installing" }
    Copy-Item $dll $pluginPath -Force
    "Installed to $pluginPath"
}

