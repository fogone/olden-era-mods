# The modders' NuGet packages, issue #30: the SDK (sdk/Nobirds.OldenEra.Modding.Sdk, the MSBuild SDK with the library)
# and, with -Tool, the catalog tool (tools/catalog, the dotnet tool oe-catalog). Built here, where the game is (the
# library inside the SDK builds against the game's files); published to nuget.org by the workflow publish-sdk.yml
# (Trusted Publishing, no key here).
#   .\publish-sdk.ps1                  -> the library and the SDK's package: sdk\Nobirds.OldenEra.Modding.Sdk\bin\Release\*.nupkg
#   .\publish-sdk.ps1 -Tool            -> the catalog tool's package: tools\catalog\bin\Release\*.nupkg
#   ... -Publish                       -> and the GitHub release (sdk-v<version>, catalog-tool-v<version>) with it, then
#                                         the workflow that pushes it to nuget.org
param([string]$GameDir = "C:\XboxGames\Heroes of Might and Magic- Olden Era\Content", [switch]$Tool, [switch]$Publish)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repo = 'fogone/olden-era-mods'
$dotnet = Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }   # a .NET SDK installed for all users, on PATH

if ($Tool) {
    $folder = Join-Path $root 'tools\catalog'
    $project = Join-Path $folder 'catalog.csproj'
    $id = 'Nobirds.OldenEra.Modding.Catalog'
    $tagPrefix = 'catalog-tool-v'
} else {
    $folder = Join-Path $root 'sdk\Nobirds.OldenEra.Modding.Sdk'
    $project = Join-Path $folder 'Nobirds.OldenEra.Modding.Sdk.csproj'
    $id = 'Nobirds.OldenEra.Modding.Sdk'
    $tagPrefix = 'sdk-v'
    & $dotnet build (Join-Path $root 'mod-common') -c Release -nologo -v:minimal "-p:GameDir=$GameDir" | Select-Object -Last 1
    if ($LASTEXITCODE -ne 0) { throw "the library does not build" }
}
$version = (& $dotnet msbuild $project -nologo -getProperty:Version).Trim()
& $dotnet pack $project -c Release -nologo -v:minimal | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { throw "the package does not build" }
$package = Join-Path $folder "bin\Release\$id.$version.nupkg"
"Package: $package"
if (-not $Publish) { return }

# A version is published once (nuget.org keeps it): raise <Version> in the csproj for another.
$tag = "$tagPrefix$version"
$released = & { $ErrorActionPreference = 'Continue'; gh release view $tag --repo $repo *> $null; $LASTEXITCODE -eq 0 }
if ($released) { throw "$tag is released already: raise the version" }
if ($Tool) {
    $title = "Catalog tool $version"
    $notes = "${id} ${version}: the dotnet tool oe-catalog, writing a mod catalog for the Olden Era mod manager. On nuget.org once the publish-sdk workflow ran. See tools/catalog/README.md."
} else {
    $game = (Select-String -Path (Join-Path $root 'mod-common\shared\GameNames.g.cs') -Pattern 'GameVersion = "([^"]+)"').Matches[0].Groups[1].Value
    $title = "Mods SDK $version (game $game)"
    $notes = "${id} ${version}: the MSBuild SDK for mods of Heroes of Might and Magic: Olden Era, with the library for the game $game. On nuget.org once the publish-sdk workflow ran. See sdk/Nobirds.OldenEra.Modding.Sdk/README.md."
}
gh release create $tag $package --repo $repo --title $title --notes $notes
if ($LASTEXITCODE -ne 0) { throw "gh release create $tag failed" }
gh workflow run publish-sdk.yml --repo $repo -f "tag=$tag"
if ($LASTEXITCODE -ne 0) { throw "the workflow did not start" }
"Published $tag; the push to nuget.org: gh run list --repo $repo --workflow publish-sdk.yml"
