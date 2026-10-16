# Turns the artist's exported walk frames into the sprites the game uses.
#
# Put the artist's full-canvas exports (3000x2000, named Walking1.png, Walking2.png, ...) in
# ArtSource/Characters/Farhad/Walk/ and run:
#     powershell -ExecutionPolicy Bypass -File Tools/process_farhad_frames.ps1
#
# Every frame is cropped to the same box (so the walk stays aligned), holes inside the figure are
# filled with white (see FrameFix.cs), and the result is written to
# Assets/Art/Characters/Farhad/Walk/farhad_walk_NN.png. The idle pose is a copy of one walk frame.

param(
    [string]$Source = "ArtSource/Characters/Farhad/Walk",
    [string]$Target = "Assets/Art/Characters/Farhad",
    [int]$IdleFrame = 4,
    # Crop box on the 3000x2000 canvas: holds Farhad in every frame, feet ~20 px above the bottom.
    [int]$CropX = 1110, [int]$CropY = 150, [int]$CropW = 845, [int]$CropH = 1510
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot "FrameFix.cs")

$sourceDir = Join-Path $root $Source
$walkDir = Join-Path $root "$Target/Walk"
New-Item -ItemType Directory -Force $walkDir | Out-Null

$frames = Get-ChildItem $sourceDir -Filter "Walking*.png" | Sort-Object { [int]($_.BaseName -replace '\D', '') }
foreach ($frame in $frames) {
    $number = [int]($frame.BaseName -replace '\D', '')
    $out = Join-Path $walkDir ("farhad_walk_{0:00}.png" -f $number)
    [FrameFix]::Process($frame.FullName, $out, $CropX, $CropY, $CropW, $CropH)
    Write-Output ("{0} -> {1}" -f $frame.Name, (Split-Path $out -Leaf))
}

$idle = Join-Path $walkDir ("farhad_walk_{0:00}.png" -f $IdleFrame)
Copy-Item $idle (Join-Path $root "$Target/farhad_idle.png") -Force
Write-Output ("idle pose <- farhad_walk_{0:00}.png" -f $IdleFrame)
