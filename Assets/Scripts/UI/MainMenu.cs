using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Farhad
{
    // The first scene: fades in from black over the artist's picture and waits for a choice. Start
    // fades to black and opens the story (the opening comic), Exit quits. Records and Settings are
    // there but do nothing yet. Works with the mouse, the keyboard and a gamepad (Start is
    // selected first, so Enter / A starts the game).
    public class MainMenu : MonoBehaviour
    {
        [Tooltip("Loaded by Start. Must be in the build's scene list.")]
        public string startScene = "ComicIntro";

        public Button startButton;
        public Button recordsButton;
        public Button settingsButton;
        public Button exitButton;
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
            settingsButton.onClick.AddListener(OpenSettings);
            exitButton.onClick.AddListener(ExitGame);
        }

        IEnumerator Start()
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            EventSystem.current?.SetSelectedGameObject(startButton.gameObject);
            yield return Fade(1f, 0f);
        }

        void Update()
        {
            // A click on empty space drops the selection; keep one so the keyboard and pad still work.
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == null && !leaving)
                events.SetSelectedGameObject(startButton.gameObject);
        }

        public void StartGame()
        {
            if (leaving) return;
            StartCoroutine(Leave(() => SceneManager.LoadScene(startScene)));
        }

        public void OpenRecords()
        {
            Debug.Log("Records: not made yet.");
        }

        public void OpenSettings()
        {
            Debug.Log("Settings: not made yet.");
        }

        public void ExitGame()
        {
            if (leaving) return;
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
