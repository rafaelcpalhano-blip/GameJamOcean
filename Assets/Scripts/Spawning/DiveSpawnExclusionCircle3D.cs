using UnityEngine;

namespace GameJamOcean.Spawning
{
    [DisallowMultipleComponent]
    public sealed class DiveSpawnExclusionCircle3D : MonoBehaviour
    {
        [Header("Island Exclusion")]
        [Tooltip("No dive buoy spawn point can be generated inside this circle.")]
        [SerializeField, Min(0f)] private float radius = 36f;

        public float Radius => radius;

        public bool ContainsXZ(Vector3 worldPosition)
        {
            Vector2 center = new(transform.position.x, transform.position.z);
            Vector2 point = new(worldPosition.x, worldPosition.z);
            return Vector2.SqrMagnitude(point - center) <= radius * radius;
        }

        public void Configure(Vector3 center, float newRadius)
        {
            transform.position = center;
            radius = Mathf.Max(0f, newRadius);
        }

        private void OnValidate()
        {
            radius = Mathf.Max(0f, radius);
        }

        private void OnDrawGizmos()
        {
            const int segments = 64;
            Vector3 center = transform.position;
            Vector3 previous = center + Vector3.right * radius;

            Gizmos.color = new Color(1f, 0.65f, 0.05f, 0.95f);
            for (int index = 1; index <= segments; index++)
            {
                float angle = index * Mathf.PI * 2f / segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }

            Gizmos.DrawLine(center + Vector3.left, center + Vector3.right);
            Gizmos.DrawLine(center + Vector3.back, center + Vector3.forward);
        }
    }
}
