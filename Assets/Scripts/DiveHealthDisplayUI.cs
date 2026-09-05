using System.Collections.Generic;
using GameJamOcean.Combat;
using GameJamOcean.Player;
using GameJamOcean.Diving;
using GameJamOcean.Weapons;
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
        public Sprite MaskSprite => maskTemplate != null ? maskTemplate.sprite : null;

        private void Awake()
        {
            FindReferencesIfNeeded();

            if (maskTemplate != null)
            {
                maskTemplate.gameObject.SetActive(false);
            }
            if (GetComponent<DivePowerUpStatusUI>() == null)
                gameObject.AddComponent<DivePowerUpStatusUI>();
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

    public sealed class DivePowerUpStatusUI : MonoBehaviour
    {
        private sealed class Slot
        {
            public RectTransform root;
            public RectTransform fill;
        }

        private DiverController diver;
        private HarpoonLauncher2D launcher;
        private DivePowerUpSettings settings;
        private RectTransform row;
        private Slot doubleShot, shield, speed;

        private void Start()
        {
            diver = FindFirstObjectByType<DiverController>();
            launcher = diver != null ? diver.GetComponent<HarpoonLauncher2D>() : null;
            settings = Resources.Load<DivePowerUpSettings>("DivePowerUpSettings");
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || settings == null) { enabled = false; return; }
            row = Rect("Active Power Ups", canvas.transform, new Vector2(460, 46));
            row.anchorMin = row.anchorMax = row.pivot = new Vector2(0f, 1f);
            row.anchoredPosition = new Vector2(18f, -92f);
            row.SetAsLastSibling();
            doubleShot = CreateSlot("Tiro duplo", settings.doubleHarpoonSprites);
            shield = CreateSlot("Escudo", new[] { settings.shieldSprite });
            speed = CreateSlot("Velocidade", new[] { settings.speedSprite });
        }

        private void Update()
        {
            if (row == null) return;
            int visible = 0;
            UpdateSlot(doubleShot, launcher != null ? launcher.DoubleShotRemaining : 0f,
                launcher != null ? launcher.DoubleShotNormalized : 0f, ref visible);
            UpdateSlot(shield, diver != null ? diver.ShieldRemaining : 0f,
                diver != null ? diver.ShieldNormalized : 0f, ref visible);
            UpdateSlot(speed, diver != null ? diver.SpeedBoostRemaining : 0f,
                diver != null ? diver.SpeedBoostNormalized : 0f, ref visible);
        }

        private Slot CreateSlot(string name, Sprite[] sprites)
        {
            RectTransform root = Rect(name, row, new Vector2(145, 42));
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, .5f);
            root.gameObject.AddComponent<Image>().color = new Color(.02f, .09f, .14f, .12f);
            if (sprites != null)
            {
                int count = Mathf.Max(1, sprites.Length);
                for (int i = 0; i < sprites.Length; i++)
                {
                    if (sprites[i] == null) continue;
                    RectTransform icon = Rect($"Icon {i + 1}", root, new Vector2(28f / count, 28));
                    icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0f, .5f);
                    icon.anchoredPosition = new Vector2(5f + i * 15f, 4f);
                    Image image = icon.gameObject.AddComponent<Image>();
                    image.sprite = sprites[i]; image.preserveAspect = true; image.raycastTarget = false;
                }
            }
            RectTransform barBack = Rect("Time", root, new Vector2(100, 8));
            barBack.anchorMin = barBack.anchorMax = barBack.pivot = new Vector2(0f, .5f);
            barBack.anchoredPosition = new Vector2(39, -10);
            barBack.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .12f);
            RectTransform fillRect = Rect("Remaining", barBack, Vector2.zero);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            Image fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.color = new Color(.2f, .9f, 1f, 1f);
            fillImage.raycastTarget = false;
            root.gameObject.SetActive(false);
            return new Slot { root = root, fill = fillRect };
        }

        private static void UpdateSlot(Slot slot, float remaining, float normalized, ref int visible)
        {
            if (slot == null) return;
            bool active = remaining > 0f;
            slot.root.gameObject.SetActive(active);
            if (!active) return;
            slot.root.anchoredPosition = new Vector2(visible * 151f, 0f);
            slot.fill.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            visible++;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = size;
            return rect;
        }
    }
}
