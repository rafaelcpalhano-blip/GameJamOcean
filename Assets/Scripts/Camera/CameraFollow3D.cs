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
        [SerializeField, Min(0f)] private float smoothTime = 0.25f;
        [SerializeField, Min(0f)] private float maximumSpeed = 40f;
        [SerializeField, Min(0f)] private float rotationSharpness = 12f;
        [SerializeField] private bool snapOnEnable = true;

        private Vector3 followVelocity;

        private void OnEnable()
        {
            followVelocity = Vector3.zero;
            if (snapOnEnable && target != null)
            {
                transform.position = target.position + offset;
                LookAtTarget(true);
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                target.position + offset,
                ref followVelocity,
                smoothTime,
                maximumSpeed,
                Time.deltaTime);
            LookAtTarget(false);
        }

        public void Configure(Transform followTarget, Vector3 followOffset)
        {
            target = followTarget;
            offset = followOffset;
        }

        private void LookAtTarget(bool immediately)
        {
            Vector3 direction = target.position + lookAtOffset - transform.position;
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = immediately
                ? desiredRotation
                : Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
        }
    }
}
