using UnityEngine;

namespace GameJamOcean.Boat
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoatWaterBounds3D : MonoBehaviour
    {
        [SerializeField] private Renderer waterRenderer;
        [SerializeField, Min(0f)] private float edgePadding = 5f;

        private Rigidbody boatRigidbody;

        public Bounds NavigableBounds
        {
            get
            {
                if (waterRenderer == null)
                {
                    return default;
                }

                Bounds bounds = waterRenderer.bounds;
                bounds.Expand(new Vector3(-edgePadding * 2f, 0f, -edgePadding * 2f));
                return bounds;
            }
        }

        private void Awake()
        {
            boatRigidbody = GetComponent<Rigidbody>();
            FindWaterIfNeeded();
        }

        private void FixedUpdate()
        {
            if (waterRenderer == null || boatRigidbody == null || boatRigidbody.isKinematic
                || GameJamOcean.UI.GameMenus.BlocksGameplay)
            {
                return;
            }

            Bounds bounds = NavigableBounds;
            Vector3 position = boatRigidbody.position;
            Vector3 clampedPosition = position;
            clampedPosition.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            clampedPosition.z = Mathf.Clamp(position.z, bounds.min.z, bounds.max.z);

            Vector3 velocity = boatRigidbody.linearVelocity;
            if (!Mathf.Approximately(position.x, clampedPosition.x))
            {
                velocity.x = 0f;
            }

            if (!Mathf.Approximately(position.z, clampedPosition.z))
            {
                velocity.z = 0f;
            }

            boatRigidbody.position = clampedPosition;
            boatRigidbody.linearVelocity = velocity;
        }

        public void Configure(Renderer targetWater, float padding)
        {
            waterRenderer = targetWater;
            edgePadding = Mathf.Max(0f, padding);
        }

        private void FindWaterIfNeeded()
        {
            if (waterRenderer != null)
            {
                return;
            }

            GameObject water = GameObject.Find("Water");
            if (water != null)
            {
                waterRenderer = water.GetComponentInChildren<Renderer>();
            }
        }

        private void OnValidate()
        {
            edgePadding = Mathf.Max(0f, edgePadding);
        }

        private void OnDrawGizmosSelected()
        {
            FindWaterIfNeeded();
            if (waterRenderer == null)
            {
                return;
            }

            Bounds bounds = NavigableBounds;
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireCube(bounds.center, new Vector3(bounds.size.x, 0.1f, bounds.size.z));
        }
    }
}
