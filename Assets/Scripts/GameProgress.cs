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
        [SerializeField] private int[] upgradeLevels = { 1, 1, 1, 1, 1, 1 };
        [SerializeField] private UnityEvent onGameCompleted = new();
        private const string SaveKey = "GameJamOcean.Progress.v1";
        private bool purchasing;
        [Serializable] private sealed class SaveData
        {
            public int gold;
            public int[] levels;
        }

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onTotalGoldChanged;

        public static GameProgress Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        public event Action<int> TotalGoldChanged;
        public event Action UpgradesChanged;
        public bool IsGameCompleted => GetLevel(UpgradeKind.Island) == 4;
        public UpgradeCatalog Catalog => upgradeCatalog;

        public int TotalGold => totalGold;

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
            SaveProgress();
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
            return true;
        }

        public void ResetProgress()
        {
            totalGold = 0;
            upgradeLevels = new[] { 1, 1, 1, 1, 1, 1 };
            SaveProgress();
            UpgradesChanged?.Invoke();
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
        }

        public int GetLevel(UpgradeKind kind)
        {
            int index = (int)kind;
            return index >= 0 && index < upgradeLevels.Length ? Mathf.Clamp(upgradeLevels[index], 1, index == 0 ? 4 : 3) : 1;
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
                totalGold = gold;
                upgradeLevels[(int)kind] = level;
                SaveProgress();
                UpgradesChanged?.Invoke();
                onTotalGoldChanged?.Invoke(totalGold);
                TotalGoldChanged?.Invoke(totalGold);
                if (kind == UpgradeKind.Island && level == 4) onGameCompleted.Invoke();
            }
            finally { purchasing = false; }
            return true;
        }

        private void SaveProgress()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData { gold = totalGold, levels = upgradeLevels }));
            PlayerPrefs.Save();
        }

        private void LoadProgress()
        {
            upgradeLevels = new[] { 1, 1, 1, 1, 1, 1 };
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try
            {
                SaveData saved = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
                if (saved == null) return;
                totalGold = Mathf.Max(0, saved.gold);
                if (saved.levels != null) for (int i = 0; i < Math.Min(6, saved.levels.Length); i++)
                    upgradeLevels[i] = Mathf.Clamp(saved.levels[i], 1, i == 0 ? 4 : 3);
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
