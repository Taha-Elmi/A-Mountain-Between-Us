using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Farhad.EditorTools
{
    // The comics that open and close the game. Each comic is a folder of page images under
    // Assets/Art/Comics/ (Intro, Outro), read in file-name order (01, 02 ... 10 comes after 9).
    // Adding, replacing, renaming or deleting an image there refills that comic's ComicSequence
    // (Assets/Art/Comics/<folder>.asset) by itself; nothing else needs touching.
    //
    // Page spec: one 16:9 image per page, the whole screen as the artist wants it seen (frame and
    // white margin included), ideally 3840x2160 so it stays sharp at 4K; 1920x1080 works too.
    // Farhad > Build Comic Scenes makes the ComicIntro and ComicOutro scenes that play them.
    public class ComicSetup : AssetPostprocessor
    {
        public const string ComicsDir = "Assets/Art/Comics";
        const string ScenesDir = "Assets/Scenes";
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".psd", ".tga", ".tif", ".tiff" };

        // A page image: any image in a folder under ComicsDir.
        static bool IsPage(string path)
        {
            if (!path.StartsWith(ComicsDir + "/")) return false;
            string rest = path.Substring(ComicsDir.Length + 1);
            return rest.Contains('/') && ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        // New pages import as full-screen sprites. Only on first import, so changes made in the
        // Inspector afterwards stick.
        void OnPreprocessTexture()
        {
            if (!IsPage(assetPath) || !assetImporter.importSettingsMissing) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            // A 4K page is shrunk on smaller screens: mipmaps keep the fine ink lines smooth.
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(IsPage)) return;
            // Creating assets in the middle of an import is unsafe; refill once it's done.
            EditorApplication.delayCall -= RefreshComics;
            EditorApplication.delayCall += RefreshComics;
        }

        [MenuItem("Farhad/Refresh Comics")]
        public static void RefreshComics()
        {
            EnsureFolder(ComicsDir + "/Intro");
            EnsureFolder(ComicsDir + "/Outro");
            foreach (string folder in AssetDatabase.GetSubFolders(ComicsDir))
                Refresh(folder);
        }

        static ComicSequence Refresh(string folder)
        {
            string name = Path.GetFileName(folder);
            string path = $"{ComicsDir}/{name}.asset";
            var comic = AssetDatabase.LoadAssetAtPath<ComicSequence>(path);
            if (comic == null)
            {
                comic = ScriptableObject.CreateInstance<ComicSequence>();
                AssetDatabase.CreateAsset(comic, path);
            }

            var images = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => Path.GetDirectoryName(p)!.Replace('\\', '/') == folder) // not subfolders
                .Distinct()
                .ToList();
            images.Sort((a, b) => EditorUtility.NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));

            var sprites = images.Select(p =>
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sprite == null) Debug.LogWarning($"Comic page {p} isn't imported as a Sprite: set its Texture Type to Sprite.");
                return sprite;
            }).Where(s => s != null).ToList();
            if (sprites.SequenceEqual(comic.pages.Select(p => p.image))) return comic;

            // Keep the settings of pages that were already there.
            var old = comic.pages.Where(p => p.image != null).GroupBy(p => p.image).ToDictionary(g => g.Key, g => g.First());
            comic.pages = sprites
                .Select(s => old.TryGetValue(s, out var page) ? page : new ComicSequence.Page { image = s })
                .ToList();
            EditorUtility.SetDirty(comic);
            AssetDatabase.SaveAssetIfDirty(comic);
            Debug.Log($"Comic {name}: {comic.pages.Count} page{(comic.pages.Count == 1 ? "" : "s")} ({string.Join(", ", images.Select(Path.GetFileName))})", comic);
            return comic;
        }

        [MenuItem("Farhad/Build Comic Scenes")]
        public static void BuildScenes()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Leave Play mode before building the comic scenes.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            RefreshComics();
            // The opening comic leads into the mountain; the closing one back to the main menu.
            BuildScene("Intro", "DigPrototype");
            BuildScene("Outro", "Menu");
            BuildSceneOrder.Apply();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene($"{ScenesDir}/ComicIntro.unity");
        }

        static void BuildScene(string name, string nextScene)
        {
            var comic = AssetDatabase.LoadAssetAtPath<ComicSequence>($"{ComicsDir}/{name}.asset");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();

            var canvasObject = new GameObject("Comic", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            // Back to front: the paper, the page, the next page fading in over it, the hint, black.
            var player = canvasObject.AddComponent<ComicPlayer>();
            player.comic = comic;
            player.nextScene = nextScene;
            player.paper = FullScreenImage(canvasObject.transform, "Paper", comic.paper);
            player.page = FullScreenImage(canvasObject.transform, "Page", Color.white);
            player.page.preserveAspect = true;
            if (comic.pages.Count > 0) player.page.sprite = comic.pages[0].image; // to see it in the editor
            player.nextPage = FullScreenImage(canvasObject.transform, "Next Page", Color.white);
            player.nextPage.preserveAspect = true;

            var hint = new GameObject("Hint", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            hint.transform.SetParent(canvasObject.transform, false);
            var rect = hint.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 20f);
            rect.sizeDelta = new Vector2(600f, 40f);
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 24;
            hint.alignment = TextAnchor.LowerRight;
            hint.color = new Color(0.4f, 0.4f, 0.4f);
            hint.text = "Click to continue";
            hint.raycastTarget = false;
            player.hint = hint;

            player.blackout = FullScreenImage(canvasObject.transform, "Blackout", Color.black);

            string path = $"{ScenesDir}/Comic{name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log($"Comic scene built at {path} ({comic.pages.Count} page{(comic.pages.Count == 1 ? "" : "s")}, then {nextScene})");
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

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
