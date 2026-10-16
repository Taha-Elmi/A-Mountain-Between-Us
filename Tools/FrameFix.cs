using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Prepares Farhad's exported animation frames for Unity:
//  1. crops every frame to the same box (so frames stay aligned, and stray specks outside it go),
//  2. fills see-through holes inside the figure (transparent areas not connected to the frame's
//     edge) with white, because the export dropped the white fills of the face, hands and cloth.
public static class FrameFix
{
    public static void Process(string input, string output, int cropX, int cropY, int cropW, int cropH)
    {
        using (var src = new Bitmap(input))
        using (var dst = new Bitmap(cropW, cropH, PixelFormat.Format32bppArgb))
        {
            using (var g = Graphics.FromImage(dst))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.DrawImage(src, new Rectangle(0, 0, cropW, cropH), new Rectangle(cropX, cropY, cropW, cropH), GraphicsUnit.Pixel);
            }

            var data = dst.LockBits(new Rectangle(0, 0, cropW, cropH), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            var px = new byte[data.Stride * cropH];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            FillHoles(px, data.Stride, cropW, cropH);
            Marshal.Copy(px, 0, data.Scan0, px.Length);
            dst.UnlockBits(data);
            dst.Save(output, ImageFormat.Png);
        }
    }

    // Pixels count as background when they are mostly transparent and reachable from the edge.
    const int OpenAlpha = 200;

    static void FillHoles(byte[] px, int stride, int w, int h)
    {
        var outside = new bool[w * h];
        var queue = new Queue<int>();
        Action<int, int> seed = (x, y) =>
        {
            int k = y * w + x;
            if (!outside[k] && px[y * stride + x * 4 + 3] < OpenAlpha) { outside[k] = true; queue.Enqueue(k); }
        };
        for (int x = 0; x < w; x++) { seed(x, 0); seed(x, h - 1); }
        for (int y = 0; y < h; y++) { seed(0, y); seed(w - 1, y); }

        while (queue.Count > 0)
        {
            int k = queue.Dequeue(), x = k % w, y = k / w;
            if (x > 0) seed(x - 1, y);
            if (x < w - 1) seed(x + 1, y);
            if (y > 0) seed(x, y - 1);
            if (y < h - 1) seed(x, y + 1);
        }

        // Everything inside the figure becomes opaque: partly transparent pixels are laid over white.
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (outside[y * w + x]) continue;
            int i = y * stride + x * 4;
            float a = px[i + 3] / 255f;
            for (int c = 0; c < 3; c++) px[i + c] = (byte)Math.Round(px[i + c] * a + 255f * (1f - a));
            px[i + 3] = 255;
        }
    }
}
