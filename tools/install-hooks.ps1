# Installs this checkout's git hooks: before a push, tools/check-public.ps1 checks the commits the push sends
# (nothing personal, no secrets, no game files). The private words: private-patterns.txt in the repository's root,
# not in git (see check-public.ps1).
#   .\tools\install-hooks.ps1
$root = Split-Path $PSScriptRoot -Parent
$hook = Join-Path $root '.git\hooks\pre-push'
$text = @'
#!/bin/sh
# tools/install-hooks.ps1: the commits a push sends are checked by tools/check-public.ps1.
while read local_ref local_sha remote_ref remote_sha; do
  [ "$local_sha" = "0000000000000000000000000000000000000000" ] && continue
  base="origin/main"
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools/check-public.ps1 -Base "$base" -Head "$local_sha" || exit 1
done
exit 0
'@
[IO.File]::WriteAllText($hook, $text.Replace("`r`n", "`n"), (New-Object Text.UTF8Encoding($false)))
"pre-push hook: $hook"
