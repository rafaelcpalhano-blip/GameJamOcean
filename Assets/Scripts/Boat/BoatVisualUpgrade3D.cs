using GameJamOcean.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    public sealed class BoatVisualUpgrade3D : MonoBehaviour
    {
        private GameObject[] variants;
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
                variants[i] = Instantiate(prefabs[i], holder);
            for (int i = 0; i < variants.Length; i++)
            {
                GameObject variant = variants[i];
                variant.transform.SetParent(holder, false);
                // The cargo hull needs a tiny extra waterline clearance while pitching under turbo.
                variant.transform.localPosition = Vector3.up * (i == 2 ? .08f : 0f);
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
            Refresh();
        }

        private void Refresh()
        {
            if (variants == null) return;
            int level = GameProgress.HasInstance ? GameProgress.Instance.GetLevel(UpgradeKind.BoatHull) : 1;
            CurrentVesselLevel = Mathf.Clamp(level, 1, variants.Length);
            for (int i = 0; i < variants.Length; i++) if (variants[i] != null) variants[i].SetActive(i == Mathf.Clamp(level - 1, 0, variants.Length - 1));
        }

        private void OnDestroy()
        {
            if (GameProgress.HasInstance) GameProgress.Instance.UpgradesChanged -= Refresh;
        }
    }
}
