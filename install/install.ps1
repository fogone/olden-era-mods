# Olden Era mods: installer (issue #18). Windows PowerShell 5.1, no administrator rights.
#
#   irm https://raw.githubusercontent.com/fogone/olden-era-mods/main/install/install.ps1 | iex
#
# Options (they cannot be passed through "| iex", so either the script block form or an environment variable):
#   & ([scriptblock]::Create((irm https://raw.githubusercontent.com/fogone/olden-era-mods/main/install/install.ps1))) -DryRun
#   $env:OLDENERA_OPTIONS = '-DryRun'; irm <url> | iex
#   -DryRun         find and check everything, print what would be done, change nothing
#   -Uninstall      remove our files (the mod manager, the mods, the library); saves are never touched
#   -RemoveBepInEx  with -Uninstall: also BepInEx's own files
#   -Game <folder>  the game's folder, the one with HeroesOldenEra.exe (otherwise found, or asked)
#
# What it does: finds the game (Game Pass or Steam), checks the game's and BepInEx's versions, installs BepInEx 6
# build 788 if it is missing or another (from builds.bepinex.dev, checked by SHA-256), then the mod manager from the
# GitHub release for your game's version (every file checked by the SHA-256 in the catalog). The mods themselves are
# installed from the Mods window in the game's main menu. Everything stays inside the game's folder.

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

# ---- constants -------------------------------------------------------------------------------------------------
$Repo        = 'fogone/olden-era-mods'
$CatalogUrl  = "https://raw.githubusercontent.com/$Repo/main/catalog/catalog.json"
$ManagerId   = 'nobirds.oldenera.manager'
$BepInExVer  = '6.0.0-be.788'
$BepInExUrl  = 'https://builds.bepinex.dev/projects/bepinex_be/788/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.788%2B5b766a3.zip'
$BepInExSha  = 'f4cc496bd098a0df4164b81e3737297707f13a47c2478dba2f60eefab784817a'
$AppxFamily  = 'HoodedHorse.HeroesofMightandMagicOldenEra_znaey1dw2bdpr'

# ---- options: the script block's arguments plus $env:OLDENERA_OPTIONS ----------------------------------------------
$tokens = @($args) + @("$env:OLDENERA_OPTIONS".Split(' ', [StringSplitOptions]::RemoveEmptyEntries))
$DryRun         = $tokens -contains '-DryRun'
$Uninstall      = $tokens -contains '-Uninstall'
$RemoveBepInEx  = $tokens -contains '-RemoveBepInEx'
$GameArg        = $null
$at = [array]::IndexOf($tokens, '-Game'); if ($at -ge 0 -and $at + 1 -lt $tokens.Count) { $GameArg = $tokens[$at + 1] }

function Say($text, $ForegroundColor = 'Gray') { Write-Host $text -ForegroundColor $ForegroundColor }
function Step($text) { Write-Host ''; Write-Host "== $text" -ForegroundColor Cyan }
function Do-It($text) { if ($DryRun) { Write-Host "   [dry run] would: $text" -ForegroundColor Yellow; $false } else { Write-Host "   $text"; $true } }
function Stop-Here($text) { Write-Host ''; Write-Host $text -ForegroundColor Red; throw $text }
function Sha256Of($stream) { ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash($stream)) -replace '-', '').ToLower() }

# ---- 1. the game -------------------------------------------------------------------------------------------------
function Test-GameFolder($dir) { $dir -and (Test-Path -LiteralPath (Join-Path $dir 'HeroesOldenEra.exe')) }

function Find-Games {
    $found = @()
    # Game Pass: the package's install location is a junction in WindowsApps; the real, writable folder is under XboxGames.
    $pkg = Get-AppxPackage -Name '*HeroesofMightandMagicOldenEra*' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($pkg) {
        $candidates = @()
        $item = Get-Item -LiteralPath $pkg.InstallLocation -Force -ErrorAction SilentlyContinue
        if ($item -and $item.Target) { $candidates += @($item.Target) }
        $candidates += $pkg.InstallLocation
        foreach ($c in $candidates) {
            $c = "$c"
            if (Test-GameFolder $c) { $found += [pscustomobject]@{ Kind = 'Game Pass'; Dir = $c; Family = $pkg.PackageFamilyName }; break }
        }
        if (-not ($found | Where-Object Kind -eq 'Game Pass')) {
            foreach ($drive in (Get-PSDrive -PSProvider FileSystem).Root) {
                $c = Join-Path $drive 'XboxGames\Heroes of Might and Magic- Olden Era\Content'
                if (Test-GameFolder $c) { $found += [pscustomobject]@{ Kind = 'Game Pass'; Dir = $c; Family = $pkg.PackageFamilyName }; break }
            }
        }
    }
    # Steam: SteamPath from the registry, the libraries from libraryfolders.vdf, the game among steamapps\common\*.
    $steam = (Get-ItemProperty 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue).SteamPath
    if ($steam) {
        $libraries = @($steam)
        $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            foreach ($m in [regex]::Matches((Get-Content -LiteralPath $vdf -Raw), '"path"\s+"([^"]+)"')) { $libraries += $m.Groups[1].Value -replace '\\\\', '\' }
        }
        foreach ($lib in ($libraries | Select-Object -Unique)) {
            $common = Join-Path $lib 'steamapps\common'
            if (Test-Path -LiteralPath $common) {
                foreach ($d in Get-ChildItem -LiteralPath $common -Directory) {
                    if (Test-GameFolder $d.FullName) { $found += [pscustomobject]@{ Kind = 'Steam'; Dir = $d.FullName; Family = $null } }
                }
            }
        }
    }
    $found
}

Step 'Looking for the game'
$game = $null
if ($GameArg) {
    if (-not (Test-GameFolder $GameArg)) { Stop-Here "No HeroesOldenEra.exe in $GameArg" }
    $game = [pscustomobject]@{ Kind = 'given'; Dir = (Resolve-Path -LiteralPath $GameArg).Path; Family = $null }
} else {
    $games = @(Find-Games)
    if ($games.Count -eq 1) { $game = $games[0] }
    elseif ($games.Count -gt 1) {
        for ($i = 0; $i -lt $games.Count; $i++) { Say "   [$($i + 1)] $($games[$i].Kind): $($games[$i].Dir)" }
        $n = [int](Read-Host 'Which one (number)')
        $game = $games[$n - 1]
    } else {
        Say '   Not found automatically.'
        $dir = (Read-Host "The game's folder (the one with HeroesOldenEra.exe)").Trim('"', ' ')
        if (-not (Test-GameFolder $dir)) { Stop-Here "No HeroesOldenEra.exe in $dir" }
        $game = [pscustomobject]@{ Kind = 'given'; Dir = $dir; Family = $null }
    }
}
$dir = $game.Dir
Say "   $($game.Kind): $dir"
try { $probe = Join-Path $dir '.oldenera-write-test'; [IO.File]::WriteAllText($probe, ''); Remove-Item -LiteralPath $probe }
catch { Stop-Here "Cannot write into $dir (Game Pass: use the folder under XboxGames, not WindowsApps)." }
if (Get-Process -Name HeroesOldenEra -ErrorAction SilentlyContinue) {
    if ($DryRun) { Say '   The game is running now: a real run would stop here and ask you to close it.' -ForegroundColor Yellow }
    else { Stop-Here 'The game is running. Close it and run the script again.' }
} else { Say '   The game is not running.' }

# ---- uninstall -------------------------------------------------------------------------------------------------
if ($Uninstall) {
    Step 'Removing the mods'
    $targets = @()
    foreach ($sub in 'BepInEx\plugins', 'BepInEx\patchers', 'BepInEx\devplugins') {
        $folder = Join-Path $dir $sub
        if (Test-Path -LiteralPath $folder) { $targets += Get-ChildItem -LiteralPath $folder -Filter 'OldenEra*.dll' -File | ForEach-Object FullName }
    }
    $targets += @(Join-Path $dir 'BepInEx\oldenera-mods') | Where-Object { Test-Path -LiteralPath $_ }
    $cfg = Join-Path $dir 'BepInEx\config'
    if (Test-Path -LiteralPath $cfg) { $targets += Get-ChildItem -LiteralPath $cfg -Filter 'OldenEra*' -ErrorAction SilentlyContinue | ForEach-Object FullName }
    if ($RemoveBepInEx) {
        foreach ($name in 'BepInEx', 'dotnet', 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt') {
            $p = Join-Path $dir $name; if (Test-Path -LiteralPath $p) { $targets = @($targets | Where-Object { -not $_.StartsWith($p, 'OrdinalIgnoreCase') }) + $p }
        }
    }
    if (-not $targets) { Say '   Nothing of ours is installed.' }
    foreach ($t in $targets) { if (Do-It "delete $t") { Remove-Item -LiteralPath $t -Recurse -Force } }
    Say '   Saves and settings of the game are not touched (they are outside the game''s folder).'
    Say ''; Say $(if ($DryRun) { 'Dry run done: nothing changed.' } else { 'Removed.' })
    return
}

# ---- 2. versions -------------------------------------------------------------------------------------------------
Step 'Checking versions'
# The version the mods compare (Application.version, "0.82.07"): Unity keeps it in globalgamemanagers as a
# length-prefixed string; the only version-like string there besides Unity's own (the same search as tools/game-names).
function Get-GameVersion($dir) {
    $file = Join-Path $dir 'HeroesOldenEra_Data\globalgamemanagers'
    if (-not (Test-Path -LiteralPath $file)) { return $null }
    $text = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($file))
    $found = @{}
    foreach ($m in [regex]::Matches($text, '[\x03-\x18]\x00\x00\x00(\d+\.\d+(?:\.\d+)+)(?![\d.])')) {
        if ($m.Groups[1].Value.Length -eq [int][char]$m.Value[0]) { $found[$m.Groups[1].Value] = $true }
    }
    if ($found.Count -eq 1) { return @($found.Keys)[0] }
    $null
}
$gameVersion = Get-GameVersion $dir
if (-not $gameVersion) {
    if ($game.Kind -eq 'Game Pass' -and (Test-Path -LiteralPath (Join-Path $dir 'appxmanifest.xml'))) {
        # appxmanifest 0.82.7.0 is the game's 0.82.07: major.minor.(patch as two digits)
        $v = ([xml](Get-Content -LiteralPath (Join-Path $dir 'appxmanifest.xml'))).Package.Identity.Version.Split('.')
        $gameVersion = '{0}.{1}.{2:00}' -f $v[0], $v[1], [int]$v[2]
    } else { $gameVersion = (Read-Host "The game's version as the main menu shows it (like 0.82.07)").Trim() }
}
Say "   Game version: $gameVersion"

$coreDll = Join-Path $dir 'BepInEx\core\BepInEx.Core.dll'
$haveBepInEx = $null
if (Test-Path -LiteralPath $coreDll) { $haveBepInEx = ((Get-Item -LiteralPath $coreDll).VersionInfo.ProductVersion -split '\+')[0] }
Say $(if ($haveBepInEx) { "   BepInEx installed: $haveBepInEx (needed: $BepInExVer)" } else { "   BepInEx: not installed (needed: $BepInExVer)" })

$cfgFile = Join-Path $dir 'BepInEx\config\BepInEx.cfg'
if ((Test-Path -LiteralPath $cfgFile) -and ((Get-Content -LiteralPath $cfgFile -Raw) -match '(?m)^[ \t]*UnhollowerDeobfuscationRegex[ \t]*=[ \t]*\S')) {
    Stop-Here ("BepInEx.cfg has a non-empty UnhollowerDeobfuscationRegex (the 'version-independent deobfuscation' setup): the mods do not load with it.`n" +
        "Empty the value, delete the folder BepInEx\interop and run the script again.")
}

# ---- the catalog: is there a manager for this game version? ---------------------------------------------------------
Step 'Reading the catalog'
$catalog = Invoke-RestMethod -Uri $CatalogUrl
$entry = $catalog.mods | Where-Object id -eq $ManagerId
if (-not $entry) { Stop-Here 'The catalog has no mod manager.' }
$meta = Invoke-RestMethod -Uri $entry.meta
$releases = @($meta.releases | Sort-Object { [version]$_.version } -Descending)
$release = $releases | Where-Object { $_.games -contains $gameVersion } | Select-Object -First 1
if (-not $release) {
    $built = @($releases | ForEach-Object { $_.games } | Select-Object -Unique)
    Stop-Here ("The mods are not built for the game version $gameVersion yet. They are built for: $($built -join ', ').`n" +
        'Nothing was installed. Watch the repository for a release for your version.')
}
Say "   Mod manager $($release.version) for the game $gameVersion (BepInEx $($release.bepinex))."
if ($release.bepinex -ne $BepInExVer) { Say "   Note: the release wants BepInEx $($release.bepinex), this script knows $BepInExVer." -ForegroundColor Yellow }

# ---- 3. BepInEx --------------------------------------------------------------------------------------------------
$work = Join-Path ([IO.Path]::GetTempPath()) ('oldenera-install-' + [guid]::NewGuid().ToString('N'))
function Download($url, $name) {
    New-Item -ItemType Directory -Force $work | Out-Null
    $path = Join-Path $work $name
    Say "   downloading $url"
    Invoke-WebRequest -Uri $url -OutFile $path -UseBasicParsing
    $path
}
function Unpack($zipPath, $to) {
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($e in $zip.Entries) {
            if (-not $e.Name) { continue }
            $target = Join-Path $to ($e.FullName -replace '/', '\')
            New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($e, $target, $true)
        }
    } finally { $zip.Dispose() }
}

Step 'BepInEx'
if ($haveBepInEx -eq $BepInExVer) { Say '   Already the right version.' }
else {
    $replace = $true
    if ($haveBepInEx) {
        Say "   Another version is installed ($haveBepInEx). The mods need $BepInExVer; replacing it overwrites BepInEx's own files, not your plugins."
        if (-not $DryRun) { $replace = (Read-Host 'Replace it? (y/n)') -match '^[yYдД]' }
    }
    if ($replace) {
        if (Do-It "download BepInEx $BepInExVer (about 34 MB), check SHA-256 $BepInExSha, unpack into $dir") {
            $zipPath = Download $BepInExUrl 'bepinex.zip'
            $actual = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLower()
            if ($actual -ne $BepInExSha) { Stop-Here "BepInEx download does not match its SHA-256 (got $actual). Nothing was installed." }
            Say '   SHA-256 matches.'
            Unpack $zipPath $dir
            Say '   BepInEx unpacked.'
        }
    } else { Stop-Here 'BepInEx left as it is; the mods will not work with it. Stopped.' }
}

# ---- 4. the mod manager ------------------------------------------------------------------------------------------
Step 'Mod manager'
$tag = "manager-v$($release.version)"
$asset = "olden-era-mod-manager-$($release.version)-for-game-$gameVersion.zip"
$assetUrl = "https://github.com/$Repo/releases/download/$tag/$asset"
if (Do-It "download $assetUrl, check every DLL against the catalog's SHA-256, unpack into $dir") {
    $zipPath = Download $assetUrl $asset
    $zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        foreach ($f in $release.files) {
            $e = $zip.Entries | Where-Object { $_.Name -eq $f.name } | Select-Object -First 1
            if (-not $e) { Stop-Here "$($f.name) is not in $asset. Nothing was installed." }
            $s = $e.Open(); try { $actual = Sha256Of $s } finally { $s.Dispose() }
            if ($actual -ne $f.sha256) { Stop-Here "$($f.name) does not match the catalog's SHA-256. Nothing was installed." }
            Say "   $($f.name): SHA-256 matches"
        }
    } finally { $zip.Dispose() }
    Unpack $zipPath $dir
    Say '   Mod manager installed (BepInEx\plugins, BepInEx\patchers).'
}
if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }

# ---- 5/6. mods and the first start -------------------------------------------------------------------------------
Step 'Done'
Say '   The mods themselves are installed from the game: main menu, Additional, Mods (Install buttons).'
Say '   The first start takes about a minute: BepInEx makes its wrappers for the game''s code. Wait for the main menu.'
if ($DryRun) { Say ''; Say 'Dry run done: nothing changed.'; return }
if ((Read-Host 'Start the game now? (y/n)') -match '^[yYдД]') {
    if ($game.Family) { Start-Process "shell:AppsFolder\$($game.Family)!Game" }
    elseif ($game.Kind -eq 'Steam') { Say '   Start the game from Steam.' }
    else { Start-Process (Join-Path $dir 'HeroesOldenEra.exe') -WorkingDirectory $dir }
}
