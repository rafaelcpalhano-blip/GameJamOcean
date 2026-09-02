using UnityEngine;

namespace GameJamOcean.World
{
    [DisallowMultipleComponent]
    public sealed class MovementBounds2D : MonoBehaviour
    {
        [Header("Area")]
        [SerializeField] private Vector2 center;
        [SerializeField] private Vector2 size = new(20f, 12f);

        public Bounds Bounds => new(
            (Vector2)transform.position + center,
            size);

        public Vector2 ClampPoint(Vector2 point, Vector2 objectExtents)
        {
            Bounds bounds = Bounds;

            float minimumX = bounds.min.x + objectExtents.x;
            float maximumX = bounds.max.x - objectExtents.x;
            float minimumY = bounds.min.y + objectExtents.y;
            float maximumY = bounds.max.y - objectExtents.y;

            if (minimumX > maximumX)
            {
                point.x = bounds.center.x;
            }
            else
            {
                point.x = Mathf.Clamp(point.x, minimumX, maximumX);
            }

            if (minimumY > maximumY)
            {
                point.y = bounds.center.y;
            }
            else
            {
                point.y = Mathf.Clamp(point.y, minimumY, maximumY);
            }

            return point;
        }

        private void OnValidate()
        {
            size.x = Mathf.Max(0.1f, size.x);
            size.y = Mathf.Max(0.1f, size.y);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.75f, 1f);
            Gizmos.DrawWireCube(
                (Vector2)transform.position + center,
                size);
        }
    }
}