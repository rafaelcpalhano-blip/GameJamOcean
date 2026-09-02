using UnityEngine;
using UnityEngine.InputSystem;

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
        private Vector2 moveInput;
        private bool enabledMoveAction;
        private int upStateHash;
        private int downStateHash;
        private int sideStateHash;
        private int currentStateHash;

        private void Awake()
        {
            diverRigidbody = GetComponent<Rigidbody2D>();

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
            ChangeAnimation(downStateHash, 0f);
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;

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
        }
    }
}
