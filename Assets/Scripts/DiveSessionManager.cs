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
    [Serializable]
    public sealed class FinalRewardTier
    {
        [Min(0)] public int minimumGold;
        [Min(0)] public int maximumGold;
        [Min(1f)] public float excellentTimeSeconds;
        [Min(.01f)] public float excellentKillsPerSecond;

        public FinalRewardTier(int minimum, int maximum, float time, float killsPerSecond)
        {
            minimumGold = minimum; maximumGold = maximum;
            excellentTimeSeconds = time; excellentKillsPerSecond = killsPerSecond;
        }
    }

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
        [Header("Final Chest Performance Reward")]
        [SerializeField] private FinalRewardTier[] finalRewardTiers =
        {
            new(100, 250, 75f, .36f),
            new(125, 300, 82f, .37f),
            new(175, 350, 90f, .37f),
            new(225, 425, 98f, .38f),
            new(300, 500, 108f, .39f)
        };
        [SerializeField, Range(0f, 1f)] private float timeScoreWeight = .4f;
        [SerializeField, Range(0f, 1f)] private float damageScoreWeight = .3f;
        [SerializeField, Range(0f, 1f)] private float killRateScoreWeight = .3f;
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
        public event Action FinalChestReady;
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
        private float sessionStartedAt;
        private float playerDamageTaken;

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
                {
                    UpgradeKind[] difficultyUpgrades = { UpgradeKind.Island, UpgradeKind.BoatHull,
                        UpgradeKind.DiverHealth, UpgradeKind.DiverSpeed, UpgradeKind.Harpoon };
                    foreach (UpgradeKind kind in difficultyUpgrades) points += GameProgress.Instance.GetLevel(kind) - 1;
                }
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
            sessionStartedAt = Time.time;
            playerDamageTaken = 0f;

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

        public int CalculateFinalChestReward()
        {
            int index = Mathf.Clamp(difficultyLevel - 1, 0,
                finalRewardTiers != null ? finalRewardTiers.Length - 1 : 0);
            if (finalRewardTiers == null || finalRewardTiers.Length == 0) return 0;
            FinalRewardTier tier = finalRewardTiers[index];
            float elapsed = Mathf.Max(1f, Time.time - sessionStartedAt);
            float timeScore = Mathf.InverseLerp(tier.excellentTimeSeconds * 1.8f,
                tier.excellentTimeSeconds, elapsed);
            float damageScore = playerHealth != null && playerHealth.MaximumHealth > 0f
                ? 1f - Mathf.Clamp01(playerDamageTaken / playerHealth.MaximumHealth) : 1f;
            float killRate = killedEnemies / elapsed;
            float killRateScore = Mathf.InverseLerp(tier.excellentKillsPerSecond * .45f,
                tier.excellentKillsPerSecond, killRate);
            float totalWeight = Mathf.Max(.001f,
                timeScoreWeight + damageScoreWeight + killRateScoreWeight);
            float score = (timeScore * timeScoreWeight + damageScore * damageScoreWeight
                + killRateScore * killRateScoreWeight) / totalWeight;
            return Mathf.RoundToInt(Mathf.Lerp(tier.minimumGold, tier.maximumGold,
                Mathf.Clamp01(score)));
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
            FinalChestReady?.Invoke();
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
            playerHealth.Damaged -= HandlePlayerDamaged;
            playerHealth.Damaged += HandlePlayerDamaged;
        }

        private void UnsubscribeFromPlayer()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= HandlePlayerDied;
                playerHealth.Damaged -= HandlePlayerDamaged;
            }
        }

        private void HandlePlayerDamaged(Health health, GameObject source)
        {
            playerDamageTaken += Mathf.Max(0f, health.LastDamageAmount);
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
        [Header("Power Ups")]
        [SerializeField, Min(.1f)] private float healthRestored = 1f;
        [SerializeField] private float[] baseSpawnPercentages = { 0f, 20f, 40f, 80f };

        private readonly List<Vector2> candidatePoints = new();
        private readonly List<float> scheduledPercentages = new();
        private DiveSessionManager session;
        private MovementBounds2D movementBounds;
        private Health playerHealth;
        private Sprite maskSprite;
        private DivePowerUpSettings settings;
        private int nextSpawnMilestone;
        private int previousPoint = -1;
        private DivePowerUpKind? previousKind;

        public void Configure(DiveSessionManager owner)
        {
            if (session != null) session.CompletionPercentageChanged -= HandleProgress;
            session = owner;
            movementBounds = FindFirstObjectByType<MovementBounds2D>();
            var diver = FindFirstObjectByType<DiverController>();
            playerHealth = diver != null ? diver.GetComponent<Health>() : null;
            maskSprite = FindFirstObjectByType<DiveHealthDisplayUI>()?.MaskSprite;
            settings = Resources.Load<DivePowerUpSettings>("DivePowerUpSettings");
            nextSpawnMilestone = 0;
            previousPoint = -1;
            previousKind = null;
            BuildSchedule();
            GenerateCandidatePoints();
            if (session != null) session.CompletionPercentageChanged += HandleProgress;
            SpawnReachedPowerUps(0f);
        }

        private void OnDisable()
        {
            if (session != null) session.CompletionPercentageChanged -= HandleProgress;
        }

        private void HandleProgress(float percentage)
        {
            SpawnReachedPowerUps(percentage);
        }

        private void SpawnReachedPowerUps(float percentage)
        {
            while (nextSpawnMilestone < scheduledPercentages.Count
                && percentage + Mathf.Epsilon >= scheduledPercentages[nextSpawnMilestone])
            {
                SpawnRandomPowerUp();
                nextSpawnMilestone++;
            }
        }

        private void BuildSchedule()
        {
            scheduledPercentages.Clear();
            if (baseSpawnPercentages != null)
                foreach (float percentage in baseSpawnPercentages)
                    scheduledPercentages.Add(Mathf.Clamp(percentage, 0f, 100f));
            int extrasBeforeHalf = session != null ? Mathf.Max(0, session.DifficultyLevel - 1) : 0;
            for (int i = 0; i < extrasBeforeHalf; i++)
                scheduledPercentages.Add(UnityEngine.Random.Range(5f, 49f));
            scheduledPercentages.Sort();
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

        private void SpawnRandomPowerUp()
        {
            if (candidatePoints.Count == 0 || playerHealth == null || maskSprite == null) return;
            int selected = SelectPoint();
            previousPoint = selected;
            DivePowerUpKind kind = SelectPowerUpKind();
            var pickup = new GameObject($"PowerUp_{kind}");
            pickup.transform.position = candidatePoints[selected];
            pickup.transform.localScale = Vector3.one * (settings != null ? settings.pickupScale : .8f);
            var playerRenderer = playerHealth.GetComponentInChildren<SpriteRenderer>();
            CreateVisuals(pickup.transform, kind, playerRenderer);
            var body = pickup.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var collider = pickup.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = .7f;
            pickup.AddComponent<DivePowerUpPickup2D>().Configure(kind, healthRestored, settings);
        }

        private DivePowerUpKind SelectPowerUpKind()
        {
            if (settings == null) return DivePowerUpKind.Health;
            bool allowDoubleHarpoon = !GameProgress.HasInstance
                || GameProgress.Instance.GetLevel(UpgradeKind.Harpoon) < 4;
            float total = 0f;
            float[] weights = new float[4];
            for (int i = 0; i < weights.Length; i++)
            {
                if (i == (int)DivePowerUpKind.DoubleHarpoon && !allowDoubleHarpoon)
                {
                    weights[i] = 0f;
                    continue;
                }
                float weight = i == (int)DivePowerUpKind.Health && session != null
                    && session.DifficultyLevel >= 3 ? 1.55f : 1f;
                if (previousKind.HasValue && i == (int)previousKind.Value) weight *= .22f;
                weights[i] = weight;
                total += weight;
            }
            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll > 0f) continue;
                previousKind = (DivePowerUpKind)i;
                return previousKind.Value;
            }
            previousKind = DivePowerUpKind.Health;
            return previousKind.Value;
        }

        private void CreateVisuals(Transform root, DivePowerUpKind kind, SpriteRenderer playerRenderer)
        {
            Sprite[] sprites = kind switch
            {
                DivePowerUpKind.DoubleHarpoon => settings != null ? settings.doubleHarpoonSprites : null,
                DivePowerUpKind.Shield => settings != null ? new[] { settings.shieldSprite } : null,
                DivePowerUpKind.Speed => settings != null ? new[] { settings.speedSprite } : null,
                _ => new[] { maskSprite }
            };
            if (sprites == null || sprites.Length == 0 || sprites[0] == null)
                sprites = new[] { maskSprite };
            for (int i = 0; i < sprites.Length; i++)
            {
                var visual = new GameObject("Visual", typeof(SpriteRenderer));
                visual.transform.SetParent(root, false);
                SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
                renderer.sprite = sprites[i];
                float normalizedScale = 1f / Mathf.Max(.01f, sprites[i].bounds.size.y);
                visual.transform.localScale = Vector3.one * normalizedScale;
                Vector3 layout = Vector3.right * ((i - (sprites.Length - 1) * .5f) * .42f);
                visual.transform.localPosition = layout - sprites[i].bounds.center * normalizedScale;
                if (kind == DivePowerUpKind.Health) renderer.color = new Color(1f, .72f, .72f, 1f);
                if (playerRenderer != null)
                {
                    renderer.sortingLayerID = playerRenderer.sortingLayerID;
                    renderer.sortingOrder = playerRenderer.sortingOrder + 3;
                }
            }
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

    public sealed class DivePowerUpPickup2D : MonoBehaviour
    {
        private DivePowerUpKind kind;
        private float healthRestored = 1f;
        private DivePowerUpSettings settings;
        private Vector3 origin;
        private Vector3 baseScale;

        public void Configure(DivePowerUpKind powerUpKind, float amount, DivePowerUpSettings configuration)
        {
            kind = powerUpKind;
            healthRestored = Mathf.Max(.1f, amount);
            settings = configuration;
            origin = transform.position;
            baseScale = transform.localScale;
        }

        private void Update()
        {
            transform.position = origin + Vector3.up * (Mathf.Sin(Time.time * 3f) * .08f);
            transform.localScale = baseScale * (1f + Mathf.Sin(Time.time * 4f) * .08f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            DiverController diver = other.GetComponentInParent<DiverController>();
            if (diver == null || !diver.TryGetComponent(out Health health) || health.IsDead) return;
            switch (kind)
            {
                case DivePowerUpKind.Health:
                    if (!health.AddTemporaryHealth(healthRestored, 9f)) return;
                    SpriteRenderer playerSprite = diver.GetComponent<SpriteRenderer>();
                    if (playerSprite != null)
                    {
                        Vector3 top = playerSprite.bounds.center
                            + Vector3.up * (playerSprite.bounds.extents.y + .2f);
                        DiveFeedbackParticle.SpawnPlayerHealthChange(top, true,
                            playerSprite.sortingLayerID, playerSprite.sortingOrder);
                    }
                    break;
                case DivePowerUpKind.DoubleHarpoon:
                    diver.GetComponent<GameJamOcean.Weapons.HarpoonLauncher2D>()?.ActivateDoubleShot(
                        settings != null ? settings.doubleHarpoonDuration : 12f,
                        settings != null ? settings.doubleHarpoonAngle : 18f);
                    break;
                case DivePowerUpKind.Shield:
                    diver.ActivateShield(settings != null ? settings.shieldDuration : 5f);
                    break;
                case DivePowerUpKind.Speed:
                    diver.ActivateSpeedBoost(settings != null ? settings.speedDuration : 7f,
                        settings != null ? settings.speedMultiplier : 1.5f);
                    break;
            }
            Destroy(gameObject);
        }
    }
}
