# Turns the artist's main menu art into the game's menu art.
#
# Put the artist's exports in ArtSource/Menu/ and run:
#     powershell -ExecutionPolicy Bypass -File Tools/process_menu.ps1
# then in Unity: Farhad > Build Menu Scene.
#
#   menu.jpg   -> Assets/Art/Menu/menu.jpg   - the 16:9 background, copied as is
#   button.png    -> Assets/Art/Menu/button.png    - the stone slab every button is drawn on, cut out
#                                                    of its transparent canvas (with a few pixels to spare)
#   game_name.png -> Assets/Art/Menu/game_name.png - the game's name, for the top left corner, cut
#                                                    out the same way

param(
    [string]$Source = "ArtSource/Menu",
    [string]$Target = "Assets/Art/Menu",
    [int]$Margin = 4
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class MenuFix
{
    // The drawing on the canvas: the rows and columns where enough is drawn, so stray specks of
    // faint paint around it are left out.
    public static Rectangle FindDrawing(Bitmap bmp)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var px = new byte[data.Stride * bmp.Height];
        Marshal.Copy(data.Scan0, px, 0, px.Length);
        bmp.UnlockBits(data);
        var cols = new int[bmp.Width];
        var rows = new int[bmp.Height];
        for (int y = 0; y < bmp.Height; y++)
        for (int x = 0; x < bmp.Width; x++)
            if (px[y * data.Stride + x * 4 + 3] > 128) { cols[x]++; rows[y]++; }
        int x0 = First(cols), x1 = Last(cols), y0 = First(rows), y1 = Last(rows);
        if (x1 < 0 || y1 < 0) throw new Exception("The image is empty.");
        return Rectangle.FromLTRB(x0, y0, x1 + 1, y1 + 1);
    }

    const int MinCount = 3;
    static int First(int[] counts) { for (int i = 0; i < counts.Length; i++) if (counts[i] >= MinCount) return i; return -1; }
    static int Last(int[] counts) { for (int i = counts.Length - 1; i >= 0; i--) if (counts[i] >= MinCount) return i; return -1; }
}
"@

$outDir = Join-Path $root $Target
New-Item -ItemType Directory -Force $outDir | Out-Null

Copy-Item (Join-Path $root "$Source/menu.jpg") (Join-Path $outDir "menu.jpg") -Force
Write-Host "menu.jpg copied"

# Cuts a drawing out of its transparent canvas, with a few pixels to spare.
function Save-Cutout([string]$name) {
    $src = New-Object System.Drawing.Bitmap (Join-Path $root "$Source/$name")
    try {
        $box = [MenuFix]::FindDrawing($src)
        $box = [System.Drawing.Rectangle]::FromLTRB(
            [Math]::Max(0, $box.Left - $Margin), [Math]::Max(0, $box.Top - $Margin),
            [Math]::Min($src.Width, $box.Right + $Margin), [Math]::Min($src.Height, $box.Bottom + $Margin))
        $dst = $src.Clone($box, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try { $dst.Save((Join-Path $outDir $name), [System.Drawing.Imaging.ImageFormat]::Png) }
        finally { $dst.Dispose() }
        Write-Host "$name cut to $($box.Width)x$($box.Height) at ($($box.X), $($box.Y))"
    }
    finally { $src.Dispose() }
}

Save-Cutout "button.png"
Save-Cutout "game_name.png"
