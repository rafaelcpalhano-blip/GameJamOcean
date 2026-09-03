using UnityEngine;

namespace GameJamOcean.Spawning
{
    [DisallowMultipleComponent]
    public sealed class SpawnedDivePoint3D : MonoBehaviour
    {
        [SerializeField] private int spawnPointIndex = -1;

        public int SpawnPointIndex => spawnPointIndex;

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
    }
}
