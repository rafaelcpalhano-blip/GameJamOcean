using GameJamOcean.Combat;
using UnityEngine;

namespace GameJamOcean.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class HarpoonProjectile2D : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField, Min(0f)] private float speed = 12f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(0.1f)] private float lifetime = 3f;
        [SerializeField] private LayerMask hittableLayers = ~0;
        [SerializeField] private bool destroyOnEnvironmentHit = true;

        private Rigidbody2D projectileRigidbody;
        private Collider2D projectileCollider;
        private GameObject owner;
        private bool hasHit;

        private void Awake()
        {
            projectileRigidbody = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileCollider.isTrigger = true;
            projectileRigidbody.gravityScale = 0f;
            projectileRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public void Launch(Vector2 direction, GameObject projectileOwner)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            owner = projectileOwner;
            transform.up = direction;
            projectileRigidbody.linearVelocity = direction * speed;

            IgnoreOwnerColliders();
            Destroy(gameObject, lifetime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (hasHit || IsOwnerCollider(other))
            {
                return;
            }

            if ((hittableLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            IDamageable damageable = FindDamageable(other);
            if (damageable != null && !damageable.IsDead)
            {
                hasHit = true;
                damageable.TakeDamage(damage, owner);
                Destroy(gameObject);
                return;
            }

            if (destroyOnEnvironmentHit && !other.isTrigger)
            {
                hasHit = true;
                Destroy(gameObject);
            }
        }

        private void IgnoreOwnerColliders()
        {
            if (owner == null)
            {
                return;
            }

            Collider2D[] ownerColliders = owner.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D ownerCollider in ownerColliders)
            {
                Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }

        private bool IsOwnerCollider(Collider2D other)
        {
            return owner != null && other.transform.root == owner.transform.root;
        }

        private static IDamageable FindDamageable(Collider2D other)
        {
            MonoBehaviour[] behaviours = other.GetComponentsInParent<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IDamageable damageable)
                {
                    return damageable;
                }
            }

            return null;
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            damage = Mathf.Max(0f, damage);
            lifetime = Mathf.Max(0.1f, lifetime);
        }
    }
}
