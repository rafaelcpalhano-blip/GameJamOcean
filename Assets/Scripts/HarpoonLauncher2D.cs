using UnityEngine;
using UnityEngine.InputSystem;
using System;

namespace GameJamOcean.Weapons
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncher2D : MonoBehaviour
    {
        public static event Action<Vector2, Vector2> HarpoonFired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEvents() => HarpoonFired = null;
        [Header("Input")]
        [SerializeField] private InputActionReference attackAction;

        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private HarpoonProjectile2D harpoonPrefab;

        [Header("Firing")]
        [SerializeField, Min(0.01f)] private float fireCooldown = 0.4f;
        [Tooltip("Additional recovery time after each shot, added to Fire Cooldown.")]
        [SerializeField, Min(0f)] private float additionalShotDelay = 0.25f;
        [SerializeField, Min(0f)] private float fallbackSpawnDistance = 0.35f;
        [SerializeField] private LayerMask blockedPointerLayers;
        [SerializeField, Min(1)] private int baseProjectileCount = 1;
        [Tooltip("Flight speed applied only to the permanent level 4 double harpoon.")]
        [SerializeField, Range(0.1f, 1f)] private float level4FlightSpeedMultiplier = 0.8f;

        private bool enabledAttackAction;
        private float nextFireTime;
        private float doubleShotUntil;
        private float doubleShotAngle = 18f;
        private float doubleShotDuration;
        private float projectileSpeedMultiplier = 1f;
        public float DoubleShotRemaining => Mathf.Max(0f, doubleShotUntil - Time.time);
        public float DoubleShotNormalized => doubleShotDuration > 0f
            ? Mathf.Clamp01(DoubleShotRemaining / doubleShotDuration) : 0f;

        public void ActivateDoubleShot(float duration, float angle)
        {
            doubleShotDuration = Mathf.Max(.1f, duration);
            doubleShotUntil = Mathf.Max(doubleShotUntil, Time.time + doubleShotDuration);
            doubleShotAngle = Mathf.Clamp(angle, 1f, 60f);
        }

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void OnEnable()
        {
            if (attackAction == null)
            {
                return;
            }

            enabledAttackAction = !attackAction.action.enabled;
            if (enabledAttackAction)
            {
                attackAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (enabledAttackAction && attackAction != null)
            {
                attackAction.action.Disable();
            }

            enabledAttackAction = false;
        }

        private void Update()
        {
            bool pressed = attackAction != null
                ? attackAction.action.IsPressed()
                : Mouse.current != null && Mouse.current.leftButton.isPressed;

            if (pressed)
            {
                TryFire();
            }
        }

        public void EquipHarpoon(HarpoonProjectile2D prefab)
        {
            if (prefab != null) harpoonPrefab = prefab;
        }

        public void SetBaseProjectileCount(int count) => baseProjectileCount = Mathf.Clamp(count, 1, 2);

        public void ApplyUpgradeLevel(int level)
        {
            bool permanentDoubleShot = level >= 4;
            baseProjectileCount = permanentDoubleShot ? 2 : 1;
            projectileSpeedMultiplier = permanentDoubleShot ? level4FlightSpeedMultiplier : 1f;
        }

        private void TryFire()
        {
            if (GameJamOcean.UI.GameMenus.BlocksGameplay) return;
            if (Time.time < nextFireTime || harpoonPrefab == null || aimCamera == null || Mouse.current == null)
            {
                return;
            }

            Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPosition = aimCamera.ScreenToWorldPoint(
                new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, -aimCamera.transform.position.z));

            if (blockedPointerLayers.value != 0 && Physics2D.OverlapPoint(
                    mouseWorldPosition,
                    blockedPointerLayers) != null)
            {
                return;
            }

            Vector2 origin = launchPoint != null
                ? launchPoint.position
                : transform.position;
            Vector2 direction = (Vector2)mouseWorldPosition - origin;

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            direction.Normalize();
            Vector2 spawnPosition = launchPoint != null
                ? origin
                : origin + direction * fallbackSpawnDistance;

            int projectileCount = baseProjectileCount * (Time.time < doubleShotUntil ? 2 : 1);
            if (projectileCount > 1)
            {
                float startAngle = -doubleShotAngle * (projectileCount - 1) * .5f;
                for (int i = 0; i < projectileCount; i++)
                    LaunchHarpoon(Rotate(direction, startAngle + doubleShotAngle * i), spawnPosition);
            }
            else LaunchHarpoon(direction, spawnPosition);
            GameJamOcean.Audio.GameAudio.Instance?.PlayHarpoon();
            nextFireTime = Time.time + fireCooldown + Mathf.Max(0f, additionalShotDelay);
        }

        private void LaunchHarpoon(Vector2 direction, Vector2 spawnPosition)
        {
            HarpoonProjectile2D harpoon = Instantiate(harpoonPrefab, spawnPosition, Quaternion.identity);
            harpoon.Launch(direction, gameObject, projectileSpeedMultiplier);
            HarpoonFired?.Invoke(spawnPosition, direction);
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine).normalized;
        }

        private void OnValidate()
        {
            fireCooldown = Mathf.Max(0.01f, fireCooldown);
            fallbackSpawnDistance = Mathf.Max(0f, fallbackSpawnDistance);
            baseProjectileCount = Mathf.Clamp(baseProjectileCount, 1, 2);
            level4FlightSpeedMultiplier = Mathf.Clamp(level4FlightSpeedMultiplier, 0.1f, 1f);
        }
    }
}
