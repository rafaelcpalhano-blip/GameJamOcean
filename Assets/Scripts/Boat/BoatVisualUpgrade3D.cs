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
            GameObject boat2 = null, boat3 = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                boat ??= root.GetComponentInChildren<BoatController3D>(true);
                if (root.name.Equals("boat2", System.StringComparison.OrdinalIgnoreCase)) boat2 = root;
                if (root.name.Equals("boat3", System.StringComparison.OrdinalIgnoreCase)) boat3 = root;
            }
            if (boat == null || boat.GetComponent<BoatVisualUpgrade3D>() != null) return;
            Transform boat1 = FindChild(boat.transform, "boat-speed-b");
            if (boat1 == null || boat2 == null || boat3 == null)
            {
                Debug.LogWarning("Boat visual upgrades need boat-speed-b, boat2 and boat3 in OceanScene_3D.");
                return;
            }
            var component = boat.gameObject.AddComponent<BoatVisualUpgrade3D>();
            component.Setup(boat1.gameObject, boat2, boat3);
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name.Equals(name, System.StringComparison.OrdinalIgnoreCase)) return child;
            return null;
        }

        private void Setup(GameObject boat1, GameObject boat2, GameObject boat3)
        {
            var holder = new GameObject("Boat Visual Variants").transform;
            holder.SetParent(transform, false);
            holder.SetPositionAndRotation(boat1.transform.position, boat1.transform.rotation);
            holder.localScale = boat1.transform.lossyScale;
            variants = new[] { boat1, boat2, boat3 };
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
            GetComponent<BoatWaterMotion3D>()?.Configure(holder);
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
