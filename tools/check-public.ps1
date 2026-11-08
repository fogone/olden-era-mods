# Checks what a change adds to this public repository: nothing personal, no secrets, nothing of the game.
# Runs on every pull request (.github/workflows/check.yml) and before a push from a checkout (the pre-push hook,
# tools/install-hooks.ps1). Only the lines a change adds are read, and the files it adds.
#   pwsh tools/check-public.ps1 -Base origin/main            -> the commits not on origin/main yet
#   pwsh tools/check-public.ps1 -Base origin/main -Head HEAD
# Words that must not be public (people's names, private places) are not written here, which would publish them: they
# come from the environment variable PRIVATE_PATTERNS (the repository's secret of that name in CI) and from the
# untracked file private-patterns.txt next to this repository's root (a checkout's own), one regular expression a line.
# Works in Windows PowerShell 5.1 and in pwsh.
param([Parameter(Mandatory)][string]$Base, [string]$Head = 'HEAD')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# Paths and addresses of a machine, secrets by their known shapes.
$rules = [ordered]@{
    'a Windows user folder'      = '[A-Za-z]:[\\/]+Users[\\/]+[^\\/\s]+'
    'a home folder'              = '(^|[\s"''(=])/(home|Users)/[a-z][\w.-]*/'
    'a WSL path'                 = 'wsl\.localhost|wsl\$'
    'an AppData path'            = 'AppData[\\/]+(Local|Roaming|LocalLow)[\\/]+(?!Unfrozen)'
    'a GitHub token'             = '\b(ghp|gho|ghu|ghs|ghr)_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}'
    'an AWS key'                 = '\bAKIA[0-9A-Z]{16}\b'
    'a Slack token'              = '\bxox[abprs]-[A-Za-z0-9-]{10,}'
    'a private key'              = '-----BEGIN [A-Z ]*PRIVATE KEY-----'
    'an API key'                 = '(?i)\b(api[_-]?key|secret|password|passwd|token)\b\s*[:=]\s*["''][^"''\s]{12,}["'']'
}
# E-mail addresses: only these may be added.
$allowedMail = @('fogone@gmail.com', 'noreply@anthropic.com', 'noreply@github.com')
$mailPattern = '[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}'
# Files that are not ours to publish: the game's or built ones.
$blockedFiles = '\.(dll|exe|pdb|zip|7z|rar|assets|bundle|bytes|dat|mp4|bank|resS)$'

$private = @()
if ($env:PRIVATE_PATTERNS) { $private += $env:PRIVATE_PATTERNS -split "`r?`n" }
$privateFile = Join-Path $root 'private-patterns.txt'
if (Test-Path $privateFile) { $private += Get-Content $privateFile -Encoding UTF8 }
$private = @($private | Where-Object { $_ -and -not $_.StartsWith('#') } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($private.Count -eq 0) { Write-Warning 'no private patterns (PRIVATE_PATTERNS or private-patterns.txt): names are not checked' }

$range = "$Base...$Head"
$problems = New-Object System.Collections.Generic.List[string]

# Added files that should not be here.
foreach ($line in (git -C $root diff --name-status --diff-filter=AR $range)) {
    $path = ($line -split "`t")[-1]
    if ($path -match $blockedFiles) { $problems.Add("${path}: a binary or the game's file") }
    if ($path -eq 'CLAUDE.local.md' -or $path -eq 'private-patterns.txt') { $problems.Add("${path}: a checkout's own file") }
}

# The added lines, by file.
$file = $null; $number = 0
foreach ($line in (git -C $root diff -U0 --no-color $range)) {
    if ($line -match '^\+\+\+ b/(.*)') { $file = $Matches[1]; continue }
    if ($line -match '^@@ -\d+(,\d+)? \+(\d+)') { $number = [int]$Matches[2]; continue }
    if (-not $line.StartsWith('+') -or $line.StartsWith('+++')) { continue }
    $text = $line.Substring(1)
    foreach ($rule in $rules.GetEnumerator()) {
        if ($text -match $rule.Value) {
            # A secret is not echoed whole: the check's log is public.
            $found = if ($rule.Key -match 'token|key') { $Matches[0].Substring(0, [Math]::Min(6, $Matches[0].Length)) + '...' } else { $Matches[0] }
            $problems.Add("${file}:${number}: $($rule.Key): $found")
        }
    }
    foreach ($m in [regex]::Matches($text, $mailPattern)) {
        if ($allowedMail -notcontains $m.Value.ToLowerInvariant()) { $problems.Add("${file}:${number}: an e-mail address: $($m.Value)") }
    }
    foreach ($p in $private) {
        # The private words are not echoed: the log of a public repository's check is public too.
        if ($text -match "(?i)$p") { $problems.Add("${file}:${number}: a private word (one of the private patterns)") }
    }
    $number++
}

# The commits' authors and committers.
foreach ($who in (git -C $root log --format='%ae%n%ce' "$Base..$Head" | Sort-Object -Unique)) {
    if ($who -and $allowedMail -notcontains $who.ToLowerInvariant()) { $problems.Add("a commit by $who") }
}

if ($problems.Count -gt 0) {
    "Not for a public repository:"
    $problems | ForEach-Object { "  $_" }
    exit 1
}
"check-public: nothing personal, no secrets, no game files in $range"
