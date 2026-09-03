using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJamOcean.Boat
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoatController3D : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference moveAction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float maximumSpeed = 6f;
        [SerializeField, Min(0f)] private float acceleration = 8f;
        [SerializeField, Min(0f)] private float deceleration = 12f;
        [SerializeField, Min(0f)] private float rotationSpeed = 240f;

        [Header("Model Orientation")]
        [SerializeField] private float modelForwardOffset;

        private Rigidbody boatRigidbody;
        private Vector2 moveInput;
        private bool enabledMoveAction;

        private void Awake()
        {
            boatRigidbody = GetComponent<Rigidbody>();
            ConfigureRigidbody();
            OceanReturnState3D.TryRestore(transform, boatRigidbody);
        }

        private void OnEnable()
        {
            if (moveAction == null)
            {
                Debug.LogError($"{nameof(BoatController3D)} on '{name}' needs a Move action.", this);
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
            if (boatRigidbody != null)
            {
                Vector3 velocity = boatRigidbody.linearVelocity;
                boatRigidbody.linearVelocity = new Vector3(0f, velocity.y, 0f);
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
        }

        private void FixedUpdate()
        {
            Vector3 desiredVelocity = new(moveInput.x, 0f, moveInput.y);
            desiredVelocity *= maximumSpeed;

            Vector3 currentVelocity = boatRigidbody.linearVelocity;
            Vector3 horizontalVelocity = new(currentVelocity.x, 0f, currentVelocity.z);
            float changeRate = moveInput.sqrMagnitude > 0.001f ? acceleration : deceleration;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity,
                desiredVelocity,
                changeRate * Time.fixedDeltaTime);

            boatRigidbody.linearVelocity = new Vector3(
                horizontalVelocity.x,
                currentVelocity.y,
                horizontalVelocity.z);

            RotateTowardsMovement();
        }

        public void ConfigureInput(InputActionReference action)
        {
            moveAction = action;
        }

        private void RotateTowardsMovement()
        {
            Vector3 direction = new(moveInput.x, 0f, moveInput.y);
            if (direction.sqrMagnitude <= 0.001f)
            {
                return;
            }

            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg
                + modelForwardOffset;
            Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);
            boatRigidbody.MoveRotation(Quaternion.RotateTowards(
                boatRigidbody.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime));
        }

        private void ConfigureRigidbody()
        {
            boatRigidbody.useGravity = false;
            boatRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            boatRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            boatRigidbody.constraints = RigidbodyConstraints.FreezePositionY
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ;
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
