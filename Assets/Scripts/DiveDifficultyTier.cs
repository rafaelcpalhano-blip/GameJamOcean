using System;
using UnityEngine;

namespace GameJamOcean.Diving
{
    [Serializable]
    public sealed class DiveDifficultyTier
    {
        [Min(0)] public int minimumPurchasedUpgrades;
        [Min(1)] public int initialEnemies;
        [Min(1)] public int totalEnemies;
        [Tooltip("Weights: AguaViva, PeixeEspada, Polvo, SereiaMaga, SereiaGuerreira. Normalized automatically.")]
        public float[] enemyWeights;
        public DiveDifficultyTier(int points, int initial, int total, params float[] weights)
        { minimumPurchasedUpgrades = points; initialEnemies = initial; totalEnemies = total; enemyWeights = weights; }
    }
}
