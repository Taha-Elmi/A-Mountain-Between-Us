# Turns the artist's rock tile into the game's three rock tiles.
#
# Put the artist's export in ArtSource/Tiles/tile.png (the tile may sit anywhere on a larger
# transparent canvas) and run:
#     powershell -ExecutionPolicy Bypass -File Tools/process_tiles.ps1
# then in Unity: Farhad > Build Dig Prototype Scene.
#
# From the one drawing it makes, at 256x256 px (a tile shows at 128 px on a 1080p screen):
#   rock_solid.png - the stones on white paper. Hollow rock uses it too: it must look the same.
#   rock_hard.png  - the stones darkened grey, so unbreakable rock can be told apart.
#   back_wall.png  - a dark version, seen where rock has been dug out.

param(
    [string]$Source = "ArtSource/Tiles/tile.png",
    [string]$Target = "Assets/Art/Tiles",
    [int]$Size = 256
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot "TileFix.cs")

$in = Join-Path $root $Source
$outDir = Join-Path $root $Target
New-Item -ItemType Directory -Force $outDir | Out-Null

$tile = [TileFix]::FindTile($in)
Write-Output ("tile found at x {0} y {1}, {2}x{3} px" -f $tile.X, $tile.Y, $tile.Width, $tile.Height)

$rgb = { param($r, $g, $b) [System.Drawing.Color]::FromArgb($r, $g, $b) }
$variants = @(
    @{ Name = "rock_solid"; Paper = (& $rgb 255 255 255); Ink = (& $rgb 0 0 0) },
    @{ Name = "rock_hard";  Paper = (& $rgb 120 122 130); Ink = (& $rgb 15 15 20) },
    @{ Name = "back_wall";  Paper = (& $rgb 48 46 46);    Ink = (& $rgb 22 20 20) }
)
foreach ($v in $variants) {
    $out = Join-Path $outDir "$($v.Name).png"
    [TileFix]::Make($in, $tile, $out, $Size, $v.Paper, $v.Ink)
    Write-Output "-> $Target/$($v.Name).png"
}
