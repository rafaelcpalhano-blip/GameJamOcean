using GameJamOcean.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    public sealed class BoatStatusHUD3D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health boatHealth;
        [SerializeField] private BoatController3D boatController;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthText;
        [FormerlySerializedAs("accelerationFill")]
        [SerializeField] private Image turboFill;
        [FormerlySerializedAs("accelerationText")]
        [SerializeField] private TMP_Text turboText;

        private void OnEnable()
        {
            FindBoatIfNeeded();
            if (boatHealth != null)
            {
                boatHealth.HealthChanged += OnHealthChanged;
            }

            RefreshHealth();
            RefreshTurbo();
        }

        private void OnDisable()
        {
            if (boatHealth != null)
            {
                boatHealth.HealthChanged -= OnHealthChanged;
            }
        }

        private void Update()
        {
            RefreshTurbo();
        }

        public void Configure(
            Health health,
            BoatController3D controller,
            Image healthBarFill,
            TMP_Text healthLabel,
            Image turboBarFill,
            TMP_Text turboLabel)
        {
            boatHealth = health;
            boatController = controller;
            healthFill = healthBarFill;
            healthText = healthLabel;
            turboFill = turboBarFill;
            turboText = turboLabel;
        }

        private void FindBoatIfNeeded()
        {
            boatController ??= Object.FindFirstObjectByType<BoatController3D>();
            if (boatHealth == null && boatController != null)
            {
                boatHealth = boatController.GetComponent<Health>();
            }
        }

        private void OnHealthChanged(Health changedHealth)
        {
            RefreshHealth();
        }

        private void RefreshHealth()
        {
            float percentage = boatHealth != null ? boatHealth.NormalizedHealth : 0f;
            if (healthFill != null)
            {
                healthFill.fillAmount = percentage;
            }

            if (healthText != null)
            {
                healthText.text = $"CASCO {Mathf.RoundToInt(percentage * 100f)}%";
            }
        }

        private void RefreshTurbo()
        {
            float percentage = boatController != null ? boatController.TurboPercentage : 0f;
            if (turboFill != null)
            {
                turboFill.fillAmount = percentage;
            }

            if (turboText != null)
            {
                turboText.text = $"TURBO {Mathf.RoundToInt(percentage * 100f)}%";
            }
        }
    }
}
