using System;
using System.Collections.Generic;
using GameJamOcean.Diving;
using GameJamOcean.Enemies;
using GameJamOcean.Player;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Spawning
{
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [SerializeField] private EnemyController2D prefab;
        [SerializeField, Min(0.01f)] private float weight = 1f;
        [SerializeField, Min(0)] private int minimumDifficultyLevel;

        public EnemyController2D Prefab => prefab;
        public float Weight => Mathf.Max(0.01f, weight);
        public bool IsAvailableAt(int difficultyLevel)
        {
            return prefab != null && difficultyLevel >= minimumDifficultyLevel;
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnemySpawner2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DiveSessionManager sessionManager;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private Transform player;
        [SerializeField] private Transform spawnedEnemiesParent;

        [Header("Enemy Pool")]
        [SerializeField] private List<EnemySpawnEntry> enemies = new();

        [Header("Spawn Points")]
        [SerializeField] private List<Transform> spawnPoints = new();
        [SerializeField, Min(0f)] private float offscreenViewportPadding = 0.08f;
        [SerializeField, Min(0f)] private float minimumPlayerDistance = 4f;
        [SerializeField, Min(0f)] private float occupancyCheckRadius = 0.5f;
        [SerializeField] private LayerMask occupancyLayers;

        [Header("Spawn Timing")]
        [SerializeField, Min(0f)] private float initialDelay = 1f;
        [SerializeField, Min(0.05f)] private float baseSpawnInterval = 2f;
        [SerializeField, Min(0f)] private float intervalReductionPerDifficulty = 0.1f;
        [SerializeField, Min(0.05f)] private float minimumSpawnInterval = 0.4f;

        [Header("Simultaneous Enemies")]
        [SerializeField, Min(1)] private int baseMaximumAlive = 3;
        [SerializeField, Min(0)] private int additionalAlivePerDifficulty = 1;

        [Header("Runtime")]
        [SerializeField] private int currentDifficulty;
        [SerializeField] private int currentMaximumAlive;
        [SerializeField] private float currentSpawnInterval;
        [SerializeField] private float nextSpawnTime;

        [Header("Events")]
        [SerializeField] private UnityEvent<GameObject> onEnemySpawned;

        private readonly List<Transform> validSpawnPoints = new();
        private readonly List<EnemySpawnEntry> availableEnemies = new();
        private bool warnedAboutConfiguration;

        private void Awake()
        {
            FindReferencesIfNeeded();
        }

        private void Start()
        {
            RefreshDifficultySettings();
            nextSpawnTime = Time.time + initialDelay;
        }

        private void Update()
        {
            if (sessionManager == null)
            {
                FindReferencesIfNeeded();
                return;
            }

            if (!sessionManager.CanSpawnEnemy || sessionManager.AliveEnemies >= currentMaximumAlive)
            {
                return;
            }

            if (Time.time < nextSpawnTime)
            {
                return;
            }

            if (TrySpawnEnemy())
            {
                nextSpawnTime = Time.time + currentSpawnInterval;
            }
            else
            {
                nextSpawnTime = Time.time + Mathf.Min(0.5f, currentSpawnInterval);
            }
        }

        public void RefreshDifficultySettings()
        {
            currentDifficulty = sessionManager != null
                ? sessionManager.DifficultyLevel
                : 0;
            currentMaximumAlive = Mathf.Max(
                1,
                baseMaximumAlive + currentDifficulty * additionalAlivePerDifficulty);
            currentSpawnInterval = Mathf.Max(
                minimumSpawnInterval,
                baseSpawnInterval - currentDifficulty * intervalReductionPerDifficulty);

            RebuildAvailableEnemyList();
        }

        public bool TrySpawnEnemy()
        {
            if (sessionManager == null || !sessionManager.CanSpawnEnemy)
            {
                return false;
            }

            RebuildValidSpawnPointList();
            if (validSpawnPoints.Count == 0 || availableEnemies.Count == 0)
            {
                WarnAboutInvalidConfiguration();
                return false;
            }

            Transform spawnPoint = validSpawnPoints[UnityEngine.Random.Range(0, validSpawnPoints.Count)];
            EnemySpawnEntry entry = ChooseWeightedEnemy();
            if (entry == null)
            {
                return false;
            }

            EnemyController2D enemy = Instantiate(
                entry.Prefab,
                spawnPoint.position,
                Quaternion.identity,
                spawnedEnemiesParent);

            if (!sessionManager.RegisterEnemy(enemy))
            {
                Destroy(enemy.gameObject);
                return false;
            }

            onEnemySpawned?.Invoke(enemy.gameObject);
            warnedAboutConfiguration = false;
            return true;
        }

        private void FindReferencesIfNeeded()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }

            if (player == null)
            {
                DiverController diver = FindFirstObjectByType<DiverController>();
                if (diver != null)
                {
                    player = diver.transform;
                }
            }
        }

        private void RebuildAvailableEnemyList()
        {
            availableEnemies.Clear();

            foreach (EnemySpawnEntry entry in enemies)
            {
                if (entry != null && entry.IsAvailableAt(currentDifficulty))
                {
                    availableEnemies.Add(entry);
                }
            }
        }

        private void RebuildValidSpawnPointList()
        {
            validSpawnPoints.Clear();

            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint == null || !IsOutsideCamera(spawnPoint.position))
                {
                    continue;
                }

                if (player != null && Vector2.Distance(spawnPoint.position, player.position) < minimumPlayerDistance)
                {
                    continue;
                }

                if (occupancyLayers.value != 0 && Physics2D.OverlapCircle(
                        spawnPoint.position,
                        occupancyCheckRadius,
                        occupancyLayers) != null)
                {
                    continue;
                }

                validSpawnPoints.Add(spawnPoint);
            }
        }

        private bool IsOutsideCamera(Vector3 worldPosition)
        {
            if (gameplayCamera == null)
            {
                return false;
            }

            Vector3 viewportPosition = gameplayCamera.WorldToViewportPoint(worldPosition);
            if (viewportPosition.z <= 0f)
            {
                return true;
            }

            return viewportPosition.x < -offscreenViewportPadding
                || viewportPosition.x > 1f + offscreenViewportPadding
                || viewportPosition.y < -offscreenViewportPadding
                || viewportPosition.y > 1f + offscreenViewportPadding;
        }

        private EnemySpawnEntry ChooseWeightedEnemy()
        {
            float totalWeight = 0f;
            foreach (EnemySpawnEntry entry in availableEnemies)
            {
                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float choice = UnityEngine.Random.value * totalWeight;
            foreach (EnemySpawnEntry entry in availableEnemies)
            {
                choice -= entry.Weight;
                if (choice <= 0f)
                {
                    return entry;
                }
            }

            return availableEnemies[^1];
        }

        private void WarnAboutInvalidConfiguration()
        {
            if (warnedAboutConfiguration)
            {
                return;
            }

            warnedAboutConfiguration = true;
            Debug.LogWarning(
                $"{nameof(EnemySpawner2D)} on '{name}' has no available enemy or valid offscreen spawn point.",
                this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.25f, 0.25f, 0.9f);

            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint != null)
                {
                    Gizmos.DrawWireSphere(spawnPoint.position, occupancyCheckRadius);
                }
            }
        }

        private void OnValidate()
        {
            offscreenViewportPadding = Mathf.Max(0f, offscreenViewportPadding);
            minimumPlayerDistance = Mathf.Max(0f, minimumPlayerDistance);
            occupancyCheckRadius = Mathf.Max(0f, occupancyCheckRadius);
            initialDelay = Mathf.Max(0f, initialDelay);
            baseSpawnInterval = Mathf.Max(0.05f, baseSpawnInterval);
            intervalReductionPerDifficulty = Mathf.Max(0f, intervalReductionPerDifficulty);
            minimumSpawnInterval = Mathf.Max(0.05f, minimumSpawnInterval);
            baseMaximumAlive = Mathf.Max(1, baseMaximumAlive);
            additionalAlivePerDifficulty = Mathf.Max(0, additionalAlivePerDifficulty);
        }
    }
}
