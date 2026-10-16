using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Prepares the artist's rock tile for Unity: finds the tile drawn on the export canvas (ignoring
// stray specks), crops it, lays the line art over a paper colour so the stones are opaque, tints it,
// and resizes it to the game's tile size.
public static class TileFix
{
    // The tile's square on the canvas: rows and columns where enough ink is drawn.
    public static Rectangle FindTile(string path)
    {
        using (var bmp = new Bitmap(path))
        {
            int stride;
            var px = Read(bmp, out stride);
            int w = bmp.Width, h = bmp.Height;
            var cols = new int[w];
            var rows = new int[h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * stride + x * 4 + 3] > 128) { cols[x]++; rows[y]++; }
            int x0 = First(cols, 20), x1 = Last(cols, 20), y0 = First(rows, 20), y1 = Last(rows, 20);
            return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }
    }

    // paper: what shows between the lines; ink: what the black lines become.
    public static void Make(string input, Rectangle tile, string output, int size, Color paper, Color ink)
    {
        using (var src = new Bitmap(input))
        using (var flat = new Bitmap(tile.Width, tile.Height, PixelFormat.Format32bppArgb))
        {
            int stride;
            var px = Read(src, out stride);
            var data = flat.LockBits(new Rectangle(0, 0, tile.Width, tile.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            var outPx = new byte[data.Stride * tile.Height];
            for (int y = 0; y < tile.Height; y++)
            for (int x = 0; x < tile.Width; x++)
            {
                int i = (tile.Y + y) * stride + (tile.X + x) * 4, o = y * data.Stride + x * 4;
                float a = px[i + 3] / 255f;
                // How dark the drawing is here (0 = paper, 1 = full ink), from its colour and coverage.
                float darkness = a * (1f - (px[i] + px[i + 1] + px[i + 2]) / (3f * 255f));
                outPx[o] = Lerp(paper.B, ink.B, darkness);
                outPx[o + 1] = Lerp(paper.G, ink.G, darkness);
                outPx[o + 2] = Lerp(paper.R, ink.R, darkness);
                outPx[o + 3] = 255;
            }
            Marshal.Copy(outPx, 0, data.Scan0, outPx.Length);
            flat.UnlockBits(data);

            using (var dst = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (var wrap = new ImageAttributes())
                {
                    wrap.SetWrapMode(WrapMode.TileFlipXY); // no soft dark rim at the edges
                    g.DrawImage(flat, new Rectangle(0, 0, size, size), 0, 0, tile.Width, tile.Height, GraphicsUnit.Pixel, wrap);
                }
                dst.Save(output, ImageFormat.Png);
            }
        }
    }

    // Windows PowerShell compiles this with C# 5: no expression-bodied members or inline out variables.
    static byte Lerp(byte a, byte b, float t) { return (byte)Math.Round(a + (b - a) * t); }

    static int First(int[] counts, int min)
    {
        for (int i = 0; i < counts.Length; i++) if (counts[i] >= min) return i;
        return 0;
    }

    static int Last(int[] counts, int min)
    {
        for (int i = counts.Length - 1; i >= 0; i--) if (counts[i] >= min) return i;
        return counts.Length - 1;
    }

    static byte[] Read(Bitmap bmp, out int stride)
    {
        var d = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var buf = new byte[d.Stride * d.Height];
        Marshal.Copy(d.Scan0, buf, 0, buf.Length);
        stride = d.Stride;
        bmp.UnlockBits(d);
        return buf;
    }
}
