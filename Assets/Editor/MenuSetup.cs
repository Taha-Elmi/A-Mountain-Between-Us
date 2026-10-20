using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Farhad.EditorTools
{
    // Builds the main menu, the first scene of the game: the artist's picture with the game's name
    // in the top left corner and a column of stone buttons in the bottom right corner (Start, a little bigger, then Records,
    // Language and Exit), labelled in the game's language; the records board and its Back stone,
    // and the two language stones, that take their place when Records or Language is chosen; and
    // its song, playing on into the opening comic. The art is made by
    // Tools/process_menu.ps1 into Assets/Art/Menu/.
    // Rebuild with Farhad > Build Menu Scene; the scene is made from scratch, so tweaks belong here.
    public static class MenuSetup
    {
        const string ArtDir = "Assets/Art/Menu";
        const string BackgroundPath = ArtDir + "/menu.jpg";
        public const string ButtonPath = ArtDir + "/button.png";
        const string TitlePath = ArtDir + "/game_name.png";
        const string MusicPath = "Assets/Audio/Music/Kalhor_WhereAreYou.mp3";
        const string PersianFontPath = "Assets/Art/Fonts/Vazirmatn-Bold.ttf";
        public const string ScenePath = "Assets/Scenes/Menu.unity";

        // On the 1920x1080 canvas. The stone is 296x201 as drawn.
        static readonly Vector2 StartSize = new Vector2(330f, 224f);
        static readonly Vector2 ButtonSize = new Vector2(250f, 170f);
        internal const float Spacing = 12f; // a little apart, so the stones don't touch
        static readonly Vector2 Margin = new Vector2(90f, 50f);
        // The game's name, from the top left corner.
        const float TitleWidth = 820f;
        static readonly Vector2 TitleMargin = new Vector2(70f, 60f);
        // The records board, above its Back stone.
        static readonly Vector2 BoardSize = new Vector2(640f, 600f);

        static Font englishFont, persianFont;

        // The fonts the stones' labels are written in; false when the Persian one is missing.
        internal static bool LoadFonts()
        {
            englishFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            persianFont = AssetDatabase.LoadAssetAtPath<Font>(PersianFontPath);
            if (persianFont == null) Debug.LogError($"No Persian font at {PersianFontPath}.");
            return persianFont != null;
        }

        [MenuItem("Farhad/Build Menu Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Leave Play mode before building the menu scene.");
                return;
            }

            var background = ImportSprite(BackgroundPath);
            var stone = ImportSprite(ButtonPath);
            var title = ImportSprite(TitlePath);
            if (background == null || stone == null || title == null)
            {
                Debug.LogError($"Menu art missing from {ArtDir}: run Tools/process_menu.ps1 first.");
                return;
            }
            if (!LoadFonts()) return;

            // Made beside the open scenes and closed once saved, so unsaved work in them is left alone.
            var active = EditorSceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SetActiveScene(scene);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // A new module already has the default UI actions; assigning them again can throw.
            var module = events.GetComponent<InputSystemUIInputModule>();
            if (module.actionsAsset == null) module.AssignDefaultActions();

            var canvasObject = new GameObject("Menu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            var menu = canvasObject.AddComponent<MainMenu>();

            // The picture fills the screen, cropped rather than squashed on screens that aren't 16:9.
            var picture = FullScreenImage(canvasObject.transform, "Background", Color.white);
            picture.sprite = background;
            var fitter = picture.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / background.rect.height;

            var gameName = new GameObject("Game Name", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            gameName.transform.SetParent(canvasObject.transform, false);
            gameName.sprite = title;
            gameName.raycastTarget = false;
            var nameRect = gameName.rectTransform;
            nameRect.anchorMin = nameRect.anchorMax = nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(TitleMargin.x, -TitleMargin.y);
            nameRect.sizeDelta = new Vector2(TitleWidth, TitleWidth * title.rect.height / title.rect.width);

            var column = Column(canvasObject.transform, "Buttons");
            menu.mainStones = column;
            menu.startButton = MakeButton(column.transform, "Start", "شروع", stone, StartSize, 58);
            menu.recordsButton = MakeButton(column.transform, "Records", "رکوردها", stone, ButtonSize, 42);
            menu.languageButton = MakeButton(column.transform, "Language", "زبان", stone, ButtonSize, 42);
            menu.exitButton = MakeButton(column.transform, "Exit", "خروج", stone, ButtonSize, 42);
            LinkUpAndDown(menu.startButton, menu.recordsButton, menu.languageButton, menu.exitButton);

            var records = Column(canvasObject.transform, "Records");
            menu.recordsStones = records;
            MakeRecordsBoard(records.transform);
            menu.recordsBackButton = MakeButton(records.transform, "Back", "بازگشت", stone, ButtonSize, 42);
            LinkUpAndDown(menu.recordsBackButton);
            records.SetActive(false);

            // Each language's stone is written in that language.
            var languages = Column(canvasObject.transform, "Languages");
            menu.languageStones = languages;
            menu.persianButton = MakeButton(languages.transform, null, "فارسی", stone, ButtonSize, 42);
            menu.englishButton = MakeButton(languages.transform, "English", null, stone, ButtonSize, 42);
            LinkUpAndDown(menu.persianButton, menu.englishButton);
            languages.SetActive(false);

            menu.blackout = FullScreenImage(canvasObject.transform, "Blackout", Color.black);

            AddMusic(MusicPath, menu.startScene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid()) EditorSceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(scene, true);
            BuildSceneOrder.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log($"Menu scene built at {ScenePath}");
        }

        // A column of stones in the bottom right corner, bottom-aligned.
        static GameObject Column(Transform parent, string name)
        {
            var column = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            column.transform.SetParent(parent, false);
            var columnRect = (RectTransform)column.transform;
            columnRect.anchorMin = columnRect.anchorMax = columnRect.pivot = new Vector2(1f, 0f);
            columnRect.anchoredPosition = new Vector2(-Margin.x, Margin.y);
            var layout = column.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = Spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = column.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return column;
        }

        // A black board edged in white, with the title at the top and the table of best runs under it,
        // all in white (see RecordsBoard, which fills it in).
        static void MakeRecordsBoard(Transform parent)
        {
            var board = new GameObject("Board", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
            board.transform.SetParent(parent, false);
            var image = board.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;
            var outline = board.GetComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            var element = board.GetComponent<LayoutElement>();
            element.preferredWidth = BoardSize.x;
            element.preferredHeight = BoardSize.y;

            var view = board.AddComponent<RecordsBoard>();
            view.englishFont = englishFont;
            view.persianFont = persianFont;
            view.title = BoardText(board.transform, "Title", 0f, 1f, 0.84f, 0.97f, 46, Color.white);
            // Equal widths, so the rank and time columns can swap sides for Persian.
            view.rankColumn = BoardText(board.transform, "Rank", 0f, 1f / 3f, 0.04f, 0.82f, 30, Color.white);
            view.rockfallsColumn = BoardText(board.transform, "Rockfalls", 1f / 3f, 2f / 3f, 0.04f, 0.82f, 30, Color.white);
            view.timeColumn = BoardText(board.transform, "Time", 2f / 3f, 1f, 0.04f, 0.82f, 30, Color.white);
            view.empty = BoardText(board.transform, "Empty", 0.05f, 0.95f, 0.3f, 0.7f, 34, Color.white);
            view.empty.alignment = TextAnchor.MiddleCenter;
            view.empty.horizontalOverflow = HorizontalWrapMode.Wrap; // a Persian line is short enough never to wrap
            view.Show(); // so the scene shows the board before Play
        }

        // Words on the board, filling the given part of it (as fractions of its width and height).
        static Text BoardText(Transform board, string name, float left, float right, float bottom, float top, int fontSize, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(board, false);
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.UpperCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // Up and down move between the stones, wrapping at the ends.
        internal static void LinkUpAndDown(params Button[] buttons)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length],
                };
            }
        }

        // A stone labelled in English and Persian; leave one out for a stone that's always in the other.
        // Call LoadFonts first.
        internal static Button MakeButton(Transform parent, string english, string persian, Sprite stone, Vector2 size, int fontSize)
        {
            var image = new GameObject(english ?? "Persian", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement))
                .GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = stone;
            image.preserveAspect = true;
            // Only the stone itself takes clicks, not the see-through corners around it.
            image.alphaHitTestMinimumThreshold = 0.5f;
            var element = image.GetComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;

            var button = image.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = new Color(0.78f, 0.78f, 0.78f);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.62f, 0.62f, 0.62f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            // The label sits on the stone's flat top, above its broken edge.
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(image.transform, false);
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.08f, 0.3f);
            rect.anchorMax = new Vector2(0.92f, 0.92f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;

            var menuButton = image.gameObject.AddComponent<MenuButton>();
            menuButton.label = label;
            menuButton.english = english;
            menuButton.persian = persian;
            menuButton.englishFont = englishFont;
            menuButton.persianFont = persianFont;
            menuButton.ShowLabel(); // so the scene shows the words before Play
            label.color = menuButton.labelColor;
            return button;
        }

        internal static Image FullScreenImage(Transform parent, string name, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // A song that starts with the scene being built and plays on through the scenes named
        // (see SceneMusic). Missing music is only a warning: the scene works without it.
        internal static void AddMusic(string path, params string[] alsoPlaysIn)
        {
            var clip = ImportMusic(path);
            if (clip == null)
            {
                Debug.LogWarning($"No music at {path}, so the scene is built without it.");
                return;
            }
            var music = new GameObject("Music", typeof(AudioSource)).AddComponent<SceneMusic>();
            music.GetComponent<AudioSource>().playOnAwake = false;
            music.music = clip;
            music.alsoPlaysIn = alsoPlaysIn;
        }

        // A song streamed from disk rather than held in memory, and heard the same everywhere.
        static AudioClip ImportMusic(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) return null;
            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.Streaming)
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.7f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        // The menu art as UI sprites. Read/Write stays on for the stone: clicks test its alpha.
        internal static Sprite ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool isButton = path == ButtonPath;
            // Unity may import a picture with separate pieces (like the name's letters) as a sheet of
            // many sprites; each of these must be one whole sprite.
            if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single
                || importer.isReadable != isButton)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.maxTextureSize = 4096;
                // The stone is small; uncompressed keeps its alpha exact for the click test.
                importer.textureCompression = isButton ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                importer.isReadable = isButton;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
