using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Farhad.EditorTools
{
    // Builds the dig prototype scene, so it can be rebuilt at any time. When the artist's background
    // layers exist (see MountainArtLayout), the level is shaped by them and its rock shows the drawn
    // mountain, cut into squares; otherwise a made-up mountain is generated. The other tiles (and
    // the rock without the art) come from Assets/Art/Tiles (see Tools/process_tiles.ps1) when they
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
        const string AudioDir = "Assets/Audio";
        const string MusicPath = AudioDir + "/Music/Soundtrack.mp3";
        const string PersianFontPath = "Assets/Art/Fonts/Vazirmatn-Regular.ttf";
        const string ShirinIconPath = "Assets/Art/UI/Shirin.png";
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
            var keySprite = MakeSprite("key_cap", KeyCapTexture(), new Vector4(20f, 24f, 20f, 20f));
            var mouseSprite = MakeSprite("mouse_left_click", MouseTexture(false));
            var scrollSprite = MakeSprite("mouse_scroll_down", MouseTexture(true));

            // Drawn over hollow rock while it is revealed: the artist's loose stones, if they exist.
            var revealTile = MakeTile("RockHollowRevealed", TileArt("rock_hollow") ?? solidSprite, Tile.ColliderType.None);
            var wallTile = MakeTile("BackWall", wallSprite, Tile.ColliderType.None);
            var frictionless = MakeFrictionless();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // With the background art, rock shows the mountain as drawn: each cell its own square of
            // the picture, so a cell dug away takes its square with it. Hollow looks the same as solid
            // on purpose.
            MountainArtLayout layout = null;
            TileBase solidTile, hollowTile;
            if (MountainArtLayout.HasArt)
            {
                layout = MountainArtLayout.Analyze();
                layout.PlaceLayers(new GameObject("Background").transform);
                solidTile = MakePictureTile("MountainSolid", layout);
                hollowTile = MakePictureTile("MountainHollow", layout);
            }
            else
            {
                solidTile = MakeTile("RockSolid", solidSprite, Tile.ColliderType.Grid);
                hollowTile = MakeTile("RockHollow", solidSprite, Tile.ColliderType.Grid);
            }

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
            if (layout != null)
            {
                // The back wall goes only behind rock: everywhere else the backdrop shows the mountain.
                generator.Generate(layout.Shape, layout.Shape, layout.Entrance, layout.Exit, layout.SpawnX);
                // The back wall and the revealed stones are drawn only inside the artist's outline of
                // the mountain; the rock's squares already show the sky where they reach past it.
                foreach (var map in new[] { reveal, backWall })
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

            // The drawn art (pivot at his feet) sits on a child at the bottom of the capsule, lowered by
            // the gap physics keeps between colliders so his feet touch the ground; without the art,
            // the generated placeholder (pivot at its center) sits on Farhad himself.
            var body = new GameObject("Sprite").AddComponent<SpriteRenderer>();
            body.transform.SetParent(farhadObject.transform, false);
            body.sortingOrder = 10;
            Animator animator = null;
            if (FarhadArtSetup.HasArt)
            {
                var controller = FarhadArtSetup.Build();
                body.transform.localPosition = new Vector3(0f, -0.875f - Physics2D.defaultContactOffset, 0f);
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
            // His recorded voice; whatever is missing falls back to the synthesized placeholder.
            sounds.callTakes = Clips("farhad_call_shirin_");

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
                farhad.strikeUp = FarhadArtSetup.TimingOf(StrikeDirection.Up);
                farhad.strikeSide = FarhadArtSetup.TimingOf(StrikeDirection.Side);
                farhad.strikeDown = FarhadArtSetup.TimingOf(StrikeDirection.Down);
            }
            farhad.targetMarker = marker;

            var call = farhadObject.AddComponent<ShirinCall>();
            call.mountain = mountain;
            call.revealDuration = 3f;
            generator.call = call;

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
            // The art covers the whole view; without it, a daylight sky.
            cam.backgroundColor = MountainArtLayout.HasArt ? new Color(0.13f, 0.13f, 0.15f) : new Color(0.62f, 0.66f, 0.78f);
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = farhadObject.transform;
            follow.offset = new Vector2(0f, 1f);
            follow.minBounds = new Vector2(0f, 0f);
            follow.maxBounds = new Vector2(generator.width, generator.height);
            rockfall.cameraFollow = follow;

            var music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            if (music != null)
            {
                var soundtrack = new GameObject("Soundtrack", typeof(AudioSource)).AddComponent<Soundtrack>();
                soundtrack.GetComponent<AudioSource>().playOnAwake = false;
                soundtrack.music = music;
            }
            else Debug.LogWarning($"No soundtrack at {MusicPath}, so the level is built without music.");

            var hud = new GameObject("Prototype HUD").AddComponent<PrototypeHud>();
            hud.farhad = farhad;
            hud.call = call;
            hud.mountain = mountain;
            hud.goal = goal;
            hud.persianFont = AssetDatabase.LoadAssetAtPath<Font>(PersianFontPath);
            hud.shirinIcon = IconArt(ShirinIconPath);

            // The controls and rules, in the sky just over his head where he starts (the sign
            // places itself there when the level begins).
            var signObject = new GameObject("Tutorial Sign", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            var signCanvas = signObject.GetComponent<Canvas>();
            signCanvas.renderMode = RenderMode.WorldSpace;
            signCanvas.worldCamera = cam;
            signCanvas.sortingOrder = 30;
            signObject.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 2f; // sharp past 1080p
            var signRect = (RectTransform)signObject.transform;
            signRect.pivot = Vector2.zero;
            signRect.localScale = Vector3.one / PixelsPerUnit;
            var sign = signObject.AddComponent<TutorialSign>();
            sign.call = call;
            sign.farhad = farhadObject.transform;
            sign.font = hud.persianFont; // Vazirmatn has the Latin letters too
            sign.keySprite = keySprite;
            sign.mouseSprite = mouseSprite;
            sign.scrollSprite = scrollSprite;

            MakePauseMenu();

            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildSceneOrder.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log($"Dig prototype scene built at {ScenePath}");
        }

        // Escape's pause menu: the main menu's stones, Resume and Back to the menu, in the middle of
        // the darkened screen.
        static void MakePauseMenu()
        {
            var stone = MenuSetup.ImportSprite(MenuSetup.ButtonPath);
            if (stone == null || !MenuSetup.LoadFonts())
            {
                Debug.LogWarning("No menu art or font, so the level is built without a pause menu.");
                return;
            }

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // A new module already has the default UI actions; assigning them again can throw.
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();

            var canvasObject = new GameObject("Pause Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var pause = canvasObject.AddComponent<PauseMenu>();

            // The dark screen catches clicks, so none reach the level beneath.
            var dark = MenuSetup.FullScreenImage(canvasObject.transform, "Panel", new Color(0f, 0f, 0f, 0.65f));
            dark.raycastTarget = true;
            pause.panel = dark.gameObject;

            var column = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            column.transform.SetParent(dark.transform, false);
            var columnRect = (RectTransform)column.transform;
            columnRect.anchorMin = columnRect.anchorMax = columnRect.pivot = new Vector2(0.5f, 0.5f);
            columnRect.anchoredPosition = Vector2.zero;
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = MenuSetup.Spacing; // apart, as on the main menu
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = column.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Wide enough for "Back to the menu" on the stone's flat top.
            var size = new Vector2(400f, 272f);
            pause.resumeButton = MenuSetup.MakeButton(column.transform, "Resume", "ادامه", stone, size, 40);
            pause.menuButton = MenuSetup.MakeButton(column.transform, "Back to the menu", "بازگشت به منو", stone, size, 34);
            MenuSetup.LinkUpAndDown(pause.resumeButton, pause.menuButton);
            dark.gameObject.SetActive(false);
        }

        // The clips in Assets/Audio whose names start with prefix, in name order (take 01, 02, ...).
        static AudioClip[] Clips(string prefix)
        {
            var clips = new System.Collections.Generic.List<AudioClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetDirectoryName(path)!.Replace('\\', '/') != AudioDir) continue;
                if (Path.GetFileName(path).StartsWith(prefix)) clips.Add(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
            }
            clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return clips.ToArray();
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

        static PictureTile MakePictureTile(string name, MountainArtLayout layout)
        {
            string path = $"{TilesDir}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<PictureTile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<PictureTile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.picture = layout.Picture;
            tile.pixelsPerCell = layout.PixelsPerCell;
            tile.colliderType = Tile.ColliderType.Grid;
            tile.ClearSquares();
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

        // A picture for the IMGUI HUD, kept small and smooth. Null if it doesn't exist.
        static Texture2D IconArt(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"No HUD icon at {path}.");
                return null;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 256;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Sprite MakeSprite(string name, Texture2D texture, Vector4 border = default)
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
            importer.spriteBorder = border;
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

        // 64x64 px key cap: pale stone, a dark rim, a deeper lip along the bottom. Sliced, it
        // stretches into the space bar.
        static Texture2D KeyCapTexture()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rim = new Color(0.12f, 0.1f, 0.1f);
            var lip = new Color(0.62f, 0.58f, 0.54f);
            var top = new Color(0.93f, 0.9f, 0.85f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float outer = RoundedBox(x, y, 0f, 0f, size, size, 12f);
                float face = RoundedBox(x, y, 3f, 8f, size - 3f, size - 3f, 9f);
                var c = Color.Lerp(Color.Lerp(rim, lip, Mathf.Clamp01(outer - 2.5f)), top, Mathf.Clamp01(face));
                c.a = Mathf.Clamp01(outer);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        // 68x92 px mouse, its left button lit; or, for scrolling, its wheel lit with an arrow
        // pointing down below it.
        static Texture2D MouseTexture(bool scroll)
        {
            const int w = 68, h = 92;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var rim = new Color(0.12f, 0.1f, 0.1f);
            var body = new Color(0.93f, 0.9f, 0.85f);
            var lit = new Color(1f, 0.72f, 0.3f);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float outer = RoundedBox(x, y, 0f, 0f, w, h, 32f);
                float inner = RoundedBox(x, y, 4f, 4f, w - 4f, h - 4f, 28f);
                bool button = y > h * 0.55f;
                bool seam = button && Mathf.Abs(x + 0.5f - w * 0.5f) < 2f || Mathf.Abs(y + 0.5f - h * 0.55f) < 2f;
                var fill = seam ? rim : button && x < w / 2 && !scroll ? lit : body;
                if (scroll)
                {
                    float px = x + 0.5f - w * 0.5f, py = y + 0.5f;
                    // The wheel, in the seam between the buttons.
                    float wheel = RoundedBox(x, y, w * 0.5f - 6f, h * 0.62f, w * 0.5f + 6f, h * 0.88f, 6f);
                    if (wheel > 0f) fill = Color.Lerp(rim, lit, Mathf.Clamp01(wheel - 1f));
                    // A down arrow on the body: a shaft and a head.
                    bool shaft = Mathf.Abs(px) < 3f && py > h * 0.24f && py < h * 0.44f;
                    bool head = py > h * 0.12f && py < h * 0.27f && Mathf.Abs(px) < (py - h * 0.12f) * 0.9f;
                    if (shaft || head) fill = lit;
                }
                var c = Color.Lerp(rim, fill, Mathf.Clamp01(inner));
                c.a = Mathf.Clamp01(outer);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        // How far pixel (x, y) is inside a box with rounded corners, in pixels (clamped to 0..1
        // gives smooth edges).
        static float RoundedBox(int x, int y, float xMin, float yMin, float xMax, float yMax, float radius)
        {
            float px = x + 0.5f, py = y + 0.5f;
            float dx = Mathf.Max(xMin + radius - px, px - (xMax - radius), 0f);
            float dy = Mathf.Max(yMin + radius - py, py - (yMax - radius), 0f);
            float edge = Mathf.Min(Mathf.Min(px - xMin, xMax - px), Mathf.Min(py - yMin, yMax - py));
            return dx > 0f || dy > 0f ? radius - Mathf.Sqrt(dx * dx + dy * dy) : edge;
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
