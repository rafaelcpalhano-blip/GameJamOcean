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
        [SerializeField] private bool percentageOfMaximumHealth;
        private BoatDamageObstacle3D sharedClock;

        public void ConfigureRock(BoatDamageObstacle3D clock)
        {
            damage = 30f;
            percentageOfMaximumHealth = true;
            sharedClock = clock;
        }

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
            var clock = sharedClock != null ? sharedClock : this;
            if (GameJamOcean.UI.GameMenus.BlocksGameplay || damage <= 0f || Time.time < clock.nextAllowedDamageTime)
            {
                return;
            }

            BoatController3D boat = other.GetComponentInParent<BoatController3D>();
            if (boat == null || !boat.TryGetComponent(out Health boatHealth) || boatHealth.IsDead)
            {
                return;
            }

            clock.nextAllowedDamageTime = Time.time + damageCooldown;
            boatHealth.TakeDamage(percentageOfMaximumHealth ? boatHealth.MaximumHealth * damage / 100f : damage, gameObject);
        }

        private void OnValidate()
        {
            damage = Mathf.Max(0f, damage);
            damageCooldown = Mathf.Max(0f, damageCooldown);
        }
    }
}
