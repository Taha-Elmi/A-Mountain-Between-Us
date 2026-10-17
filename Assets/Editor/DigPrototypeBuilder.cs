using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Farhad.EditorTools
{
    // Builds the dig prototype scene, so it can be rebuilt at any time. When the artist's background
    // layers exist (see MountainArtLayout), the level is shaped by them; otherwise a made-up mountain
    // is generated. Rock tiles come from Assets/Art/Tiles (see Tools/process_tiles.ps1) when they
    // exist, else from generated placeholders; 1 tile = 1 unit.
    // The camera shows 8.4 tiles vertically, so a tile is 128 px tall on a 1080p screen: tile art
    // should be painted at 256x256 px to stay sharp at 4K.
    public static class DigPrototypeBuilder
    {
        const string ArtDir = "Assets/Art/Placeholder";
        const string TileArtDir = "Assets/Art/Tiles";
        const string TilesDir = "Assets/Tiles";
        const string PhysicsDir = "Assets/Physics";
        const string ScenePath = "Assets/Scenes/DigPrototype.unity";
        const int PixelsPerUnit = 128;
        const float OrthographicSize = 1080f / 128f / 2f;

        [MenuItem("Farhad/Build Dig Prototype Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Leave Play mode before building the dig prototype scene.");
                return;
            }

            EnsureFolder(ArtDir);
            EnsureFolder(TilesDir);
            EnsureFolder(PhysicsDir);

            // The artist's tiles (made by Tools/process_tiles.ps1) when they exist, else placeholders.
            var solidSprite = TileArt("rock_solid") ?? MakeSprite("rock_solid", RockTexture(new Color(0.66f, 0.53f, 0.42f), new Color(0.44f, 0.33f, 0.27f), 11, true));
            var wallSprite = TileArt("back_wall") ?? MakeSprite("back_wall", RockTexture(new Color(0.24f, 0.19f, 0.17f), new Color(0.16f, 0.12f, 0.11f), 37, false));
            var markerSprite = MakeSprite("target_marker", MarkerTexture());
            var farhadSprite = MakeSprite("farhad", FarhadTexture());
            var beaconSprite = MakeSprite("goal_beacon", BeaconTexture());

            var solidTile = MakeTile("RockSolid", solidSprite, Tile.ColliderType.Grid);
            var hollowTile = MakeTile("RockHollow", solidSprite, Tile.ColliderType.Grid); // same look as solid on purpose
            // Drawn over hollow rock while it is revealed: the artist's loose stones, if they exist.
            var revealTile = MakeTile("RockHollowRevealed", TileArt("rock_hollow") ?? solidSprite, Tile.ColliderType.None);
            var wallTile = MakeTile("BackWall", wallSprite, Tile.ColliderType.None);
            var frictionless = MakeFrictionless();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            // Mountain tilemaps
            var grid = new GameObject("Mountain", typeof(Grid));
            var backWall = MakeTilemap(grid.transform, "Back Wall", -10);
            var rock = MakeTilemap(grid.transform, "Rock", 0);
            rock.gameObject.AddComponent<TilemapCollider2D>();
            var reveal = MakeTilemap(grid.transform, "Revealed Hollow Rock", 1);

            var generator = grid.AddComponent<MountainGenerator>();
            generator.rockMap = rock;
            generator.backWallMap = backWall;
            generator.solidTile = solidTile;
            generator.hollowTile = hollowTile;
            generator.backWallTile = wallTile;
            if (MountainArtLayout.HasArt)
            {
                var layout = MountainArtLayout.Analyze();
                generator.Generate(layout.Shape, layout.BackWallShape, layout.Entrance, layout.Exit, layout.SpawnX);
                layout.PlaceLayers(new GameObject("Background").transform);
                // Rock is drawn only inside the artist's outline of the mountain.
                foreach (var map in new[] { rock, reveal, backWall })
                    map.GetComponent<TilemapRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            else generator.Generate();
            MakeWorldBounds(generator.width, generator.height);

            var mountain = grid.AddComponent<MountainGrid>();
            mountain.rockMap = rock;
            mountain.solidTile = solidTile;
            mountain.hollowTile = hollowTile;
            mountain.revealMap = reveal;
            mountain.revealTile = revealTile;

            var rockfall = grid.AddComponent<Rockfall>();
            rockfall.mountain = mountain;
            rockfall.debrisSprite = solidSprite;

            // Goal on the far side
            var goalObject = new GameObject("Goal (Far Side)");
            goalObject.transform.position = generator.GoalPoint;
            var goalTrigger = goalObject.AddComponent<BoxCollider2D>();
            goalTrigger.isTrigger = true;
            goalTrigger.size = new Vector2(3f, 4f);
            var goal = goalObject.AddComponent<GoalZone>();
            if (!MountainArtLayout.HasArt) // with the art, the drawn palace marks the goal
            {
                var beacon = goalObject.AddComponent<SpriteRenderer>();
                beacon.sprite = beaconSprite;
                beacon.sortingOrder = -5;
            }

            // Farhad
            var farhadObject = new GameObject("Farhad");
            farhadObject.transform.position = generator.SpawnPoint + new Vector3(0f, 0.9f, 0f); // capsule is 1.75 tall, centered

            // The drawn art (pivot at his feet) sits on a child at the bottom of the capsule; without
            // the art, the generated placeholder (pivot at its center) sits on Farhad himself.
            var body = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            body.transform.SetParent(farhadObject.transform, false);
            body.sortingOrder = 10;
            Animator animator = null;
            if (FarhadArtSetup.HasArt)
            {
                var controller = FarhadArtSetup.Build();
                body.transform.localPosition = new Vector3(0f, -0.875f, 0f);
                body.sprite = FarhadArtSetup.IdleSprite;
                animator = body.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
            }
            else body.sprite = farhadSprite;
            var rb = farhadObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var capsule = farhadObject.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.7f, 1.75f);
            capsule.sharedMaterial = frictionless;
            farhadObject.AddComponent<AudioSource>().playOnAwake = false;
            var sounds = farhadObject.AddComponent<RockSounds>();

            var marker = new GameObject("Target Marker").AddComponent<SpriteRenderer>();
            marker.sprite = markerSprite;
            marker.sortingOrder = 20;

            var farhad = farhadObject.AddComponent<FarhadController>();
            farhad.mountain = mountain;
            farhad.sounds = sounds;
            farhad.rockfall = rockfall;
            farhad.body = body;
            farhad.animator = animator;
            if (animator != null)
            {
                farhad.strikeImpactTime = FarhadArtSetup.StrikeImpactTime;
                farhad.strikeDuration = FarhadArtSetup.StrikeDuration;
            }
            farhad.targetMarker = marker;

            var call = farhadObject.AddComponent<ShirinCall>();
            call.mountain = mountain;

            // His glow: a copy of his sprite just behind him, drawn by the halo shader.
            if (animator != null)
            {
                var haloRenderer = new GameObject("Halo").AddComponent<SpriteRenderer>();
                haloRenderer.transform.SetParent(body.transform, false);
                haloRenderer.sortingOrder = body.sortingOrder - 1;
                haloRenderer.sharedMaterial = HaloMaterial();
                var halo = haloRenderer.gameObject.AddComponent<FarhadHalo>();
                halo.body = body;
                halo.call = call;
            }

            // Camera
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = farhadObject.transform.position + new Vector3(0f, 1f, -10f);
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = OrthographicSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // Shows only through gaps in the art, so it matches the night sky.
            cam.backgroundColor = MountainArtLayout.HasArt ? new Color(0.13f, 0.13f, 0.15f) : new Color(0.62f, 0.66f, 0.78f);
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = farhadObject.transform;
            follow.offset = new Vector2(0f, 1f);
            follow.minBounds = new Vector2(0f, 0f);
            follow.maxBounds = new Vector2(generator.width, generator.height);
            rockfall.cameraFollow = follow;

            var hud = new GameObject("Prototype HUD").AddComponent<PrototypeHud>();
            hud.farhad = farhad;
            hud.call = call;
            hud.mountain = mountain;
            hud.goal = goal;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var buildScenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            buildScenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = buildScenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"Dig prototype scene built at {ScenePath}");
        }

        static Material HaloMaterial()
        {
            string path = FarhadArtSetup.ArtDir + "/FarhadHalo.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Farhad/SpriteHalo"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // Invisible walls on both sides of the level and a ceiling over it.
        static void MakeWorldBounds(int width, int height)
        {
            var bounds = new GameObject("World Bounds");
            void Wall(string name, Vector2 center, Vector2 size)
            {
                var wall = new GameObject(name).AddComponent<BoxCollider2D>();
                wall.transform.SetParent(bounds.transform, false);
                wall.offset = center;
                wall.size = size;
            }
            Wall("Left", new Vector2(-0.5f, height * 0.5f), new Vector2(1f, height * 2f));
            Wall("Right", new Vector2(width + 0.5f, height * 0.5f), new Vector2(1f, height * 2f));
            Wall("Ceiling", new Vector2(width * 0.5f, height + 0.5f), new Vector2(width + 2f, 1f));
        }

        static Tilemap MakeTilemap(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<TilemapRenderer>().sortingOrder = sortingOrder;
            return go.GetComponent<Tilemap>();
        }

        static Tile MakeTile(string name, Sprite sprite, Tile.ColliderType collider)
        {
            string path = $"{TilesDir}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = collider;
            tile.flags = TileFlags.None;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static PhysicsMaterial2D MakeFrictionless()
        {
            string path = $"{PhysicsDir}/Frictionless.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material != null) return material;
            material = new PhysicsMaterial2D("Frictionless") { friction = 0f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // A tile drawn by the artist, imported so it covers exactly one cell. Null if it doesn't exist.
        static Sprite TileArt(string name)
        {
            string path = $"{TileArtDir}/{name}.png";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(path) == null) return null;

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.GetSourceTextureWidthAndHeight(out int width, out _);
            importer.spritePixelsPerUnit = width;
            importer.mipmapEnabled = true; // drawn at twice the 1080p size
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static Sprite MakeSprite(string name, Texture2D texture)
        {
            string path = $"{ArtDir}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // --- Placeholder textures -------------------------------------------------------------

        static Texture2D RockTexture(Color light, Color dark, int seed, bool bevel)
        {
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Fbm(x / 40f + seed, y / 40f + seed * 2f);
                var c = Color.Lerp(dark, light, n);
                if (bevel)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    if (edge < 6) c *= x < 6 || y > size - 7 ? 1.12f : 0.72f; // lit top-left, shaded bottom-right
                }
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        static Texture2D MarkerTexture()
        {
            const int size = 128, border = 6;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool edge = x < border || y < border || x >= size - border || y >= size - border;
                tex.SetPixel(x, y, edge ? Color.white : Color.clear);
            }
            tex.Apply();
            return tex;
        }

        // 96x230 px silhouette: 0.75 x 1.8 units, facing right.
        static Texture2D FarhadTexture()
        {
            const int w = 96, h = 230;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var tunic = new Color(0.55f, 0.2f, 0.16f);
            var skin = new Color(0.82f, 0.64f, 0.5f);
            var hair = new Color(0.15f, 0.1f, 0.08f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = Color.clear;
                float dx = x - w / 2f;
                if (y < 168 && Mathf.Abs(dx) < (y < 20 ? 30f : 40f - y * 0.06f)) c = y < 20 ? hair : tunic;
                float hx = x - w / 2f, hy = y - 196f;
                if (hx * hx + hy * hy < 30f * 30f) c = hy > 12f ? hair : skin;
                if (Mathf.Abs(x - 66) < 4 && Mathf.Abs(y - 200) < 4) c = hair; // eye shows facing
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        static Texture2D BeaconTexture()
        {
            const int w = 64, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float across = 1f - Mathf.Abs(x - w / 2f) / (w / 2f);
                float up = 1f - y / (float)h;
                tex.SetPixel(x, y, new Color(1f, 0.85f, 0.5f, across * across * up * 0.8f));
            }
            tex.Apply();
            return tex;
        }

        static float Fbm(float x, float y)
        {
            float sum = 0f, amp = 0.5f, freq = 1f;
            for (int i = 0; i < 4; i++)
            {
                sum += Mathf.PerlinNoise(x * freq, y * freq) * amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return Mathf.Clamp01(sum * 1.1f);
        }
    }
}
