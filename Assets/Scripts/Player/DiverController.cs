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
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
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
        }
    }
}
