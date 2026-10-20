using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farhad
{
    // The level's music. Loops on its own AudioSource, so rock sounds never cut it off. It outlives the
    // level restarting after a death, so the song carries on instead of starting over, fades out once
    // Farhad reaches the far side of the mountain, and stops as soon as any other scene loads.
    [RequireComponent(typeof(AudioSource))]
    public class Soundtrack : MonoBehaviour
    {
        public AudioClip music;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("Seconds after first entering the level before the music starts.")]
        [Min(0f)] public float startDelay = 2f;
        [Tooltip("Seconds the music takes to fade out after the goal is reached.")]
        [Min(0.01f)] public float fadeSeconds = 3f;

        // The soundtrack already playing; a restarted level's own copy defers to it.
        static Soundtrack current;

        AudioSource source;
        GoalZone goal;
        int sceneIndex;
        float fade = 1f;

        void Awake()
        {
            sceneIndex = gameObject.scene.buildIndex;
            if (current != null && current.sceneIndex == sceneIndex)
            {
                Destroy(gameObject);
                return;
            }

            current = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;

            source = GetComponent<AudioSource>();
            source.clip = music;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = volume;
            source.ignoreListenerPause = true; // plays on under the pause menu
            goal = FindAnyObjectByType<GoalZone>();
            if (music != null) source.PlayDelayed(startDelay);
        }

        void OnDestroy()
        {
            if (current != this) return;
            current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // The level restarting keeps the music going with the new level's goal; any other scene ends it.
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == sceneIndex) goal = FindAnyObjectByType<GoalZone>();
            else Destroy(gameObject);
        }

        void Update()
        {
            if (goal == null || !goal.Reached || fade <= 0f) return;
            fade = Mathf.MoveTowards(fade, 0f, Time.deltaTime / fadeSeconds);
            source.volume = volume * fade;
            if (fade <= 0f) source.Stop();
        }
    }
}
