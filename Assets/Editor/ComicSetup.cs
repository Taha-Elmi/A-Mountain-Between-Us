using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Farhad.EditorTools
{
    // The comics that open and close the game. Each comic is a folder of page images under
    // Assets/Art/Comics/ (Intro, Outro), read in file-name order (01, 02 ... 10 comes after 9).
    // Adding, replacing, renaming or deleting an image there refills that comic's ComicSequence
    // (Assets/Art/Comics/<folder>.asset) by itself; nothing else needs touching.
    //
    // Page spec: one 16:9 image per page, the whole screen as the artist wants it seen (frame and
    // white margin included), ideally 3840x2160 so it stays sharp at 4K; 1920x1080 works too.
    // The words go in Assets/Art/Comics/<folder> Script.txt (format in ComicScript), which also
    // says which image each page shows, or that it's black; saving it checks it and reports
    // mistakes in the Console.
    // Farhad > Build Comic Scenes makes the ComicIntro and ComicOutro scenes that play them;
    // Farhad > Preview Comic Words draws every page with its words, in both languages, as
    // pictures in ComicPreviews/ (outside Assets) to check the layout without playing.
    public class ComicSetup : AssetPostprocessor
    {
        public const string ComicsDir = "Assets/Art/Comics";
        const string ScenesDir = "Assets/Scenes";
        const string ScriptSuffix = " Script.txt";
        const string PersianFontPath = "Assets/Art/Fonts/Vazirmatn-Regular.ttf";
        const string EnglishFontPath = "Assets/Art/Fonts/Vazirmatn-Regular.ttf";
        const string PreviewDir = "ComicPreviews";
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

        // A comic's script: "<folder> Script.txt" next to its folder.
        static bool IsScript(string path) =>
            path.StartsWith(ComicsDir + "/") && path.EndsWith(ScriptSuffix) && !path.Substring(ComicsDir.Length + 1).Contains('/');

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Concat(deleted).Concat(moved).Concat(movedFrom).Any(p => IsPage(p) || IsScript(p))) return;
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

            var script = AssetDatabase.LoadAssetAtPath<TextAsset>(folder + ScriptSuffix);
            bool pagesChanged = !sprites.SequenceEqual(comic.pages.Select(p => p.image));
            if (pagesChanged)
            {
                // Keep the settings of pages that were already there.
                var old = comic.pages.Where(p => p.image != null).GroupBy(p => p.image).ToDictionary(g => g.Key, g => g.First());
                comic.pages = sprites
                    .Select(s => old.TryGetValue(s, out var page) ? page : new ComicSequence.Page { image = s })
                    .ToList();
                Debug.Log($"Comic {name}: {comic.pages.Count} page{(comic.pages.Count == 1 ? "" : "s")} ({string.Join(", ", images.Select(Path.GetFileName))})", comic);
            }
            if (pagesChanged || comic.script != script)
            {
                comic.script = script;
                EditorUtility.SetDirty(comic);
                AssetDatabase.SaveAssetIfDirty(comic);
            }
            CheckScript(comic, name);
            return comic;
        }

        // Reports mistakes in a comic's script: rows it can't read, missing translations, and
        // images that aren't there.
        static void CheckScript(ComicSequence comic, string name)
        {
            if (comic.script == null) return;
            var problems = new List<string>();
            var pages = comic.ReadPages(problems);
            for (int i = 0; i < pages.Count; i++)
            {
                foreach (var line in pages[i].lines)
                {
                    if (string.IsNullOrEmpty(line.persian)) problems.Add($"Page {i + 1}, [{line.speaker}]: no Persian (fa:) text.");
                    if (string.IsNullOrEmpty(line.english)) problems.Add($"Page {i + 1}, [{line.speaker}]: no English (en:) text.");
                }
            }
            if (problems.Count > 0)
                Debug.LogWarning($"Comic {name} script ({AssetDatabase.GetAssetPath(comic.script)}):\n" + string.Join("\n", problems), comic.script);
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

            // Back to front: the paper, the page, the next page fading in over it, the words, the
            // hint, black.
            var player = canvasObject.AddComponent<ComicPlayer>();
            player.comic = comic;
            player.nextScene = nextScene;
            player.paper = FullScreenImage(canvasObject.transform, "Paper", comic.paper);
            player.page = FullScreenImage(canvasObject.transform, "Page", Color.white);
            player.page.preserveAspect = true;
            if (comic.pages.Count > 0) player.page.sprite = comic.pages[0].image; // to see it in the editor
            player.nextPage = FullScreenImage(canvasObject.transform, "Next Page", Color.white);
            player.nextPage.preserveAspect = true;

            player.lines = MakeLines(canvasObject.transform);
            player.lines.gameObject.AddComponent<CanvasGroup>();

            var hint = new GameObject("Hint", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            hint.transform.SetParent(canvasObject.transform, false);
            var rect = hint.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-48f, 20f);
            rect.sizeDelta = new Vector2(600f, 40f);
            hint.font = player.lines.englishFont;
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

        static ComicLines MakeLines(Transform parent)
        {
            var lines = new GameObject("Words", typeof(RectTransform)).AddComponent<ComicLines>();
            lines.transform.SetParent(parent, false);
            lines.persianFont = AssetDatabase.LoadAssetAtPath<Font>(PersianFontPath);
            lines.englishFont = AssetDatabase.LoadAssetAtPath<Font>(EnglishFontPath);
            if (lines.persianFont == null || lines.englishFont == null)
                Debug.LogError($"Comic font missing: expected {PersianFontPath} and {EnglishFontPath}.");
            return lines;
        }

        [MenuItem("Farhad/Preview Comic Words")]
        public static void PreviewWords()
        {
            RefreshComics();
            Directory.CreateDirectory(PreviewDir);
            int count = 0;
            foreach (string folder in AssetDatabase.GetSubFolders(ComicsDir))
            {
                string name = Path.GetFileName(folder);
                var comic = AssetDatabase.LoadAssetAtPath<ComicSequence>($"{ComicsDir}/{name}.asset");
                if (comic == null) continue;
                var pages = comic.ReadPages();
                for (int i = 0; i < pages.Count; i++)
                    foreach (Lang lang in Enum.GetValues(typeof(Lang)))
                    {
                        RenderPreview(comic.paper, pages[i].image, pages[i].lines, lang, $"{PreviewDir}/{name}_{i + 1:00}_{lang}.png");
                        count++;
                    }
            }
            Debug.Log($"Drew {count} comic page previews in {Path.GetFullPath(PreviewDir)}");
        }

        // Draws one page with all its words shown, 1920x1080, in a scene of its own.
        static void RenderPreview(Color paper, Sprite image, List<ComicLine> words, Lang lang, string path)
        {
            const int width = 1920, height = 1080;
            var scene = EditorSceneManager.NewPreviewScene();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            try
            {
                var cameraObject = new GameObject("Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var cam = cameraObject.AddComponent<Camera>();
                cam.scene = scene;
                cam.orthographic = true;
                cam.orthographicSize = height * 0.5f;
                cam.transform.position = new Vector3(0f, 0f, -10f);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = image != null ? paper : Color.black;
                cam.targetTexture = target;

                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = cam;
                ((RectTransform)canvasObject.transform).sizeDelta = new Vector2(width, height);

                var pageImage = FullScreenImage(canvasObject.transform, "Page", Color.white);
                pageImage.sprite = image;
                pageImage.preserveAspect = true;
                pageImage.enabled = image != null; // a black page
                var lines = MakeLines(canvasObject.transform);
                float aspect = image != null ? image.rect.width / image.rect.height : 16f / 9f;
                lines.Show(words, lang, aspect);
                for (int i = 0; i < lines.Count; i++) lines.SetAlpha(i, 1f);

                Canvas.ForceUpdateCanvases();
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                picture.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, picture.EncodeToPNG());
                Object.DestroyImmediate(picture);
                lines.Clear();
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
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
