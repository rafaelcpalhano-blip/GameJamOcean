using UnityEngine;

namespace GameJamOcean.Effects
{
    [DisallowMultipleComponent]
    public sealed class AutoDestroy2D : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float lifetime = 0.6f;

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        private void OnValidate()
        {
            lifetime = Mathf.Max(0f, lifetime);
        }
    }
}
