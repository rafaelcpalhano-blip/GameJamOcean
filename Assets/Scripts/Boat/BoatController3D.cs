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
        [SerializeField] private InputActionReference turboAction;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float maximumSpeed = 6f;
        [SerializeField, Min(0f)] private float acceleration = 2.5f;
        [Tooltip("Braking strength when pressing the opposite throttle direction.")]
        [SerializeField, Min(0f)] private float deceleration = 4f;
        [SerializeField, Min(0f)] private float rotationSpeed = 35f;

        [Header("Water Inertia and Steering")]
        [SerializeField, Min(0f)] private float coastDeceleration = 0.65f;
        [SerializeField, Min(0f)] private float lateralDrag = 1.2f;
        [Tooltip("Extra lateral grip without changing forward coasting. 1 keeps the original drift.")]
        [SerializeField, Range(1f, 6f)] private float lateralGripMultiplier = 3f;
        [SerializeField, Range(0.1f, 1f)] private float reverseSpeedRatio = 0.35f;
        [SerializeField, Min(0.01f)] private float steeringSmoothTime = 0.65f;
        [SerializeField, Min(0.1f)] private float fullSteeringSpeed = 1.5f;

        [Header("Turbo")]
        [SerializeField, Min(0.1f)] private float turboCapacity = 3f;
        [SerializeField, Min(1f)] private float turboSpeedMultiplier = 1.75f;
        [SerializeField, Min(1f)] private float turboAccelerationMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float turboRechargePerSecond = 0.5f;
        [SerializeField, Min(0f)] private float turboRechargeDelay = 1.25f;

        [Header("Turbo Runtime")]
        [SerializeField] private float currentTurboCharge;
        [SerializeField] private bool turboActive;

        [Header("Scenery Impact Audio")]
        [SerializeField, Min(0f)] private float collisionAudioMinimumSpeed = .5f;
        [SerializeField, Min(0f)] private float collisionAudioCooldown = .4f;

        [Header("Solid Collision Response")]
        [Tooltip("Fraction of the incoming speed used for the short impact recoil.")]
        [SerializeField, Range(0f, .5f)] private float collisionRecoil = .12f;
        [SerializeField, Min(0f)] private float maximumCollisionRecoilSpeed = .45f;
        [SerializeField, Range(0f, 1f)] private float collisionTangentialRetention = .9f;
        [SerializeField, Min(0f)] private float collisionSlideAssistDuration = .35f;

        [Header("Model Orientation")]
        [SerializeField] private float modelForwardOffset;

        private Rigidbody boatRigidbody;
        private Vector2 moveInput;
        private float smoothedSteering;
        private float steeringVelocity;
        private bool enabledMoveAction;
        private bool enabledTurboAction;
        private InputAction activeTurboAction;
        private InputAction fallbackTurboAction;
        private float lastTurboUseTime = float.NegativeInfinity;
        private Vector3 lastDrivenVelocity;
        private Quaternion lastDrivenRotation;
        private float nextCollisionAudioTime;
        private GameJamOcean.CameraSystem.CameraFollow3D cameraFollow;
        private Vector3 collisionSlideNormal;
        private float collisionSlideUntil;
        private float nextCollisionResponseTime;

        public float MaximumSpeed => maximumSpeed;
        public Vector3 DockPosition { get; private set; }
        public Quaternion DockRotation { get; private set; }
        public Vector3 NavigationForward => transform.rotation
            * Quaternion.Euler(0f, -modelForwardOffset, 0f) * Vector3.forward;
        public float Acceleration => acceleration;
        public float CurrentSpeed
        {
            get
            {
                if (boatRigidbody == null)
                {
                    return 0f;
                }

                Vector3 velocity = boatRigidbody.linearVelocity;
                return new Vector2(velocity.x, velocity.z).magnitude;
            }
        }
        public float SpeedPercentage => maximumSpeed > 0f
            ? Mathf.Clamp01(CurrentSpeed / maximumSpeed)
            : 0f;
        public float TurboPercentage => turboCapacity > 0f
            ? Mathf.Clamp01(currentTurboCharge / turboCapacity)
            : 0f;
        public bool IsTurboActive => turboActive;

        private void Awake()
        {
            DockPosition = transform.position;
            DockRotation = transform.rotation;
            boatRigidbody = GetComponent<Rigidbody>();
            cameraFollow = FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>();
            ConfigureRigidbody();
            currentTurboCharge = turboCapacity;
            OceanReturnState3D.TryRestore(transform, boatRigidbody);
            lastDrivenVelocity = boatRigidbody.linearVelocity;
            lastDrivenRotation = boatRigidbody.rotation;
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

            activeTurboAction = turboAction != null
                ? turboAction.action
                : CreateFallbackTurboAction();
            enabledTurboAction = !activeTurboAction.enabled;
            if (enabledTurboAction)
            {
                activeTurboAction.Enable();
            }
        }

        private void OnDisable()
        {
            moveInput = Vector2.zero;
            smoothedSteering = 0f;
            steeringVelocity = 0f;
            turboActive = false;
            cameraFollow?.SetTurboMicroshake(false);
            GameJamOcean.Audio.GameAudio.Instance?.StopBoatTurbo();
            GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineState(false, false);
            if (boatRigidbody != null && !boatRigidbody.isKinematic)
            {
                Vector3 velocity = boatRigidbody.linearVelocity;
                boatRigidbody.linearVelocity = new Vector3(0f, velocity.y, 0f);
            }

            if (enabledMoveAction && moveAction != null)
            {
                moveAction.action.Disable();
            }

            if (enabledTurboAction && activeTurboAction != null)
            {
                activeTurboAction.Disable();
            }

            enabledMoveAction = false;
            enabledTurboAction = false;
            activeTurboAction = null;
        }

        private void OnDestroy()
        {
            fallbackTurboAction?.Dispose();
        }

        private void Update()
        {
            if (GameJamOcean.UI.GameMenus.BlocksGameplay)
            {
                if (turboActive)
                {
                    turboActive = false;
                    GameJamOcean.Audio.GameAudio.Instance?.StopBoatTurbo();
                }
                GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineState(false, false);
                cameraFollow?.SetTurboMicroshake(false);
                return;
            }
            moveInput = moveAction.action.ReadValue<Vector2>();
            // Throttle and rudder are independent axes; W+D must not reduce engine power.
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                bool forward = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
                bool backward = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                if (forward || backward) moveInput.y = (forward ? 1f : 0f) - (backward ? 1f : 0f);
                if (left || right) moveInput.x = (right ? 1f : 0f) - (left ? 1f : 0f);
            }
            moveInput.x = Mathf.Clamp(moveInput.x, -1f, 1f);
            moveInput.y = Mathf.Clamp(moveInput.y, -1f, 1f);
            UpdateTurbo();
            cameraFollow?.SetTurboMicroshake(turboActive);
            GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineState(moveInput.y > .05f, turboActive);
        }

        private void FixedUpdate()
        {
            if (GameJamOcean.UI.GameMenus.BlocksGameplay || boatRigidbody.isKinematic) return;
            // Steering owns yaw; collision torque must not keep turning the hull.
            boatRigidbody.angularVelocity = Vector3.zero;
            float deltaTime = Time.fixedDeltaTime;
            Vector3 forward = boatRigidbody.rotation
                * Quaternion.Euler(0f, -modelForwardOffset, 0f) * Vector3.forward;
            float targetMaximumSpeed = turboActive
                ? maximumSpeed * turboSpeedMultiplier
                : maximumSpeed;

            Vector3 currentVelocity = boatRigidbody.linearVelocity;
            Vector3 horizontalVelocity = new(currentVelocity.x, 0f, currentVelocity.z);
            float forwardSpeed = Vector3.Dot(horizontalVelocity, forward);
            Vector3 sidewaysVelocity = horizontalVelocity - forward * forwardSpeed;
            float activeAcceleration = turboActive
                ? acceleration * turboAccelerationMultiplier
                : acceleration;

            if (moveInput.y > 0.05f)
            {
                float targetSpeed = targetMaximumSpeed * moveInput.y;
                // Stop reverse motion before engaging forward thrust.
                float rate = forwardSpeed < -0.05f ? deceleration
                    : forwardSpeed > targetSpeed ? coastDeceleration : activeAcceleration;
                forwardSpeed = Mathf.MoveTowards(forwardSpeed,
                    forwardSpeed < -0.05f ? 0f : targetSpeed, rate * deltaTime);
            }
            else if (moveInput.y < -0.05f)
            {
                // S first brakes; holding it after stopping engages a slower reverse.
                bool braking = forwardSpeed > 0.05f;
                forwardSpeed = Mathf.MoveTowards(forwardSpeed,
                    braking ? 0f : maximumSpeed * reverseSpeedRatio * moveInput.y,
                    (braking ? deceleration : acceleration * 0.45f) * deltaTime);
            }
            else
            {
                forwardSpeed = Mathf.MoveTowards(forwardSpeed, 0f, coastDeceleration * deltaTime);
            }

            sidewaysVelocity *= Mathf.Exp(-lateralDrag * lateralGripMultiplier * deltaTime);
            horizontalVelocity = forward * forwardSpeed + sidewaysVelocity;
            if (Time.time < collisionSlideUntil)
            {
                float velocityIntoSurface = Vector3.Dot(horizontalVelocity, collisionSlideNormal);
                if (velocityIntoSurface < 0f)
                    horizontalVelocity -= collisionSlideNormal * velocityIntoSurface;
            }

            boatRigidbody.linearVelocity = new Vector3(
                horizontalVelocity.x,
                currentVelocity.y,
                horizontalVelocity.z);

            smoothedSteering = Mathf.SmoothDamp(smoothedSteering, moveInput.x,
                ref steeringVelocity, steeringSmoothTime, Mathf.Infinity, deltaTime);
            float steeringAuthority = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / fullSteeringSpeed);
            float reverseDirection = forwardSpeed < -0.05f ? -1f : 1f;
            float yawStep = smoothedSteering * rotationSpeed * GameJamOcean.UI.GameMenus.SteeringMultiplier * steeringAuthority
                * reverseDirection * deltaTime;
            Quaternion drivenRotation = boatRigidbody.rotation * Quaternion.Euler(0f, yawStep, 0f);
            boatRigidbody.MoveRotation(drivenRotation);
            lastDrivenVelocity = boatRigidbody.linearVelocity;
            lastDrivenRotation = drivenRotation;
        }

        public void RejectCollisionRecoil()
        {
            if (boatRigidbody == null || boatRigidbody.isKinematic) return;
            float vertical = boatRigidbody.linearVelocity.y;
            boatRigidbody.linearVelocity = new Vector3(lastDrivenVelocity.x, vertical, lastDrivenVelocity.z);
            boatRigidbody.angularVelocity = Vector3.zero;
            boatRigidbody.MoveRotation(lastDrivenRotation);
        }

        public void ConfigureInput(
            InputActionReference movementAction,
            InputActionReference boostAction = null)
        {
            moveAction = movementAction;
            turboAction = boostAction;
        }

        public void ConfigureMovement(float newMaximumSpeed, float newAcceleration)
        {
            maximumSpeed = Mathf.Max(0f, newMaximumSpeed);
            acceleration = Mathf.Max(0f, newAcceleration);
        }

        public void ConfigureTurbo(
            float newCapacity,
            float newSpeedMultiplier,
            float newAccelerationMultiplier,
            float newRechargePerSecond,
            float newRechargeDelay,
            bool refillToFull = false)
        {
            float previousPercentage = TurboPercentage;
            turboCapacity = Mathf.Max(0.1f, newCapacity);
            turboSpeedMultiplier = Mathf.Max(1f, newSpeedMultiplier);
            turboAccelerationMultiplier = Mathf.Max(1f, newAccelerationMultiplier);
            turboRechargePerSecond = Mathf.Max(0f, newRechargePerSecond);
            turboRechargeDelay = Mathf.Max(0f, newRechargeDelay);
            currentTurboCharge = refillToFull
                ? turboCapacity
                : turboCapacity * previousPercentage;
        }

        public void RefillTurbo()
        {
            currentTurboCharge = turboCapacity;
        }

        private void UpdateTurbo()
        {
            Vector3 forward = boatRigidbody.rotation
                * Quaternion.Euler(0f, -modelForwardOffset, 0f) * Vector3.forward;
            bool hasMovementInput = moveInput.y > 0.05f
                && Vector3.Dot(boatRigidbody.linearVelocity, forward) >= -0.05f;
            bool turboPressed = activeTurboAction != null && activeTurboAction.IsPressed();
            if (Keyboard.current != null)
            {
                turboPressed |= Keyboard.current.leftShiftKey.isPressed
                    || Keyboard.current.rightShiftKey.isPressed;
            }

            bool wantsTurbo = turboPressed && hasMovementInput;
            bool wasTurboActive = turboActive;
            turboActive = wantsTurbo && currentTurboCharge > 0f;
            if (turboActive && !wasTurboActive)
                GameJamOcean.Audio.GameAudio.Instance?.StartBoatTurbo();
            else if (!turboActive && wasTurboActive)
                GameJamOcean.Audio.GameAudio.Instance?.StopBoatTurbo();
            if (turboActive)
            {
                currentTurboCharge = Mathf.Max(0f, currentTurboCharge - Time.deltaTime);
                lastTurboUseTime = Time.time;
                if (currentTurboCharge <= 0f)
                {
                    turboActive = false;
                    GameJamOcean.Audio.GameAudio.Instance?.StopBoatTurbo();
                }
                return;
            }

            if (wantsTurbo)
            {
                return;
            }

            if (Time.time >= lastTurboUseTime + turboRechargeDelay)
            {
                currentTurboCharge = Mathf.Min(
                    turboCapacity,
                    currentTurboCharge + turboRechargePerSecond * Time.deltaTime);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsIslandScenery(collision.transform)) return;
            ResolveSolidCollision(collision);
            if (Time.time < nextCollisionAudioTime
                || collision.relativeVelocity.magnitude < collisionAudioMinimumSpeed) return;
            nextCollisionAudioTime = Time.time + collisionAudioCooldown;
            GameJamOcean.Audio.GameAudio.Instance?.PlayBoatCollision();
            // Rocks already trigger this feedback through BoatDamageObstacle3D.
            if (IsRock(collision.transform)) return;

            Vector3 push = collision.contactCount > 0
                ? collision.GetContact(0).normal : -collision.relativeVelocity.normalized;
            push.y = 0f;
            if (push.sqrMagnitude < .001f) push = -NavigationForward;
            push.Normalize();
            FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>()?.PlayCollisionImpact(push);
            GetComponent<BoatWaterMotion3D>()?.PlayCollisionImpact(push);
        }

        private void ResolveSolidCollision(Collision collision)
        {
            if (boatRigidbody == null || boatRigidbody.isKinematic || collision.contactCount == 0
                || Time.time < nextCollisionResponseTime) return;

            Vector3 incoming = lastDrivenVelocity;
            incoming.y = 0f;
            Vector3 normal = collision.GetContact(0).normal;
            normal.y = 0f;
            if (normal.sqrMagnitude < .001f)
                normal = transform.position - collision.transform.position;
            normal.y = 0f;
            if (normal.sqrMagnitude < .001f || incoming.sqrMagnitude < .001f) return;
            normal.Normalize();
            // Always orient the normal away from the surface relative to the incoming motion.
            if (Vector3.Dot(incoming, normal) > 0f) normal = -normal;
            float incomingNormalSpeed = Mathf.Max(0f, -Vector3.Dot(incoming, normal));
            if (incomingNormalSpeed <= .01f) return;

            Vector3 tangent = Vector3.ProjectOnPlane(incoming, normal)
                * collisionTangentialRetention;
            float recoilSpeed = Mathf.Min(maximumCollisionRecoilSpeed,
                incomingNormalSpeed * collisionRecoil);
            Vector3 resolved = tangent + normal * recoilSpeed;
            float vertical = boatRigidbody.linearVelocity.y;
            boatRigidbody.linearVelocity = new Vector3(resolved.x, vertical, resolved.z);
            lastDrivenVelocity = boatRigidbody.linearVelocity;
            collisionSlideNormal = normal;
            collisionSlideUntil = Time.time + collisionSlideAssistDuration;
            nextCollisionResponseTime = Time.time + .08f;
        }

        private static bool IsRock(Transform candidate)
        {
            for (Transform current = candidate; current != null; current = current.parent)
                if (current.name.Contains("rock", System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static bool IsIslandScenery(Transform candidate)
        {
            for (Transform current = candidate; current != null; current = current.parent)
            {
                string objectName = current.name.ToLowerInvariant();
                if (objectName.Contains("rock") || objectName.Contains("terrain")
                    || objectName.Contains("mainisland") || objectName.Contains("aldeia")
                    || objectName.Contains("pier")) return true;
            }
            return false;
        }

        private InputAction CreateFallbackTurboAction()
        {
            if (fallbackTurboAction != null)
            {
                return fallbackTurboAction;
            }

            fallbackTurboAction = new InputAction("Turbo", InputActionType.Button);
            fallbackTurboAction.AddBinding("<Keyboard>/leftShift");
            fallbackTurboAction.AddBinding("<Keyboard>/rightShift");
            return fallbackTurboAction;
        }

        public void ConfigureRelaxedHandling()
        {
            acceleration = 2.5f;
            deceleration = 4f;
            rotationSpeed = 35f;
            coastDeceleration = 0.65f;
            lateralDrag = 1.2f;
            steeringSmoothTime = 0.65f;
            reverseSpeedRatio = 0.35f;
            fullSteeringSpeed = 1.5f;
        }

        private void ConfigureRigidbody()
        {
            boatRigidbody.useGravity = false;
            boatRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            boatRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            boatRigidbody.constraints = RigidbodyConstraints.FreezePositionY
                | RigidbodyConstraints.FreezeRotationX
                | RigidbodyConstraints.FreezeRotationZ
                | RigidbodyConstraints.FreezeRotationY;
        }

        private void OnValidate()
        {
            maximumSpeed = Mathf.Max(0f, maximumSpeed);
            acceleration = Mathf.Max(0f, acceleration);
            deceleration = Mathf.Max(0f, deceleration);
            rotationSpeed = Mathf.Max(0f, rotationSpeed);
            coastDeceleration = Mathf.Max(0f, coastDeceleration);
            lateralDrag = Mathf.Max(0f, lateralDrag);
            steeringSmoothTime = Mathf.Max(0.01f, steeringSmoothTime);
            fullSteeringSpeed = Mathf.Max(0.1f, fullSteeringSpeed);
            turboCapacity = Mathf.Max(0.1f, turboCapacity);
            turboSpeedMultiplier = Mathf.Max(1f, turboSpeedMultiplier);
            turboAccelerationMultiplier = Mathf.Max(1f, turboAccelerationMultiplier);
            turboRechargePerSecond = Mathf.Max(0f, turboRechargePerSecond);
            turboRechargeDelay = Mathf.Max(0f, turboRechargeDelay);
            currentTurboCharge = Mathf.Clamp(currentTurboCharge, 0f, turboCapacity);
            collisionRecoil = Mathf.Clamp(collisionRecoil, 0f, .5f);
            maximumCollisionRecoilSpeed = Mathf.Max(0f, maximumCollisionRecoilSpeed);
            collisionTangentialRetention = Mathf.Clamp01(collisionTangentialRetention);
            collisionSlideAssistDuration = Mathf.Max(0f, collisionSlideAssistDuration);
        }
    }
}
