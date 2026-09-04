using UnityEngine;
using UnityEngine.InputSystem;
using GameJamOcean.World;
using GameJamOcean.Progression;
using GameJamOcean.Combat;
using GameJamOcean.Weapons;

namespace GameJamOcean.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class DiverController : MonoBehaviour
    {
        private const float InputDeadZone = 0.01f;

        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float maximumSpeed = 4.5f;
        [SerializeField, Min(0f)] private float acceleration = 10f;
        [SerializeField, Min(0f)] private float deceleration = 14f;

        [Header("Damage Feedback")]
        [SerializeField, Min(0.1f)] private float hitImmunitySeconds = 1.5f;
        [SerializeField, Min(1)] private int hitBlinks = 3;
        [Header("Dash (Shift)")]
        [SerializeField, Min(0.1f)] private float dashCooldown = 5f;
        [SerializeField, Range(0f, 40f)] private float dashSteeringAngle = 30f;
        [SerializeField, Min(1f)] private float dashSteeringSpeed = 240f;
        private Vector2 dashHeading;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.25f;
        [SerializeField, Min(1f)] private float dashSpeedMultiplier = 3f;
        [SerializeField, Min(0.02f)] private float bubbleInterval = 0.035f;
        [SerializeField, Min(0.1f)] private float bubbleLifetime = 1.2f;
        private Health health;
        private float hitTime = float.NegativeInfinity;
        private float dashUntil;
        private float nextDashTime;
        private float nextBubbleTime;
        private Vector2 lastDirection = Vector2.down;
        private Vector2 dashDirection;
        public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);
        public bool IsDashing => enabled && Time.time < dashUntil;

        [Header("Movement Bounds")]
        [SerializeField] private MovementBounds2D movementBounds;
        [SerializeField, Min(0f)] private float boundsPadding = 0.05f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private string upStateName = "Up Swim";
        [SerializeField] private string downStateName = "Down Swim";
        [SerializeField] private string sideStateName = "Side Swim";
        [SerializeField, Min(0f)] private float transitionDuration = 0.12f;
        [SerializeField, Range(0f, 1f)] private float idleAnimationSpeed = 0.65f;
        [SerializeField, Min(0f)] private float animationSpeedChange = 4f;

        private Rigidbody2D diverRigidbody;
        private Collider2D diverCollider;
        private Vector2 moveInput;
        private bool enabledMoveAction;
        private int upStateHash;
        private int downStateHash;
        private int sideStateHash;
        private int currentStateHash;

        private void Awake()
        {
            diverRigidbody = GetComponent<Rigidbody2D>();
            diverCollider = GetComponent<Collider2D>();
            health = GetComponent<Health>();
            if (health != null) health.ConfigureDamageImmunity(hitImmunitySeconds);

            if (movementBounds == null)
            {
                movementBounds = FindFirstObjectByType<MovementBounds2D>();
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            upStateHash = Animator.StringToHash($"Base Layer.{upStateName}");
            downStateHash = Animator.StringToHash($"Base Layer.{downStateName}");
            sideStateHash = Animator.StringToHash($"Base Layer.{sideStateName}");

            ConfigureRigidbody();
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += OnDamaged;
            if (moveAction == null)
            {
                Debug.LogError(
                    $"{nameof(DiverController)} on '{name}' needs a Move Input Action Reference.",
                    this);
                enabled = false;
                return;
            }

            enabledMoveAction = !moveAction.action.enabled;
            if (enabledMoveAction)
            {
                moveAction.action.Enable();
            }
        }

        private void Start()
        {
            if (GameProgress.HasInstance && GameProgress.Instance.Catalog != null)
            {
                var progress = GameProgress.Instance;
                var catalog = progress.Catalog;
                var life = catalog.Find(UpgradeKind.DiverHealth);
                var speed = catalog.Find(UpgradeKind.DiverSpeed);
                var harpoon = catalog.Find(UpgradeKind.Harpoon);
                if (life != null && TryGetComponent(out Health health))
                    health.SetMaximumHealth(life.Tier(progress.GetLevel(UpgradeKind.DiverHealth)).value, true);
                if (speed != null)
                    maximumSpeed *= 1f + speed.Tier(progress.GetLevel(UpgradeKind.DiverSpeed)).value / 100f;
                if (harpoon != null && TryGetComponent(out HarpoonLauncher2D launcher))
                    launcher.EquipHarpoon(harpoon.Tier(progress.GetLevel(UpgradeKind.Harpoon)).harpoonPrefab);
            }
            ChangeAnimation(downStateHash, 0f);
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= OnDamaged;
            if (spriteRenderer != null) spriteRenderer.enabled = true;
            hitTime = float.NegativeInfinity;
            dashUntil = 0f;
            moveInput = Vector2.zero;

            if (diverRigidbody != null)
            {
                diverRigidbody.linearVelocity = Vector2.zero;
            }

            if (enabledMoveAction && moveAction != null)
            {
                moveAction.action.Disable();
            }

            enabledMoveAction = false;
        }

        private void Update()
        {
            moveInput = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
            if (Time.timeScale > 0f && (health == null || !health.IsDead))
            {
                if (moveInput.sqrMagnitude > InputDeadZone) lastDirection = moveInput.normalized;
                if (Keyboard.current != null && (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame)
                    && Time.time >= nextDashTime)
                {
                    dashDirection = lastDirection;
                    dashHeading = dashDirection;
                    dashUntil = Time.time + dashDuration;
                    nextDashTime = Time.time + dashCooldown;
                }
                if (IsDashing && Time.time >= nextBubbleTime)
                {
                    DiveFeedbackParticle.SpawnBubble(transform.position, spriteRenderer, bubbleLifetime);
                    nextBubbleTime = Time.time + bubbleInterval;
                }
            }
            UpdateAnimation();
        }

        private void OnDamaged(Health target, GameObject source) => hitTime = Time.time;

        private void LateUpdate()
        {
            float elapsed = Time.time - hitTime;
            spriteRenderer.enabled = elapsed >= hitImmunitySeconds
                || Mathf.FloorToInt(elapsed / hitImmunitySeconds * hitBlinks * 2) % 2 == 1;
        }

        private void FixedUpdate()
        {
            if (health != null && health.IsDead) { diverRigidbody.linearVelocity = Vector2.zero; return; }
            if (IsDashing)
            {
                Vector2 perpendicular = new(-dashDirection.y, dashDirection.x);
                float steering = Mathf.Clamp(Vector2.Dot(moveInput, perpendicular), -1f, 1f);
                Vector2 desired = (dashDirection + perpendicular * (steering * Mathf.Tan(dashSteeringAngle * Mathf.Deg2Rad))).normalized;
                dashHeading = Vector3.RotateTowards(dashHeading, desired, dashSteeringSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                diverRigidbody.linearVelocity = dashHeading * maximumSpeed * dashSpeedMultiplier;
                ApplyMovementBounds();
                return;
            }
            Vector2 desiredVelocity = moveInput * maximumSpeed;
            float speedChange = moveInput.sqrMagnitude > InputDeadZone
                ? acceleration
                : deceleration;

            diverRigidbody.linearVelocity = Vector2.MoveTowards(
                diverRigidbody.linearVelocity,
                desiredVelocity,
                speedChange * Time.fixedDeltaTime);

            ApplyMovementBounds();
        }

        private void ApplyMovementBounds()
        {
            if (movementBounds == null)
            {
                return;
            }

            Vector2 currentPosition = diverRigidbody.position;
            Vector2 extents = diverCollider != null
                ? (Vector2)diverCollider.bounds.extents
                : Vector2.zero;
            extents += Vector2.one * boundsPadding;

            Vector2 clampedPosition = movementBounds.ClampPoint(currentPosition, extents);
            Vector2 velocity = diverRigidbody.linearVelocity;

            if (!Mathf.Approximately(currentPosition.x, clampedPosition.x))
            {
                velocity.x = 0f;
            }

            if (!Mathf.Approximately(currentPosition.y, clampedPosition.y))
            {
                velocity.y = 0f;
            }

            diverRigidbody.position = clampedPosition;
            // Clamp the next physics step too: a fast dash must not cross DiveArea.
            Vector2 next = movementBounds.ClampPoint(clampedPosition + velocity * Time.fixedDeltaTime, extents);
            velocity = (next - clampedPosition) / Time.fixedDeltaTime;
            diverRigidbody.linearVelocity = velocity;
        }

        private void UpdateAnimation()
        {
            bool isMoving = moveInput.sqrMagnitude > InputDeadZone;
            float targetAnimationSpeed = isMoving ? 1f : idleAnimationSpeed;
            animator.speed = Mathf.MoveTowards(
                animator.speed,
                targetAnimationSpeed,
                animationSpeedChange * Time.deltaTime);

            if (!isMoving)
            {
                return;
            }

            if (Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
            {
                spriteRenderer.flipX = moveInput.x > 0f;
                ChangeAnimation(sideStateHash, transitionDuration);
                return;
            }

            spriteRenderer.flipX = false;
            ChangeAnimation(
                moveInput.y > 0f ? upStateHash : downStateHash,
                transitionDuration);
        }

        private void ChangeAnimation(int nextStateHash, float fadeDuration)
        {
            if (currentStateHash == nextStateHash)
            {
                return;
            }

            currentStateHash = nextStateHash;
            animator.CrossFadeInFixedTime(nextStateHash, fadeDuration);
        }

        private void ConfigureRigidbody()
        {
            diverRigidbody.gravityScale = 0f;
            diverRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            diverRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            diverRigidbody.freezeRotation = true;
        }

        private void OnValidate()
        {
            maximumSpeed = Mathf.Max(0f, maximumSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            transitionDuration = Mathf.Max(0f, transitionDuration);
            animationSpeedChange = Mathf.Max(0f, animationSpeedChange);
            boundsPadding = Mathf.Max(0f, boundsPadding);
            hitImmunitySeconds = Mathf.Max(0.1f, hitImmunitySeconds);
            hitBlinks = Mathf.Max(1, hitBlinks);
            dashDuration = Mathf.Max(0.05f, dashDuration);
            dashCooldown = Mathf.Max(dashDuration, dashCooldown);
            dashSpeedMultiplier = Mathf.Max(1f, dashSpeedMultiplier);
            if (Application.isPlaying && health != null) health.ConfigureDamageImmunity(hitImmunitySeconds);
        }
    }
}
