using System;
using System.Collections.Generic;
using GameJamOcean.Combat;
using GameJamOcean.Enemies;
using GameJamOcean.Player;
using UnityEngine;
using UnityEngine.Events;

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
}
