using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJamOcean.Weapons
{
    [DisallowMultipleComponent]
    public sealed class HarpoonLauncher2D : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference attackAction;

        [Header("References")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private Transform launchPoint;
        [SerializeField] private HarpoonProjectile2D harpoonPrefab;

        [Header("Firing")]
        [SerializeField, Min(0.01f)] private float fireCooldown = 0.4f;
        [SerializeField, Min(0f)] private float fallbackSpawnDistance = 0.35f;
        [SerializeField] private LayerMask blockedPointerLayers;

        private bool enabledAttackAction;
        private float nextFireTime;

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
                ? attackAction.action.WasPressedThisFrame()
                : Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

            if (pressed)
            {
                TryFire();
            }
        }

        public void EquipHarpoon(HarpoonProjectile2D prefab)
        {
            if (prefab != null) harpoonPrefab = prefab;
        }

        private void TryFire()
        {
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

            HarpoonProjectile2D harpoon = Instantiate(
                harpoonPrefab,
                spawnPosition,
                Quaternion.identity);
            harpoon.Launch(direction, gameObject);
            nextFireTime = Time.time + fireCooldown;
        }

        private void OnValidate()
        {
            fireCooldown = Mathf.Max(0.01f, fireCooldown);
            fallbackSpawnDistance = Mathf.Max(0f, fallbackSpawnDistance);
        }
    }
}
