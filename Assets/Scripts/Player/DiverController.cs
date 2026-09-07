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
        [Tooltip("Invisible trigger used only to collect power-ups; it does not collide with enemies.")]
        [SerializeField, Min(.1f)] private float powerUpDetectionRadius = .75f;

        [Header("Damage Feedback")]
        [SerializeField, Min(0.1f)] private float hitImmunitySeconds = 1.5f;
        [SerializeField, Min(1)] private int hitBlinks = 3;
        [Header("Dash (Shift)")]
        [SerializeField, Min(0.1f)] private float dashCooldown = 5f;
        [SerializeField, Range(0f, 40f)] private float dashSteeringAngle = 30f;
        [SerializeField, Min(1f)] private float dashSteeringSpeed = 240f;
        private Vector2 dashHeading;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.21f;
        [SerializeField, Min(1f)] private float dashSpeedMultiplier = 2.65f;
        [SerializeField, Min(0.02f)] private float bubbleInterval = 0.035f;
        [SerializeField, Min(0.1f)] private float bubbleLifetime = 1.2f;
        private Health health;
        private float hitTime = float.NegativeInfinity;
        private float dashUntil;
        private float nextDashTime;
        private float nextBubbleTime;
        private Vector2 lastDirection = Vector2.down;
        private Vector2 dashDirection;
        private Vector2 knockbackVelocity;
        private float knockbackUntil;
        private float speedBoostUntil;
        private float speedBoostMultiplier = 1f;
        private float speedBoostDuration;
        private float shieldUntil;
        private float shieldDuration;
        public float SpeedBoostRemaining => Mathf.Max(0f, speedBoostUntil - Time.time);
        public float SpeedBoostNormalized => speedBoostDuration > 0f
            ? Mathf.Clamp01(SpeedBoostRemaining / speedBoostDuration) : 0f;
        public float ShieldRemaining => Mathf.Max(0f, shieldUntil - Time.time);
        public float ShieldNormalized => shieldDuration > 0f
            ? Mathf.Clamp01(ShieldRemaining / shieldDuration) : 0f;
        public float DashCooldownRemaining => Mathf.Max(0f, nextDashTime - Time.time);
        public float PowerUpDetectionRadius => powerUpDetectionRadius;
        public bool IsDashing => enabled && Time.time < dashUntil;
        public void ApplyKnockback(Vector2 velocity, float duration)
        {
            if (health == null || health.IsDead) return;
            knockbackVelocity = velocity;
            knockbackUntil = Mathf.Max(knockbackUntil, Time.time + Mathf.Max(.05f, duration));
        }

        public void ActivateSpeedBoost(float duration, float multiplier)
        {
            speedBoostDuration = Mathf.Max(.1f, duration);
            speedBoostUntil = Mathf.Max(speedBoostUntil, Time.time + speedBoostDuration);
            speedBoostMultiplier = Mathf.Max(speedBoostMultiplier, Mathf.Max(1f, multiplier));
        }

        public void ActivateShield(float duration)
        {
            shieldDuration = Mathf.Max(.1f, duration);
            shieldUntil = Mathf.Max(shieldUntil, Time.time + shieldDuration);
            health?.GrantImmunity(shieldDuration);
        }

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
            ConfigurePowerUpDetector();
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
                {
                    int harpoonLevel = progress.GetLevel(UpgradeKind.Harpoon);
                    launcher.EquipHarpoon(harpoon.Tier(harpoonLevel).harpoonPrefab);
                    launcher.SetBaseProjectileCount(harpoonLevel >= 4 ? 2 : 1);
                }
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
            if (GameJamOcean.UI.GameMenus.BlocksGameplay) return;
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
                    GameJamOcean.Audio.GameAudio.Instance?.PlayAquaticDash(false);
                }
                if (IsDashing && Time.time >= nextBubbleTime)
                {
                    DiveFeedbackParticle.SpawnBubble(transform.position, spriteRenderer, bubbleLifetime);
                    nextBubbleTime = Time.time + bubbleInterval;
                }
            }
            UpdateAnimation();
        }

        private void OnDamaged(Health target, GameObject source)
        {
            hitTime = Time.time;
            if (spriteRenderer == null) return;
            Vector3 top = spriteRenderer.bounds.center
                + Vector3.up * (spriteRenderer.bounds.extents.y + .2f);
            DiveFeedbackParticle.SpawnPlayerHealthChange(top, false,
                spriteRenderer.sortingLayerID, spriteRenderer.sortingOrder);
        }

        private void LateUpdate()
        {
            float elapsed = Time.time - hitTime;
            spriteRenderer.enabled = elapsed >= hitImmunitySeconds
                || Mathf.FloorToInt(elapsed / hitImmunitySeconds * hitBlinks * 2) % 2 == 1;
        }

        private void FixedUpdate()
        {
            if (health != null && health.IsDead) { diverRigidbody.linearVelocity = Vector2.zero; return; }
            float activeMaximumSpeed = maximumSpeed
                * (Time.time < speedBoostUntil ? speedBoostMultiplier : 1f);
            if (Time.time < knockbackUntil)
            {
                diverRigidbody.linearVelocity = knockbackVelocity;
                ApplyMovementBounds();
                return;
            }
            if (IsDashing)
            {
                Vector2 perpendicular = new(-dashDirection.y, dashDirection.x);
                float steering = Mathf.Clamp(Vector2.Dot(moveInput, perpendicular), -1f, 1f);
                Vector2 desired = (dashDirection + perpendicular * (steering * Mathf.Tan(dashSteeringAngle * Mathf.Deg2Rad))).normalized;
                dashHeading = Vector3.RotateTowards(dashHeading, desired, dashSteeringSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f);
                // A speed buff must not multiply the dash too; dash strength remains predictable.
                diverRigidbody.linearVelocity = dashHeading * maximumSpeed * dashSpeedMultiplier;
                ApplyMovementBounds();
                return;
            }
            Vector2 desiredVelocity = moveInput * activeMaximumSpeed;
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

        private void ConfigurePowerUpDetector()
        {
            const string detectorName = "PowerUp Pickup Detector";
            Transform detector = transform.Find(detectorName);
            if (detector == null)
            {
                var detectorObject = new GameObject(detectorName);
                detectorObject.transform.SetParent(transform, false);
                detector = detectorObject.transform;
            }
            CircleCollider2D trigger = detector.GetComponent<CircleCollider2D>();
            if (trigger == null) trigger = detector.gameObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = powerUpDetectionRadius;
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
            powerUpDetectionRadius = Mathf.Max(.1f, powerUpDetectionRadius);
            if (Application.isPlaying && health != null) health.ConfigureDamageImmunity(hitImmunitySeconds);
        }
    }
}
