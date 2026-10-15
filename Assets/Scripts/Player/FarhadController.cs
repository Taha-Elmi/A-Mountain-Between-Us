using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Farhad
{
    // Walking, jumping, and working the rock: left mouse strikes, right mouse knocks and listens.
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class FarhadController : MonoBehaviour
    {
        public MountainGrid mountain;
        public RockSounds sounds;
        public SpriteRenderer body;
        public SpriteRenderer targetMarker;

        [Header("Movement")]
        public float moveSpeed = 4.5f;
        public float jumpHeight = 1.4f;
        public LayerMask groundMask = ~0;

        [Header("Working the rock")]
        public float reach = 2.2f;
        public float strikeInterval = 0.3f;
        public float knockRadius = 2.5f;

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float solidStrikeCost = 8f;
        public float hollowStrikeCost = 3f;
        public float hardStrikeCost = 8f;
        public float knockCost = 4f;
        public float staminaRegen = 15f;
        public float regenDelay = 0.8f;

        public float Stamina { get; private set; }
        public float MaxStamina => maxStamina;
        public bool OutOfBreath => Time.time - lastFailedTime < 1.2f;

        Rigidbody2D rb;
        CapsuleCollider2D col;
        Camera cam;
        ContactFilter2D groundFilter;
        readonly List<Collider2D> groundHits = new();
        float moveInput;
        bool jumpQueued;
        float nextStrikeTime;
        float lastExertTime;
        float lastFailedTime = float.NegativeInfinity;

        Vector2 ReachOrigin => (Vector2)transform.position + new Vector2(0f, 0.3f);

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CapsuleCollider2D>();
            cam = Camera.main;
            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetLayerMask(groundMask);
            Stamina = maxStamina;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            moveInput = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput += 1f;
            if ((kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) && IsGrounded())
                jumpQueued = true;

            Vector2 aim = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var cell = mountain.rockMap.WorldToCell(aim);
            Vector2 cellCenter = mountain.rockMap.GetCellCenterWorld(cell);
            var rock = mountain.GetRock(cell);
            bool inReach = Vector2.Distance(ReachOrigin, cellCenter) <= reach;

            body.flipX = aim.x < transform.position.x;
            UpdateMarker(cellCenter, rock, inReach);

            if (inReach && rock != RockType.Empty)
            {
                if (mouse.leftButton.isPressed && Time.time >= nextStrikeTime) StrikeAt(cell, rock);
                else if (mouse.rightButton.wasPressedThisFrame) KnockAt(cell, rock);
            }

            if (Time.time - lastExertTime > regenDelay)
                Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * Time.deltaTime);
        }

        void FixedUpdate()
        {
            float vy = rb.linearVelocity.y;
            if (jumpQueued)
            {
                vy = Mathf.Sqrt(2f * Mathf.Abs(Physics2D.gravity.y * rb.gravityScale) * jumpHeight);
                jumpQueued = false;
            }
            rb.linearVelocity = new Vector2(moveInput * moveSpeed, vy);
        }

        void StrikeAt(Vector3Int cell, RockType rock)
        {
            nextStrikeTime = Time.time + strikeInterval;
            float cost = rock switch
            {
                RockType.Hollow => hollowStrikeCost,
                RockType.Hard => hardStrikeCost,
                _ => solidStrikeCost,
            };
            if (!TrySpend(cost)) return;

            bool broke = mountain.Strike(cell);
            sounds.PlayStrike(rock, broke);
        }

        void KnockAt(Vector3Int cell, RockType rock)
        {
            if (!TrySpend(knockCost)) return;
            int answered = mountain.Knock(cell, knockRadius);
            sounds.PlayKnock(answered > 0 || rock == RockType.Hollow);
        }

        bool TrySpend(float cost)
        {
            if (Stamina < cost)
            {
                lastFailedTime = Time.time;
                return false;
            }
            Stamina -= cost;
            lastExertTime = Time.time;
            return true;
        }

        bool IsGrounded()
        {
            var bounds = col.bounds;
            var feet = new Vector2(bounds.center.x, bounds.min.y - 0.05f);
            Physics2D.OverlapBox(feet, new Vector2(bounds.size.x * 0.9f, 0.1f), 0f, groundFilter, groundHits);
            foreach (var hit in groundHits)
                if (hit != col) return true;
            return false;
        }

        void UpdateMarker(Vector2 cellCenter, RockType rock, bool inReach)
        {
            targetMarker.enabled = rock != RockType.Empty;
            targetMarker.transform.position = cellCenter;
            targetMarker.color = !inReach ? new Color(1f, 0.35f, 0.3f, 0.35f)
                : rock == RockType.Hard ? new Color(0.7f, 0.75f, 0.85f, 0.6f)
                : new Color(1f, 1f, 1f, 0.85f);
        }
    }
}
