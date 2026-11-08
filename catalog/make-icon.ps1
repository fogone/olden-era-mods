# Draws the catalog's icon (catalog/icon.png, 256x256): a gold hexagon on the game's dark blue with the nobirds sign
# (fogone's own logo, nobirds.org) inside. Run it again after a change; publish-mods.ps1 puts it into catalog.json (--icon).
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

# The nobirds sign (nobirds-sign.png: the bird in the red "no" circle of nobirds.org's logo) on a light disc.
$sign = [System.Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'nobirds-sign.png'))
$g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 233, 218))), 46, 46, 164, 164)
$g.DrawImage($sign, 44, 44, 168, 168)
$sign.Dispose()
$out = Join-Path $PSScriptRoot 'icon.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
"$out $((Get-Item $out).Length) bytes"
