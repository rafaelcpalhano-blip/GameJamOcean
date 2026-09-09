using System.Collections.Generic;
using GameJamOcean.Interaction;
using UnityEngine;

namespace GameJamOcean.Spawning
{
    [DisallowMultipleComponent]
    public sealed class SpawnedDivePoint3D : MonoBehaviour
    {
        private static readonly List<SpawnedDivePoint3D> activePoints = new();
        [SerializeField] private int spawnPointIndex = -1;
        private bool tornadoCaptured;

        public int SpawnPointIndex => spawnPointIndex;
        public bool IsAvailableForTornado => !tornadoCaptured && isActiveAndEnabled;
        public static IReadOnlyList<SpawnedDivePoint3D> ActivePoints => activePoints;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => activePoints.Clear();

        private void OnEnable()
        {
            if (!activePoints.Contains(this)) activePoints.Add(this);
        }

        private void OnDisable() => activePoints.Remove(this);

        public void Configure(int index)
        {
            spawnPointIndex = index;
        }

        public void NotifySelected()
        {
            if (spawnPointIndex >= 0)
            {
                DivePointSpawnManager3D.MarkDivePointUsed(spawnPointIndex);
            }
        }

        public bool TryBeginTornadoCapture()
        {
            if (tornadoCaptured || spawnPointIndex < 0) return false;
            tornadoCaptured = true;
            DivePointInteractable3D interaction = GetComponent<DivePointInteractable3D>();
            if (interaction != null) interaction.enabled = false;
            foreach (Collider targetCollider in GetComponentsInChildren<Collider>(true))
                targetCollider.enabled = false;
            return true;
        }

        public void NotifyDestroyedByTornado()
        {
            DivePointSpawnManager3D manager = FindFirstObjectByType<DivePointSpawnManager3D>();
            if (manager != null) manager.ReplaceDivePointDestroyedByTornado(spawnPointIndex, gameObject);
            else Destroy(gameObject);
        }
    }
}
