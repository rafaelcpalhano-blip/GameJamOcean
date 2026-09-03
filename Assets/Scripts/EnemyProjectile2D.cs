using GameJamOcean.Combat;
using UnityEngine;

namespace GameJamOcean.Weapons
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class EnemyProjectile2D : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField, Min(0f)] private float speed = 5f;
        [SerializeField, Min(0.1f)] private float lifetime = 5f;
        [SerializeField] private bool destroyOnEnvironmentHit = true;

        private Rigidbody2D projectileRigidbody;
        private CircleCollider2D projectileCollider;
        private Health targetHealth;
        private GameObject owner;
        private float damage;
        private bool hasHit;

        private void Awake()
        {
            projectileRigidbody = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<CircleCollider2D>();
            projectileCollider.isTrigger = true;
            projectileRigidbody.gravityScale = 0f;
            projectileRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            projectileRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        public void Launch(
            Vector2 direction,
            Health intendedTarget,
            GameObject projectileOwner,
            float projectileDamage)
        {
            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.left;
            }

            direction.Normalize();
            targetHealth = intendedTarget;
            owner = projectileOwner;
            damage = Mathf.Max(0f, projectileDamage);
            transform.right = direction;
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

            Health hitHealth = other.GetComponentInParent<Health>();
            if (hitHealth != null && hitHealth == targetHealth && !hitHealth.IsDead)
            {
                hasHit = true;
                hitHealth.TakeDamage(damage, owner);
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

            foreach (Collider2D ownerCollider in owner.GetComponentsInChildren<Collider2D>())
            {
                Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
            }
        }

        private bool IsOwnerCollider(Collider2D other)
        {
            return owner != null && other.transform.root == owner.transform.root;
        }

        private void OnValidate()
        {
            speed = Mathf.Max(0f, speed);
            lifetime = Mathf.Max(0.1f, lifetime);
        }
    }
}
