using UnityEngine;

namespace GameJamOcean.CameraSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector2 offset = new(0f, 1.25f);

        [Header("Follow")]
        [SerializeField, Min(0f)] private float smoothTime = 0.2f;
        [SerializeField, Min(0f)] private float maximumSpeed = 30f;
        [SerializeField] private bool snapOnEnable = true;

        private Vector3 followVelocity;
        private float cameraDepth;

        private void Awake()
        {
            cameraDepth = transform.position.z;
        }

        private void OnEnable()
        {
            followVelocity = Vector3.zero;

            if (snapOnEnable && target != null)
            {
                transform.position = GetDesiredPosition();
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desiredPosition = GetDesiredPosition();
            if (smoothTime <= Mathf.Epsilon)
            {
                transform.position = desiredPosition;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref followVelocity,
                smoothTime,
                maximumSpeed,
                Time.deltaTime);
        }

        private Vector3 GetDesiredPosition()
        {
            return new Vector3(
                target.position.x + offset.x,
                target.position.y + offset.y,
                cameraDepth);
        }

        private void OnValidate()
        {
            smoothTime = Mathf.Max(0f, smoothTime);
            maximumSpeed = Mathf.Max(0f, maximumSpeed);
        }
    }
}
