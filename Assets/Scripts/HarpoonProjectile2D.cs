using GameJamOcean.Combat;
using System.Collections.Generic;
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
        [SerializeField, Range(.1f, 1f)] private float flightSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float damage = 1f;
        [SerializeField, Min(0.1f)] private float lifetime = 3f;
        [SerializeField, Range(.05f, .5f)] private float fadeDuration = .2f;
        [SerializeField] private LayerMask hittableLayers = ~0;
        [SerializeField] private bool destroyOnEnvironmentHit = true;

        private Rigidbody2D projectileRigidbody;
        private Collider2D projectileCollider;
        private GameObject owner;
        private bool hasHit;
        private float destroyAt;
        private SpriteRenderer[] visuals;
        private Color[] originalColors;
        private static readonly List<HarpoonProjectile2D> ActiveProjectiles = new();
        public static IReadOnlyList<HarpoonProjectile2D> Active => ActiveProjectiles;
        public Vector2 Position => projectileRigidbody != null ? projectileRigidbody.position : transform.position;
        public Vector2 Velocity => projectileRigidbody != null ? projectileRigidbody.linearVelocity : Vector2.zero;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActive() => ActiveProjectiles.Clear();

        private void OnEnable()
        {
            if (!ActiveProjectiles.Contains(this)) ActiveProjectiles.Add(this);
        }

        private void OnDisable() => ActiveProjectiles.Remove(this);

        private void Awake()
        {
            projectileRigidbody = GetComponent<Rigidbody2D>();
            projectileCollider = GetComponent<Collider2D>();
            projectileCollider.isTrigger = true;
            projectileRigidbody.gravityScale = 0f;
            projectileRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            visuals = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[visuals.Length];
            for (int i = 0; i < visuals.Length; i++) originalColors[i] = visuals[i].color;
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
            projectileRigidbody.linearVelocity = direction * speed * flightSpeedMultiplier;

            IgnoreOwnerColliders();
            destroyAt = Time.time + lifetime;
        }

        private void Update()
        {
            if (destroyAt <= 0f) return;
            float remaining = destroyAt - Time.time;
            float alpha = Mathf.Clamp01(remaining / Mathf.Min(fadeDuration, lifetime));
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] == null) continue;
                Color color = originalColors[i];
                color.a *= alpha;
                visuals[i].color = color;
            }
            // Collider and velocity remain active throughout the fade.
            if (remaining <= 0f) Destroy(gameObject);
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
