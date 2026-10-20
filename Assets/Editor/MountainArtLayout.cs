using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Farhad.EditorTools
{
    // Reads the level's layout from the artist's background layers in ArtSource/Background/:
    //   sky, background-mountains, palace - the scenery, drawn with the mountain left transparent;
    //   mountain                          - the whole mountain and the ground, black ink on transparent,
    //                                       meant to be seen over white paper.
    // The mountain is what lies inside the mountain layer's outline, not covered by the scenery and
    // connected to the bottom of the picture; it becomes rock tiles.
    //
    // The layers are flattened over white into one picture, which is both the backdrop and what the
    // rock tiles show: each rock cell shows its own square of it (see PictureTile), so the mountain
    // looks exactly as drawn until a cell is dug away, and then the back wall shows there.
    //
    // Scale: the picture spans LevelWidth tiles across. Exported at 3840x2160 that is 128 px per tile,
    // the same as a tile on a 1080p screen with the camera zoomed in 2x, so the art is pixel-sharp.
    // The art is shifted up or down by whole pixels so the drawn ground line where Farhad starts lies
    // as near a tile edge as it can, and his feet meet it.
    public class MountainArtLayout
    {
        public const string SourceDir = "ArtSource/Background";
        public const string ArtDir = "Assets/Art/Background";
        public const int LevelWidth = 30;

        const string PicturePath = ArtDir + "/mountain-picture.png";
        const string MaskPath = ArtDir + "/mountain-mask.png";

        // Back to front.
        static readonly string[] Layers = { "sky", "background-mountains", "palace", MountainLayer };
        static readonly string[] SceneryLayers = { "sky", "background-mountains", "palace" };
        const string MountainLayer = "mountain";

        public bool[,] Shape;          // [x, y]: true where the cell is rock
        public Vector2Int Entrance;    // foot of the mountain, on the ground Farhad starts on
        public Vector2Int Exit;        // first open cell above the rock at the palace's foot
        public float SpawnX = 2.5f;
        public float ArtOffsetY;       // world y of the picture's bottom edge, a whole number of pixels
        public float PixelsPerTile;
        public int PixelsPerCell => Mathf.RoundToInt(PixelsPerTile);
        public Texture2D Picture;      // the flattened picture laid on the grid, set by PlaceLayers

        // Per pixel of the picture (bottom row first): true where it shows the mountain.
        bool[] mountainPixels;
        int pictureWidth, pictureHeight;

        public static bool HasArt => File.Exists($"{SourceDir}/{MountainLayer}.png");

        public static MountainArtLayout Analyze()
        {
            var scenery = new List<Color32[]>();
            int w = 0, h = 0;
            foreach (var name in SceneryLayers) scenery.Add(LoadPixels(name, out w, out h));
            var mountain = LoadPixels(MountainLayer, out _, out _);
            if (w % LevelWidth != 0)
                Debug.LogWarning($"The background art is {w} px wide, which doesn't split into {LevelWidth} whole-pixel tiles: " +
                                 "the squares cut from it will be off by a fraction of a pixel. Export it 1920 or 3840 px wide.");

            var layout = new MountainArtLayout { PixelsPerTile = w / (float)LevelWidth };
            float px = layout.PixelsPerTile;
            var inside = MountainPixels(mountain, scenery, w, h);

            // The ground line: how high the ground reaches under Farhad where he starts.
            var heights = new List<int>();
            int spawnLeft = Mathf.Max(0, Mathf.FloorToInt((layout.SpawnX - 0.5f) * px));
            int spawnRight = Mathf.Min(w, Mathf.CeilToInt((layout.SpawnX + 0.5f) * px));
            for (int x = spawnLeft; x < spawnRight; x++)
            {
                int r = 0;
                while (r < h && inside[r * w + x]) r++;
                heights.Add(r);
            }
            heights.Sort();
            float groundTiles = heights[heights.Count / 2] / px;
            int groundLevel = Mathf.RoundToInt(groundTiles);
            layout.ArtOffsetY = Mathf.Round((groundLevel - groundTiles) * px) / px;

            layout.mountainPixels = inside;
            layout.pictureWidth = w;
            layout.pictureHeight = h;

            // A cell is rock when at least a quarter of the picture under it is mountain: the rest of
            // its square shows the sky as drawn, while a gap left inside the outline would show.
            // Cells below the picture are rock; cells above it are open.
            int rows = Mathf.RoundToInt(h / px + layout.ArtOffsetY);
            layout.Shape = new bool[LevelWidth, rows];
            for (int cx = 0; cx < LevelWidth; cx++)
            for (int cy = 0; cy < rows; cy++)
            {
                int x0 = Mathf.FloorToInt(cx * px), x1 = Mathf.FloorToInt((cx + 1) * px);
                int r0 = Mathf.FloorToInt((cy - layout.ArtOffsetY) * px), r1 = Mathf.FloorToInt((cy + 1 - layout.ArtOffsetY) * px);
                int inCell = 0, total = 0;
                for (int r = Mathf.Max(0, r0); r < Mathf.Min(h, r1); r++)
                for (int x = x0; x < x1; x++)
                {
                    total++;
                    if (inside[r * w + x]) inCell++;
                }
                layout.Shape[cx, cy] = total == 0 ? r1 <= 0 : inCell * 4 >= total;
            }

            // The vein starts at the mountain's foot and comes out at the palace, near the right edge.
            int entranceX = 0;
            while (entranceX < LevelWidth - 1 && !layout.Shape[entranceX, groundLevel]) entranceX++;
            layout.Entrance = new Vector2Int(entranceX, groundLevel);
            int exitX = LevelWidth - 4;
            layout.Exit = new Vector2Int(exitX, TopOf(layout.Shape, exitX));
            return layout;
        }

        // Pixels (bottom row first) showing the mountain or its ground: inside the mountain layer's
        // outline and not covered by the scenery. The layer also has a halftone haze drawn in the air
        // left of the mountain, whose specks are dropped.
        static bool[] MountainPixels(Color32[] mountain, List<Color32[]> scenery, int w, int h)
        {
            var uncovered = Flood(w, h, i =>
            {
                foreach (var layer in scenery) if (layer[i].a > 127) return false;
                return true;
            }, fromBottom: true);
            var outline = Flood(w, h, i => mountain[i].a < 32, fromBottom: false);

            var rough = new bool[w * h];
            for (int i = 0; i < rough.Length; i++) rough[i] = uncovered[i] && !outline[i];
            var smooth = Majority(rough, w, h, 4);

            // Fill holes left inside, and drop specks not joined to the ground.
            var air = Flood(w, h, i => !smooth[i], fromBottom: false);
            return Flood(w, h, i => !air[i], fromBottom: true);
        }

        // Every pixel reachable through passable ones from the picture's bottom (or top) edge.
        static bool[] Flood(int w, int h, System.Func<int, bool> passable, bool fromBottom)
        {
            var reached = new bool[w * h];
            var queue = new Queue<int>();
            void Visit(int i)
            {
                if (reached[i] || !passable(i)) return;
                reached[i] = true;
                queue.Enqueue(i);
            }
            int edgeRow = fromBottom ? 0 : h - 1;
            for (int x = 0; x < w; x++) Visit(edgeRow * w + x);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, r = i / w;
                if (x > 0) Visit(i - 1);
                if (x < w - 1) Visit(i + 1);
                if (r > 0) Visit(i - w);
                if (r < h - 1) Visit(i + w);
            }
            return reached;
        }

        // True where more than half the pixels within radius (a square) are true.
        static bool[] Majority(bool[] pixels, int w, int h, int radius)
        {
            var sums = new int[(w + 1) * (h + 1)]; // sums[(r + 1) * (w + 1) + x + 1]: pixels in rows 0..r, columns 0..x
            for (int r = 0; r < h; r++)
            {
                int row = 0;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[r * w + x]) row++;
                    sums[(r + 1) * (w + 1) + x + 1] = sums[r * (w + 1) + x + 1] + row;
                }
            }
            var result = new bool[w * h];
            for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                int r0 = Mathf.Max(0, r - radius), r1 = Mathf.Min(h, r + radius + 1);
                int x0 = Mathf.Max(0, x - radius), x1 = Mathf.Min(w, x + radius + 1);
                int count = sums[r1 * (w + 1) + x1] - sums[r0 * (w + 1) + x1] - sums[r1 * (w + 1) + x0] + sums[r0 * (w + 1) + x0];
                result[r * w + x] = count * 2 > (r1 - r0) * (x1 - x0);
            }
            return result;
        }

        static int TopOf(bool[,] shape, int x)
        {
            int top = 0;
            for (int y = 0; y < shape.GetLength(1); y++) if (shape[x, y]) top = y + 1;
            return top;
        }

        // Writes the flattened picture, laid on the grid (bottom-left corner at cell 0, 0) and placed
        // as the backdrop, plus a mask in the mountain's exact shape. Returns the mask, which the back
        // wall should be drawn inside of. The picture itself is kept in Picture, for the rock tiles.
        public SpriteMask PlaceLayers(Transform parent)
        {
            if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/Art", "Background");

            // The picture, shifted by the art offset and filling whole rows of cells. Rows below the
            // picture (where the art was shifted up) mirror the ground above them; rows above it
            // repeat its top row.
            int w = pictureWidth, h = pictureHeight, cell = PixelsPerCell;
            int offsetPx = Mathf.RoundToInt(ArtOffsetY * PixelsPerTile);
            int height = Mathf.CeilToInt((h + offsetPx) / (float)cell) * cell;
            var flat = Flatten(out _, out _);
            var picturePixels = new Color32[w * height];
            for (int r = 0; r < height; r++)
            {
                int source = r - offsetPx;
                if (source < 0) source = -source - 1;
                source = Mathf.Min(source, h - 1);
                System.Array.Copy(flat, source * w, picturePixels, r * w, w);
            }
            WritePng(PicturePath, picturePixels, w, height);

            var backdrop = new GameObject("Picture").AddComponent<SpriteRenderer>();
            backdrop.transform.SetParent(parent, false);
            backdrop.sprite = ImportLayer(PicturePath, mipmaps: true);
            backdrop.sortingOrder = -100;
            Picture = backdrop.sprite.texture;

            // The mask: opaque wherever the picture shows the mountain, plus extra opaque rows below the
            // picture so the rock under it (where the art was shifted up) shows too.
            int padRows = Mathf.CeilToInt((ArtOffsetY + 1f) * PixelsPerTile);
            var maskPixels = new Color32[w * (h + padRows)];
            for (int i = 0; i < maskPixels.Length; i++)
            {
                int pictureIndex = i - padRows * w;
                bool mountain = pictureIndex < 0 || mountainPixels[pictureIndex];
                maskPixels[i] = new Color32(255, 255, 255, (byte)(mountain ? 255 : 0));
            }
            WritePng(MaskPath, maskPixels, w, h + padRows);

            var mask = new GameObject("Mountain Mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(parent, false);
            mask.transform.localPosition = new Vector3(0f, ArtOffsetY - padRows / PixelsPerTile, 0f);
            mask.sprite = ImportLayer(MaskPath, mipmaps: false);
            return mask;
        }

        // The layers laid over white paper, back to front (bottom row first).
        static Color32[] Flatten(out int width, out int height)
        {
            int w = 0, h = 0;
            var layers = new List<Color32[]>();
            foreach (var name in Layers) layers.Add(LoadPixels(name, out w, out h));
            var flat = new Color32[w * h];
            for (int i = 0; i < flat.Length; i++)
            {
                Color c = Color.white;
                foreach (var layer in layers)
                {
                    Color l = layer[i];
                    c = Color.Lerp(c, l, l.a);
                }
                c.a = 1f;
                flat[i] = c;
            }
            width = w;
            height = h;
            return flat;
        }

        static void WritePng(string path, Color32[] pixels, int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        Sprite ImportLayer(string path, bool mipmaps)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerTile;
            importer.mipmapEnabled = mipmaps; // the picture is also seen zoomed out over the whole mountain
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomLeft;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Writes Temp/mountain_layout.png: the picture with the tile grid, rock cells tinted, the
        // mountain's outline as read in white, and the vein's entrance (green) and exit (gold) marked.
        // For checking how the art was read.
        [MenuItem("Farhad/Debug Mountain Layout Image")]
        public static void WriteDebugImage()
        {
            var layout = Analyze();
            var flat = Flatten(out int w, out int h);
            float px = layout.PixelsPerTile;
            int offsetPx = Mathf.RoundToInt(layout.ArtOffsetY * px);
            int outH = Mathf.CeilToInt(layout.Shape.GetLength(1) * px);
            var outPixels = new Color32[w * outH];

            for (int r = 0; r < outH; r++)
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0.2f, 0.2f, 0.22f);
                int sr = r - offsetPx;
                bool inPicture = sr >= 0 && sr < h;
                if (inPicture) c = flat[sr * w + x];

                int cx = Mathf.Min(LevelWidth - 1, (int)(x / px)), cy = (int)(r / px);
                var cell = new Vector2Int(cx, cy);
                if (cy < layout.Shape.GetLength(1) && layout.Shape[cx, cy]) c = Color.Lerp(c, new Color(0.85f, 0.55f, 0.3f), 0.45f);
                if (cell == layout.Entrance) c = Color.Lerp(c, Color.green, 0.6f);
                if (cell == layout.Exit) c = Color.Lerp(c, new Color(1f, 0.8f, 0f), 0.6f);
                if (inPicture && x > 0 && layout.mountainPixels[sr * w + x] != layout.mountainPixels[sr * w + x - 1]) c = Color.white;
                if (x % Mathf.RoundToInt(px) == 0 || r % Mathf.RoundToInt(px) == 0) c = Color.Lerp(c, Color.black, 0.35f);
                outPixels[r * w + x] = c;
            }

            WritePng("Temp/mountain_layout.png", outPixels, w, outH);
            Debug.Log($"Mountain layout: {LevelWidth}x{layout.Shape.GetLength(1)} tiles, art offset {layout.ArtOffsetY:0.000}, " +
                      $"entrance {layout.Entrance}, exit {layout.Exit}. Image: Temp/mountain_layout.png");
        }

        static Color32[] LoadPixels(string name, out int width, out int height)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes($"{SourceDir}/{name}.png"));
            width = tex.width;
            height = tex.height;
            var pixels = tex.GetPixels32(); // bottom row first
            Object.DestroyImmediate(tex);
            return pixels;
        }
    }
}
