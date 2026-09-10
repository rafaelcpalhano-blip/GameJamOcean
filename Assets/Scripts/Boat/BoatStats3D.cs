using System.Collections;
using GameJamOcean.Combat;
using GameJamOcean.Progression;
using UnityEngine;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoatController3D), typeof(Health))]
    public sealed class BoatStats3D : MonoBehaviour
    {
        [Header("Base Stats")]
        [SerializeField, Min(1f)] private float baseMaximumHealth = 100f;
        [SerializeField, Min(0.1f)] private float baseMaximumSpeed = 6f;
        [SerializeField, Min(0.1f)] private float baseAcceleration = 2.5f;

        [Header("Base Turbo")]
        [SerializeField, Min(0.1f)] private float baseTurboCapacity = 3f;
        [SerializeField, Min(1f)] private float baseTurboSpeedMultiplier = 1.75f;
        [SerializeField, Min(1f)] private float baseTurboAccelerationMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float baseTurboRechargePerSecond = 0.5f;
        [SerializeField, Min(0f)] private float turboRechargeDelay = 1.25f;

        [Header("Upgrade Bonuses (%)")]
        [Tooltip("Future upgrade bonus. Example: 25 means +25% maximum health.")]
        [SerializeField] private float healthUpgradePercent;
        [SerializeField] private float speedUpgradePercent;
        [SerializeField] private float accelerationUpgradePercent;
        [SerializeField] private float turboCapacityUpgradePercent;
        [SerializeField] private float turboPowerUpgradePercent;
        [SerializeField] private float turboRechargeUpgradePercent;

        [Header("Current Calculated Stats")]
        [SerializeField] private float currentMaximumHealth;
        [SerializeField] private float currentMaximumSpeed;
        [SerializeField] private float currentAcceleration;
        [SerializeField] private float currentTurboCapacity;
        [SerializeField] private float currentTurboSpeedMultiplier;
        [SerializeField] private float currentTurboAccelerationMultiplier;
        [SerializeField] private float currentTurboRechargePerSecond;

        private Health health;
        private BoatController3D controller;
        [Header("Sinking and Rescue")]
        [SerializeField, Min(.5f)] private float sinkingDuration = 3.5f;
        [SerializeField, Min(1f)] private float sinkingDepth = 5f;
        [SerializeField, Range(0f, 40f)] private float sinkingRoll = 22f;
        [Tooltip("Custo cobrado da segunda destruição do barco em diante.")]
        [SerializeField, Min(0)] private int destructionGoldCost = 200;
        private bool sinking;
        private int activeBoatLevel = 1;
        private bool switchingBoat;
        private bool healthMemoryInitialized;

        public Health Health => health;
        public float HealthPercentage => health != null ? health.NormalizedHealth : 0f;
        public float TurboPercentage => controller != null ? controller.TurboPercentage : 0f;
        public float CurrentMaximumHealth => currentMaximumHealth;
        public float CurrentMaximumSpeed => currentMaximumSpeed;
        public float CurrentAcceleration => currentAcceleration;
        public float CurrentTurboCapacity => currentTurboCapacity;
        public float CurrentTurboSpeedMultiplier => currentTurboSpeedMultiplier;
        public float CurrentTurboRechargePerSecond => currentTurboRechargePerSecond;

        private void Awake()
        {
            health = GetComponent<Health>();
            controller = GetComponent<BoatController3D>();
            activeBoatLevel = GameProgress.HasInstance ? GameProgress.Instance.SelectedBoatLevel : 1;
            ApplyStats(true);
        }

        private void OnEnable()
        {
            health ??= GetComponent<Health>();
            health.Died += OnBoatDestroyed;
            health.HealthChanged += SaveActiveBoatHealth;
            if (GameProgress.HasInstance) GameProgress.Instance.UpgradesChanged += ApplyPurchasedUpgrades;
        }

        private void Start()
        {
            ApplyPurchasedUpgrades();
            if (!OceanReturnState3D.TryRestoreHealth(health)) RestoreSelectedBoatHealth();
            healthMemoryInitialized = true;
            SaveActiveBoatHealth(health);
        }

        private void ApplyPurchasedUpgrades()
        {
            if (!GameProgress.HasInstance || GameProgress.Instance.Catalog == null) return;
            var progress = GameProgress.Instance;
            var hull = progress.Catalog.Find(UpgradeKind.BoatHull);
            var turbo = progress.Catalog.Find(UpgradeKind.BoatTurbo);
            if (hull == null || turbo == null) return;
            int vesselLevel = progress.GetLevel(UpgradeKind.BoatHull);
            bool resetForNewCampaign = progress.IsStartingNewCampaign;
            if (resetForNewCampaign) activeBoatLevel = progress.SelectedBoatLevel;
            float ratio = resetForNewCampaign ? 1f : health.NormalizedHealth;
            bool wasSwitching = switchingBoat;
            switchingBoat = true;
            try
            {
                healthUpgradePercent = hull.Tier(vesselLevel).value;
                turboCapacityUpgradePercent = turbo.Tier(vesselLevel).value;
                ApplyStats();
                health.SetCurrentHealth(health.MaximumHealth * ratio);
            }
            finally { switchingBoat = wasSwitching; }
            if (!wasSwitching && healthMemoryInitialized) SaveActiveBoatHealth(health);
        }

        private void OnDisable()
        {
            if (GameProgress.HasInstance) GameProgress.Instance.UpgradesChanged -= ApplyPurchasedUpgrades;
            if (health != null)
            {
                health.Died -= OnBoatDestroyed;
                health.HealthChanged -= SaveActiveBoatHealth;
            }
        }

        public void ConfigureRelaxedAcceleration()
        {
            baseAcceleration = 2.5f;
            ApplyStats();
        }

        public void ConfigureBaseStats(
            float maximumHealth,
            float maximumSpeed,
            float acceleration)
        {
            baseMaximumHealth = Mathf.Max(1f, maximumHealth);
            baseMaximumSpeed = Mathf.Max(0.1f, maximumSpeed);
            baseAcceleration = Mathf.Max(0.1f, acceleration);
            ApplyStats(true);
        }

        public void SetUpgradeBonuses(
            float healthPercent,
            float speedPercent,
            float accelerationPercent,
            bool restoreHealth = false)
        {
            healthUpgradePercent = Mathf.Max(-90f, healthPercent);
            speedUpgradePercent = Mathf.Max(-90f, speedPercent);
            accelerationUpgradePercent = Mathf.Max(-90f, accelerationPercent);
            ApplyStats(restoreHealth);
        }

        public void ConfigureBaseTurbo(
            float capacity,
            float speedMultiplier,
            float accelerationMultiplier,
            float rechargePerSecond,
            float rechargeDelay)
        {
            baseTurboCapacity = Mathf.Max(0.1f, capacity);
            baseTurboSpeedMultiplier = Mathf.Max(1f, speedMultiplier);
            baseTurboAccelerationMultiplier = Mathf.Max(1f, accelerationMultiplier);
            baseTurboRechargePerSecond = Mathf.Max(0f, rechargePerSecond);
            turboRechargeDelay = Mathf.Max(0f, rechargeDelay);
            ApplyStats(true);
        }

        public void SetTurboUpgradeBonuses(
            float capacityPercent,
            float powerPercent,
            float rechargePercent,
            bool refillTurbo = false)
        {
            turboCapacityUpgradePercent = Mathf.Max(-90f, capacityPercent);
            turboPowerUpgradePercent = Mathf.Max(-90f, powerPercent);
            turboRechargeUpgradePercent = Mathf.Max(-90f, rechargePercent);
            ApplyStats(false, refillTurbo);
        }

        public void ApplyStats(bool restoreHealth = false, bool refillTurbo = false)
        {
            health ??= GetComponent<Health>();
            controller ??= GetComponent<BoatController3D>();
            RecalculateStats();
            health.SetMaximumHealth(currentMaximumHealth, restoreHealth);
            controller.ConfigureMovement(currentMaximumSpeed, currentAcceleration);
            controller.ConfigureTurbo(
                currentTurboCapacity,
                currentTurboSpeedMultiplier,
                currentTurboAccelerationMultiplier,
                currentTurboRechargePerSecond,
                turboRechargeDelay,
                restoreHealth || refillTurbo);
        }

        public void RepairToFull()
        {
            health ??= GetComponent<Health>();
            controller ??= GetComponent<BoatController3D>();
            health.Restore();
            controller.RefillTurbo();
            controller.enabled = true;
        }

        public void SaveCurrentBoatHealth()
        {
            SaveActiveBoatHealth(health);
        }

        public bool SelectBoat(int level)
        {
            if (!GameProgress.HasInstance || level == activeBoatLevel
                || level < 1 || level > GameProgress.Instance.GetLevel(UpgradeKind.BoatHull)) return false;
            switchingBoat = true;
            try
            {
                GameProgress.Instance.SetSavedBoatHealthState(activeBoatLevel,
                    health.CurrentHealth, health.MaximumHealth);
                if (!GameProgress.Instance.SelectBoat(level)) return false;
                activeBoatLevel = GameProgress.Instance.SelectedBoatLevel;
                RestoreSelectedBoatHealth();
                GameJamOcean.Audio.GameAudio.Instance?.PlayBoatUpgrade();
                GameJamOcean.Audio.GameAudio.Instance?.PlayBoatEngine(level);
                return true;
            }
            finally { switchingBoat = false; }
        }

        private void RestoreSelectedBoatHealth()
        {
            if (!GameProgress.HasInstance) { health.Restore(); return; }
            activeBoatLevel = GameProgress.Instance.SelectedBoatLevel;
            float saved = GameProgress.Instance.GetSavedBoatHealth(activeBoatLevel);
            float savedMaximum = GameProgress.Instance.GetSavedBoatMaximumHealth(activeBoatLevel);
            if (saved < 0f) health.Restore();
            else
            {
                float restored = savedMaximum > 0f
                    ? health.MaximumHealth * Mathf.Clamp01(saved / savedMaximum)
                    : Mathf.Min(saved, health.MaximumHealth);
                health.SetCurrentHealth(restored);
            }
            GameProgress.Instance.SetSavedBoatHealthState(activeBoatLevel,
                health.CurrentHealth, health.MaximumHealth);
        }

        private void SaveActiveBoatHealth(Health changedHealth)
        {
            if (healthMemoryInitialized && !switchingBoat
                && GameProgress.HasInstance && changedHealth != null)
                GameProgress.Instance.SetSavedBoatHealthState(activeBoatLevel,
                    changedHealth.CurrentHealth, changedHealth.MaximumHealth);
        }

        private void OnBoatDestroyed(Health destroyedHealth, GameObject damageSource)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }
            if (!sinking) StartCoroutine(SinkAndRescue());
        }

        private IEnumerator SinkAndRescue()
        {
            sinking = true;
            GameJamOcean.UI.GameMenus.BoatRecoveryActive = true;
            bool firstDeathFree = true;
            int chargedGold = 0;
            int applicableDestructionCost = destructionGoldCost;
            if (GameProgress.HasInstance)
            {
                applicableDestructionCost = GameDifficultyRules.GetBoatDestructionCost(
                    GameProgress.Instance.CurrentDifficulty, destructionGoldCost);
                GameProgress.Instance.RegisterBoatDestruction(applicableDestructionCost,
                    out firstDeathFree, out chargedGold);
            }
            var body = GetComponent<Rigidbody>();
            bool wasKinematic = body.isKinematic;
            // Do not interpolate from underwater poses across the rescue teleport/pause.
            var previousInterpolation = body.interpolation;
            body.interpolation = RigidbodyInterpolation.None;
            body.isKinematic = true;
            var colliders = GetComponentsInChildren<Collider>();
            var enabledColliders = new System.Collections.Generic.List<Collider>();
            foreach (var c in colliders) if (c.enabled) { enabledColliders.Add(c); c.enabled = false; }
            var motion = GetComponent<BoatWaterMotion3D>();
            bool hadMotion = motion != null && motion.enabled;
            if (hadMotion) motion.enabled = false;
            Vector3 start = transform.position;
            Quaternion rotation = transform.rotation;
            float elapsed = 0;
            while (elapsed < sinkingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / sinkingDuration);
                float strength = Mathf.SmoothStep(0, 1, t);
                Vector3 sinkPosition = start - Vector3.up * (sinkingDepth * strength);
                Quaternion sinkRotation = rotation * Quaternion.Euler(strength * 12f, 0,
                    Mathf.Sin(t * Mathf.PI * 3f) * sinkingRoll * strength);
                body.position = sinkPosition;
                body.rotation = sinkRotation;
                transform.SetPositionAndRotation(sinkPosition, sinkRotation);
                yield return null;
            }
            body.position = controller.DockPosition;
            body.rotation = controller.DockRotation;
            transform.SetPositionAndRotation(controller.DockPosition, controller.DockRotation);
            body.isKinematic = wasKinematic;
            if (!wasKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            foreach (var c in enabledColliders) if (c != null) c.enabled = true;
            if (hadMotion) motion.enabled = true;
            Physics.SyncTransforms();
            RepairToFull();
            var camera = FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>();
            if (camera != null) camera.ShowRescueView();
            sinking = false;
            GameJamOcean.UI.GameMenus.ShowRescueLetter(firstDeathFree, chargedGold, applicableDestructionCost);
            // The rescue overlay keeps the world flowing but anchors this rigidbody.
            // Preserve the exact dock pose until it has fresh interpolation samples.
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            if (body != null) body.interpolation = previousInterpolation;
        }

        private void RecalculateStats()
        {
            currentMaximumHealth = ApplyPercentage(baseMaximumHealth, healthUpgradePercent);
            currentMaximumSpeed = ApplyPercentage(baseMaximumSpeed, speedUpgradePercent);
            currentAcceleration = ApplyPercentage(baseAcceleration, accelerationUpgradePercent);
            currentTurboCapacity = ApplyPercentage(
                baseTurboCapacity,
                turboCapacityUpgradePercent);
            currentTurboSpeedMultiplier = Mathf.Max(
                1f,
                ApplyPercentage(baseTurboSpeedMultiplier, turboPowerUpgradePercent));
            currentTurboAccelerationMultiplier = Mathf.Max(
                1f,
                ApplyPercentage(baseTurboAccelerationMultiplier, turboPowerUpgradePercent));
            currentTurboRechargePerSecond = ApplyPercentage(
                baseTurboRechargePerSecond,
                turboRechargeUpgradePercent);
        }

        private static float ApplyPercentage(float baseValue, float percentage)
        {
            return baseValue * Mathf.Max(0.1f, 1f + percentage / 100f);
        }

        private void OnValidate()
        {
            baseMaximumHealth = Mathf.Max(1f, baseMaximumHealth);
            baseMaximumSpeed = Mathf.Max(0.1f, baseMaximumSpeed);
            baseAcceleration = Mathf.Max(0.1f, baseAcceleration);
            baseTurboCapacity = Mathf.Max(0.1f, baseTurboCapacity);
            baseTurboSpeedMultiplier = Mathf.Max(1f, baseTurboSpeedMultiplier);
            baseTurboAccelerationMultiplier = Mathf.Max(1f, baseTurboAccelerationMultiplier);
            baseTurboRechargePerSecond = Mathf.Max(0f, baseTurboRechargePerSecond);
            turboRechargeDelay = Mathf.Max(0f, turboRechargeDelay);
            healthUpgradePercent = Mathf.Max(-90f, healthUpgradePercent);
            speedUpgradePercent = Mathf.Max(-90f, speedUpgradePercent);
            accelerationUpgradePercent = Mathf.Max(-90f, accelerationUpgradePercent);
            turboCapacityUpgradePercent = Mathf.Max(-90f, turboCapacityUpgradePercent);
            turboPowerUpgradePercent = Mathf.Max(-90f, turboPowerUpgradePercent);
            turboRechargeUpgradePercent = Mathf.Max(-90f, turboRechargeUpgradePercent);
            RecalculateStats();
        }
    }
}
