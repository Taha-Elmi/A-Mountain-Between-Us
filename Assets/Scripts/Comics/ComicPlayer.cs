using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Farhad
{
    // Plays a comic full screen, one page at a time: fades in from black, turns the page (a
    // crossfade) when the player clicks or presses Space / Enter / Right, goes back with Left or
    // right-click, and fades to black before loading the next scene. Escape skips the rest.
    public class ComicPlayer : MonoBehaviour
    {
        public ComicSequence comic;
        [Tooltip("Loaded when the comic ends. Must be in the build's scene list.")]
        public string nextScene;

        public Image paper;
        public Image page;
        public Image nextPage; // drawn over the page, fading in as it turns
        public Graphic hint;
        public Image blackout;

        public float fadeSeconds = 1f;
        public float turnSeconds = 0.5f;
        [Tooltip("Seconds a page waits before showing how to continue.")]
        public float hintDelay = 4f;

        const int Skip = int.MaxValue;

        // Real time, capped: the first frame after a scene loads carries the whole load, which
        // would otherwise skip the fade-in.
        static float DeltaTime => Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        IEnumerator Start()
        {
            if (comic == null || comic.pages.Count == 0)
            {
                LoadNextScene();
                yield break;
            }

            paper.color = comic.paper;
            float hintAlpha = hint.color.a;
            SetAlpha(hint, 0f);
            SetAlpha(nextPage, 0f);
            page.sprite = comic.pages[0].image;
            yield return Fade(blackout, 1f, 0f, fadeSeconds);

            int index = 0;
            while (true)
            {
                float shown = 0f;
                int step = 0;
                while (step == 0)
                {
                    yield return null;
                    shown += DeltaTime;
                    step = ReadInput();
                    float autoAdvance = comic.pages[index].autoAdvance;
                    if (autoAdvance > 0f && shown >= autoAdvance) step = 1;
                    SetAlpha(hint, Mathf.Clamp01((shown - hintDelay) / fadeSeconds) * hintAlpha);
                }

                int next = index + step;
                if (step == Skip || next >= comic.pages.Count) break;
                if (next < 0) continue; // already on the first page

                SetAlpha(hint, 0f);
                nextPage.sprite = comic.pages[next].image;
                yield return Fade(nextPage, 0f, 1f, turnSeconds);
                page.sprite = nextPage.sprite;
                SetAlpha(nextPage, 0f);
                index = next;
            }

            SetAlpha(hint, 0f);
            yield return Fade(blackout, 0f, 1f, fadeSeconds);
            LoadNextScene();
        }

        // 1 for the next page, -1 for the previous one, Skip to end the comic, 0 for nothing.
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
