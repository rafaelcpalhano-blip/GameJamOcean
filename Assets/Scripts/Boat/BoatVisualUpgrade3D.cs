using GameJamOcean.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    public sealed class BoatVisualUpgrade3D : MonoBehaviour
    {
        private GameObject[] variants;
        private BoxCollider physicalCollider;
        private Vector3 originalColliderCenter;
        private Vector3 originalColliderSize;
        private Vector3[] variantColliderCenters;
        private Vector3[] variantColliderSizes;
        private bool[] variantColliderValid;
        private Vector3[] variantRestPositions;
        private Vector3 expectedColliderCenter;
        private Vector3 expectedColliderSize;
        private bool hasExpectedCollider;
        public int CurrentVesselLevel { get; private set; } = 1;

        public static void ConfigureScene(Scene scene)
        {
            BoatController3D boat = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                boat ??= root.GetComponentInChildren<BoatController3D>(true);
            }
            if (boat == null || boat.GetComponent<BoatVisualUpgrade3D>() != null) return;
            BoatVisualUpgradeSettings settings = Resources.Load<BoatVisualUpgradeSettings>("BoatVisualUpgradeSettings");
            if (settings == null || !settings.IsValid)
            {
                Debug.LogError("BoatVisualUpgradeSettings is missing its boat1, boat2 or boat3 prefab reference.");
                return;
            }
            var component = boat.gameObject.AddComponent<BoatVisualUpgrade3D>();
            component.Setup(settings.Prefabs, scene);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        private void Setup(GameObject[] prefabs, Scene scene)
        {
            physicalCollider = GetComponent<BoxCollider>();
            if (physicalCollider != null)
            {
                originalColliderCenter = physicalCollider.center;
                originalColliderSize = physicalCollider.size;
            }
            // Hide legacy scene instances while they still exist. They can now be safely deleted.
            Transform legacyBoat1 = FindChild(transform, "boat-speed-b") ?? FindChild(transform, "boat1");
            if (legacyBoat1 != null) legacyBoat1.gameObject.SetActive(false);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name.Equals("boat2", System.StringComparison.OrdinalIgnoreCase)
                    || root.name.Equals("boat3", System.StringComparison.OrdinalIgnoreCase))
                    root.SetActive(false);

            var holder = new GameObject("Boat Visual Variants").transform;
            holder.SetParent(transform, false);
            holder.localPosition = Vector3.zero;
            holder.localRotation = Quaternion.identity;
            holder.localScale = Vector3.one;
            variants = new GameObject[prefabs.Length];
            variantColliderCenters = new Vector3[prefabs.Length];
            variantColliderSizes = new Vector3[prefabs.Length];
            variantColliderValid = new bool[prefabs.Length];
            variantRestPositions = new Vector3[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                variants[i] = Instantiate(prefabs[i], holder);
            }
            for (int i = 0; i < variants.Length; i++)
            {
                GameObject variant = variants[i];
                variant.transform.SetParent(holder, false);
                // The cargo hull needs a tiny extra waterline clearance while pitching under turbo.
                variantRestPositions[i] = Vector3.up * (i == 2 ? .12f : 0f);
                variant.transform.localPosition = variantRestPositions[i];
                variant.transform.localRotation = Quaternion.identity;
                variant.transform.localScale = Vector3.one;
                foreach (Collider collider in variant.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    Destroy(collider);
                }
                foreach (Rigidbody body in variant.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
                CachePhysicalCollider(i);
            }
            BoatWaterMotion3D waterMotion = GetComponent<BoatWaterMotion3D>();
            if (waterMotion != null)
            {
                waterMotion.Configure(holder);
                waterMotion.enabled = true;
            }
            if (GameProgress.HasInstance) GameProgress.Instance.UpgradesChanged += Refresh;
            if (GameProgress.HasInstance) GameProgress.Instance.BoatSelectionChanged += HandleBoatSelection;
            Refresh();
        }

        private void Refresh()
        {
            if (variants == null) return;
            int level = GameProgress.HasInstance ? GameProgress.Instance.SelectedBoatLevel : 1;
            CurrentVesselLevel = Mathf.Clamp(level, 1, variants.Length);
            for (int i = 0; i < variants.Length; i++) if (variants[i] != null) variants[i].SetActive(i == Mathf.Clamp(level - 1, 0, variants.Length - 1));
            RefreshPhysicalCollider();
        }

        private void HandleBoatSelection(int unusedLevel) => Refresh();

        public float RockDamagePercent => CurrentVesselLevel switch { 2 => 35f, 3 => 25f, _ => 40f };
        public float SharkDamagePercent => CurrentVesselLevel switch { 2 => 25f, 3 => 15f, _ => 30f };

        private void RefreshPhysicalCollider()
        {
            if (physicalCollider == null) return;
            if (CurrentVesselLevel == 1 || variants == null || CurrentVesselLevel > variants.Length
                || variants[CurrentVesselLevel - 1] == null)
            {
                SetExpectedCollider(originalColliderCenter, originalColliderSize);
                return;
            }
            int index = CurrentVesselLevel - 1;
            if (variantColliderValid == null || index >= variantColliderValid.Length || !variantColliderValid[index])
                return;
            SetExpectedCollider(variantColliderCenters[index], variantColliderSizes[index]);
        }

        private void SetExpectedCollider(Vector3 center, Vector3 size)
        {
            expectedColliderCenter = center;
            expectedColliderSize = size;
            hasExpectedCollider = true;
            ApplyExpectedCollider();
        }

        private void LateUpdate()
        {
            ApplyExpectedCollider();
            ApplyExpectedVisualPoses();
        }

        private void ApplyExpectedCollider()
        {
            if (!hasExpectedCollider || physicalCollider == null) return;
            if (physicalCollider.center != expectedColliderCenter) physicalCollider.center = expectedColliderCenter;
            if (physicalCollider.size != expectedColliderSize) physicalCollider.size = expectedColliderSize;
        }

        private void ApplyExpectedVisualPoses()
        {
            if (variants == null || variantRestPositions == null) return;
            for (int i = 0; i < variants.Length && i < variantRestPositions.Length; i++)
            {
                GameObject variant = variants[i];
                if (variant == null) continue;
                Transform visual = variant.transform;
                if (visual.localPosition != variantRestPositions[i])
                    visual.localPosition = variantRestPositions[i];
                if (visual.localRotation != Quaternion.identity)
                    visual.localRotation = Quaternion.identity;
                if (visual.localScale != Vector3.one)
                    visual.localScale = Vector3.one;
            }
        }

        private void CachePhysicalCollider(int index)
        {
            Renderer[] renderers = variants[index].GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return;
            Bounds local = default;
            bool hasLocalBounds = false;
            foreach (Renderer renderer in renderers)
            {
                // Renderer.bounds is an axis-aligned world box. Converting that box back to
                // boat space inflates the hull whenever a saved boat returns at an angle.
                // Transform the renderer's own local bounds instead so the result is stable
                // for every boat rotation and every scene reload.
                Bounds bounds = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 rendererPoint = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f,
                            (corner & 2) == 0 ? -1f : 1f,
                            (corner & 4) == 0 ? -1f : 1f));
                    Vector3 boatPoint = transform.InverseTransformPoint(
                        renderer.transform.TransformPoint(rendererPoint));
                    if (!hasLocalBounds)
                    {
                        local = new Bounds(boatPoint, Vector3.zero);
                        hasLocalBounds = true;
                    }
                    else
                    {
                        local.Encapsulate(boatPoint);
                    }
                }
            }
            if (!hasLocalBounds) return;
            variantColliderCenters[index] = new Vector3(local.center.x, originalColliderCenter.y, local.center.z);
            variantColliderSizes[index] = new Vector3(local.size.x, originalColliderSize.y, local.size.z);
            variantColliderValid[index] = true;
        }

        private void OnDestroy()
        {
            if (GameProgress.HasInstance) GameProgress.Instance.UpgradesChanged -= Refresh;
            if (GameProgress.HasInstance) GameProgress.Instance.BoatSelectionChanged -= HandleBoatSelection;
        }
    }
}
