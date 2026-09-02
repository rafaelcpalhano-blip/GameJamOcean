using UnityEngine;

namespace GameJamOcean.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount, GameObject source);
    }
}
