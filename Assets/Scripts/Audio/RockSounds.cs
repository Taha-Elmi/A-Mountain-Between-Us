using System;
using UnityEngine;

namespace Farhad
{
    // Placeholder rock sounds synthesized at startup, so hollow and solid rock can be told apart by ear
    // before real audio exists. Swap the clips for recorded ones later.
    [RequireComponent(typeof(AudioSource))]
    public class RockSounds : MonoBehaviour
    {
        const int SampleRate = 44100;

        AudioSource source;
        AudioClip solidHit, hollowHit, hardHit, rockBreak, knockSolid, knockHollow, rumble, rockfall, thought;
        readonly System.Random noise = new(7);

        void Awake()
        {
            source = GetComponent<AudioSource>();

            // Solid: a short dull thud.
            solidHit = Make("SolidHit", 0.2f, t =>
                Sine(85f, t) * Decay(t, 18f) * 0.8f + Noise() * Decay(t, 40f) * 0.5f);

            // Hollow: a resonant "tok" that rings and echoes back.
            hollowHit = Make("HollowHit", 0.7f, t => Ring(t) + Ring(t - 0.13f) * 0.35f + Noise() * Decay(t, 60f) * 0.3f);

            // Hard: a bright clang that does nothing.
            hardHit = Make("HardHit", 0.4f, t =>
                (Sine(1180f, t) * 0.5f + Sine(1730f, t) * 0.3f) * Decay(t, 12f) + Noise() * Decay(t, 80f) * 0.3f);

            rockBreak = Make("RockBreak", 0.45f, t =>
                Noise() * Decay(t, 9f) * 0.6f + Sine(60f, t) * Decay(t, 7f) * 0.6f);

            knockSolid = Make("KnockSolid", 0.15f, t => Sine(140f, t) * Decay(t, 35f) * 0.6f);

            knockHollow = Make("KnockHollow", 1.1f, t =>
                (Ring(t) + Ring(t - 0.18f) * 0.45f + Ring(t - 0.36f) * 0.2f) * 0.8f);

            // The mountain groaning: deep noise that swells and fades.
            var groan = Brown();
            rumble = Make("Rumble", 1.2f, t => groan() * Mathf.Sin(Mathf.PI * t / 1.2f));

            // The collapse: a long roar with rocks cracking through it.
            var roar = Brown();
            rockfall = Make("Rockfall", 2.4f, t =>
                roar() * Mathf.Min(1f, t * 20f) * Decay(t, 1.2f) * 1.3f
                + (noise.NextDouble() < 0.002 ? 1f : 0f) + Noise() * Decay(t, 3f) * 0.25f);

            // Thinking of Shirin: a soft, warm swell.
            thought = Make("Thought", 1.6f, t =>
                (Sine(392f, t) * 0.4f + Sine(587f, t) * 0.25f + Sine(784f, t) * 0.1f)
                * Mathf.Min(1f, t * 4f) * Decay(t, 2.2f));
        }

        public void PlayStrike(RockType rock, bool broke)
        {
            var clip = rock switch
            {
                RockType.Hollow => hollowHit,
                RockType.Hard => hardHit,
                _ => solidHit,
            };
            Play(clip, 0.9f);
            if (broke) Play(rockBreak, 0.6f);
        }

        public void PlayKnock(bool hollowAnswered) => Play(hollowAnswered ? knockHollow : knockSolid, 0.7f);

        // severity: 0..1, how close the mountain is to collapsing.
        public void PlayRumble(float severity) => Play(rumble, 0.3f + 0.7f * severity);

        public void PlayRockfall() => Play(rockfall, 1f);

        public void PlayThought() => Play(thought, 0.6f);

        void Play(AudioClip clip, float volume)
        {
            source.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
            source.PlayOneShot(clip, volume);
        }

        static float Ring(float t) =>
            t < 0f ? 0f : (Sine(230f, t) * 0.6f + Sine(345f, t) * 0.3f + Sine(560f, t) * 0.15f) * Decay(t, 6f);

        static float Sine(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);
        static float Decay(float t, float rate) => Mathf.Exp(-rate * t);
        float Noise() => (float)(noise.NextDouble() * 2.0 - 1.0);

        // Low, rumbling noise: white noise run through a leaky integrator.
        Func<float> Brown()
        {
            float level = 0f;
            return () =>
            {
                level = (level + 0.02f * Noise()) / 1.02f;
                return level * 3.5f;
            };
        }

        static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float fadeOut = Mathf.Clamp01((count - i) / (SampleRate * 0.02f));
                data[i] = Mathf.Clamp(wave(t) * fadeOut, -1f, 1f);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
