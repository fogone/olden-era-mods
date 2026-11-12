# Builds the shared library of our plugins with the per-user .NET SDK.
#   .\build.ps1             -> builds bin\Release\net6.0\OldenEraCommon.dll
#   .\build.ps1 -Install    -> builds and copies it into <game>\BepInEx\plugins
#   .\build.ps1 -Uninstall  -> removes it from BepInEx\plugins
param([switch]$Install, [switch]$Dev, [switch]$Uninstall,
      [string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content")

$ErrorActionPreference = 'Stop'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }   # a .NET SDK installed for all users, on PATH
$pluginPath = Join-Path $GameDir 'BepInEx\plugins\OldenEraCommon.dll'
$devPath = Join-Path (Join-Path $GameDir 'BepInEx\devplugins') (Split-Path $pluginPath -Leaf)

if ($Uninstall) {
    if (Test-Path $pluginPath) { Remove-Item $pluginPath -Confirm:$false; "Removed $pluginPath" } else { "Not installed" }
    return
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
& $dotnet build (Join-Path $PSScriptRoot 'OldenEraCommon.csproj') -c Release -nologo -v:minimal "-p:GameDir=$GameDir"
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)" }

$dll = Join-Path $PSScriptRoot 'bin\Release\net6.0\OldenEraCommon.dll'
"Built $dll"

if ($Install) {
    if (Get-Process HeroesOldenEra -ErrorAction SilentlyContinue) { throw "Close the game before installing" }
    Copy-Item $dll $pluginPath -Force
    # The library reads and writes mods' AssetBundles with AssetsTools.NET (MIT): it goes next to it.
    Copy-Item (Join-Path $PSScriptRoot 'bin\Release\net6.0\AssetsTools.NET.dll') (Split-Path $pluginPath) -Force
    "Installed to $pluginPath"
}

if ($Install -and (Test-Path $devPath)) {
    Remove-Item $devPath -Confirm:$false
    "Removed the dev copy $devPath"
}
if ($Dev) { throw 'mod-common is the library every plugin loads: install it with -Install, the game closed' }
