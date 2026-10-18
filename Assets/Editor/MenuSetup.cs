using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Farhad.EditorTools
{
    // Builds the main menu, the first scene of the game: the artist's picture with a column of
    // stone buttons in the bottom right corner (Start, a little bigger, then Records, Settings and
    // Exit). The art is made by Tools/process_menu.ps1 into Assets/Art/Menu/. Rebuild with
    // Farhad > Build Menu Scene; the scene is made from scratch, so tweaks belong here.
    public static class MenuSetup
    {
        const string ArtDir = "Assets/Art/Menu";
        const string BackgroundPath = ArtDir + "/menu.jpg";
        const string ButtonPath = ArtDir + "/button.png";
        public const string ScenePath = "Assets/Scenes/Menu.unity";

        // On the 1920x1080 canvas. The stone is 296x201 as drawn.
        static readonly Vector2 StartSize = new Vector2(330f, 224f);
        static readonly Vector2 ButtonSize = new Vector2(250f, 170f);
        const float Spacing = -18f; // the stones overlap a little, like a pile
        static readonly Vector2 Margin = new Vector2(90f, 50f);

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
            if (background == null || stone == null)
            {
                Debug.LogError($"Menu art missing from {ArtDir}: run Tools/process_menu.ps1 first.");
                return;
            }

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
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();

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

            var column = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            column.transform.SetParent(canvasObject.transform, false);
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

            menu.startButton = MakeButton(column.transform, "Start", stone, StartSize, 58);
            menu.recordsButton = MakeButton(column.transform, "Records", stone, ButtonSize, 42);
            menu.settingsButton = MakeButton(column.transform, "Settings", stone, ButtonSize, 42);
            menu.exitButton = MakeButton(column.transform, "Exit", stone, ButtonSize, 42);

            // Up and down move between the stones, wrapping at the ends.
            var buttons = new[] { menu.startButton, menu.recordsButton, menu.settingsButton, menu.exitButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons[(i + buttons.Length - 1) % buttons.Length],
                    selectOnDown = buttons[(i + 1) % buttons.Length],
                };
            }

            menu.blackout = FullScreenImage(canvasObject.transform, "Blackout", Color.black);

            EditorSceneManager.SaveScene(scene, ScenePath);
            if (active.IsValid()) EditorSceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(scene, true);
            BuildSceneOrder.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log($"Menu scene built at {ScenePath}");
        }

        static Button MakeButton(Transform parent, string text, Sprite stone, Vector2 size, int fontSize)
        {
            var image = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement))
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
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.text = text;
            label.raycastTarget = false;

            var menuButton = image.gameObject.AddComponent<MenuButton>();
            menuButton.label = label;
            label.color = menuButton.labelColor;
            return button;
        }

        static Image FullScreenImage(Transform parent, string name, Color color)
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

        // The menu art as UI sprites. Read/Write stays on for the stone: clicks test its alpha.
        static Sprite ImportSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;
            bool isButton = path == ButtonPath;
            if (importer.textureType != TextureImporterType.Sprite || importer.isReadable != isButton)
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
