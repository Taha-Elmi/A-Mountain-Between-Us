using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Farhad.EditorTools
{
    // Reads the level's layout from the artist's background layers in ArtSource/Background/:
    //   sky, background-mountains, palace - the scenery, drawn with the mountain left transparent;
    //   mountain-border                   - the rocky outline of the diggable mountain, drawn over it.
    // Everything not covered by the scenery and connected to the bottom of the picture is the
    // mountain (and the ground), which becomes rock tiles.
    //
    // Scale: the picture spans LevelWidth tiles across. Exported at 3840x2160 that is 128 px per tile,
    // the same as a tile on a 1080p screen with the camera zoomed in 2x, so the art is pixel-sharp.
    // The art is shifted up or down by a fraction of a tile so the drawn ground line on the left lies
    // exactly on a tile edge.
    public class MountainArtLayout
    {
        public const string SourceDir = "ArtSource/Background";
        public const string ArtDir = "Assets/Art/Background";
        public const int LevelWidth = 30;

        // Back to front is the order they're listed in; the border is drawn over the rock tiles.
        static readonly (string name, int sortingOrder)[] Layers =
        {
            ("sky", -100), ("background-mountains", -90), ("palace", -80), ("mountain-border", 5),
        };
        const string BorderLayer = "mountain-border";
        static readonly string[] SceneryLayers = { "sky", "background-mountains", "palace" };

        public bool[,] Shape;          // [x, y]: true where the cell is rock
        public bool[,] BackWallShape;  // [x, y]: true where the drawn mountain reaches into the cell at all
        public Vector2Int Entrance;    // foot of the mountain, on the ground Farhad starts on
        public Vector2Int Exit;        // first open cell above the rock at the palace's foot
        public float SpawnX = 2.5f;
        public float ArtOffsetY;       // world y of the picture's bottom edge
        public float PixelsPerTile;

        // Per pixel of the picture (bottom row first): true where it shows the mountain.
        bool[] mountainPixels;
        int pictureWidth, pictureHeight;

        public static bool HasArt => File.Exists($"{SourceDir}/{BorderLayer}.png");

        public static MountainArtLayout Analyze()
        {
            var scenery = new List<Color32[]>();
            int w = 0, h = 0;
            foreach (var name in SceneryLayers) scenery.Add(LoadPixels(name, out w, out h));
            var border = LoadPixels(BorderLayer, out _, out _);

            var layout = new MountainArtLayout { PixelsPerTile = w / (float)LevelWidth };
            float px = layout.PixelsPerTile;

            // The ground line: the row of the border layer with the most ink on the far left.
            int groundRow = 0;
            long mostInk = -1;
            for (int r = 0; r < h; r++)
            {
                long ink = 0;
                for (int x = 0; x < w * 0.15f; x++) ink += border[r * w + x].a;
                if (ink > mostInk) { mostInk = ink; groundRow = r; }
            }
            float groundTiles = (groundRow + 0.5f) / px;
            int groundLevel = Mathf.RoundToInt(groundTiles);
            layout.ArtOffsetY = groundLevel - groundTiles;

            // Mountain pixels: not covered by scenery, and connected to the bottom of the picture.
            var open = new bool[w * h];
            var queue = new Queue<int>();
            void Visit(int x, int r)
            {
                int i = r * w + x;
                if (open[i]) return;
                foreach (var layer in scenery) if (layer[i].a > 127) return;
                open[i] = true;
                queue.Enqueue(i);
            }
            for (int x = 0; x < w; x++) Visit(x, 0);
            while (queue.Count > 0)
            {
                int i = queue.Dequeue(), x = i % w, r = i / w;
                if (x > 0) Visit(x - 1, r);
                if (x < w - 1) Visit(x + 1, r);
                if (r > 0) Visit(x, r - 1);
                if (r < h - 1) Visit(x, r + 1);
            }

            layout.mountainPixels = open;
            layout.pictureWidth = w;
            layout.pictureHeight = h;

            // A cell is rock when at least a quarter of the picture under it is mountain: tiles are
            // only drawn inside the mountain's outline (see PlaceLayers), so a cell reaching a little
            // past it doesn't show, while a gap left inside it would. Cells below the picture are
            // rock; cells above it are open.
            int rows = Mathf.RoundToInt(h / px + layout.ArtOffsetY);
            layout.Shape = new bool[LevelWidth, rows];
            layout.BackWallShape = new bool[LevelWidth, rows];
            for (int cx = 0; cx < LevelWidth; cx++)
            for (int cy = 0; cy < rows; cy++)
            {
                int x0 = Mathf.FloorToInt(cx * px), x1 = Mathf.FloorToInt((cx + 1) * px);
                int r0 = Mathf.FloorToInt((cy - layout.ArtOffsetY) * px), r1 = Mathf.FloorToInt((cy + 1 - layout.ArtOffsetY) * px);
                int inside = 0, total = 0;
                for (int r = Mathf.Max(0, r0); r < Mathf.Min(h, r1); r++)
                for (int x = x0; x < x1; x++)
                {
                    total++;
                    if (open[r * w + x]) inside++;
                }
                layout.Shape[cx, cy] = total == 0 ? r1 <= 0 : inside * 4 >= total;
                layout.BackWallShape[cx, cy] = total == 0 ? r1 <= 0 : inside > 0;
            }

            // The vein starts at the mountain's foot and comes out at the palace, near the right edge.
            int entranceX = 0;
            while (entranceX < LevelWidth - 1 && !layout.Shape[entranceX, groundLevel]) entranceX++;
            layout.Entrance = new Vector2Int(entranceX, groundLevel);
            int exitX = LevelWidth - 4;
            layout.Exit = new Vector2Int(exitX, TopOf(layout.Shape, exitX));
            return layout;
        }

        static int TopOf(bool[,] shape, int x)
        {
            int top = 0;
            for (int y = 0; y < shape.GetLength(1); y++) if (shape[x, y]) top = y + 1;
            return top;
        }

        // Copies the layers into the project and places them behind (and the border over) the mountain,
        // plus a mask in the mountain's exact shape. Returns the mask, which the rock and back-wall
        // tilemaps should be drawn inside of.
        public SpriteMask PlaceLayers(Transform parent)
        {
            if (!AssetDatabase.IsValidFolder(ArtDir)) AssetDatabase.CreateFolder("Assets/Art", "Background");
            foreach (var (name, sortingOrder) in Layers)
            {
                string path = $"{ArtDir}/{name}.png";
                File.Copy($"{SourceDir}/{name}.png", path, overwrite: true);
                var sr = new GameObject(name).AddComponent<SpriteRenderer>();
                sr.transform.SetParent(parent, false);
                sr.transform.localPosition = new Vector3(0f, ArtOffsetY, 0f);
                sr.sprite = ImportLayer(path);
                sr.sortingOrder = sortingOrder;
            }

            // The mask: opaque wherever the picture shows the mountain, plus extra opaque rows below the
            // picture so the rock under it (where the art was shifted up) shows too.
            const string maskPath = ArtDir + "/mountain-mask.png";
            int padRows = Mathf.CeilToInt((ArtOffsetY + 1f) * PixelsPerTile);
            var maskTex = new Texture2D(pictureWidth, pictureHeight + padRows, TextureFormat.RGBA32, false);
            var maskPixels = new Color32[pictureWidth * (pictureHeight + padRows)];
            for (int i = 0; i < maskPixels.Length; i++)
            {
                int pictureIndex = i - padRows * pictureWidth;
                bool mountain = pictureIndex < 0 || mountainPixels[pictureIndex];
                maskPixels[i] = new Color32(255, 255, 255, (byte)(mountain ? 255 : 0));
            }
            maskTex.SetPixels32(maskPixels);
            File.WriteAllBytes(maskPath, maskTex.EncodeToPNG());
            Object.DestroyImmediate(maskTex);

            var mask = new GameObject("Mountain Mask").AddComponent<SpriteMask>();
            mask.transform.SetParent(parent, false);
            mask.transform.localPosition = new Vector3(0f, ArtOffsetY - padRows / PixelsPerTile, 0f);
            mask.sprite = ImportLayer(maskPath);
            return mask;
        }

        Sprite ImportLayer(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerTile;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
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

        // Writes Temp/mountain_layout.png: the picture with the tile grid, rock cells tinted, and the
        // vein's entrance (green) and exit (gold) marked. For checking how the art was read.
        [MenuItem("Farhad/Debug Mountain Layout Image")]
        public static void WriteDebugImage()
        {
            var layout = Analyze();
            int w = 0, h = 0;
            var layers = new List<Color32[]>();
            foreach (var (name, _) in Layers) layers.Add(LoadPixels(name, out w, out h));
            float px = layout.PixelsPerTile;
            int offsetPx = Mathf.RoundToInt(layout.ArtOffsetY * px);
            int outH = Mathf.CeilToInt(layout.Shape.GetLength(1) * px);
            var outPixels = new Color32[w * outH];

            for (int r = 0; r < outH; r++)
            for (int x = 0; x < w; x++)
            {
                Color c = new Color(0.2f, 0.2f, 0.22f);
                int sr = r - offsetPx;
                if (sr >= 0 && sr < h)
                    foreach (var layer in layers) { Color l = layer[sr * w + x]; c = Color.Lerp(c, l, l.a); }

                int cx = Mathf.Min(LevelWidth - 1, (int)(x / px)), cy = (int)(r / px);
                var cell = new Vector2Int(cx, cy);
                if (cy < layout.Shape.GetLength(1) && layout.Shape[cx, cy]) c = Color.Lerp(c, new Color(0.85f, 0.55f, 0.3f), 0.45f);
                if (cell == layout.Entrance) c = Color.Lerp(c, Color.green, 0.6f);
                if (cell == layout.Exit) c = Color.Lerp(c, new Color(1f, 0.8f, 0f), 0.6f);
                if (x % Mathf.RoundToInt(px) == 0 || r % Mathf.RoundToInt(px) == 0) c = Color.Lerp(c, Color.black, 0.35f);
                outPixels[r * w + x] = c;
            }

            var tex = new Texture2D(w, outH, TextureFormat.RGBA32, false);
            tex.SetPixels32(outPixels);
            tex.Apply();
            File.WriteAllBytes("Temp/mountain_layout.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
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
