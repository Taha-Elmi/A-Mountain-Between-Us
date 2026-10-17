# Turns the artist's rock tiles into the game's rock tiles.
#
# Put the artist's exports in ArtSource/Tiles/ (each tile may sit anywhere on a larger transparent
# canvas) and run:
#     powershell -ExecutionPolicy Bypass -File Tools/process_tiles.ps1
# then in Unity: Farhad > Build Dig Prototype Scene.
#
# The stones are drawn looser the more hollow the rock is. Each drawing is laid on white paper and
# written at 256x256 px (a tile shows at 128 px on a 1080p screen):
#   tile-light.png  -> rock_hollow.png - big loose stones: hollow rock, breaks in one strike
#   tile-medium.png -> rock_solid.png  - solid rock, takes several strikes
#   tile-heavy.png  -> rock_hard.png   - small packed stones: hard rock, can't be broken
#   tile-heavy.png  -> back_wall.png   - a dark version, seen where rock has been dug out

param(
    [string]$Source = "ArtSource/Tiles",
    [string]$Target = "Assets/Art/Tiles",
    [int]$Size = 256
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot "TileFix.cs")

$outDir = Join-Path $root $Target
New-Item -ItemType Directory -Force $outDir | Out-Null

$rgb = { param($r, $g, $b) [System.Drawing.Color]::FromArgb($r, $g, $b) }
$white = & $rgb 255 255 255
$black = & $rgb 0 0 0
$tiles = @(
    @{ From = "tile-light";  Name = "rock_hollow"; Paper = $white;             Ink = $black },
    @{ From = "tile-medium"; Name = "rock_solid";  Paper = $white;             Ink = $black },
    @{ From = "tile-heavy";  Name = "rock_hard";   Paper = $white;             Ink = $black },
    @{ From = "tile-heavy";  Name = "back_wall";   Paper = (& $rgb 48 46 46);  Ink = (& $rgb 22 20 20) }
)
foreach ($t in $tiles) {
    $in = Join-Path $root "$Source/$($t.From).png"
    $box = [TileFix]::FindTile($in)
    $out = Join-Path $outDir "$($t.Name).png"
    [TileFix]::Make($in, $box, $out, $Size, $t.Paper, $t.Ink)
    Write-Output ("{0}.png ({1}x{2} px at x {3} y {4}) -> {5}/{6}.png" -f $t.From, $box.Width, $box.Height, $box.X, $box.Y, $Target, $t.Name)
}
