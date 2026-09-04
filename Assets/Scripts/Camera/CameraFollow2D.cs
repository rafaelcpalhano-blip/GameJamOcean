using UnityEngine;
using GameJamOcean.World;

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

        [Header("Movement Bounds")]
        [SerializeField] private MovementBounds2D movementBounds;
        [SerializeField, Min(0f)] private float boundsPadding = 0.1f;

        private Vector3 followVelocity;
        private float cameraDepth;
        private Camera followCamera;

        private void Awake()
        {
            cameraDepth = transform.position.z;
            followCamera = GetComponent<Camera>();

            if (movementBounds == null)
            {
                movementBounds = FindFirstObjectByType<MovementBounds2D>();
            }
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
            if (GameJamOcean.UI.GameMenus.BlocksGameplay) return;
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
            Vector3 desiredPosition = new(
                target.position.x + offset.x,
                target.position.y + offset.y,
                cameraDepth);

            if (movementBounds == null || followCamera == null || !followCamera.orthographic)
            {
                return desiredPosition;
            }

            float halfHeight = followCamera.orthographicSize;
            float halfWidth = halfHeight * followCamera.aspect;
            Vector2 cameraExtents = new(
                halfWidth + boundsPadding,
                halfHeight + boundsPadding);
            Vector2 clampedPosition = movementBounds.ClampPoint(desiredPosition, cameraExtents);

            desiredPosition.x = clampedPosition.x;
            desiredPosition.y = clampedPosition.y;
            return desiredPosition;
        }

        private void OnValidate()
        {
            smoothTime = Mathf.Max(0f, smoothTime);
            maximumSpeed = Mathf.Max(0f, maximumSpeed);
            boundsPadding = Mathf.Max(0f, boundsPadding);
        }
    }
}
