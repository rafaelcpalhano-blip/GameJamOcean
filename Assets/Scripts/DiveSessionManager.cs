using System;
using System.Collections.Generic;
using GameJamOcean.Combat;
using GameJamOcean.Enemies;
using GameJamOcean.Player;
using GameJamOcean.Progression;
using UnityEngine;
using UnityEngine.Events;
using GameJamOcean.UI;
using GameJamOcean.World;

namespace GameJamOcean.Diving
{
    public enum DiveSessionState
    {
        NotStarted,
        Running,
        AwaitingFinalChest,
        Won,
        Lost
    }

    [DisallowMultipleComponent]
    public sealed class DiveSessionManager : MonoBehaviour
    {
        [Header("Session Configuration")]
        [SerializeField, Min(1)] private int totalEnemies = 12;
        [SerializeField, Min(0)] private int difficultyLevel;
        [SerializeField] private bool beginAutomatically = true;
        [SerializeField] private bool registerEnemiesAlreadyInScene = true;
        [Header("Progression Difficulty (N1 to N5)")]
        [SerializeField] private bool useUpgradeDifficulty = true;
        [SerializeField] private DiveDifficultyTier[] difficultyTiers =
        {
            new(0, 10, 27, 40, 30, 20, 5, 5),
            new(3, 12, 30, 20, 30, 30, 10, 10),
            new(6, 14, 33, 10, 35, 25, 15, 15),
            new(9, 15, 37, 10, 25, 25, 25, 25),
            new(12, 16, 42, 15, 15, 10, 30, 30)
        };
        public DiveDifficultyTier ActiveDifficulty { get; private set; }

        [Header("Chest Milestones (%)")]
        [SerializeField] private List<float> chestMilestones = new() { 25f, 50f, 75f, 100f };

        [Header("Player")]
        [SerializeField] private Health playerHealth;

        [Header("Runtime - Enemy Progress")]
        [SerializeField] private int spawnedEnemies;
        [SerializeField] private int aliveEnemies;
        [SerializeField] private int killedEnemies;
        [SerializeField, Range(0f, 100f)] private float completionPercentage;

        [Header("Runtime - Rewards")]
        [SerializeField] private int releasedChests;
        [SerializeField] private int securedGold;
        [SerializeField] private int collectedCoinGold;
        [SerializeField] private int finalRewardGold;

        [Header("Runtime - State")]
        [SerializeField] private DiveSessionState sessionState = DiveSessionState.NotStarted;

        [Header("Events")]
        [SerializeField] private UnityEvent onSessionStarted;
        [SerializeField] private UnityEvent<int, int> onEnemyProgressChanged;
        [SerializeField] private UnityEvent<float> onCompletionPercentageChanged;
        [SerializeField] private UnityEvent<float> onChestMilestoneReached;
        [SerializeField] private UnityEvent<int> onSecuredGoldChanged;
        [SerializeField] private UnityEvent<int> onCollectedCoinGoldChanged;
        [SerializeField] private UnityEvent<int> onFinalRewardGoldChanged;
        [SerializeField] private UnityEvent onAllEnemiesDefeated;
        [SerializeField] private UnityEvent onVictory;
        [SerializeField] private UnityEvent onDefeat;

        public event Action<float> ChestMilestoneReached;
        public event Action<int> SecuredGoldChanged;
        public event Action<int> CollectedCoinGoldChanged;
        public event Action<float> CompletionPercentageChanged;
        public event Action SessionStarted;
        public event Action SessionWon;
        public event Action SessionLost;

        public int TotalEnemies => totalEnemies;
        public int DifficultyLevel => difficultyLevel;
        public int SpawnedEnemies => spawnedEnemies;
        public int AliveEnemies => aliveEnemies;
        public int KilledEnemies => killedEnemies;
        public int RemainingEnemiesToSpawn => Mathf.Max(0, totalEnemies - spawnedEnemies);
        public int ReleasedChests => releasedChests;
        public int SecuredGold => securedGold;
        public int CollectedCoinGold => collectedCoinGold;
        public int FinalRewardGold => finalRewardGold;
        public int OpenedSmallChests { get; private set; }
        public Sprite SmallChestIcon { get; private set; }
        public Sprite FinalChestIcon { get; private set; }
        public void RecordSmallChest(int gold, Sprite icon)
        {
            if (sessionState != DiveSessionState.Running && sessionState != DiveSessionState.AwaitingFinalChest) return;
            OpenedSmallChests++;
            SmallChestIcon = icon;
            AddSecuredGold(gold);
        }
        public void SetFinalChestIcon(Sprite icon) => FinalChestIcon = icon;
        public int TotalCollectedGold => securedGold + collectedCoinGold + finalRewardGold;
        public float CompletionPercentage => completionPercentage;
        public DiveSessionState SessionState => sessionState;
        public bool CanSpawnEnemy => sessionState == DiveSessionState.Running && spawnedEnemies < totalEnemies;

        public void SetDifficultyLevel(int value)
        {
            if (sessionState == DiveSessionState.Running)
            {
                Debug.LogWarning("Difficulty cannot be changed while a dive session is running.", this);
                return;
            }

            difficultyLevel = Mathf.Max(0, value);
        }

        private readonly HashSet<EnemyController2D> registeredEnemies = new();
        private bool[] releasedMilestones;
        private DivePowerUpSpawner2D powerUpSpawner;

        private void Awake()
        {
            FindPlayerHealthIfNeeded();
        }

        private void OnEnable()
        {
            SubscribeToPlayer();
        }

        private void Start()
        {
            if (beginAutomatically)
            {
                BeginSession();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromPlayer();
            UnsubscribeFromAllEnemies();
        }

        public void BeginSession()
        {
            if (sessionState != DiveSessionState.NotStarted) return;
            if (useUpgradeDifficulty && difficultyTiers != null && difficultyTiers.Length > 0)
            {
                int points = 0;
                if (GameProgress.HasInstance)
                    foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind))) points += GameProgress.Instance.GetLevel(kind) - 1;
                int[] thresholds = new int[difficultyTiers.Length];
                for (int i = 0; i < difficultyTiers.Length; i++)
                    thresholds[i] = difficultyTiers[i] != null ? difficultyTiers[i].minimumPurchasedUpgrades : -1;
                int selected = DiveDifficultyRules.SelectTier(points, thresholds);
                if (selected >= 0) { ActiveDifficulty = difficultyTiers[selected]; difficultyLevel = selected + 1; }
                if (ActiveDifficulty != null) totalEnemies = Mathf.Max(1, ActiveDifficulty.totalEnemies);
            }
            UnsubscribeFromAllEnemies();
            registeredEnemies.Clear();

            spawnedEnemies = 0;
            aliveEnemies = 0;
            killedEnemies = 0;
            completionPercentage = 0f;
            releasedChests = 0;
            securedGold = 0;
            collectedCoinGold = 0;
            finalRewardGold = 0;
            releasedMilestones = new bool[chestMilestones.Count];
            sessionState = DiveSessionState.Running;

            if (!TryGetComponent(out powerUpSpawner))
                powerUpSpawner = gameObject.AddComponent<DivePowerUpSpawner2D>();
            powerUpSpawner.Configure(this);

            FindPlayerHealthIfNeeded();
            SubscribeToPlayer();

            if (registerEnemiesAlreadyInScene)
            {
                EnemyController2D[] existingEnemies = FindObjectsByType<EnemyController2D>(
                    FindObjectsSortMode.None);

                foreach (EnemyController2D enemy in existingEnemies)
                {
                    RegisterEnemy(enemy);
                }
            }

            onSessionStarted?.Invoke();
            SessionStarted?.Invoke();
            NotifyProgressChanged();
        }

        public bool RegisterEnemy(EnemyController2D enemy)
        {
            if (enemy == null || !CanSpawnEnemy || !registeredEnemies.Add(enemy))
            {
                return false;
            }

            spawnedEnemies++;
            aliveEnemies++;
            enemy.EnemyDied += HandleEnemyDied;
            NotifyProgressChanged();
            return true;
        }

        public void AddSecuredGold(int amount)
        {
            if (amount <= 0 || sessionState == DiveSessionState.Lost)
            {
                return;
            }

            securedGold += amount;
            onSecuredGoldChanged?.Invoke(securedGold);
            SecuredGoldChanged?.Invoke(securedGold);
        }

        public void AddCollectedCoinGold(int amount = 1)
        {
            if (amount <= 0
                || sessionState is DiveSessionState.NotStarted or DiveSessionState.Lost or DiveSessionState.Won)
            {
                return;
            }

            collectedCoinGold += amount;
            onCollectedCoinGoldChanged?.Invoke(collectedCoinGold);
            CollectedCoinGoldChanged?.Invoke(collectedCoinGold);
        }

        public void CollectFinalChest(int goldAmount)
        {
            if (sessionState != DiveSessionState.AwaitingFinalChest)
            {
                return;
            }

            finalRewardGold = Mathf.Max(0, goldAmount);
            onFinalRewardGoldChanged?.Invoke(finalRewardGold);
            sessionState = DiveSessionState.Won;
            onVictory?.Invoke();
            SessionWon?.Invoke();
        }

        public void MarkDefeat()
        {
            if (sessionState is DiveSessionState.Lost or DiveSessionState.Won)
            {
                return;
            }

            sessionState = DiveSessionState.Lost;
            onDefeat?.Invoke();
            SessionLost?.Invoke();
        }

        private void HandleEnemyDied(EnemyController2D enemy)
        {
            if (enemy == null || !registeredEnemies.Remove(enemy))
            {
                return;
            }

            enemy.EnemyDied -= HandleEnemyDied;
            aliveEnemies = Mathf.Max(0, aliveEnemies - 1);
            killedEnemies = Mathf.Min(totalEnemies, killedEnemies + 1);
            completionPercentage = totalEnemies > 0
                ? killedEnemies * 100f / totalEnemies
                : 100f;

            ReleaseReachedMilestones();
            NotifyProgressChanged();

            if (killedEnemies < totalEnemies)
            {
                return;
            }

            sessionState = DiveSessionState.AwaitingFinalChest;
            onAllEnemiesDefeated?.Invoke();
        }

        private void ReleaseReachedMilestones()
        {
            EnsureMilestoneState();

            for (int index = 0; index < chestMilestones.Count; index++)
            {
                if (releasedMilestones[index])
                {
                    continue;
                }

                float milestone = Mathf.Clamp(chestMilestones[index], 0.01f, 100f);
                if (completionPercentage + Mathf.Epsilon < milestone)
                {
                    continue;
                }

                releasedMilestones[index] = true;
                releasedChests++;
                onChestMilestoneReached?.Invoke(milestone);
                ChestMilestoneReached?.Invoke(milestone);
            }
        }

        private void NotifyProgressChanged()
        {
            onEnemyProgressChanged?.Invoke(killedEnemies, totalEnemies);
            onCompletionPercentageChanged?.Invoke(completionPercentage);
            CompletionPercentageChanged?.Invoke(completionPercentage);
        }

        private void HandlePlayerDied(Health health, GameObject source)
        {
            MarkDefeat();
        }

        private void FindPlayerHealthIfNeeded()
        {
            if (playerHealth != null)
            {
                return;
            }

            DiverController diver = FindFirstObjectByType<DiverController>();
            if (diver != null)
            {
                playerHealth = diver.GetComponent<Health>();
            }
        }

        private void SubscribeToPlayer()
        {
            if (playerHealth == null)
            {
                return;
            }

            playerHealth.Died -= HandlePlayerDied;
            playerHealth.Died += HandlePlayerDied;
        }

        private void UnsubscribeFromPlayer()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
            }
        }

        private void UnsubscribeFromAllEnemies()
        {
            foreach (EnemyController2D enemy in registeredEnemies)
            {
                if (enemy != null)
                {
                    enemy.EnemyDied -= HandleEnemyDied;
                }
            }
        }

        private void EnsureMilestoneState()
        {
            if (releasedMilestones == null || releasedMilestones.Length != chestMilestones.Count)
            {
                releasedMilestones = new bool[chestMilestones.Count];
            }
        }

        private void OnValidate()
        {
            totalEnemies = Mathf.Max(1, totalEnemies);
            difficultyLevel = Mathf.Max(0, difficultyLevel);

            for (int index = 0; index < chestMilestones.Count; index++)
            {
                chestMilestones[index] = Mathf.Clamp(chestMilestones[index], 0.01f, 100f);
            }

            chestMilestones.Sort();
        }
    }

    /// <summary>Reusable phase power-up scheduler. New power-up types can be added to its selection later.</summary>
    [DisallowMultipleComponent]
    public sealed class DivePowerUpSpawner2D : MonoBehaviour
    {
        [Header("Random Spawn Area")]
        [SerializeField, Min(20)] private int candidatePointCount = 20;
        [SerializeField, Min(.1f)] private float boundaryMargin = .75f;
        [SerializeField, Min(.1f)] private float minimumPlayerDistance = 2.5f;
        [Header("Health Power Up")]
        [SerializeField, Min(.1f)] private float healthRestored = 1f;
        [SerializeField, Range(.1f, 1f)] private float spriteScale = .65f;
        [SerializeField, Range(1f, 99f)] private float secondSpawnPercentage = 20f;

        private readonly List<Vector2> candidatePoints = new();
        private DiveSessionManager session;
        private MovementBounds2D movementBounds;
        private Health playerHealth;
        private Sprite maskSprite;
        private bool initialSpawned, milestoneSpawned;
        private int previousPoint = -1;

        public void Configure(DiveSessionManager owner)
        {
            if (session != null) session.CompletionPercentageChanged -= HandleProgress;
            session = owner;
            movementBounds = FindFirstObjectByType<MovementBounds2D>();
            var diver = FindFirstObjectByType<DiverController>();
            playerHealth = diver != null ? diver.GetComponent<Health>() : null;
            maskSprite = FindFirstObjectByType<DiveHealthDisplayUI>()?.MaskSprite;
            initialSpawned = milestoneSpawned = false;
            previousPoint = -1;
            GenerateCandidatePoints();
            if (session != null) session.CompletionPercentageChanged += HandleProgress;
            SpawnHealthPowerUp();
            initialSpawned = true;
        }

        private void OnDisable()
        {
            if (session != null) session.CompletionPercentageChanged -= HandleProgress;
        }

        private void HandleProgress(float percentage)
        {
            if (!initialSpawned || milestoneSpawned || percentage + Mathf.Epsilon < secondSpawnPercentage) return;
            milestoneSpawned = true;
            SpawnHealthPowerUp();
        }

        private void GenerateCandidatePoints()
        {
            candidatePoints.Clear();
            Bounds bounds = movementBounds != null
                ? movementBounds.Bounds : new Bounds(Vector3.zero, new Vector3(18f, 10f, 0f));
            float minX = bounds.min.x + boundaryMargin;
            float maxX = bounds.max.x - boundaryMargin;
            float minY = bounds.min.y + boundaryMargin;
            float maxY = bounds.max.y - boundaryMargin;
            for (int i = 0; i < Mathf.Max(20, candidatePointCount); i++)
                candidatePoints.Add(new Vector2(UnityEngine.Random.Range(minX, maxX),
                    UnityEngine.Random.Range(minY, maxY)));
        }

        private void SpawnHealthPowerUp()
        {
            if (candidatePoints.Count == 0 || playerHealth == null || maskSprite == null) return;
            int selected = SelectPoint();
            previousPoint = selected;
            var pickup = new GameObject("PowerUp_Vida");
            pickup.transform.position = candidatePoints[selected];
            pickup.transform.localScale = Vector3.one * spriteScale;
            var renderer = pickup.AddComponent<SpriteRenderer>();
            renderer.sprite = maskSprite;
            renderer.color = new Color(1f, .72f, .72f, 1f);
            var playerRenderer = playerHealth.GetComponentInChildren<SpriteRenderer>();
            if (playerRenderer != null)
            {
                renderer.sortingLayerID = playerRenderer.sortingLayerID;
                renderer.sortingOrder = playerRenderer.sortingOrder + 3;
            }
            var body = pickup.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var collider = pickup.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = .45f;
            pickup.AddComponent<DiveHealthPowerUp2D>().Configure(healthRestored);
        }

        private int SelectPoint()
        {
            int best = 0;
            float bestDistance = -1f;
            for (int attempt = 0; attempt < candidatePoints.Count; attempt++)
            {
                int index = UnityEngine.Random.Range(0, candidatePoints.Count);
                if (index == previousPoint) continue;
                float distance = Vector2.Distance(candidatePoints[index], playerHealth.transform.position);
                if (distance >= minimumPlayerDistance) return index;
                if (distance > bestDistance) { bestDistance = distance; best = index; }
            }
            return best;
        }
    }

    public sealed class DiveHealthPowerUp2D : MonoBehaviour
    {
        private float healthRestored = 1f;
        private Vector3 origin;

        public void Configure(float amount)
        {
            healthRestored = Mathf.Max(.1f, amount);
            origin = transform.position;
        }

        private void Update()
        {
            transform.position = origin + Vector3.up * (Mathf.Sin(Time.time * 3f) * .08f);
            transform.Rotate(0f, 0f, 35f * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            DiverController diver = other.GetComponentInParent<DiverController>();
            if (diver == null || !diver.TryGetComponent(out Health health)
                || health.IsDead || health.CurrentHealth >= health.MaximumHealth) return;
            health.Heal(Mathf.Min(healthRestored, health.MaximumHealth - health.CurrentHealth));
            Destroy(gameObject);
        }
    }
}
