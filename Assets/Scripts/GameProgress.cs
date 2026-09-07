using System;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Progression
{
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
        [SerializeField] private UnityEvent onGameCompleted = new();
        private const string SaveKey = "GameJamOcean.Progress.v1";
        private const int NewGameGold = 100;
        private bool purchasing;
        private int pendingGoldDelta;
        [Serializable] private sealed class SaveData
        {
            public int gold;
            public int[] levels;
            public int boatDeaths;
            public int selectedBoat;
            public float[] boatHealth;
            public float[] boatMaximumHealth;
        }

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onTotalGoldChanged;

        public static GameProgress Instance { get; private set; }
        public static bool HasInstance => Instance != null;
        public bool HasSavedGame { get; private set; }

        public event Action<int> TotalGoldChanged;
        public event Action UpgradesChanged;
        public event Action GameCompleted;
        public event Action<int> BoatSelectionChanged;
        public bool IsGameCompleted => GetLevel(UpgradeKind.Island) == 4;
        public UpgradeCatalog Catalog => upgradeCatalog;

        public int TotalGold => totalGold;
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

        public void ResetProgress()
        {
            totalGold = NewGameGold;
            pendingGoldDelta = 0;
            upgradeLevels = new[] { 0, 1, 1, 1, 1, 1 };
            boatDestructions = 0;
            selectedBoatLevel = 1;
            savedBoatHealth = new[] { -1f, -1f, -1f };
            savedBoatMaximumHealth = new[] { -1f, -1f, -1f };
            SaveProgress();
            UpgradesChanged?.Invoke();
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
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
            chargedGold = firstDeathFree ? 0 : Mathf.Min(totalGold, Mathf.Max(0, cost));
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
            HasSavedGame = true;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData
                { gold = totalGold, levels = upgradeLevels, boatDeaths = boatDestructions,
                    selectedBoat = selectedBoatLevel, boatHealth = savedBoatHealth,
                    boatMaximumHealth = savedBoatMaximumHealth }));
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            upgradeLevels = new[] { 0, 1, 1, 1, 1, 1 };
            if (!PlayerPrefs.HasKey(SaveKey)) { totalGold = NewGameGold; return; }
            try
            {
                SaveData saved = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
                if (saved == null) return;
                HasSavedGame = true;
                totalGold = Mathf.Max(0, saved.gold);
                boatDestructions = Mathf.Max(0, saved.boatDeaths);
                selectedBoatLevel = Mathf.Clamp(saved.selectedBoat <= 0 ? 1 : saved.selectedBoat, 1, 3);
                savedBoatHealth = saved.boatHealth;
                savedBoatMaximumHealth = saved.boatMaximumHealth;
                EnsureBoatData();
                if (saved.levels != null) for (int i = 0; i < Math.Min(6, saved.levels.Length); i++)
                    upgradeLevels[i] = Mathf.Clamp(saved.levels[i], i == 0 ? 0 : 1,
                        i == 0 || i == (int)UpgradeKind.Harpoon ? 4 : 3);
                int vesselLevel = Mathf.Max(upgradeLevels[(int)UpgradeKind.BoatHull], upgradeLevels[(int)UpgradeKind.BoatTurbo]);
                upgradeLevels[(int)UpgradeKind.BoatHull] = vesselLevel;
                upgradeLevels[(int)UpgradeKind.BoatTurbo] = vesselLevel;
                selectedBoatLevel = Mathf.Min(selectedBoatLevel, vesselLevel);
            }
            catch (Exception error) { Debug.LogWarning($"Não foi possível ler o progresso: {error.Message}"); }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public static void ResetSavedProgress()
        {
            if (Instance != null) Instance.ResetProgress();
            else { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); }
        }

        private void OnValidate()
        {
            totalGold = Mathf.Max(0, totalGold);
        }
    }
}
