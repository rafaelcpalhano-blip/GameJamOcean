using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class BoatController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float maximumSpeed = 6f;
        [SerializeField, Min(0f)] private float acceleration = 12f;
        [SerializeField, Min(0f)] private float deceleration = 16f;
        [SerializeField, Min(0f)] private float rotationSpeed = 360f;

        [Header("Model Orientation")]
        [Tooltip("Rotation offset if the model's bow does not point toward screen up at zero rotation.")]
        [SerializeField] private float modelForwardOffset;

        private Rigidbody2D boatRigidbody;
        private Vector2 moveInput;
        private bool enabledMoveAction;

        private void Awake()
        {
            boatRigidbody = GetComponent<Rigidbody2D>();
            ConfigureRigidbody();
        }

        private void OnEnable()
        {
            if (moveAction == null)
            {
                Debug.LogError(
                    $"{nameof(BoatController)} on '{name}' needs a Move Input Action Reference.",
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

        private void OnDisable()
        {
            moveInput = Vector2.zero;

            if (!enabledMoveAction || moveAction == null)
            {
                return;
            }

            moveAction.action.Disable();
            enabledMoveAction = false;
        }

        private void Update()
        {
            moveInput = Vector2.ClampMagnitude(moveAction.action.ReadValue<Vector2>(), 1f);
        }

        private void FixedUpdate()
        {
            Move();
            RotateTowardsMovement();
        }

        private void Move()
        {
            Vector2 desiredVelocity = moveInput;
            desiredVelocity *= maximumSpeed;

            float speedChange = moveInput.sqrMagnitude > 0f ? acceleration : deceleration;

            boatRigidbody.linearVelocity = Vector2.MoveTowards(
                boatRigidbody.linearVelocity,
                desiredVelocity,
                speedChange * Time.fixedDeltaTime);
        }

        private void RotateTowardsMovement()
        {
            if (moveInput.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float targetRotation = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg
                - 90f
                + modelForwardOffset;
            float nextRotation = Mathf.MoveTowardsAngle(
                boatRigidbody.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime);

            boatRigidbody.MoveRotation(nextRotation);
        }

        private void ConfigureRigidbody()
        {
            boatRigidbody.gravityScale = 0f;
            boatRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            boatRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void OnValidate()
        {
            maximumSpeed = Mathf.Max(0f, maximumSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
        }
    }
}
