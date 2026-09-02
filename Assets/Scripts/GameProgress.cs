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

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onTotalGoldChanged;

        public static GameProgress Instance { get; private set; }
        public static bool HasInstance => Instance != null;

        public event Action<int> TotalGoldChanged;

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
        }

        public void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            totalGold += amount;
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
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
            return true;
        }

        public void ResetProgress()
        {
            totalGold = 0;
            onTotalGoldChanged?.Invoke(totalGold);
            TotalGoldChanged?.Invoke(totalGold);
        }

        private void OnValidate()
        {
            totalGold = Mathf.Max(0, totalGold);
        }
    }
}
