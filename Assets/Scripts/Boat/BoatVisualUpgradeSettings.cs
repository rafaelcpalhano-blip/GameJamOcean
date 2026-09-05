using UnityEngine;

namespace GameJamOcean.Boat
{
    [CreateAssetMenu(menuName = "GameJamOcean/Boat Visual Upgrade Settings")]
    public sealed class BoatVisualUpgradeSettings : ScriptableObject
    {
        public GameObject boat1Prefab;
        public GameObject boat2Prefab;
        public GameObject boat3Prefab;

        public bool IsValid => boat1Prefab != null && boat2Prefab != null && boat3Prefab != null;
        public GameObject[] Prefabs => new[] { boat1Prefab, boat2Prefab, boat3Prefab };
    }
}
