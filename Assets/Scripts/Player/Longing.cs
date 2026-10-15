using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace Farhad
{
    // Farhad's only source of strength is thinking of Shirin: standing still and typing her name
    // restores stamina. Each thought feeds his madness (Sowda), thinking again soon after feeds it
    // more, and only work calms him. At high madness his knocks start to lie; past the limit he
    // falls into a dream, and every dream raises the madness he can never come back below.
    [RequireComponent(typeof(FarhadController))]
    public class Longing : MonoBehaviour
    {
        public MountainGrid mountain;
        public Tilemap backWallMap;
        public Sprite carvingSprite;

        [Header("Thinking of Shirin")]
        public string belovedName = "SHIRIN";
        public float staminaPerThought = 30f;

        [Header("Madness (Sowda)")]
        public float madnessLimit = 100f;
        public float madnessPerThought = 10f;
        [Tooltip("Extra madness per thought for each other thought within the obsession window.")]
        public float obsessionGrowth = 0.5f;
        public float obsessionWindow = 10f;
        [Tooltip("Madness eased by every strike that lands: work is his only cure.")]
        public float calmPerStrike = 1f;

        [Header("Lying knocks")]
        [Tooltip("Madness at which solid rock starts to ring hollow when knocked.")]
        public float falseEchoStart = 50f;
        [Range(0f, 1f)] public float maxFalseEchoChance = 0.6f;

        [Header("Dream")]
        public float dreamDuration = 4f;
        [Tooltip("How far the madness floor rises after each dream.")]
        public float floorRisePerDream = 15f;
        public float maxFloor = 60f;

        public float Madness { get; private set; }
        public float Floor { get; private set; }
        public int TypedLetters { get; private set; }
        public bool IsDreaming => farhad.IsDreaming;
        public float Madness01 => Madness / madnessLimit;

        FarhadController farhad;
        Transform carvings;
        readonly List<float> recentThoughts = new();

        void Awake()
        {
            farhad = GetComponent<FarhadController>();
            carvings = new GameObject("Carvings of Shirin").transform;
        }

        void OnEnable()
        {
            farhad.Struck += OnStruck;
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
        }

        void OnDisable()
        {
            farhad.Struck -= OnStruck;
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
        }

        void Update()
        {
            // He stops working to think of her: moving breaks the thought.
            if (TypedLetters > 0 && !CanThink()) TypedLetters = 0;

            mountain.FalseEchoChance = maxFalseEchoChance *
                Mathf.InverseLerp(falseEchoStart, madnessLimit, Madness);
        }

        bool CanThink() => !farhad.IsDead && !farhad.IsDreaming && farhad.IsStill;

        void OnTextInput(char c)
        {
            if (!CanThink()) return;

            char letter = char.ToUpperInvariant(c);
            if (letter == belovedName[TypedLetters]) TypedLetters++;
            else TypedLetters = letter == belovedName[0] ? 1 : 0; // a wrong letter breaks the thought

            if (TypedLetters == belovedName.Length)
            {
                TypedLetters = 0;
                ThinkOfHer();
            }
        }

        void ThinkOfHer()
        {
            recentThoughts.RemoveAll(t => Time.time - t > obsessionWindow);
            float madness = madnessPerThought * (1f + obsessionGrowth * recentThoughts.Count);
            recentThoughts.Add(Time.time);

            farhad.RestoreStamina(staminaPerThought);
            farhad.sounds.PlayThought();
            Carve();

            Madness = Mathf.Min(madnessLimit, Madness + madness);
            if (Madness >= madnessLimit) StartCoroutine(Dream());
        }

        void OnStruck(RockType rock) => Madness = Mathf.Max(Floor, Madness - calmPerStrike);

        // Farhad the sculptor leaves her image in the rock wherever he thinks of her.
        void Carve()
        {
            var head = transform.position + Vector3.up * 0.6f;
            if (!backWallMap.HasTile(backWallMap.WorldToCell(head))) return;

            var sr = new GameObject("Carving").AddComponent<SpriteRenderer>();
            sr.transform.SetParent(carvings, false);
            sr.transform.position = head + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.3f, 0.5f), 0f);
            sr.transform.localScale = Vector3.one * Random.Range(0.7f, 1.1f);
            sr.sprite = carvingSprite;
            sr.flipX = Random.value < 0.5f;
            sr.color = new Color(1f, 1f, 1f, Random.Range(0.25f, 0.45f));
            sr.sortingOrder = -9; // just above the back wall, behind the rock
        }

        // Placeholder: the dream itself is not designed yet. Farhad freezes, the HUD shows the
        // dream screen, and he wakes with his madness floor raised.
        IEnumerator Dream()
        {
            farhad.IsDreaming = true;
            TypedLetters = 0;
            yield return new WaitForSeconds(dreamDuration);

            Floor = Mathf.Min(maxFloor, Floor + floorRisePerDream);
            Madness = Floor;
            recentThoughts.Clear();
            farhad.IsDreaming = false;
        }
    }
}
