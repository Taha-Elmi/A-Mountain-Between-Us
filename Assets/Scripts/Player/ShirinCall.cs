using UnityEngine;
using UnityEngine.InputSystem;

namespace Farhad
{
    // Farhad cries out for Shirin. He can do it only a few times in the whole game, and each time the
    // hollow rock around him answers: for a moment it shows itself, then the mountain is mute again.
    [RequireComponent(typeof(FarhadController))]
    public class ShirinCall : MonoBehaviour
    {
        public MountainGrid mountain;
        public Key callKey = Key.Q;

        // The mountain's hollow rock is laid out so these calls are enough to find the way through
        // (see MountainGenerator), so changing them changes how the mountain is made.
        public const int DefaultCalls = 3;
        public const float DefaultRevealRadius = 7.5f;

        [Min(0)] public int calls = DefaultCalls;
        [Tooltip("How far from Farhad's body hollow rock is revealed, in tiles.")]
        [Min(0f)] public float revealRadius = DefaultRevealRadius;
        [Tooltip("How long revealed hollow rock stays visible, in seconds.")]
        [Min(0f)] public float revealDuration = 5f;
        [Tooltip("How fast the reveal spreads out from Farhad, in tiles per second (0: all at once).")]
        [Min(0f)] public float revealSpread = 10f;

        public int CallsLeft { get; private set; }
        public float LastCallTime { get; private set; } = float.NegativeInfinity;
        // When he last tried to call her with no calls left.
        public float LastSpentCallTime { get; private set; } = float.NegativeInfinity;
        public bool IsRevealing => Time.time < LastCallTime + revealDuration;

        FarhadController farhad;

        void Awake()
        {
            farhad = GetComponent<FarhadController>();
            CallsLeft = calls;
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || farhad.IsDead || PauseMenu.Paused || !kb[callKey].wasPressedThisFrame) return;
            if (CallsLeft > 0) Call();
            else LastSpentCallTime = Time.time;
        }

        void Call()
        {
            CallsLeft--;
            LastCallTime = Time.time;
            farhad.sounds.PlayCall();
            mountain.RevealAround(farhad.BodyBounds, revealRadius, revealDuration, revealSpread);
        }
    }
}
