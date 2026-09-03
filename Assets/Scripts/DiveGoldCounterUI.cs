using GameJamOcean.Diving;
using TMPro;
using UnityEngine;

namespace GameJamOcean.UI
{
    [DisallowMultipleComponent]
    public sealed class DiveGoldCounterUI : MonoBehaviour
    {
        [SerializeField] private DiveSessionManager sessionManager;
        [SerializeField] private TMP_Text counterText;
        [SerializeField] private string prefix = "x ";

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }
        }

        private void OnEnable()
        {
            if (sessionManager != null)
            {
                sessionManager.SecuredGoldChanged += HandleGoldChanged;
                sessionManager.CollectedCoinGoldChanged += HandleGoldChanged;
                sessionManager.SessionStarted += RefreshCounter;
            }

            RefreshCounter();
        }

        private void OnDisable()
        {
            if (sessionManager != null)
            {
                sessionManager.SecuredGoldChanged -= HandleGoldChanged;
                sessionManager.CollectedCoinGoldChanged -= HandleGoldChanged;
                sessionManager.SessionStarted -= RefreshCounter;
            }
        }

        private void RefreshCounter()
        {
            int guaranteedGold = sessionManager != null
                ? sessionManager.SecuredGold + sessionManager.CollectedCoinGold
                : 0;
            UpdateCounter(guaranteedGold);
        }

        private void HandleGoldChanged(int unusedValue)
        {
            RefreshCounter();
        }

        private void UpdateCounter(int value)
        {
            if (counterText != null)
            {
                counterText.text = $"{prefix}{value}";
            }
        }
    }
}
