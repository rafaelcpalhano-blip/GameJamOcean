using System.Collections.Generic;
using GameJamOcean.Combat;
using GameJamOcean.Player;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamOcean.UI
{
    [DisallowMultipleComponent]
    public sealed class DiveHealthDisplayUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Health playerHealth;
        [SerializeField] private RectTransform maskContainer;
        [SerializeField] private Image maskTemplate;

        [Header("Runtime")]
        [SerializeField] private int displayedMaximumHealth;
        [SerializeField] private int displayedCurrentHealth;

        private readonly List<Image> masks = new();

        private void Awake()
        {
            FindReferencesIfNeeded();

            if (maskTemplate != null)
            {
                maskTemplate.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            FindReferencesIfNeeded();

            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= Refresh;
                playerHealth.HealthChanged += Refresh;
                Refresh(playerHealth);
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.HealthChanged -= Refresh;
            }
        }

        private void Refresh(Health health)
        {
            if (health == null || maskTemplate == null || maskContainer == null)
            {
                return;
            }

            displayedMaximumHealth = Mathf.CeilToInt(health.MaximumHealth);
            displayedCurrentHealth = Mathf.CeilToInt(health.CurrentHealth);
            EnsureMaskCount(displayedMaximumHealth);

            for (int index = 0; index < masks.Count; index++)
            {
                masks[index].gameObject.SetActive(index < displayedCurrentHealth);
            }
        }

        private void EnsureMaskCount(int requiredCount)
        {
            while (masks.Count < requiredCount)
            {
                Image newMask = Instantiate(maskTemplate, maskContainer);
                newMask.name = $"DiveMask_{masks.Count + 1}";
                masks.Add(newMask);
            }

            for (int index = requiredCount; index < masks.Count; index++)
            {
                masks[index].gameObject.SetActive(false);
            }
        }

        private void FindReferencesIfNeeded()
        {
            if (playerHealth == null)
            {
                DiverController diver = FindFirstObjectByType<DiverController>();
                if (diver != null)
                {
                    playerHealth = diver.GetComponent<Health>();
                }
            }

            if (maskContainer == null)
            {
                maskContainer = transform as RectTransform;
            }
        }
    }
}
