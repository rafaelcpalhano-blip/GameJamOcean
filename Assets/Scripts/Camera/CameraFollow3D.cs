using UnityEngine;

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

        [Header("Follow Heading")]
        [SerializeField] private bool followTargetYaw = true;
        [SerializeField, Min(0.01f)] private float headingSmoothTime = 1.1f;
        [SerializeField, Min(1f)] private float maximumHeadingSpeed = 45f;

        [Header("Subtle Water Motion")]
        [SerializeField] private bool enableWaterMotion = true;
        [SerializeField, Range(0f, 1f)] private float waterMotionIntensity = 1f;
        [SerializeField, Range(0f, 0.1f)] private float bobHeight = 0.015f;
        [SerializeField, Range(0f, 0.5f)] private float pitchDegrees = 0.08f;
        [SerializeField, Range(0.05f, 0.5f)] private float motionFrequency = 0.18f;

        private Vector3 followVelocity;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float heading;
        private float headingVelocity;

        private void OnEnable()
        {
            followVelocity = Vector3.zero;
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
            if (target == null)
            {
                return;
            }

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
            transform.SetPositionAndRotation(
                basePosition + Vector3.up * (wave * bobHeight * intensity),
                baseRotation * Quaternion.Euler(wave * pitchDegrees * intensity, 0f, 0f));
        }

        private Vector3 GetRotatedOffset()
        {
            return followTargetYaw
                ? Quaternion.Euler(0f, heading, 0f) * offset
                : offset;
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
