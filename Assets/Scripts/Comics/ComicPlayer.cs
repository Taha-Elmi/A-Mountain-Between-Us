using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Farhad
{
    // Plays a comic full screen, one page at a time: fades in from black, then shows the page's
    // words one line at a time (the first by itself, the rest when the player clicks or presses
    // Space / Enter / Right). After the last line the same input turns the page (a crossfade);
    // Left or right-click goes back a page. A page without a picture is black; a page can take
    // longer to fade in (see ComicScript). Fades to black before loading the next scene; Escape
    // skips the rest. The words are in the language the player chose (Persian by default).
    public class ComicPlayer : MonoBehaviour
    {
        public ComicSequence comic;
        [Tooltip("Loaded when the comic ends. Must be in the build's scene list.")]
        public string nextScene;

        public Image paper;
        public Image page;
        public Image nextPage; // drawn over the page, fading in as it turns
        public ComicLines lines;
        public Text hint;
        public Image blackout;

        public float fadeSeconds = 1f;
        public float turnSeconds = 0.5f;
        public float lineFadeSeconds = 0.3f;
        [Tooltip("Seconds a new page shows before its first line appears by itself.")]
        public float firstLineDelay = 0.5f;
        [Tooltip("Seconds the comic waits before showing how to continue.")]
        public float hintDelay = 4f;

        const int Skip = int.MaxValue;
        const string HintEnglish = "Click to continue";
        const string HintPersian = "برای ادامه کلیک کنید";

        List<ComicPage> pages;
        CanvasGroup linesGroup;
        int index;
        int shownLines;
        readonly List<float> lineAlphas = new();
        float hintAlpha;

        // Real time, capped: the first frame after a scene loads carries the whole load, which
        // would otherwise skip the fade-in.
        static float DeltaTime => Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        IEnumerator Start()
        {
            pages = comic != null ? comic.ReadPages() : null;
            if (pages == null || pages.Count == 0)
            {
                LoadNextScene();
                yield break;
            }

            linesGroup = lines.GetComponent<CanvasGroup>();
            if (linesGroup == null) linesGroup = lines.gameObject.AddComponent<CanvasGroup>();
            linesGroup.interactable = linesGroup.blocksRaycasts = false;

            paper.color = comic.paper;
            hintAlpha = hint.color.a;
            SetAlpha(hint, 0f);
            SetAlpha(nextPage, 0f);
            ShowPicture(page, pages[0].image);
            ShowLines(0);
            Language.Changed += OnLanguageChanged;
            yield return Fade(blackout, 1f, 0f, fadeSeconds);

            while (true)
            {
                // Wait for the player, fading lines in as they're shown.
                float idle = 0f;
                int step = 0;
                while (step == 0)
                {
                    yield return null;
                    idle += DeltaTime;
                    FadeLinesIn();
                    step = ReadInput();
                    float autoAdvance = pages[index].autoAdvance;
                    if (autoAdvance > 0f && idle >= autoAdvance) step = 1;
                    if (step == 0 && shownLines == 0 && lines.Count > 0 && idle >= firstLineDelay)
                    {
                        RevealLine();
                        idle = 0f;
                    }
                    SetAlpha(hint, Mathf.Clamp01((idle - hintDelay) / fadeSeconds) * hintAlpha);
                }
                SetAlpha(hint, 0f);

                if (step == 1 && shownLines < lines.Count)
                {
                    RevealLine();
                    continue;
                }

                int next = index + step;
                if (step == Skip || next >= pages.Count) break;
                if (next < 0) continue; // already on the first page

                // Going back shows the page as it was left, words and all.
                yield return TurnTo(next, step < 0);
            }

            SceneMusic.Leaving(string.IsNullOrEmpty(nextScene) ? null : nextScene, fadeSeconds);
            yield return Fade(blackout, 0f, 1f, fadeSeconds);
            LoadNextScene();
        }

        void OnDestroy() => Language.Changed -= OnLanguageChanged;

        void OnLanguageChanged() => ShowLines(index, shownLines);

        // The next page fades in over this one; the words go first, in the usual turn's time.
        IEnumerator TurnTo(int next, bool allLines)
        {
            bool fromBlack = page.sprite == null;
            ShowPicture(nextPage, pages[next].image);
            SetAlpha(nextPage, 0f);
            float seconds = pages[next].fadeSeconds > 0f ? pages[next].fadeSeconds : turnSeconds;
            for (float t = 0f; t < seconds; t += DeltaTime)
            {
                SetAlpha(nextPage, Mathf.SmoothStep(0f, 1f, t / seconds));
                linesGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t / turnSeconds);
                yield return null;
            }
            ShowPicture(page, nextPage.sprite);
            SetAlpha(nextPage, 0f);
            // A black page covers the whole screen; coming out of one onto a picture that isn't
            // the screen's shape, the margins around it turn from black to paper.
            if (fromBlack && page.sprite != null)
            {
                paper.color = Color.black;
                StartCoroutine(Fade(paper, comic.paper, turnSeconds));
            }
            index = next;
            ShowLines(index, allLines ? int.MaxValue : 0);
            linesGroup.alpha = 1f;
        }

        // Lays out the current page's words; the first `shown` of them are already visible.
        void ShowLines(int pageIndex, int shown = 0)
        {
            var sprite = pages[pageIndex].image;
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 16f / 9f;
            lines.Show(pages[pageIndex].lines, Language.Current, aspect);
            shownLines = Mathf.Min(shown, lines.Count);
            lineAlphas.Clear();
            for (int i = 0; i < lines.Count; i++)
            {
                lineAlphas.Add(i < shownLines ? 1f : 0f);
                lines.SetAlpha(i, lineAlphas[i]);
            }
            UpdateHint();
        }

        void RevealLine() => shownLines++;

        void FadeLinesIn()
        {
            for (int i = 0; i < shownLines; i++)
            {
                if (lineAlphas[i] >= 1f) continue;
                lineAlphas[i] = Mathf.Min(1f, lineAlphas[i] + DeltaTime / lineFadeSeconds);
                lines.SetAlpha(i, Mathf.SmoothStep(0f, 1f, lineAlphas[i]));
            }
        }

        void UpdateHint()
        {
            bool persian = Language.Current == Lang.Persian;
            hint.font = persian ? lines.persianFont : lines.englishFont;
            hint.text = persian ? PersianText.Visual(PersianText.Shape(HintPersian)) : HintEnglish;
        }

        // 1 for the next line or page, -1 for the previous page, Skip to end the comic, 0 for nothing.
        static int ReadInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            if (kb != null && kb.escapeKey.wasPressedThisFrame || pad != null && pad.startButton.wasPressedThisFrame)
                return Skip;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame ||
                kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame ||
                               kb.numpadEnterKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame ||
                               kb.dKey.wasPressedThisFrame) ||
                pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame))
                return 1;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame ||
                kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame ||
                               kb.backspaceKey.wasPressedThisFrame) ||
                pad != null && (pad.buttonEast.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame))
                return -1;
            return 0;
        }

        void LoadNextScene()
        {
            if (string.IsNullOrEmpty(nextScene)) Application.Quit();
            else SceneManager.LoadScene(nextScene);
        }

        // A page's picture, or black over the whole screen when it has none.
        static void ShowPicture(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : Color.black;
        }

        static IEnumerator Fade(Graphic graphic, Color to, float seconds)
        {
            Color from = graphic.color;
            for (float t = 0f; t < seconds; t += DeltaTime)
            {
                graphic.color = Color.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            graphic.color = to;
        }

        static IEnumerator Fade(Graphic graphic, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += DeltaTime)
            {
                SetAlpha(graphic, Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetAlpha(graphic, to);
        }

        static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
