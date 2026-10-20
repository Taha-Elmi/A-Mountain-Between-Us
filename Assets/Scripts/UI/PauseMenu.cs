using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Farhad
{
    // Escape (or Start on a pad) stops the level and shows two stones over a darkened screen:
    // Resume, and Back to the menu. Escape, right-click or B again resumes. While paused, time stands
    // still, the rock sounds hold their breath (the music plays on) and Farhad takes no orders.
    public class PauseMenu : MonoBehaviour
    {
        [Tooltip("Loaded by Back to the menu. Must be in the build's scene list.")]
        public string menuScene = "Menu";

        [Tooltip("The dark screen and the stones on it; hidden while playing.")]
        public GameObject panel;
        public Button resumeButton;
        public Button menuButton;

        // True while paused, and for the frame it resumes on, so the key or click that resumed
        // isn't also taken as a jump or a swing.
        public static bool Paused => paused || Time.frameCount == resumedFrame;

        static bool paused;
        static int resumedFrame = -1;

        void Awake()
        {
            resumeButton.onClick.AddListener(Resume);
            menuButton.onClick.AddListener(BackToMenu);
            panel.SetActive(false);
        }

        // The level restarting or closing never leaves the game stopped.
        void OnDestroy()
        {
            if (paused) SetPaused(false);
        }

        void Update()
        {
            if (!paused)
            {
                if (PausePressed()) Pause();
                return;
            }

            if (PausePressed() || BackPressed()) Resume();

            // A click on empty space drops the selection; keep one so the keyboard and pad still work.
            var events = EventSystem.current;
            if (paused && events != null && events.currentSelectedGameObject == null)
                events.SetSelectedGameObject(resumeButton.gameObject);
        }

        static bool PausePressed()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame || pad != null && pad.startButton.wasPressedThisFrame;
        }

        static bool BackPressed()
        {
            var mouse = Mouse.current;
            var pad = Gamepad.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame;
        }

        public void Pause()
        {
            if (paused) return;
            SetPaused(true);
            panel.SetActive(true);
            var events = EventSystem.current;
            events?.SetSelectedGameObject(null);
            events?.SetSelectedGameObject(resumeButton.gameObject);
        }

        public void Resume()
        {
            if (!paused) return;
            EventSystem.current?.SetSelectedGameObject(null);
            panel.SetActive(false);
            SetPaused(false);
        }

        public void BackToMenu()
        {
            SetPaused(false);
            SceneManager.LoadScene(menuScene);
        }

        static void SetPaused(bool pause)
        {
            if (!pause && paused) resumedFrame = Time.frameCount;
            paused = pause;
            Time.timeScale = pause ? 0f : 1f;
            AudioListener.pause = pause;
        }
    }
}
