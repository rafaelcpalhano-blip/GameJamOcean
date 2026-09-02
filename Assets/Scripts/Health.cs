using System;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Combat
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maximumHealth = 5f;
        [SerializeField, Min(0f)] private float currentHealth;
        [SerializeField] private bool restoreOnEnable = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onDamaged;
        [SerializeField] private UnityEvent onDied;

        public event Action<Health, GameObject> Damaged;
        public event Action<Health, GameObject> Died;

        public float CurrentHealth => currentHealth;
        public float MaximumHealth => maximumHealth;
        public bool IsDead { get; private set; }

        private void Awake()
        {
            Restore();
        }

        private void OnEnable()
        {
            if (restoreOnEnable)
            {
                Restore();
            }
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            onDamaged?.Invoke();
            Damaged?.Invoke(this, source);

            if (currentHealth > 0f)
            {
                return;
            }

            IsDead = true;
            onDied?.Invoke();
            Died?.Invoke(this, source);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Min(maximumHealth, currentHealth + amount);
        }

        public void Restore()
        {
            currentHealth = maximumHealth;
            IsDead = false;
        }

        private void OnValidate()
        {
            maximumHealth = Mathf.Max(1f, maximumHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0f, maximumHealth);
        }
    }
}
