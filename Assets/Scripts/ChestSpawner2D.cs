using System.Collections.Generic;
using GameJamOcean.Diving;
using GameJamOcean.Rewards;
using GameJamOcean.World;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Spawning
{
    [DisallowMultipleComponent]
    public sealed class ChestSpawner2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DiveSessionManager sessionManager;
        [SerializeField] private MovementBounds2D diveArea;
        [SerializeField] private Transform spawnedChestsParent;

        [Header("Prefabs")]
        [SerializeField] private ChestRewardInteractable smallChestPrefab;
        [SerializeField] private ChestRewardInteractable finalChestPrefab;

        [Header("Small Chest Spawn Points")]
        [SerializeField] private List<Transform> smallChestSpawnPoints = new();
        [SerializeField] private bool avoidReusingPoints = true;

        [Header("Final Chest")]
        [SerializeField] private Transform finalChestSpawnPoint;

        [Header("Events")]
        [SerializeField] private UnityEvent<GameObject> onSmallChestSpawned;
        [SerializeField] private UnityEvent<GameObject> onFinalChestSpawned;

        [Header("Runtime")]
        [SerializeField] private int smallChestsSpawned;
        [SerializeField] private bool finalChestSpawned;

        private readonly List<int> availablePointIndices = new();

        private void Awake()
        {
            FindReferencesIfNeeded();
        }

        private void OnEnable()
        {
            FindReferencesIfNeeded();
            SubscribeToSession();
        }

        private void Start()
        {
            ResetSpawner();
        }

        private void OnDisable()
        {
            UnsubscribeFromSession();
        }

        private void SubscribeToSession()
        {
            if (sessionManager == null)
            {
                return;
            }

            sessionManager.SessionStarted -= ResetSpawner;
            sessionManager.ChestMilestoneReached -= HandleChestMilestoneReached;
            sessionManager.SessionStarted += ResetSpawner;
            sessionManager.ChestMilestoneReached += HandleChestMilestoneReached;
        }

        private void UnsubscribeFromSession()
        {
            if (sessionManager == null)
            {
                return;
            }

            sessionManager.SessionStarted -= ResetSpawner;
            sessionManager.ChestMilestoneReached -= HandleChestMilestoneReached;
        }

        private void HandleChestMilestoneReached(float milestone)
        {
            if (milestone >= 100f - Mathf.Epsilon)
            {
                SpawnFinalChest();
            }
            else
            {
                SpawnSmallChest();
            }
        }

        public void SpawnSmallChest()
        {
            if (smallChestPrefab == null)
            {
                Debug.LogWarning("Small chest prefab is not assigned.", this);
                return;
            }

            RebuildAvailablePointsIfNeeded();
            if (availablePointIndices.Count == 0)
            {
                Debug.LogWarning("There is no valid small chest spawn point assigned.", this);
                return;
            }

            int listIndex = Random.Range(0, availablePointIndices.Count);
            int pointIndex = availablePointIndices[listIndex];
            Transform spawnPoint = smallChestSpawnPoints[pointIndex];

            if (avoidReusingPoints)
            {
                availablePointIndices.RemoveAt(listIndex);
            }

            ChestRewardInteractable chest = Instantiate(
                smallChestPrefab,
                spawnPoint.position,
                Quaternion.identity,
                spawnedChestsParent);
            smallChestsSpawned++;
            onSmallChestSpawned?.Invoke(chest.gameObject);
        }

        public void SpawnFinalChest()
        {
            if (finalChestSpawned || finalChestPrefab == null)
            {
                return;
            }

            Vector3 spawnPosition;
            if (finalChestSpawnPoint != null)
            {
                spawnPosition = finalChestSpawnPoint.position;
            }
            else if (diveArea != null)
            {
                spawnPosition = diveArea.Bounds.center;
            }
            else
            {
                spawnPosition = Vector3.zero;
            }

            ChestRewardInteractable chest = Instantiate(
                finalChestPrefab,
                spawnPosition,
                Quaternion.identity,
                spawnedChestsParent);
            finalChestSpawned = true;
            onFinalChestSpawned?.Invoke(chest.gameObject);
        }

        public void ResetSpawner()
        {
            smallChestsSpawned = 0;
            finalChestSpawned = false;
            availablePointIndices.Clear();

            for (int index = 0; index < smallChestSpawnPoints.Count; index++)
            {
                if (smallChestSpawnPoints[index] != null)
                {
                    availablePointIndices.Add(index);
                }
            }
        }

        private void RebuildAvailablePointsIfNeeded()
        {
            if (availablePointIndices.Count > 0)
            {
                return;
            }

            if (avoidReusingPoints && smallChestsSpawned < smallChestSpawnPoints.Count)
            {
                return;
            }

            for (int index = 0; index < smallChestSpawnPoints.Count; index++)
            {
                if (smallChestSpawnPoints[index] != null)
                {
                    availablePointIndices.Add(index);
                }
            }
        }

        private void FindReferencesIfNeeded()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }

            if (diveArea == null)
            {
                diveArea = FindFirstObjectByType<MovementBounds2D>();
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.9f);
            foreach (Transform spawnPoint in smallChestSpawnPoints)
            {
                if (spawnPoint != null)
                {
                    Gizmos.DrawWireSphere(spawnPoint.position, 0.25f);
                }
            }

            Gizmos.color = new Color(1f, 0.3f, 0f, 1f);
            if (finalChestSpawnPoint != null)
            {
                Gizmos.DrawWireCube(finalChestSpawnPoint.position, Vector3.one * 0.5f);
            }
        }
    }
}
