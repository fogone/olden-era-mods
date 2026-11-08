# Draws the catalog's icon (catalog/icon.png, 256x256): a gold hexagon on the game's dark blue with three small
# hexagons inside, the mods as pieces. Run it again after a change; publish-mods.ps1 puts it into catalog.json (--icon).
Add-Type -AssemblyName System.Drawing
$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.Color]::Transparent)

function Hex([double]$cx, [double]$cy, [double]$r) {
    $points = for ($i = 0; $i -lt 6; $i++) {
        $a = [Math]::PI / 180 * (60 * $i - 90)
        New-Object System.Drawing.PointF([float]($cx + $r * [Math]::Cos($a)), [float]($cy + $r * [Math]::Sin($a)))
    }
    , [System.Drawing.PointF[]]$points
}

$navy = [System.Drawing.Color]::FromArgb(255, 21, 36, 67)
$navyLight = [System.Drawing.Color]::FromArgb(255, 36, 64, 110)
$gold = [System.Drawing.Color]::FromArgb(255, 201, 164, 99)
$goldDark = [System.Drawing.Color]::FromArgb(255, 138, 109, 62)

$outer = Hex 128 128 120
$g.FillPolygon((New-Object System.Drawing.SolidBrush($navy)), $outer)
$g.DrawPolygon((New-Object System.Drawing.Pen($goldDark, 14)), $outer)
$g.DrawPolygon((New-Object System.Drawing.Pen($gold, 7)), $outer)

# Three hexagons, two below one, like cells of the battle grid.
$r = 33
$dx = $r * [Math]::Sqrt(3) / 2 + 3
foreach ($c in @(@(128, 92), @((128 - $dx), (92 + 1.5 * $r + 4)), @((128 + $dx), (92 + 1.5 * $r + 4)))) {
    $h = Hex $c[0] $c[1] $r
    $g.FillPolygon((New-Object System.Drawing.SolidBrush($navyLight)), $h)
    $g.DrawPolygon((New-Object System.Drawing.Pen($gold, 6)), $h)
}

$out = Join-Path $PSScriptRoot 'icon.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"$out $((Get-Item $out).Length) bytes"
