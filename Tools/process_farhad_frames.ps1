# Turns the artist's exported animation frames into the sprites the game uses.
#
# Put the artist's full-canvas exports (3000x2000, Farhad standing in the same spot in every
# animation) in ArtSource/Characters/Farhad/<Animation>/ and run:
#     powershell -ExecutionPolicy Bypass -File Tools/process_farhad_frames.ps1
# then in Unity: Farhad > Rebuild Farhad Animations.
#
# Every frame of every animation is cropped to the same box (so Farhad doesn't shift when one
# animation changes to another), holes inside the figure are filled with white (see FrameFix.cs),
# and the result is written to Assets/Art/Characters/Farhad/<Animation>/farhad_<name>_NN.png.
# The idle pose is a copy of one walk frame.
#
# Some frames were exported on a larger canvas, grown evenly on every side around the same drawing
# (Action7-9 are 3496x2362): those are shifted back onto the 3000x2000 layout before cropping.
#
# If you change the crop box, update FeetPivot in Assets/Editor/FarhadArtSetup.cs to match.

param(
    [string]$Target = "Assets/Art/Characters/Farhad",
    [int]$IdleFrame = 4,
    # Crop box on the 3000x2000 canvas: holds Farhad and his full pickaxe swing in every frame (the
    # low forward strike of Action9 reaches furthest, to x ~2375), with room around him for his
    # glow (see FarhadHalo).
    [int]$CropX = 830, [int]$CropY = 0, [int]$CropW = 1600, [int]$CropH = 1720,
    [int]$CanvasW = 3000, [int]$CanvasH = 2000
)

# Folder in ArtSource, the artist's file prefix, and the name used in the game.
$animations = @(
    @{ Folder = "Walk";   Prefix = "Walking"; Name = "walk" },
    @{ Folder = "Strike"; Prefix = "Action";  Name = "strike" }
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -Path (Join-Path $PSScriptRoot "FrameFix.cs")

foreach ($animation in $animations) {
    $sourceDir = Join-Path $root "ArtSource/Characters/Farhad/$($animation.Folder)"
    if (-not (Test-Path $sourceDir)) { continue }
    $outDir = Join-Path $root "$Target/$($animation.Folder)"
    New-Item -ItemType Directory -Force $outDir | Out-Null

    $frames = Get-ChildItem $sourceDir -Filter "$($animation.Prefix)*.png" | Sort-Object { [int]($_.BaseName -replace '\D', '') }
    foreach ($frame in $frames) {
        $number = [int]($frame.BaseName -replace '\D', '')
        $out = Join-Path $outDir ("farhad_{0}_{1:00}.png" -f $animation.Name, $number)
        $image = [System.Drawing.Image]::FromFile($frame.FullName)
        $offsetX = [int][math]::Floor(($image.Width - $CanvasW) / 2)
        $offsetY = [int][math]::Floor(($image.Height - $CanvasH) / 2)
        $image.Dispose()
        [FrameFix]::Process($frame.FullName, $out, $CropX + $offsetX, $CropY + $offsetY, $CropW, $CropH)
        Write-Output ("{0}/{1} -> {2}" -f $animation.Folder, $frame.Name, (Split-Path $out -Leaf))
    }
}

$idle = Join-Path $root ("$Target/Walk/farhad_walk_{0:00}.png" -f $IdleFrame)
Copy-Item $idle (Join-Path $root "$Target/farhad_idle.png") -Force
Write-Output ("idle pose <- farhad_walk_{0:00}.png" -f $IdleFrame)
