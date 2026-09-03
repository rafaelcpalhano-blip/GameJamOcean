using GameJamOcean.Combat;
using UnityEngine;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    public sealed class BoatDamageObstacle3D : MonoBehaviour
    {
        [Header("Collision Damage")]
        [SerializeField, Min(0f)] private float damage = 10f;
        [SerializeField, Min(0f)] private float damageCooldown = 0.75f;

        private float nextAllowedDamageTime;

        private void OnCollisionEnter(Collision collision)
        {
            TryDamage(collision.collider);
        }

        private void OnTriggerEnter(Collider other)
        {
            TryDamage(other);
        }

        private void TryDamage(Collider other)
        {
            if (damage <= 0f || Time.time < nextAllowedDamageTime)
            {
                return;
            }

            BoatController3D boat = other.GetComponentInParent<BoatController3D>();
            if (boat == null || !boat.TryGetComponent(out Health boatHealth) || boatHealth.IsDead)
            {
                return;
            }

            nextAllowedDamageTime = Time.time + damageCooldown;
            boatHealth.TakeDamage(damage, gameObject);
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            damageCooldown = Mathf.Max(0f, damageCooldown);
        }
    }
}
