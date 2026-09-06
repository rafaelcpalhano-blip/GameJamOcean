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
        }

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onTotalGoldChanged;

        public static GameProgress Instance { get; private set; }
        public static bool HasInstance => Instance != null;
        public bool HasSavedGame { get; private set; }

        public event Action<int> TotalGoldChanged;
        public event Action UpgradesChanged;
        public event Action GameCompleted;
        public bool IsGameCompleted => GetLevel(UpgradeKind.Island) == 4;
        public UpgradeCatalog Catalog => upgradeCatalog;

        public int TotalGold => totalGold;
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
                    upgradeLevels[(int)UpgradeKind.BoatTurbo] = level;
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
                { gold = totalGold, levels = upgradeLevels, boatDeaths = boatDestructions }));
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
                if (saved.levels != null) for (int i = 0; i < Math.Min(6, saved.levels.Length); i++)
                    upgradeLevels[i] = Mathf.Clamp(saved.levels[i], i == 0 ? 0 : 1,
                        i == 0 || i == (int)UpgradeKind.Harpoon ? 4 : 3);
                int vesselLevel = Mathf.Max(upgradeLevels[(int)UpgradeKind.BoatHull], upgradeLevels[(int)UpgradeKind.BoatTurbo]);
                upgradeLevels[(int)UpgradeKind.BoatHull] = vesselLevel;
                upgradeLevels[(int)UpgradeKind.BoatTurbo] = vesselLevel;
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
