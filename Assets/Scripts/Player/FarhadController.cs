using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Farhad
{
    // Walking, jumping, and working the rock with left mouse. Hollow rock breaks; solid rock doesn't,
    // and striking it strains the mountain until it falls on him.
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

        // A strike is a swing: stamina is spent when it starts, the rock is hit at the impact moment
        // (matched to the drawn animation), and he can't move or swing again until it ends.
        [Header("Working the rock")]
        public float strikeImpactTime = 0.42f;
        public float strikeDuration = 0.67f;

        [Header("Mountain strain")]
        [Tooltip("Strikes on solid rock (in quick succession) that bring the rockfall. 1: the first one does. " +
                 "Higher: the earlier ones only warn, with rumbling, pebbles and shaking.")]
        [Min(1f)] public float strainLimit = 1f;
        [Tooltip("Strain shed per second once Farhad stops striking solid rock.")]
        public float strainRecovery = 0.35f;
        public float strainRecoveryDelay = 1.5f;

        public float Strain { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsSwinging => Time.time < swingEnd;
        public Bounds BodyBounds => col.bounds;

        Rigidbody2D rb;
        CapsuleCollider2D col;
        Camera cam;
        ContactFilter2D groundFilter;
        readonly List<Collider2D> groundHits = new();
        float moveInput;
        bool jumpQueued;
        float swingEnd;
        float swingImpact;
        bool swingLanded = true;
        Vector3Int swingCell;
        float lastStrainTime;
        float deathTime;

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<CapsuleCollider2D>();
            cam = Camera.main;
            groundFilter = new ContactFilter2D { useTriggers = false };
            groundFilter.SetLayerMask(groundMask);
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
            if (!swingLanded && Time.time >= swingImpact) LandSwing();
            if (IsSwinging)
            {
                // Mid-swing he is planted: no walking, jumping, turning, or new swings.
                targetMarker.enabled = false;
                UpdateStrain();
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

            // He faces the way he walks, and keeps facing that way when he stops.
            if (moveInput != 0f) body.flipX = moveInput < 0f;
            UpdateMarker(cellCenter, rock, inReach);

            if (inReach && rock != RockType.Empty && mouse.leftButton.isPressed) StartSwing(cell);

            UpdateStrain();
        }

        void UpdateStrain()
        {
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

        static readonly int StrikeId = Animator.StringToHash("Strike");

        void StartSwing(Vector3Int cell)
        {
            swingCell = cell;
            swingImpact = Time.time + strikeImpactTime;
            swingEnd = Time.time + strikeDuration;
            swingLanded = false;
            moveInput = 0f;
            // Turn toward rock beside him; rock above or below keeps his current facing.
            int side = SideOf(cell);
            if (side != 0) body.flipX = side < 0;
            if (animator != null) animator.SetTrigger(StrikeId);
        }

        void LandSwing()
        {
            swingLanded = true;
            var rock = mountain.GetRock(swingCell);
            if (rock == RockType.Empty) return;

            bool broke = mountain.Strike(swingCell);
            sounds.PlayStrike(rock, broke);
            if (rock == RockType.Solid) AddStrain();
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

        // -1 if the cell lies left of his body's columns, +1 if right, 0 if within them.
        int SideOf(Vector3Int cell)
        {
            const float inset = 0.05f;
            var bounds = col.bounds;
            int minX = mountain.rockMap.WorldToCell(new Vector3(bounds.min.x + inset, bounds.center.y)).x;
            int maxX = mountain.rockMap.WorldToCell(new Vector3(bounds.max.x - inset, bounds.center.y)).x;
            return cell.x < minX ? -1 : cell.x > maxX ? 1 : 0;
        }

        void UpdateMarker(Vector2 cellCenter, RockType rock, bool inReach)
        {
            // Only rock he can actually strike is marked.
            targetMarker.enabled = inReach && rock != RockType.Empty;
            targetMarker.transform.position = cellCenter;
            targetMarker.color = new Color(1f, 1f, 1f, 0.85f);
        }
    }
}
