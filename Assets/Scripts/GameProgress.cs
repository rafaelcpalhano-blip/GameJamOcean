using System;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Progression
{
    public enum GameDifficulty
    {
        Normal = 0,
        Easy = 1,
        Hard = 2
    }

    public readonly struct DiveSpawnSettings
    {
        public readonly int InitialEnemies;
        public readonly int TotalEnemies;
        public readonly float SpawnInterval;

        public DiveSpawnSettings(int initialEnemies, int totalEnemies, float spawnInterval)
        {
            InitialEnemies = initialEnemies;
            TotalEnemies = totalEnemies;
            SpawnInterval = spawnInterval;
        }
    }

    public static class GameDifficultyRules
    {
        private static readonly int[] NormalInitial = { 6, 12, 14, 15, 16 };
        private static readonly int[] NormalTotal = { 18, 30, 33, 38, 45 };
        private static readonly int[] EasyInitial = { 5, 8, 10, 11, 12 };
        private static readonly int[] EasyTotal = { 15, 22, 24, 28, 35 };
        private static readonly int[] HardInitial = { 9, 15, 17, 19, 21 };
        private static readonly int[] HardTotal = { 25, 35, 45, 51, 58 };

        public static DiveSpawnSettings GetDiveSpawnSettings(GameDifficulty difficulty, int tierIndex)
        {
            int index = Mathf.Clamp(tierIndex, 0, NormalInitial.Length - 1);
            if (difficulty == GameDifficulty.Easy)
                return new DiveSpawnSettings(EasyInitial[index], EasyTotal[index], 3f);
            if (difficulty == GameDifficulty.Hard)
                return new DiveSpawnSettings(HardInitial[index], HardTotal[index], 2.5f);
            return new DiveSpawnSettings(
                NormalInitial[index], NormalTotal[index], 2f);
        }

        public static int GetBoatDestructionCost(GameDifficulty difficulty, int normalCost)
        {
            return difficulty == GameDifficulty.Easy ? 125 : Mathf.Max(0, normalCost);
        }
    }

    [DisallowMultipleComponent]
    public sealed class GameProgress : MonoBehaviour
    {
        [Header("Runtime Progress")]
        [SerializeField, Min(0)] private int totalGold;
        [SerializeField] private UpgradeCatalog upgradeCatalog;
        [SerializeField] private int[] upgradeLevels = { 0, 1, 1, 1, 1, 1 };
        [SerializeField, Min(0)] private int boatDestructions;
        [SerializeField, Range(1, 3)] private int selectedBoatLevel = 1;
        [SerializeField] private float[] savedBoatHealth = { -1f, -1f, -1f };
        [SerializeField] private float[] savedBoatMaximumHealth = { -1f, -1f, -1f };
        [SerializeField] private GameDifficulty currentDifficulty = GameDifficulty.Normal;
        [SerializeField] private UnityEvent onGameCompleted = new();
        private const string SaveKey = "GameJamOcean.Progress.v1";
        private const int CurrentSaveVersion = 2;
        private const int NewGameGold = 100;
        private const string TutorialsArmedKey = "GameJamOcean.Tutorials.Armed";
        private const string OceanTutorialKey = "GameJamOcean.Tutorials.OceanShown";
        private const string DiveTutorialKey = "GameJamOcean.Tutorials.DiveShown";
        private bool purchasing;
        private int pendingGoldDelta;
        private bool introCompleted;
        [Serializable] private sealed class SaveData
        {
            public int version;
            public bool campaignStarted;
            public bool introCompleted;
            public int gold;
            public int[] levels;
            public int boatDeaths;
            public int selectedBoat;
            public float[] boatHealth;
            public float[] boatMaximumHealth;
            public GameDifficulty difficulty;
        }

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onTotalGoldChanged;

        public static GameProgress Instance { get; private set; }
        public static bool HasInstance => Instance != null;
        public bool HasValidCampaign { get; private set; }
        // Compatibility for code outside this project that may still use the old property name.
        public bool HasSavedGame => HasValidCampaign;
        public bool IsIntroPending => HasValidCampaign && !introCompleted;

        public event Action<int> TotalGoldChanged;
        public event Action UpgradesChanged;
        public event Action GameCompleted;
        public event Action<int> BoatSelectionChanged;
        public bool IsGameCompleted => GetLevel(UpgradeKind.Island) == 4;
        public bool IsStartingNewCampaign { get; private set; }
        public UpgradeCatalog Catalog => upgradeCatalog;

        public int TotalGold => totalGold;
        public GameDifficulty CurrentDifficulty => currentDifficulty;
        public int SelectedBoatLevel => Mathf.Clamp(selectedBoatLevel, 1,
            Mathf.Clamp(GetLevel(UpgradeKind.BoatHull), 1, 3));
        public int ConsumePendingGoldDelta()
        {
            int value = pendingGoldDelta;
            pendingGoldDelta = 0;
            return value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null)
            {
                return;
            }

            GameObject progressObject = new("GameProgress");
            progressObject.AddComponent<GameProgress>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            upgradeCatalog = Resources.Load<UpgradeCatalog>("UpgradeCatalog");
            LoadProgress();
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            totalGold = (int)Math.Min(int.MaxValue, (long)totalGold + amount);
            pendingGoldDelta += amount;
            SaveProgress();
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
        }

        public bool TrySpendGold(int amount)
        {
            if (amount <= 0 || totalGold < amount)
            {
                return false;
            }

            totalGold -= amount;
            pendingGoldDelta -= amount;
            SaveProgress();
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
            return true;
        }

        public void BeginNewCampaign(GameDifficulty difficulty)
        {
            SetDefaultProgress();
            currentDifficulty = Enum.IsDefined(typeof(GameDifficulty), difficulty)
                ? difficulty : GameDifficulty.Normal;
            HasValidCampaign = true;
            introCompleted = false;
            GameJamOcean.Boat.OceanReturnState3D.ResetRuntimeState();
            IsStartingNewCampaign = true;
            try
            {
                // Active scene systems must discard the previous campaign before the scene reloads.
                UpgradesChanged?.Invoke();
            }
            finally
            {
                IsStartingNewCampaign = false;
            }
            SaveProgress();
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
        }

        public void BeginNewCampaign() => BeginNewCampaign(GameDifficulty.Normal);

        public void MarkIntroCompleted()
        {
            if (!HasValidCampaign || introCompleted) return;
            introCompleted = true;
            SaveProgress();
        }

        public int GetLevel(UpgradeKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < upgradeLevels.Length
                ? Mathf.Clamp(upgradeLevels[index], kind == UpgradeKind.Island ? 0 : 1,
                    kind == UpgradeKind.Island || kind == UpgradeKind.Harpoon ? 4 : 3) : 1;
        }

        public void RegisterBoatDestruction(int cost, out bool firstDeathFree, out int chargedGold)
        {
            firstDeathFree = boatDestructions == 0;
            boatDestructions++;
            int configuredCost = Mathf.Max(0, cost);
            chargedGold = firstDeathFree ? 0
                : totalGold >= configuredCost ? configuredCost : totalGold / 2;
            if (chargedGold > 0)
            {
                totalGold -= chargedGold;
                pendingGoldDelta -= chargedGold;
                onTotalGoldChanged?.Invoke(totalGold);
                TotalGoldChanged?.Invoke(totalGold);
            }
            SaveProgress();
        }

        public bool SelectBoat(int level)
        {
            level = Mathf.Clamp(level, 1, 3);
            if (level > GetLevel(UpgradeKind.BoatHull)) return false;
            if (selectedBoatLevel == level) return true;
            selectedBoatLevel = level;
            SaveProgress();
            BoatSelectionChanged?.Invoke(level);
            return true;
        }

        public float GetSavedBoatHealth(int level)
        {
            EnsureBoatData();
            return savedBoatHealth[Mathf.Clamp(level, 1, 3) - 1];
        }

        public void SetSavedBoatHealth(int level, float value)
        {
            EnsureBoatData();
            savedBoatHealth[Mathf.Clamp(level, 1, 3) - 1] = Mathf.Max(0f, value);
            SaveProgress();
        }

        public float GetSavedBoatMaximumHealth(int level)
        {
            EnsureBoatData();
            return savedBoatMaximumHealth[Mathf.Clamp(level, 1, 3) - 1];
        }

        public void SetSavedBoatHealthState(int level, float currentHealth, float maximumHealth)
        {
            EnsureBoatData();
            int index = Mathf.Clamp(level, 1, 3) - 1;
            savedBoatMaximumHealth[index] = Mathf.Max(1f, maximumHealth);
            savedBoatHealth[index] = Mathf.Clamp(currentHealth, 0f, savedBoatMaximumHealth[index]);
            SaveProgress();
        }

        private void EnsureBoatData()
        {
            savedBoatHealth = EnsureThreeEntries(savedBoatHealth);
            savedBoatMaximumHealth = EnsureThreeEntries(savedBoatMaximumHealth);
        }

        private static float[] EnsureThreeEntries(float[] source)
        {
            if (source != null && source.Length == 3) return source;
            float[] result = { -1f, -1f, -1f };
            if (source != null)
                for (int i = 0; i < Mathf.Min(source.Length, result.Length); i++) result[i] = source[i];
            return result;
        }

        public bool TryPurchase(UpgradeKind kind, int expectedLevel, out string message)
        {
            if (purchasing) { message = "Compra em andamento."; return false; }
            if (upgradeCatalog == null) { message = "Catálogo de upgrades não configurado."; return false; }
            if (!upgradeCatalog.Validate(out message)) return false;
            var definition = upgradeCatalog.Find(kind);
            if (definition == null) { message = "Upgrade desconhecido."; return false; }
            int level = GetLevel(kind);
            int gold = totalGold;
            if (!UpgradePurchaseRules.TryApply(ref gold, ref level, expectedLevel,
                definition.MaximumLevel, definition.Tier(Mathf.Min(definition.MaximumLevel, level + 1)).goldCost, out message)) return false;
            // Commit both values before notifying any observers; callbacks cannot purchase recursively.
            purchasing = true;
            try
            {
                int spent = totalGold - gold;
                totalGold = gold;
                pendingGoldDelta -= spent;
                upgradeLevels[(int)kind] = level;
                if (kind == UpgradeKind.BoatHull)
                {
                    upgradeLevels[(int)UpgradeKind.BoatTurbo] = level;
                    EnsureBoatData();
                    // A newly unlocked hull must never inherit stale health from another boat or an older save.
                    int newBoatIndex = Mathf.Clamp(level, 1, 3) - 1;
                    savedBoatHealth[newBoatIndex] = -1f;
                    savedBoatMaximumHealth[newBoatIndex] = -1f;
                }
                SaveProgress();
                UpgradesChanged?.Invoke();
                onTotalGoldChanged?.Invoke(totalGold);
                TotalGoldChanged?.Invoke(totalGold);
                if (kind == UpgradeKind.Island && level == 4)
                {
                    onGameCompleted.Invoke();
                    GameCompleted?.Invoke();
                }
            }
            finally { purchasing = false; }
            return true;
        }

        public void SaveProgress()
        {
            // Scene objects may report their initial state while the main menu is open.
            // Such runtime synchronization must never create a campaign.
            if (!HasValidCampaign) return;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData
                { version = CurrentSaveVersion, campaignStarted = true,
                    introCompleted = introCompleted, gold = totalGold,
                    levels = upgradeLevels, boatDeaths = boatDestructions,
                    selectedBoat = selectedBoatLevel, boatHealth = savedBoatHealth,
                    boatMaximumHealth = savedBoatMaximumHealth, difficulty = currentDifficulty }));
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            SetDefaultProgress();
            HasValidCampaign = false;
            introCompleted = false;
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try
            {
                SaveData saved = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
                if (saved == null) return;
                bool legacySave = saved.version < CurrentSaveVersion;
                bool validCampaign = legacySave ? IsValidLegacyCampaign(saved) : saved.campaignStarted;
                if (!validCampaign) return;

                HasValidCampaign = true;
                introCompleted = legacySave ? InferLegacyIntroCompleted(saved) : saved.introCompleted;
                totalGold = Mathf.Max(0, saved.gold);
                boatDestructions = Mathf.Max(0, saved.boatDeaths);
                selectedBoatLevel = Mathf.Clamp(saved.selectedBoat <= 0 ? 1 : saved.selectedBoat, 1, 3);
                savedBoatHealth = saved.boatHealth;
                savedBoatMaximumHealth = saved.boatMaximumHealth;
                currentDifficulty = Enum.IsDefined(typeof(GameDifficulty), saved.difficulty)
                    ? saved.difficulty : GameDifficulty.Normal;
                EnsureBoatData();
                if (saved.levels != null) for (int i = 0; i < Math.Min(6, saved.levels.Length); i++)
                    upgradeLevels[i] = Mathf.Clamp(saved.levels[i], i == 0 ? 0 : 1,
                        i == 0 || i == (int)UpgradeKind.Harpoon ? 4 : 3);
                int vesselLevel = Mathf.Max(upgradeLevels[(int)UpgradeKind.BoatHull], upgradeLevels[(int)UpgradeKind.BoatTurbo]);
                upgradeLevels[(int)UpgradeKind.BoatHull] = vesselLevel;
                upgradeLevels[(int)UpgradeKind.BoatTurbo] = vesselLevel;
                selectedBoatLevel = Mathf.Min(selectedBoatLevel, vesselLevel);
                if (legacySave) SaveProgress();
            }
            catch (Exception error) { Debug.LogWarning($"Não foi possível ler o progresso: {error.Message}"); }
        }

        private void SetDefaultProgress()
        {
            totalGold = NewGameGold;
            pendingGoldDelta = 0;
            upgradeLevels = new[] { 0, 1, 1, 1, 1, 1 };
            boatDestructions = 0;
            selectedBoatLevel = 1;
            savedBoatHealth = new[] { -1f, -1f, -1f };
            savedBoatMaximumHealth = new[] { -1f, -1f, -1f };
            currentDifficulty = GameDifficulty.Normal;
        }

        private static bool IsValidLegacyCampaign(SaveData saved)
        {
            // Every campaign created by the current onboarding writes these keys.
            // The settings-only bug wrote only default boat health and none of them.
            if (HasAnyTutorialState()) return true;
            if (saved.gold != NewGameGold || saved.boatDeaths > 0 || saved.selectedBoat > 1)
                return true;
            if (saved.levels != null)
                for (int i = 0; i < saved.levels.Length; i++)
                {
                    int expected = i == 0 ? 0 : 1;
                    if (saved.levels[i] != expected) return true;
                }
            return HasDamagedBoat(saved.boatHealth, saved.boatMaximumHealth);
        }

        private static bool HasAnyTutorialState()
        {
            return PlayerPrefs.HasKey(TutorialsArmedKey)
                || PlayerPrefs.HasKey(OceanTutorialKey)
                || PlayerPrefs.HasKey(DiveTutorialKey);
        }

        private static bool InferLegacyIntroCompleted(SaveData saved)
        {
            if (PlayerPrefs.HasKey(OceanTutorialKey))
                return PlayerPrefs.GetInt(OceanTutorialKey, 0) != 0;
            // Very old saves with real progress predate the current onboarding keys.
            // Preserve their established campaign instead of forcing a new introduction.
            return !HasAnyTutorialState() && IsValidLegacyCampaign(saved);
        }

        private static bool HasDamagedBoat(float[] health, float[] maximumHealth)
        {
            if (health == null || maximumHealth == null) return false;
            int count = Math.Min(health.Length, maximumHealth.Length);
            for (int i = 0; i < count; i++)
                if (health[i] >= 0f && maximumHealth[i] > 0f
                    && health[i] < maximumHealth[i] - .01f) return true;
            return false;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void ResetSavedProgress()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            if (Instance == null) return;
            Instance.SetDefaultProgress();
            Instance.HasValidCampaign = false;
            Instance.introCompleted = false;
            Instance.UpgradesChanged?.Invoke();
            Instance.onTotalGoldChanged?.Invoke(Instance.totalGold);
            Instance.TotalGoldChanged?.Invoke(Instance.totalGold);
        }

        private void OnValidate()
        {
            totalGold = Mathf.Max(0, totalGold);
        }
    }
}
