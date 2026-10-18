using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farhad
{
    // The far side of the mountain. Reaching it ends the level: a moment later the screen fades to
    // black and the closing comic plays.
    [RequireComponent(typeof(Collider2D))]
    public class GoalZone : MonoBehaviour
    {
        [Tooltip("Loaded once Farhad reaches the goal. Empty stays in the level.")]
        public string nextScene = "ComicOutro";
        [Tooltip("Seconds from reaching the goal to the next scene; the last second fades to black.")]
        public float nextSceneDelay = 3f;

        const float FadeSeconds = 1f;

        public bool Reached { get; private set; }
        float reachedTime;

        // How far the screen has faded to black on the way out: 0 to 1.
        public float FadeOut => Reached && !string.IsNullOrEmpty(nextScene)
            ? Mathf.Clamp01((Time.time - reachedTime - (nextSceneDelay - FadeSeconds)) / FadeSeconds)
            : 0f;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (Reached || other.GetComponent<FarhadController>() == null) return;
            Reached = true;
            reachedTime = Time.time;
            if (!string.IsNullOrEmpty(nextScene)) Invoke(nameof(LoadNextScene), nextSceneDelay);
        }

        void LoadNextScene() => SceneManager.LoadScene(nextScene);
    }
}
