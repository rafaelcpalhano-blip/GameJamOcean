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
        [Tooltip("Auto recognizes the existing prefab names. Otherwise select the explicit type.")]
        [SerializeField] private EnemySpecies species;
        [SerializeField] private EnemyController2D prefab;
        [SerializeField, Min(0.01f)] private float weight = 1f;
        [SerializeField, Min(0)] private int minimumDifficultyLevel;

        public EnemyController2D Prefab => prefab;
        public float Weight => Mathf.Max(0.01f, weight);
        public int SpeciesIndex
        {
            get
            {
                if (species != EnemySpecies.Auto) return (int)species - 1;
                string n = prefab != null ? prefab.name.ToLowerInvariant().Replace(" ", "") : "";
                if (n.Contains("aguaviva")) return 0;
                if (n.Contains("peixeespada")) return 1;
                if (n.Contains("polvo")) return 2;
                if (n.Contains("sereiamaga")) return 3;
                if (n.Contains("sereiaguerreira")) return 4;
                return -1;
            }
        }
        public bool IsAvailableAt(int difficultyLevel)
        {
            return prefab != null && difficultyLevel >= minimumDifficultyLevel;
        }
    }

    public enum EnemySpecies { Auto, AguaViva, PeixeEspada, Polvo, SereiaMaga, SereiaGuerreira }

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
        [Header("Adaptive Pressure")]
        [SerializeField, Min(0.1f)] private float killWindowSeconds = 5f;
        [SerializeField, Min(1)] private int fastKillThreshold = 3;
        [SerializeField, Min(0.1f)] private float fastSpawnInterval = 0.75f;
        [SerializeField, Min(0f)] private float spawnPointScatterRadius = 3f;
        private readonly Queue<float> recentKills = new();
        private readonly List<EnemySpawnEntry> spawnPlan = new();
        private readonly List<Vector3> reservedPositions = new();
        private readonly List<EnemyController2D> liveSpawned = new();
        private int planIndex;
        private int lastKilled;
        private int initialTarget;
        private bool sessionPrepared;
        private float lastSpawnTime;

        [Header("Legacy Simultaneous Limit (without upgrade difficulty)")]
        [SerializeField, Min(1)] private int baseMaximumAlive = 3;
        [SerializeField, Min(0)] private int additionalAlivePerDifficulty = 1;

        [Header("Runtime")]
        [SerializeField] private int currentDifficulty;
        [SerializeField] private int currentMaximumAlive;
        [SerializeField] private float currentSpawnInterval;
        [SerializeField] private float nextSpawnTime;

        [Header("Events")]
        [SerializeField] private UnityEvent<GameObject> onEnemySpawned;

        private readonly List<EnemySpawnEntry> availableEnemies = new();
        private bool warnedAboutConfiguration;

        private void Awake()
        {
            FindReferencesIfNeeded();
        }

        private void Start()
        {
            if (sessionManager != null) sessionManager.SessionStarted += PrepareSession;
            if (sessionManager != null && sessionManager.SessionState == DiveSessionState.Running) PrepareSession();
        }

        private void OnDestroy()
        { if (sessionManager != null) sessionManager.SessionStarted -= PrepareSession; }

        private void PrepareSession()
        {
            if (sessionPrepared) return;
            RefreshDifficultySettings();
            var tier = sessionManager.ActiveDifficulty;
            initialTarget = tier != null ? Mathf.Clamp(tier.initialEnemies, 1, sessionManager.TotalEnemies) : 0;
            spawnPlan.Clear(); planIndex = 0; recentKills.Clear(); lastKilled = sessionManager.KilledEnemies;
            if (tier != null)
            {
                // Fail visibly rather than silently changing the requested species distribution.
                if (tier.enemyWeights == null || tier.enemyWeights.Length != 5)
                { Debug.LogError("Dive difficulty needs five enemy weights.", this); enabled = false; return; }
                int[] quotas;
                try { quotas = DiveDifficultyRules.Allocate(sessionManager.RemainingEnemiesToSpawn, tier.enemyWeights); }
                catch (ArgumentException error) { Debug.LogError(error.Message, this); enabled = false; return; }
                for (int type = 0; type < 5; type++)
                {
                    var entry = availableEnemies.Find(e => e.SpeciesIndex == type);
                    if (quotas[type] > 0 && entry == null)
                    { Debug.LogError($"Missing enemy prefab for {(EnemySpecies)(type + 1)}.", this); enabled = false; return; }
                    for (int i = 0; i < quotas[type]; i++) spawnPlan.Add(entry);
                }
                for (int i = spawnPlan.Count - 1; i > 0; i--)
                { int j = UnityEngine.Random.Range(0, i + 1); (spawnPlan[i], spawnPlan[j]) = (spawnPlan[j], spawnPlan[i]); }
            }
            sessionPrepared = true;
            SpawnInitialEnemies();
            lastSpawnTime = Time.time;
            nextSpawnTime = Time.time + (tier != null ? baseSpawnInterval : initialDelay);
        }

        private void SpawnInitialEnemies()
        {
            reservedPositions.Clear();
            int consecutiveFailures = 0;
            int maximumFailures = Mathf.Max(10, initialTarget * 2);
            while (sessionManager.CanSpawnEnemy && sessionManager.SpawnedEnemies < initialTarget)
            {
                if (TrySpawnEnemy()) consecutiveFailures = 0;
                else if (++consecutiveFailures >= maximumFailures) break;
            }
        }

        private void Update()
        {
            if (!sessionPrepared || Time.timeScale <= 0f) return;
            if (sessionManager == null)
            {
                FindReferencesIfNeeded();
                return;
            }

            if (!sessionManager.CanSpawnEnemy || sessionManager.AliveEnemies >= currentMaximumAlive)
            {
                return;
            }

            while (lastKilled < sessionManager.KilledEnemies) { recentKills.Enqueue(Time.time); lastKilled++; }
            while (recentKills.Count > 0 && Time.time - recentKills.Peek() > killWindowSeconds) recentKills.Dequeue();
            if (sessionManager.ActiveDifficulty != null)
            {
                currentSpawnInterval = recentKills.Count >= fastKillThreshold
                    ? Mathf.Max(0.1f, Mathf.Min(baseSpawnInterval, fastSpawnInterval)) : baseSpawnInterval;
                nextSpawnTime = lastSpawnTime + currentSpawnInterval;
            }

            if (Time.time < nextSpawnTime)
            {
                return;
            }

            reservedPositions.Clear();
            if (sessionManager.SpawnedEnemies < initialTarget)
            {
                SpawnInitialEnemies();
                lastSpawnTime = Time.time;
                nextSpawnTime = Time.time + currentSpawnInterval;
                return;
            }
            if (TrySpawnEnemy())
            {
                lastSpawnTime = Time.time;
                nextSpawnTime = Time.time + currentSpawnInterval;
            }
            else
            {
                lastSpawnTime = Time.time - currentSpawnInterval + Mathf.Min(0.5f, currentSpawnInterval);
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
            if (sessionManager != null && sessionManager.ActiveDifficulty != null)
            { currentMaximumAlive = sessionManager.TotalEnemies; currentSpawnInterval = baseSpawnInterval; }

            RebuildAvailableEnemyList();
        }

        public bool TrySpawnEnemy()
        {
            if (sessionManager == null || !sessionManager.CanSpawnEnemy)
            {
                return false;
            }

            if (!TryGetSpawnPosition(out Vector3 spawnPosition) || availableEnemies.Count == 0)
            {
                WarnAboutInvalidConfiguration();
                return false;
            }

            EnemySpawnEntry entry = sessionManager.ActiveDifficulty != null
                ? (planIndex < spawnPlan.Count ? spawnPlan[planIndex] : null) : ChooseWeightedEnemy();
            if (entry == null)
            {
                return false;
            }

            EnemyController2D enemy = Instantiate(
                entry.Prefab,
                spawnPosition,
                Quaternion.identity,
                spawnedEnemiesParent);

            if (!sessionManager.RegisterEnemy(enemy))
            {
                Destroy(enemy.gameObject);
                return false;
            }

            planIndex++;
            reservedPositions.Add(spawnPosition);
            liveSpawned.Add(enemy);
            onEnemySpawned?.Invoke(enemy.gameObject);
            warnedAboutConfiguration = false;
            return true;
        }

        private bool TryGetSpawnPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (spawnPoints.Count == 0) return false;
            liveSpawned.RemoveAll(e => e == null);
            for (int attempt = 0; attempt < 150; attempt++)
            {
                Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Count)];
                if (point == null) continue;
                position = point.position + (Vector3)(UnityEngine.Random.insideUnitCircle * spawnPointScatterRadius);
                if (!IsOutsideCamera(position) || (player != null && Vector2.Distance(position, player.position) < minimumPlayerDistance)) continue;
                Vector3 candidate = position;
                bool occupied = reservedPositions.Exists(p => Vector2.Distance(p, candidate) < occupancyCheckRadius * 2)
                    || liveSpawned.Exists(e => Vector2.Distance(e.transform.position, candidate) < occupancyCheckRadius * 2);
                if (occupied) continue;
                if (occupancyLayers.value != 0 && Physics2D.OverlapCircle(position, occupancyCheckRadius, occupancyLayers) != null) continue;
                return true;
            }
            return false;
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
