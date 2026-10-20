using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farhad
{
    // A song that runs across scenes: it starts with the scene it's placed in, loops, carries on
    // through the scenes listed in alsoPlaysIn, and fades out once the game heads anywhere else.
    // The menu's song plays on through the opening comic; the closing comic's song plays until
    // the menu. A scene that leaves on a fade to black calls Leaving first, so the song fades with
    // the picture; otherwise it fades just after the next scene loads.
    [RequireComponent(typeof(AudioSource))]
    public class SceneMusic : MonoBehaviour
    {
        public AudioClip music;
        [Range(0f, 1f)] public float volume = 0.6f;
        [Tooltip("Scenes, by name, the song keeps playing in after the one it starts in.")]
        public string[] alsoPlaysIn = { };
        [Tooltip("Seconds the song takes to fade out when the game leaves its scenes.")]
        [Min(0.01f)] public float fadeSeconds = 1f;

        // The song playing now, if any.
        static SceneMusic current;

        AudioSource source;
        string startScene;
        bool fading;
        float fadeSpeed;

        void Awake()
        {
            startScene = gameObject.scene.name;
            // Coming back to a scene of a song that's still going (the menu reloaded) keeps it going.
            if (current != null && current.music == music && current.PlaysIn(startScene))
            {
                Destroy(gameObject);
                return;
            }
            if (current != null) Destroy(current.gameObject);

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
            source.ignoreListenerPause = true;
            if (music != null) source.Play();
        }

        void OnDestroy()
        {
            if (current != this) return;
            current = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        // The game is about to load nextScene (null when quitting), fading to black over `seconds`:
        // the song fades with it unless it plays on there.
        public static void Leaving(string nextScene, float seconds)
        {
            if (current != null && (nextScene == null || !current.PlaysIn(nextScene))) current.FadeOut(seconds);
        }

        bool PlaysIn(string scene) => scene == startScene || System.Array.IndexOf(alsoPlaysIn, scene) >= 0;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!PlaysIn(scene.name)) FadeOut(fadeSeconds);
        }

        void FadeOut(float seconds)
        {
            if (fading) return;
            fading = true;
            fadeSpeed = volume / Mathf.Max(seconds, 0.01f);
        }

        void Update()
        {
            if (!fading) return;
            // Real time: the song fades even if the game is paused.
            source.volume = Mathf.MoveTowards(source.volume, 0f, fadeSpeed * Time.unscaledDeltaTime);
            if (source.volume <= 0f) Destroy(gameObject);
        }
    }
}
