using UnityEngine;
using UnityEngine.SceneManagement;

namespace Farhad
{
    // One way through the mountain: from coming into the level after the opening comic to breaking
    // out on the far side, across every retry after a rockfall. A retry (R) loads the level again
    // with the same hollow rock, so what the player learned of this path still holds; only a fresh
    // run lays the mountain out anew. The run counts its rockfalls and its time (paused time
    // doesn't count), and reaching the far side keeps them in the Records.
    public static class DigRun
    {
        static bool retrying;
        static float startTime;

        // The hollow rock's layout for this run.
        public static int Seed { get; private set; }
        public static int Rockfalls { get; private set; }
        public static bool Finished { get; private set; }
        public static float Seconds => Time.time - startTime;

        // Called as the level loads. False: a fresh run, with a new seed; true: a retry of this one.
        public static bool Enter()
        {
            bool retry = retrying;
            retrying = false;
            if (retry) return true;

            Seed = new System.Random().Next();
            Rockfalls = 0;
            Finished = false;
            startTime = Time.time;
            return false;
        }

        public static void RockFell() => Rockfalls++;

        // Loads the level again for another try at the same path.
        public static void Retry()
        {
            retrying = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public static void Finish()
        {
            if (Finished) return;
            Finished = true;
            Records.Add(Rockfalls, Seconds);
        }
    }
}
