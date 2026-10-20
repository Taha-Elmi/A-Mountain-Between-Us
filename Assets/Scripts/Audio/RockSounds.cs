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

        [Tooltip("Recorded takes of Farhad calling Shirin; played in order, one per call, so he grows sadder each time; the last repeats if he calls more often. Empty: the synthesized placeholder.")]
        public AudioClip[] callTakes;
        [Range(0f, 1f)] public float callVolume = 1f;

        [Header("Echo on the call")]
        [Tooltip("Seconds between Farhad's voice and each echo coming back from the mountain.")]
        [Min(0.01f)] public float callEchoDelay = 0.38f;
        [Tooltip("How loud each echo is compared to the one before it.")]
        [Range(0f, 1f)] public float callEchoDecay = 0.45f;
        [Min(0)] public int callEchoes = 4;
        [Tooltip("How much each echo loses its brightness as it carries through the rock.")]
        [Range(0f, 1f)] public float callEchoDamping = 0.5f;

        // His voice plays on its own source: a source's pitch applies to everything it is playing, so the
        // rock's pitch wobble would otherwise bend a call that is still echoing.
        AudioSource source, callSource;
        AudioClip solidHit, hollowHit, rockBreak, rumble, rockfall, call;
        AudioClip[] echoedTakes;
        int callsMade;
        readonly System.Random noise = new(7);

        void Awake()
        {
            source = GetComponent<AudioSource>();
            callSource = AddVoiceSource();

            // The echo is baked into copies of the takes rather than put on the AudioSource, so only his
            // voice echoes and the tail isn't cut off when the clip ends.
            if (callTakes != null)
                echoedTakes = Array.ConvertAll(callTakes, WithEcho);

            // Solid: a short dull thud.
            solidHit = Make("SolidHit", 0.2f, t =>
                Sine(85f, t) * Decay(t, 18f) * 0.8f + Noise() * Decay(t, 40f) * 0.5f);

            // Hollow: a resonant "tok" that rings and echoes back.
            hollowHit = Make("HollowHit", 0.7f, t => Ring(t) + Ring(t - 0.13f) * 0.35f + Noise() * Decay(t, 60f) * 0.3f);

            rockBreak = Make("RockBreak", 0.45f, t =>
                Noise() * Decay(t, 9f) * 0.6f + Sine(60f, t) * Decay(t, 7f) * 0.6f);

            // The mountain groaning: deep noise that swells and fades.
            var groan = Brown();
            rumble = Make("Rumble", 1.2f, t => groan() * Mathf.Sin(Mathf.PI * t / 1.2f));

            // The collapse: a long roar with rocks cracking through it.
            var roar = Brown();
            rockfall = Make("Rockfall", 2.4f, t =>
                roar() * Mathf.Min(1f, t * 20f) * Decay(t, 1.2f) * 1.3f
                + (noise.NextDouble() < 0.002 ? 1f : 0f) + Noise() * Decay(t, 3f) * 0.25f);

            // Calling Shirin: a soft, warm swell.
            call = Make("Call", 1.6f, t =>
                (Sine(392f, t) * 0.4f + Sine(587f, t) * 0.25f + Sine(784f, t) * 0.1f)
                * Mathf.Min(1f, t * 4f) * Decay(t, 2.2f));
        }

        public void PlayStrike(RockType rock, bool broke)
        {
            Play(rock == RockType.Hollow ? hollowHit : solidHit, 0.9f);
            if (broke) Play(rockBreak, 0.6f);
        }

        // severity: 0..1, how close the mountain is to collapsing.
        public void PlayRumble(float severity) => Play(rumble, 0.3f + 0.7f * severity);

        public void PlayRockfall() => Play(rockfall, 1f);

        public void PlayCall()
        {
            int take = callsMade++;
            if (echoedTakes == null || echoedTakes.Length == 0)
            {
                Play(call, 0.6f);
                return;
            }
            // His own voice: no pitch wobble, or it stops sounding like him.
            callSource.PlayOneShot(echoedTakes[Mathf.Min(take, echoedTakes.Length - 1)], callVolume);
        }

        AudioSource AddVoiceSource()
        {
            var voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.outputAudioMixerGroup = source.outputAudioMixerGroup;
            voice.volume = source.volume;
            voice.spatialBlend = source.spatialBlend;
            return voice;
        }

        // A copy of the take with its echoes coming back from the mountain, each quieter and duller than
        // the last.
        AudioClip WithEcho(AudioClip dry)
        {
            if (dry == null || callEchoes == 0) return dry;

            int channels = dry.channels;
            var input = new float[dry.samples * channels];
            dry.GetData(input, 0);

            int delay = Mathf.RoundToInt(callEchoDelay * dry.frequency) * channels;
            var output = new float[input.Length + callEchoes * delay];
            Array.Copy(input, output, input.Length);

            var echo = (float[])input.Clone();
            float smoothing = Mathf.Lerp(1f, 0.15f, callEchoDamping);
            for (int k = 1; k <= callEchoes; k++)
            {
                for (int c = 0; c < channels; c++)
                {
                    float low = 0f;
                    for (int i = c; i < echo.Length; i += channels)
                    {
                        low += smoothing * (echo[i] - low);
                        echo[i] = low * callEchoDecay;
                    }
                }
                for (int i = 0; i < echo.Length; i++) output[k * delay + i] += echo[i];
            }

            float peak = 0f;
            foreach (float v in output) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > 0.99f)
                for (int i = 0; i < output.Length; i++) output[i] *= 0.99f / peak;

            var clip = AudioClip.Create(dry.name + "_echo", output.Length / channels, channels, dry.frequency, false);
            clip.SetData(output, 0);
            return clip;
        }

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
