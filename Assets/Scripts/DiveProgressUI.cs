using GameJamOcean.Diving;
using TMPro;
using UnityEngine;

namespace GameJamOcean.UI
{
    [DisallowMultipleComponent]
    public sealed class DiveProgressUI : MonoBehaviour
    {
        [SerializeField] private DiveSessionManager sessionManager;
        [SerializeField] private TMP_Text percentageText;
        [SerializeField] private string suffix = "%";

        private void Awake()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }

            if (percentageText == null)
            {
                percentageText = GetComponent<TMP_Text>();
            }
        }

        private void OnEnable()
        {
            if (sessionManager != null)
            {
                sessionManager.CompletionPercentageChanged += UpdatePercentage;
                sessionManager.SessionStarted += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (sessionManager != null)
            {
                sessionManager.CompletionPercentageChanged -= UpdatePercentage;
                sessionManager.SessionStarted -= Refresh;
            }
        }

        private void Refresh()
        {
            UpdatePercentage(sessionManager != null ? sessionManager.CompletionPercentage : 0f);
        }

        private void UpdatePercentage(float value)
        {
            if (percentageText != null)
            {
                percentageText.text = $"{Mathf.RoundToInt(value)}{suffix}";
            }
        }
    }
}
