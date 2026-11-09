# The mods' SDK (sdk/Nobirds.OldenEra.Modding.Sdk) as a NuGet package, issue #30. Built here, where the game is (the
# library inside builds against the game's files); published to nuget.org by the workflow publish-sdk.yml (Trusted
# Publishing, no key here).
#   .\publish-sdk.ps1            -> the library and the package: sdk\Nobirds.OldenEra.Modding.Sdk\bin\Release\*.nupkg
#   .\publish-sdk.ps1 -Publish   -> and the GitHub release sdk-v<version> with it, then runs the workflow that pushes it
param([string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content", [switch]$Publish)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repo = 'fogone/olden-era-mods'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }   # a .NET SDK installed for all users, on PATH
$project = Join-Path $root 'sdk\Nobirds.OldenEra.Modding.Sdk\Nobirds.OldenEra.Modding.Sdk.csproj'

& $dotnet build (Join-Path $root 'mod-common') -c Release -nologo -v:minimal "-p:GameDir=$GameDir" | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { throw "the library does not build" }
$version = (& $dotnet msbuild $project -nologo -getProperty:Version).Trim()
& $dotnet pack $project -c Release -nologo -v:minimal | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { throw "the package does not build" }
$package = Join-Path $root "sdk\Nobirds.OldenEra.Modding.Sdk\bin\Release\Nobirds.OldenEra.Modding.Sdk.$version.nupkg"
"Package: $package"
if (-not $Publish) { return }

# A version is published once (nuget.org keeps it): raise <Version> in the SDK's csproj for another.
$tag = "sdk-v$version"
$released = & { $ErrorActionPreference = 'Continue'; gh release view $tag --repo $repo *> $null; $LASTEXITCODE -eq 0 }
if ($released) { throw "$tag is released already: raise the SDK's version" }
$game = (Select-String -Path (Join-Path $root 'mod-common\shared\GameNames.g.cs') -Pattern 'GameVersion = "([^"]+)"').Matches[0].Groups[1].Value
gh release create $tag $package --repo $repo --title "Mods SDK $version (game $game)" `
    --notes "Nobirds.OldenEra.Modding.Sdk ${version}: the MSBuild SDK for mods of Heroes of Might and Magic: Olden Era, with the library for the game $game. On nuget.org once the publish-sdk workflow ran. See sdk/Nobirds.OldenEra.Modding.Sdk/README.md."
if ($LASTEXITCODE -ne 0) { throw "gh release create $tag failed" }
gh workflow run publish-sdk.yml --repo $repo -f "tag=$tag"
if ($LASTEXITCODE -ne 0) { throw "the workflow did not start" }
"Published $tag; the push to nuget.org: gh run list --repo $repo --workflow publish-sdk.yml"
