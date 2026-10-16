using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Farhad
{
    // Walking, jumping, and working the rock: left mouse strikes, right mouse knocks and listens.
    // Striking rock that isn't hollow strains the mountain; strain it too far and it falls on him.
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D))]
    public class FarhadController : MonoBehaviour
    {
        public MountainGrid mountain;
        public RockSounds sounds;
        public Rockfall rockfall;
        public SpriteRenderer body;
        public Animator animator; // optional: drives the "Walking" state of his drawn animation
        public SpriteRenderer targetMarker;

        [Header("Movement")]
        public float moveSpeed = 4.5f;
        public float jumpHeight = 1.4f;
        public LayerMask groundMask = ~0;

        [Header("Working the rock")]
        public float strikeInterval = 0.3f;
        public float knockRadius = 2.5f;

        // Stamina never refills on its own: only thinking of Shirin restores it (see Longing).
        [Header("Stamina")]
        public float maxStamina = 100f;
        public float solidStrikeCost = 8f;
        public float hollowStrikeCost = 3f;
        public float hardStrikeCost = 8f;
        public float knockCost = 4f;

        [Header("Mountain strain")]
        [Tooltip("Strikes on non-hollow rock in quick succession before the mountain collapses.")]
        public float strainLimit = 4f;
        [Tooltip("Strain shed per second once Farhad stops striking non-hollow rock.")]
        public float strainRecovery = 0.35f;
        public float strainRecoveryDelay = 1.5f;

        public float Stamina { get; private set; }
        public float MaxStamina => maxStamina;
        public bool OutOfBreath => Time.time - lastFailedTime < 1.2f;
        public float Strain { get; private set; }
        public bool IsDead { get; private set; }
        // While dreaming Farhad stands frozen and ignores input.
        public bool IsDreaming { get; set; }
        public bool IsStill => moveInput == 0f && rb.linearVelocity.sqrMagnitude < 0.01f;

        // Raised after every strike that landed (stamina was spent), with the rock that was struck.
        public event Action<RockType> Struck;

        Rigidbody2D rb;
        CapsuleCollider2D col;
        Camera cam;
        ContactFilter2D groundFilter;
        readonly List<Collider2D> groundHits = new();
        float moveInput;
        bool jumpQueued;
        float nextStrikeTime;
        float lastFailedTime = float.NegativeInfinity;
        float lastStrainTime;
        float deathTime;

        public void RestoreStamina(float amount) => Stamina = Mathf.Min(maxStamina, Stamina + amount);

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

            if (IsDead)
            {
                if (kb.rKey.wasPressedThisFrame && Time.time - deathTime > 1f)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            moveInput = 0f;
            if (IsDreaming)
            {
                targetMarker.enabled = false;
                return;
            }

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveInput -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveInput += 1f;
            if ((kb.spaceKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame) && IsGrounded())
                jumpQueued = true;

            Vector2 aim = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            var cell = mountain.rockMap.WorldToCell(aim);
            Vector2 cellCenter = mountain.rockMap.GetCellCenterWorld(cell);
            var rock = mountain.GetRock(cell);
            bool inReach = IsNextTo(cell);

            body.flipX = aim.x < transform.position.x;
            UpdateMarker(cellCenter, rock, inReach);

            if (inReach && rock != RockType.Empty)
            {
                if (mouse.leftButton.isPressed && Time.time >= nextStrikeTime) StrikeAt(cell, rock);
                else if (mouse.rightButton.wasPressedThisFrame) KnockAt(cell, rock);
            }

            if (Time.time - lastStrainTime > strainRecoveryDelay)
                Strain = Mathf.Max(0f, Strain - strainRecovery * Time.deltaTime);
        }

        void FixedUpdate()
        {
            if (IsDead) return;
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
            Struck?.Invoke(rock);
            if (rock != RockType.Hollow) AddStrain();
        }

        void AddStrain()
        {
            Strain += 1f;
            lastStrainTime = Time.time;
            if (Strain >= strainLimit)
            {
                Die();
                return;
            }
            float severity = Strain / strainLimit;
            sounds.PlayRumble(severity);
            rockfall.Warn(transform.position + Vector3.up, severity);
        }

        void Die()
        {
            IsDead = true;
            deathTime = Time.time;
            moveInput = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.simulated = false;
            targetMarker.enabled = false;
            rockfall.Collapse(this);
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
            return true;
        }

        static readonly int WalkingId = Animator.StringToHash("Walking");

        void LateUpdate()
        {
            // Walk only while actually moving, so pushing against a wall shows him standing.
            if (animator != null)
                animator.SetBool(WalkingId, !IsDead && moveInput != 0f && Mathf.Abs(rb.linearVelocity.x) > 0.1f && IsGrounded());
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

        // Only rock directly beside, above, or below Farhad's body can be struck; never corners.
        bool IsNextTo(Vector3Int cell)
        {
            const float inset = 0.05f;
            var bounds = col.bounds;
            var min = mountain.rockMap.WorldToCell(new Vector3(bounds.min.x + inset, bounds.min.y + inset));
            var max = mountain.rockMap.WorldToCell(new Vector3(bounds.max.x - inset, bounds.max.y - inset));
            bool inBodyColumns = cell.x >= min.x && cell.x <= max.x;
            bool inBodyRows = cell.y >= min.y && cell.y <= max.y;
            return (inBodyRows && (cell.x == min.x - 1 || cell.x == max.x + 1))
                || (inBodyColumns && (cell.y == min.y - 1 || cell.y == max.y + 1));
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
