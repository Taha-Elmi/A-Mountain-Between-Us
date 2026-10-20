using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Farhad
{
    // The first scene: fades in from black over the artist's picture and waits for a choice. Start
    // fades to black and opens the story (the opening comic), Exit quits. Records swaps the stones
    // for the player's best runs and a Back stone. Language swaps them for two, فارسی and English;
    // picking one sets the game's language and brings the stones back. Escape, right-click or B
    // goes back from either without picking.
    // Works with the mouse, the keyboard and a gamepad (Start is selected first, so Enter / A
    // starts the game).
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("Loaded by Start. Must be in the build's scene list.")]
        public string startScene = "ComicIntro";

        public Button startButton;
        public Button recordsButton;
        public Button languageButton;
        public Button exitButton;
        [Tooltip("Holds the four stones above.")]
        public GameObject mainStones;
        [Tooltip("Holds the records board and its Back stone, shown in the main stones' place.")]
        public GameObject recordsStones;
        public Button recordsBackButton;
        [Tooltip("Holds the language stones, shown in the main stones' place.")]
        public GameObject languageStones;
        public Button persianButton;
        public Button englishButton;
        public Image blackout;

        public float fadeSeconds = 1f;

        bool leaving;

        // Real time, capped: the first frame after a scene loads carries the whole load, which
        // would otherwise skip the fade-in.
        static float DeltaTime => Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        void Awake()
        {
            startButton.onClick.AddListener(StartGame);
            recordsButton.onClick.AddListener(OpenRecords);
            recordsBackButton.onClick.AddListener(BackToMainStones);
            languageButton.onClick.AddListener(OpenLanguages);
            exitButton.onClick.AddListener(ExitGame);
            persianButton.onClick.AddListener(() => PickLanguage(Lang.Persian));
            englishButton.onClick.AddListener(() => PickLanguage(Lang.English));
        }

        Button CurrentLanguageButton => Language.Current == Lang.Persian ? persianButton : englishButton;

        // The stone selected when nothing is, for the stones on screen.
        Button DefaultButton => languageStones.activeSelf ? CurrentLanguageButton
            : recordsStones.activeSelf ? recordsBackButton
            : startButton;

        IEnumerator Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            ShowStones(mainStones, startButton);
            yield return Fade(1f, 0f);
        }

        void Update()
        {
            if (leaving) return;
            if (!mainStones.activeSelf && BackPressed()) BackToMainStones();

            // A click on empty space drops the selection; keep one so the keyboard and pad still work.
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null)
                events.SetSelectedGameObject(DefaultButton.gameObject);
        }

        static bool BackPressed()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            return kb != null && (kb.escapeKey.wasPressedThisFrame || kb.backspaceKey.wasPressedThisFrame) ||
                   mouse != null && mouse.rightButton.wasPressedThisFrame ||
                   pad != null && pad.buttonEast.wasPressedThisFrame;
        }

        public void StartGame()
        {
            if (leaving) return;
            SceneMusic.Leaving(startScene, fadeSeconds);
            StartCoroutine(Leave(() => SceneManager.LoadScene(startScene)));
        }

        public void OpenRecords()
        {
            if (leaving) return;
            ShowStones(recordsStones, recordsBackButton);
        }

        public void OpenLanguages()
        {
            if (leaving) return;
            ShowStones(languageStones, CurrentLanguageButton);
        }

        public void PickLanguage(Lang language)
        {
            if (leaving) return;
            Language.Current = language;
            ShowStones(mainStones, languageButton);
        }

        // Back from the records or the languages, with the stone that opened them selected.
        public void BackToMainStones()
        {
            if (leaving) return;
            ShowStones(mainStones, languageStones.activeSelf ? languageButton : recordsButton);
        }

        // Shows one group of stones (the main ones, the records or the languages) in place of the
        // others, with the given stone selected.
        void ShowStones(GameObject stones, Button selected)
        {
            var events = EventSystem.current;
            events?.SetSelectedGameObject(null);
            mainStones.SetActive(stones == mainStones);
            recordsStones.SetActive(stones == recordsStones);
            languageStones.SetActive(stones == languageStones);
            events?.SetSelectedGameObject(selected.gameObject);
        }

        public void ExitGame()
        {
            if (leaving) return;
            SceneMusic.Leaving(null, fadeSeconds);
            StartCoroutine(Leave(() =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }));
        }

        IEnumerator Leave(System.Action then)
        {
            leaving = true;
            // No more clicks while the screen goes dark.
            blackout.raycastTarget = true;
            yield return Fade(blackout.color.a, 1f);
            then();
        }

        IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < fadeSeconds; t += DeltaTime)
            {
                SetAlpha(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / fadeSeconds)));
                yield return null;
            }
            SetAlpha(to);
        }

        void SetAlpha(float alpha)
        {
            var color = blackout.color;
            color.a = alpha;
            blackout.color = color;
        }
    }
}
