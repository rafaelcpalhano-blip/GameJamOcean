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
            for (int i = 0; i < prefabs.Length; i++)
            {
                variants[i] = Instantiate(prefabs[i], holder);
            }
            for (int i = 0; i < variants.Length; i++)
            {
                GameObject variant = variants[i];
                variant.transform.SetParent(holder, false);
                // The cargo hull needs a tiny extra waterline clearance while pitching under turbo.
                variant.transform.localPosition = Vector3.up * (i == 2 ? .12f : 0f);
                variant.transform.localRotation = Quaternion.identity;
                variant.transform.localScale = Vector3.one;
                foreach (Collider collider in variant.GetComponentsInChildren<Collider>(true)) Destroy(collider);
                foreach (Rigidbody body in variant.GetComponentsInChildren<Rigidbody>(true)) Destroy(body);
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
                physicalCollider.center = originalColliderCenter;
                physicalCollider.size = originalColliderSize;
                return;
            }
            Renderer[] renderers = variants[CurrentVesselLevel - 1].GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return;
            Bounds local = new(transform.InverseTransformPoint(renderers[0].bounds.center), Vector3.zero);
            foreach (Renderer renderer in renderers)
            {
                Bounds bounds = renderer.bounds;
                for (int corner = 0; corner < 8; corner++)
                    local.Encapsulate(transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1,
                            (corner & 4) == 0 ? -1 : 1))));
            }
            physicalCollider.center = new Vector3(local.center.x, originalColliderCenter.y, local.center.z);
            physicalCollider.size = new Vector3(local.size.x, originalColliderSize.y, local.size.z);
        }

        private void OnDestroy()
        {
            if (GameProgress.HasInstance) GameProgress.Instance.UpgradesChanged -= Refresh;
            if (GameProgress.HasInstance) GameProgress.Instance.BoatSelectionChanged -= HandleBoatSelection;
        }
    }
}
