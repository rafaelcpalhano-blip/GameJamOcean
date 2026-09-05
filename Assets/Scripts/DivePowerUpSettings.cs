using UnityEngine;

namespace GameJamOcean.Diving
{
    public enum DivePowerUpKind { Health, DoubleHarpoon, Shield, Speed }

    [CreateAssetMenu(menuName = "GameJamOcean/Dive Power Up Settings")]
    public sealed class DivePowerUpSettings : ScriptableObject
    {
        [Header("Sprites")]
        public Sprite[] doubleHarpoonSprites;
        public Sprite shieldSprite;
        public Sprite speedSprite;

        [Header("Double harpoon")]
        [Min(.1f)] public float doubleHarpoonDuration = 12f;
        [Range(1f, 60f)] public float doubleHarpoonAngle = 18f;

        [Header("Shield")]
        [Min(.1f)] public float shieldDuration = 5f;

        [Header("Speed")]
        [Min(.1f)] public float speedDuration = 7f;
        [Min(1f)] public float speedMultiplier = 1.5f;

        [Header("Presentation")]
        [Range(.1f, 2f)] public float pickupScale = .9f;
    }
}
