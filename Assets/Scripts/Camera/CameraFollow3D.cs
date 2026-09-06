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
        [SerializeField] private Vector3 offset = new(0f, 12f, -10f);
        [SerializeField] private Vector3 lookAtOffset = new(0f, 0.5f, 0f);
        [SerializeField, Min(0f)] private float smoothTime = 0.5f;
        [SerializeField, Min(0f)] private float maximumSpeed = 40f;
        [SerializeField, Min(0f)] private float rotationSharpness = 3f;
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
        [SerializeField, Min(.1f)] private float endGameOrbitDegreesPerSecond = 5f;

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
            endGameOrbitAngle += endGameOrbitDegreesPerSecond * deltaTime;
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
                followVelocity = Vector3.zero;
                headingVelocity = desiredOrbitYaw = orbitYaw = orbitVelocity = 0f;
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
        [SerializeField, Min(0.01f)] private float headingSmoothTime = 1.1f;
        [SerializeField, Min(1f)] private float maximumHeadingSpeed = 45f;

        [Header("Right Mouse Orbit (Boat)")]
        [SerializeField] private bool enableMouseOrbit = true;
        [Tooltip("Degrees per pixel of horizontal mouse movement.")]
        [SerializeField, Range(0.01f, 0.5f)] private float mouseOrbitSensitivity = 0.12f;
        [SerializeField, Range(10f, 180f)] private float maximumOrbitAngle = 180f;
        [SerializeField, Min(0.01f)] private float orbitSmoothTime = 0.3f;
        [SerializeField, Min(0.01f)] private float orbitReturnTime = 1.5f;
        [SerializeField, Min(0f)] private float orbitReturnDelay = 1.5f;

        [Header("Mouse Wheel Zoom")]
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
        private float zoomDistance;
        private float orbitReleasedAt = float.NegativeInfinity;
        private bool draggingOrbit;
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

        private Vector3 followVelocity;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float heading;
        private float headingVelocity;

        private void OnEnable()
        {
            viewCamera = GetComponent<Camera>();
            desiredLookElevation = lookElevation = elevationVelocity = 0f;
            followVelocity = Vector3.zero;
            desiredOrbitYaw = orbitYaw = orbitVelocity = 0f;
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
            basePosition = Vector3.SmoothDamp(
                basePosition,
                target.position + GetRotatedOffset(),
                ref followVelocity,
                smoothTime,
                maximumSpeed,
                Time.deltaTime);
            LookAtTarget(false);
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
            transform.SetPositionAndRotation(
                basePosition + Vector3.up * (wave * bobHeight * intensity) + impactOffset,
                baseRotation * Quaternion.Euler(wave * pitchDegrees * intensity - appliedElevation,
                    0f, shake * impactRollDegrees));
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
            headingVelocity = desiredOrbitYaw = orbitYaw = orbitVelocity = 0f;
            desiredLookElevation = lookElevation = elevationVelocity = 0f;
            followVelocity = Vector3.zero;
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
            return offset.sqrMagnitude > .001f ? offset.normalized * zoomDistance : offset;
        }

        private void UpdateMouseOrbit()
        {
            Mouse mouse = Mouse.current;
            bool canOrbit = enableMouseOrbit && applicationFocused && Time.timeScale > 0f
                && mouse != null && target.GetComponent<BoatController3D>() != null;
            bool wasDragging = draggingOrbit;
            bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (canOrbit && !pointerOverUi)
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
                }
            }

            if (draggingOrbit)
            {
                if (!mouse.rightButton.wasPressedThisFrame)
                {
                    // Mouse delta already represents this frame's displacement, not a speed.
                    desiredOrbitYaw = Mathf.Clamp(desiredOrbitYaw
                        + mouse.delta.ReadValue().x * mouseOrbitSensitivity,
                        -maximumOrbitAngle, maximumOrbitAngle);
                    desiredLookElevation = Mathf.Clamp(desiredLookElevation
                        + mouse.delta.ReadValue().y * verticalLookSensitivity,
                        -maximumLookDown, maximumLookUp);
                }
            }
            else
            {
                if (wasDragging) orbitReleasedAt = Time.time;
                if (Time.time - orbitReleasedAt >= orbitReturnDelay)
                {
                    desiredOrbitYaw = 0f;
                    desiredLookElevation = 0f;
                }
            }

            orbitYaw = Mathf.SmoothDamp(orbitYaw, desiredOrbitYaw, ref orbitVelocity,
                draggingOrbit ? orbitSmoothTime : orbitReturnTime,
                Mathf.Infinity, Time.deltaTime);
            lookElevation = Mathf.SmoothDamp(lookElevation, desiredLookElevation, ref elevationVelocity,
                draggingOrbit ? orbitSmoothTime : orbitReturnTime,
                Mathf.Infinity, Time.deltaTime);
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused) draggingOrbit = false;
        }

        private void OnDisable()
        {
            draggingOrbit = false;
            desiredOrbitYaw = 0f;
            orbitReleasedAt = float.NegativeInfinity;
            desiredLookElevation = lookElevation = elevationVelocity = 0f;
        }

        public void ConfigureRelaxedFollow()
        {
            smoothTime = 0.5f;
            rotationSharpness = 3f;
            headingSmoothTime = 1.1f;
            maximumHeadingSpeed = 45f;
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
