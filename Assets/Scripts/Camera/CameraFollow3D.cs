using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using GameJamOcean.Boat;

namespace GameJamOcean.CameraSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow3D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new(0f, 10.7f, -11.38f);
        [SerializeField] private Vector3 lookAtOffset = new(0f, 4f, 0f);
        [SerializeField, Min(0f)] private float rotationSharpness = 6f;
        [SerializeField] private bool snapOnEnable = true;

        [Header("Main Menu View (World Space)")]
        [Tooltip("Optional pose marker. When empty, the position and angles below are used.")]
        [SerializeField] private Transform menuViewPoint;
        [SerializeField] private Vector3 menuPosition = new(33.8f, 13.47f, -116.34f);
        [SerializeField] private Vector3 menuAngles = new(35.579f, 25.78f, 0f);
        [SerializeField, Min(0.1f)] private float menuTransitionSeconds = 4f;
        [Header("Rescue View")]
        [Tooltip("Move the menu camera closer to the rescued boat, preserving the menu view angles.")]
        [SerializeField, Range(0f, .35f)] private float rescueViewApproach = .12f;
        public float MenuTransitionSeconds => menuTransitionSeconds;
        private Vector3 introPosition;
        private Quaternion introRotation;
        private Coroutine menuReturn;
        private bool cinematicCamera;
        private Vector3 endGameCenter;
        private Vector3 endGameOrbitOffset;
        private float endGameOrbitAngle;
        private float endGameOrbitCurrentSpeed;
        [SerializeField, Min(.1f)] private float endGameOrbitDegreesPerSecond = 8f;
        [SerializeField, Min(.1f)] private float endGameOrbitAcceleration = 3f;
        public bool EndGameOrbitCompleted => endGameOrbitAngle >= 360f;

        public void ShowMenuView()
        {
            StopCinematicMotion();
            introPosition = menuViewPoint != null ? menuViewPoint.position : menuPosition;
            introRotation = menuViewPoint != null ? menuViewPoint.rotation : Quaternion.Euler(menuAngles);
            transform.SetPositionAndRotation(introPosition, introRotation);
        }

        public void ReturnFromMenuView(float durationMultiplier = 1f)
        {
            if (menuReturn != null) StopCoroutine(menuReturn);
            menuReturn = StartCoroutine(ReturnFromMenu(Mathf.Max(.1f,
                menuTransitionSeconds * Mathf.Max(.05f, durationMultiplier))));
        }

        private IEnumerator ReturnFromMenu(float duration)
        {
            cinematicCamera = true;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                EvaluateMenuTransition(elapsed / duration);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            EvaluateMenuTransition(1f);
            cinematicCamera = false;
            menuReturn = null;
        }

        public void BeginEndGameOrbit(Vector3 center)
        {
            StopCinematicMotion();
            cinematicCamera = true;
            endGameCenter = center;
            Vector3 menu = menuViewPoint != null ? menuViewPoint.position : menuPosition;
            endGameOrbitOffset = menu - center;
            endGameOrbitAngle = 0f;
            endGameOrbitCurrentSpeed = 0f;
            UpdateEndGameOrbit(0f);
        }

        public void EndEndGameOrbit()
        {
            cinematicCamera = false;
            introPosition = transform.position;
            introRotation = transform.rotation;
            ReturnFromMenuView(.5f);
        }

        private void StopCinematicMotion()
        {
            if (menuReturn != null) StopCoroutine(menuReturn);
            menuReturn = null;
            cinematicCamera = false;
        }

        private void UpdateEndGameOrbit(float deltaTime)
        {
            endGameOrbitCurrentSpeed = Mathf.MoveTowards(endGameOrbitCurrentSpeed,
                endGameOrbitDegreesPerSecond, endGameOrbitAcceleration * deltaTime);
            endGameOrbitAngle += endGameOrbitCurrentSpeed * deltaTime;
            Vector3 horizontal = Quaternion.Euler(0f, endGameOrbitAngle, 0f)
                * new Vector3(endGameOrbitOffset.x, 0f, endGameOrbitOffset.z);
            Vector3 position = endGameCenter + horizontal;
            position.y = endGameCenter.y + endGameOrbitOffset.y;
            Vector3 look = endGameCenter - position;
            transform.SetPositionAndRotation(position,
                look.sqrMagnitude > .001f ? Quaternion.LookRotation(look, Vector3.up) : transform.rotation);
        }

        public void ShowRescueView()
        {
            ShowMenuView();
            if (target != null)
                introPosition = Vector3.Lerp(introPosition, target.position, rescueViewApproach);
            // Keep this pose as the start of the later transition back behind the boat.
            transform.SetPositionAndRotation(introPosition, introRotation);
        }

        // Driven by the menu's unscaled clock while player control remains blocked.
        public void EvaluateMenuTransition(float progress)
        {
            if (target == null) return;
            float t = Mathf.Clamp01(progress);
            t = t * t * t * (t * (t * 6f - 15f) + 10f); // zero velocity/acceleration at both ends
            Vector3 endOffset = Quaternion.Euler(0, followTargetYaw ? target.eulerAngles.y : 0, 0)
                * GetZoomedOffset();
            Vector3 startOffset = introPosition - target.position;
            float startYaw = Mathf.Atan2(startOffset.x, startOffset.z) * Mathf.Rad2Deg;
            float endYaw = Mathf.Atan2(endOffset.x, endOffset.z) * Mathf.Rad2Deg;
            float yaw = Mathf.LerpAngle(startYaw, endYaw, t) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(new Vector2(startOffset.x, startOffset.z).magnitude,
                new Vector2(endOffset.x, endOffset.z).magnitude, t);
            Vector3 position = target.position + new Vector3(Mathf.Sin(yaw) * radius,
                Mathf.Lerp(startOffset.y, endOffset.y, t), Mathf.Cos(yaw) * radius);
            Vector3 endPosition = target.position + endOffset;
            Quaternion endRotation = Quaternion.LookRotation(target.position + lookAtOffset - endPosition, Vector3.up);
            transform.SetPositionAndRotation(position, Quaternion.Slerp(introRotation, endRotation, t));
            if (progress >= 1f)
            {
                basePosition = endPosition;
                baseRotation = endRotation;
                heading = target.eulerAngles.y;
                headingVelocity = desiredOrbitYaw = orbitYaw = orbitVelocity = orbitRoll = 0f;
                recenterTargetChosen = true;
                desiredLookElevation = lookElevation = elevationVelocity = 0f;
                draggingOrbit = false;
                transform.SetPositionAndRotation(basePosition, baseRotation);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Capture Scene View as Menu View")]
        private void CaptureMenuView()
        {
            var view = UnityEditor.SceneView.lastActiveSceneView;
            if (view == null || view.camera == null) return;
            UnityEditor.Undo.RecordObject(this, "Capture menu camera view");
            menuViewPoint = null;
            menuPosition = view.camera.transform.position;
            menuAngles = view.camera.transform.eulerAngles;
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        [Header("Follow Heading")]
        [SerializeField] private bool followTargetYaw = true;
        [SerializeField, Min(0.01f)] private float headingSmoothTime = 0.75f;
        [SerializeField, Min(1f)] private float maximumHeadingSpeed = 75f;

        [Header("Right Mouse Orbit (Boat)")]
        [SerializeField] private bool enableMouseOrbit = true;
        [Tooltip("Degrees per pixel of horizontal mouse movement.")]
        [SerializeField, Range(0.01f, 0.5f)] private float mouseOrbitSensitivity = 0.05f;
        [SerializeField, Min(0.01f)] private float orbitSmoothTime = 0.2f;
        [Tooltip("Smooth braking time after the mouse button is released, before automatic recentering begins.")]
        [SerializeField, Min(0.01f)] private float orbitReleaseSmoothTime = 0.3f;
        [SerializeField, Min(0.01f)] private float orbitReturnTime = 2.5f;
        [SerializeField, Min(0f)] private float orbitReturnDelay = 0.2f;
        [Tooltip("Very small panoramic bank toward the direction of camera rotation.")]
        [SerializeField, Range(0f, 3f)] private float maximumOrbitRoll = 0.8f;
        [SerializeField, Min(0.1f)] private float orbitRollSharpness = 5f;

        [Header("Mouse Wheel Zoom")]
        [Tooltip("Keeps the camera on a constant-radius orbit around the boat center and disables wheel zoom.")]
        [SerializeField] private bool lockOrbitDistance = true;
        [SerializeField, Min(.1f)] private float minimumZoomDistance = 10f;
        [SerializeField, Min(.1f)] private float maximumZoomDistance = 20f;
        [SerializeField, Min(.001f)] private float zoomSensitivity = .012f;

        [Header("Right Mouse Vertical Look")]
        [SerializeField, Range(0.01f, 0.3f)] private float verticalLookSensitivity = 0.06f;
        [SerializeField, Range(0f, 16f)] private float maximumLookUp = 9f;
        [SerializeField, Range(0f, 12f)] private float maximumLookDown = 4f;
        [Tooltip("Keeps the top of the view below the horizon when raising the view.")]
        [SerializeField, Range(1f, 10f)] private float horizonMargin = 3f;
        private float desiredLookElevation, lookElevation, elevationVelocity;
        private Camera viewCamera;

        private float desiredOrbitYaw;
        private float orbitYaw;
        private float orbitVelocity;
        private float orbitRoll;
        private float zoomDistance;
        private float orbitReleasedAt = float.NegativeInfinity;
        private bool draggingOrbit;
        private bool recenterTargetChosen = true;
        private bool applicationFocused = true;

        [Header("Subtle Water Motion")]
        [SerializeField] private bool enableWaterMotion = true;
        [SerializeField, Range(0f, 1f)] private float waterMotionIntensity = 1f;
        [SerializeField, Range(0f, 0.1f)] private float bobHeight = 0.015f;
        [SerializeField, Range(0f, 0.5f)] private float pitchDegrees = 0.08f;
        [SerializeField, Range(0.05f, 0.5f)] private float motionFrequency = 0.18f;

        [Header("Collision Impact")]
        [SerializeField, Min(.05f)] private float impactDuration = 1.1f;
        [SerializeField, Range(0f, 1.5f)] private float impactRecoilDistance = .9f;
        [SerializeField, Range(0f, .4f)] private float impactShakeDistance = .22f;
        [SerializeField, Range(0f, 4f)] private float impactRollDegrees = 1.8f;
        [SerializeField, Min(1f)] private float impactShakeFrequency = 5f;
        private float impactStartedAt = float.NegativeInfinity;
        private Vector3 impactDirection;

        [Header("Turbo Microshake")]
        [SerializeField, Range(0f, .08f)] private float turboShakeDistance = .012f;
        [SerializeField, Range(0f, 1f)] private float turboShakeRollDegrees = .1f;
        [SerializeField, Min(1f)] private float turboShakeFrequency = 17f;
        [SerializeField, Min(.1f)] private float turboShakeBlendSpeed = 7f;
        private bool turboShakeRequested;
        private float turboShakeWeight;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float heading;
        private float headingVelocity;

        private void OnEnable()
        {
            viewCamera = GetComponent<Camera>();
            desiredLookElevation = lookElevation = elevationVelocity = 0f;
            desiredOrbitYaw = orbitYaw = orbitVelocity = orbitRoll = 0f;
            zoomDistance = Mathf.Clamp(offset.magnitude, minimumZoomDistance, maximumZoomDistance);
            draggingOrbit = false;
            applicationFocused = Application.isFocused;
            headingVelocity = 0f;
            heading = target != null ? target.eulerAngles.y : 0f;
            basePosition = transform.position;
            baseRotation = transform.rotation;
            if (snapOnEnable && target != null)
            {
                basePosition = target.position + GetRotatedOffset();
                transform.position = basePosition;
                LookAtTarget(true);
                transform.rotation = baseRotation;
            }
        }

        private void LateUpdate()
        {
            turboShakeWeight = Mathf.MoveTowards(turboShakeWeight,
                turboShakeRequested ? 1f : 0f, turboShakeBlendSpeed * Time.unscaledDeltaTime);
            if (cinematicCamera)
            {
                if (menuReturn == null) UpdateEndGameOrbit(Time.unscaledDeltaTime);
                return;
            }
            if (GameJamOcean.UI.GameMenus.BlocksGameplay) return;
            if (target == null)
            {
                return;
            }

            UpdateMouseOrbit();
            heading = Mathf.SmoothDampAngle(
                heading, target.eulerAngles.y, ref headingVelocity,
                headingSmoothTime, maximumHeadingSpeed, Time.deltaTime);
            // The camera is mounted on the boat's central orbit: its radius never
            // changes while navigating, even as the boat or camera rotates.
            basePosition = target.position + GetRotatedOffset();
            LookAtTarget(lockOrbitDistance);
            // Apply motion after smoothing, never feed it back into the follow state.
            float intensity = enableWaterMotion ? waterMotionIntensity : 0f;
            float wave = Mathf.Sin(Time.time * motionFrequency * Mathf.PI * 2f);
            // Limit only the added upward look; do not change the user's neutral framing.
            Vector3 viewForward = baseRotation * Vector3.forward;
            float downwardAngle = Mathf.Atan2(-viewForward.y,
                new Vector2(viewForward.x, viewForward.z).magnitude) * Mathf.Rad2Deg;
            float safeLookUp = viewCamera != null && !viewCamera.orthographic
                ? Mathf.Max(0f, downwardAngle - viewCamera.fieldOfView * .5f - horizonMargin
                    - Mathf.Abs(pitchDegrees * intensity)) : maximumLookUp;
            float appliedElevation = Mathf.Clamp(lookElevation, -maximumLookDown,
                Mathf.Min(maximumLookUp, safeLookUp));
            float impactProgress = Mathf.Clamp01((Time.time - impactStartedAt) / impactDuration);
            float impactEnvelope = 1f - impactProgress;
            float recoilPulse = Mathf.Sin(Mathf.PI * Mathf.Sqrt(impactProgress)) * impactEnvelope;
            float shake = Mathf.Sin(impactProgress * impactShakeFrequency * Mathf.PI * 2f)
                * impactEnvelope * impactEnvelope;
            Vector3 impactOffset = impactDirection * (recoilPulse * impactRecoilDistance)
                + (baseRotation * Vector3.right + baseRotation * Vector3.up * .45f)
                    * (shake * impactShakeDistance);
            float turboPhase = Time.time * turboShakeFrequency * Mathf.PI * 2f;
            float turboHorizontal = Mathf.Sin(turboPhase) * turboShakeWeight;
            float turboVertical = Mathf.Sin(turboPhase * 1.37f + 1.1f) * turboShakeWeight;
            Vector3 turboOffset = (baseRotation * Vector3.right * turboHorizontal
                + baseRotation * Vector3.up * turboVertical) * turboShakeDistance;
            Vector3 feedbackPosition = lockOrbitDistance ? Vector3.zero
                : Vector3.up * (wave * bobHeight * intensity) + impactOffset + turboOffset;
            float feedbackPitch = lockOrbitDistance ? 0f : wave * pitchDegrees * intensity;
            float feedbackRoll = lockOrbitDistance ? 0f
                : shake * impactRollDegrees + turboHorizontal * turboShakeRollDegrees;
            transform.SetPositionAndRotation(basePosition + feedbackPosition,
                baseRotation * Quaternion.Euler(feedbackPitch - appliedElevation,
                    0f, orbitRoll + feedbackRoll));
        }

        public void SetTurboMicroshake(bool active)
        {
            turboShakeRequested = active;
        }

        public void PlayCollisionImpact(Vector3 worldPushDirection)
        {
            worldPushDirection.y = 0f;
            impactDirection = worldPushDirection.sqrMagnitude > .001f
                ? worldPushDirection.normalized : transform.forward;
            impactStartedAt = Time.time;
        }

        public void SnapBehindTarget()
        {
            if (target == null) return;
            heading = target.eulerAngles.y;
            headingVelocity = desiredOrbitYaw = orbitYaw = orbitVelocity = orbitRoll = 0f;
            recenterTargetChosen = true;
            desiredLookElevation = lookElevation = elevationVelocity = 0f;
            basePosition = target.position + GetRotatedOffset();
            LookAtTarget(true);
            transform.SetPositionAndRotation(basePosition, baseRotation);
        }

        private Vector3 GetRotatedOffset()
        {
            float yaw = (followTargetYaw ? heading : 0f) + orbitYaw;
            return Quaternion.Euler(0f, yaw, 0f) * GetZoomedOffset();
        }

        private Vector3 GetZoomedOffset()
        {
            if (lockOrbitDistance) return offset;
            return offset.sqrMagnitude > .001f ? offset.normalized * zoomDistance : offset;
        }

        private void UpdateMouseOrbit()
        {
            Mouse mouse = Mouse.current;
            bool canOrbit = enableMouseOrbit && applicationFocused && Time.timeScale > 0f
                && mouse != null && target.GetComponent<BoatController3D>() != null;
            bool wasDragging = draggingOrbit;
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (canOrbit && !pointerOverUi && !lockOrbitDistance)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .01f)
                {
                    zoomDistance = Mathf.Clamp(zoomDistance - scroll * zoomSensitivity,
                        minimumZoomDistance, maximumZoomDistance);
                }
            }
            if (!canOrbit || !mouse.rightButton.isPressed)
            {
                draggingOrbit = false;
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                draggingOrbit = !pointerOverUi;
                // Start from the current view if the player grabs it during recentering.
                if (draggingOrbit)
                {
                    desiredOrbitYaw = orbitYaw;
                    desiredLookElevation = lookElevation;
                    recenterTargetChosen = false;
                }
            }

            if (draggingOrbit)
            {
                if (!mouse.rightButton.wasPressedThisFrame)
                {
                    // Mouse delta already represents this frame's displacement, not a speed.
                    desiredOrbitYaw += mouse.delta.ReadValue().x * mouseOrbitSensitivity;
                    desiredLookElevation = Mathf.Clamp(desiredLookElevation
                        + mouse.delta.ReadValue().y * verticalLookSensitivity,
                        -maximumLookDown, maximumLookUp);
                }
            }
            else
            {
                if (wasDragging)
                {
                    orbitReleasedAt = Time.time;
                    recenterTargetChosen = false;
                }
                if (!recenterTargetChosen && Time.time - orbitReleasedAt >= orbitReturnDelay)
                {
                    // Zero degrees has an equivalent behind-the-boat angle every
                    // full revolution. Choose the nearest one, never unwind turns.
                    desiredOrbitYaw = orbitYaw + Mathf.DeltaAngle(orbitYaw, 0f);
                    desiredLookElevation = 0f;
                    recenterTargetChosen = true;
                }
            }

            bool waitingToRecenter = !draggingOrbit
                && !recenterTargetChosen;
            float yawSmoothTime = draggingOrbit ? orbitSmoothTime
                : waitingToRecenter ? orbitReleaseSmoothTime : orbitReturnTime;
            orbitYaw = Mathf.SmoothDamp(orbitYaw, desiredOrbitYaw, ref orbitVelocity,
                yawSmoothTime,
                Mathf.Infinity, Time.deltaTime);
            lookElevation = Mathf.SmoothDamp(lookElevation, desiredLookElevation, ref elevationVelocity,
                yawSmoothTime,
                Mathf.Infinity, Time.deltaTime);
            float desiredRoll = Mathf.Clamp(-orbitVelocity * .015f,
                -maximumOrbitRoll, maximumOrbitRoll);
            orbitRoll = Mathf.Lerp(orbitRoll, desiredRoll,
                1f - Mathf.Exp(-orbitRollSharpness * Time.deltaTime));
            if (!draggingOrbit && recenterTargetChosen
                && Mathf.Abs(Mathf.DeltaAngle(orbitYaw, 0f)) < .01f
                && Mathf.Abs(orbitVelocity) < .01f)
            {
                // Keep long play sessions numerically stable after multiple turns.
                desiredOrbitYaw = orbitYaw = orbitVelocity = 0f;
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused && draggingOrbit)
            {
                draggingOrbit = false;
                orbitReleasedAt = Time.time;
                recenterTargetChosen = false;
            }
        }

        private void OnDisable()
        {
            turboShakeRequested = false;
            turboShakeWeight = 0f;
            draggingOrbit = false;
            desiredOrbitYaw = 0f;
            orbitReleasedAt = float.NegativeInfinity;
            recenterTargetChosen = true;
            desiredLookElevation = lookElevation = elevationVelocity = orbitRoll = 0f;
        }

        public void ConfigureRelaxedFollow()
        {
            rotationSharpness = 6f;
            headingSmoothTime = 0.75f;
            maximumHeadingSpeed = 75f;
            mouseOrbitSensitivity = 0.05f;
            orbitSmoothTime = 0.2f;
            orbitReleaseSmoothTime = 0.3f;
            orbitReturnTime = 2.5f;
            orbitReturnDelay = 0.2f;
            maximumOrbitRoll = 0.8f;
            orbitRollSharpness = 5f;
            lockOrbitDistance = true;
        }

        public void ConfigureHeadingFollow(Transform followTarget)
        {
            target = followTarget;
            followTargetYaw = true;
        }

        public void Configure(Transform followTarget, Vector3 followOffset)
        {
            target = followTarget;
            offset = followOffset;
        }

        private void LookAtTarget(bool immediately)
        {
            Vector3 direction = target.position + lookAtOffset - basePosition;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
            baseRotation = immediately
                ? desiredRotation
                : Quaternion.Slerp(
                    baseRotation,
                    desiredRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }
    }
}
