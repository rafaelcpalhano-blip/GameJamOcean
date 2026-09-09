using System.Collections;
using System.Collections.Generic;
using GameJamOcean.Interaction;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameJamOcean.Spawning
{
    [DisallowMultipleComponent]
    public sealed class DivePointSpawnManager3D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DivePointInteractable3D divePointPrefab;
        [SerializeField] private List<Transform> spawnPoints = new();
        [SerializeField] private Transform activeDivePointsParent;

        [Header("Active Points")]
        [SerializeField, Min(1)] private int simultaneousDivePoints = 10;

        [Header("Replacement Effect")]
        [SerializeField, Min(0.1f)] private float fadeDuration = 1.25f;
        [SerializeField, Min(0f)] private float replacementDelay = 0.2f;

        [Header("Runtime")]
        [SerializeField] private int activePointCount;
        [SerializeField] private int lastReplacedPoint = -1;

        private static readonly List<int> PersistentActiveIndices = new();
        private static int persistentPointCount = -1;
        private static int pendingUsedPoint = -1;

        private readonly Dictionary<int, GameObject> instancesByPoint = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetRuntimeState()
        {
            PersistentActiveIndices.Clear();
            persistentPointCount = -1;
            pendingUsedPoint = -1;
        }

        private void Start()
        {
            spawnPoints.RemoveAll(point => point == null);
            if (divePointPrefab == null || spawnPoints.Count == 0)
            {
                Debug.LogWarning(
                    $"{nameof(DivePointSpawnManager3D)} needs a prefab and spawn points.",
                    this);
                return;
            }

            EnsurePersistentSelection();
            foreach (int pointIndex in PersistentActiveIndices)
            {
                SpawnAt(pointIndex);
            }

            activePointCount = instancesByPoint.Count;
            if (pendingUsedPoint >= 0 && instancesByPoint.TryGetValue(
                    pendingUsedPoint, out GameObject usedInstance))
            {
                int usedPoint = pendingUsedPoint;
                pendingUsedPoint = -1;
                StartCoroutine(FadeAndReplace(usedPoint, usedInstance));
            }
            else
            {
                pendingUsedPoint = -1;
            }
        }

        public static void MarkDivePointUsed(int pointIndex)
        {
            pendingUsedPoint = pointIndex;
        }

        public void Configure(
            DivePointInteractable3D prefab,
            List<Transform> points,
            Transform instancesParent,
            int activeAmount)
        {
            divePointPrefab = prefab;
            spawnPoints = points;
            activeDivePointsParent = instancesParent;
            simultaneousDivePoints = Mathf.Max(1, activeAmount);
        }

        private void EnsurePersistentSelection()
        {
            int desiredAmount = Mathf.Min(simultaneousDivePoints, spawnPoints.Count);
            if (persistentPointCount != spawnPoints.Count)
            {
                PersistentActiveIndices.Clear();
                persistentPointCount = spawnPoints.Count;
                pendingUsedPoint = -1;
            }

            PersistentActiveIndices.RemoveAll(index => index < 0 || index >= spawnPoints.Count);
            while (PersistentActiveIndices.Count < desiredAmount)
            {
                int newIndex = GetRandomUnusedIndex();
                if (newIndex < 0)
                {
                    break;
                }

                PersistentActiveIndices.Add(newIndex);
            }

            while (PersistentActiveIndices.Count > desiredAmount)
            {
                PersistentActiveIndices.RemoveAt(PersistentActiveIndices.Count - 1);
            }
        }

        private void SpawnAt(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= spawnPoints.Count || spawnPoints[pointIndex] == null)
            {
                return;
            }

            Transform point = spawnPoints[pointIndex];
            DivePointInteractable3D instance = Instantiate(
                divePointPrefab,
                point.position,
                point.rotation,
                activeDivePointsParent);
            instance.name = $"LifeguardDivePoint3D_{pointIndex + 1:00}";

            SpawnedDivePoint3D marker = instance.GetComponent<SpawnedDivePoint3D>();
            if (marker == null)
            {
                marker = instance.gameObject.AddComponent<SpawnedDivePoint3D>();
            }

            marker.Configure(pointIndex);
            instancesByPoint[pointIndex] = instance.gameObject;
        }

        private IEnumerator FadeAndReplace(int usedPointIndex, GameObject usedInstance)
        {
            DisableInteraction(usedInstance);
            List<FadingMaterial> fadingMaterials = PrepareFadeMaterials(usedInstance);
            Vector3 initialScale = usedInstance.transform.localScale;
            float elapsed = 0f;

            while (elapsed < fadeDuration && usedInstance != null)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                float alphaMultiplier = 1f - Mathf.SmoothStep(0f, 1f, progress);

                foreach (FadingMaterial fadingMaterial in fadingMaterials)
                {
                    fadingMaterial.SetAlpha(alphaMultiplier);
                }

                usedInstance.transform.localScale = initialScale * Mathf.Lerp(1f, 0.92f, progress);
                yield return null;
            }

            if (usedInstance != null)
            {
                Destroy(usedInstance);
            }

            instancesByPoint.Remove(usedPointIndex);
            PersistentActiveIndices.Remove(usedPointIndex);
            lastReplacedPoint = usedPointIndex;

            if (replacementDelay > 0f)
            {
                yield return new WaitForSeconds(replacementDelay);
            }

            int replacementIndex = GetRandomUnusedIndex(usedPointIndex);
            if (replacementIndex >= 0)
            {
                PersistentActiveIndices.Add(replacementIndex);
                SpawnAt(replacementIndex);
            }

            activePointCount = instancesByPoint.Count;
        }

        public void ReplaceDivePointDestroyedByTornado(int pointIndex, GameObject destroyedInstance)
        {
            if (pointIndex < 0 || !instancesByPoint.TryGetValue(pointIndex, out GameObject current)
                || current != destroyedInstance) return;
            StartCoroutine(ReplaceTornadoDestroyedPoint(pointIndex, destroyedInstance));
        }

        private IEnumerator ReplaceTornadoDestroyedPoint(int pointIndex, GameObject destroyedInstance)
        {
            if (destroyedInstance != null) Destroy(destroyedInstance);
            instancesByPoint.Remove(pointIndex);
            PersistentActiveIndices.Remove(pointIndex);
            lastReplacedPoint = pointIndex;
            activePointCount = instancesByPoint.Count;
            if (replacementDelay > 0f) yield return new WaitForSeconds(replacementDelay);
            int replacementIndex = GetRandomUnusedIndex(pointIndex);
            if (replacementIndex >= 0)
            {
                PersistentActiveIndices.Add(replacementIndex);
                SpawnAt(replacementIndex);
            }
            activePointCount = instancesByPoint.Count;
        }

        private int GetRandomUnusedIndex(int excludedIndex = -1)
        {
            List<int> availableIndices = new();
            for (int index = 0; index < spawnPoints.Count; index++)
            {
                if (index != excludedIndex && spawnPoints[index] != null
                    && !PersistentActiveIndices.Contains(index))
                {
                    availableIndices.Add(index);
                }
            }

            return availableIndices.Count > 0
                ? availableIndices[Random.Range(0, availableIndices.Count)]
                : -1;
        }

        private static void DisableInteraction(GameObject target)
        {
            DivePointInteractable3D interaction = target.GetComponent<DivePointInteractable3D>();
            if (interaction != null)
            {
                interaction.enabled = false;
            }

            foreach (Collider collider in target.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
        }

        private static List<FadingMaterial> PrepareFadeMaterials(GameObject target)
        {
            List<FadingMaterial> materials = new();
            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(true))
            {
                Material[] rendererMaterials = targetRenderer.materials;
                foreach (Material material in rendererMaterials)
                {
                    ConfigureTransparentMaterial(material);
                    materials.Add(new FadingMaterial(material));
                }
            }

            return materials;
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void OnValidate()
        {
            simultaneousDivePoints = Mathf.Max(1, simultaneousDivePoints);
            fadeDuration = Mathf.Max(0.1f, fadeDuration);
            replacementDelay = Mathf.Max(0f, replacementDelay);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.85f, 0.8f);
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                }
            }
        }

        private sealed class FadingMaterial
        {
            private readonly Material material;
            private readonly string colorProperty;
            private readonly Color initialColor;

            public FadingMaterial(Material targetMaterial)
            {
                material = targetMaterial;
                colorProperty = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                initialColor = material.HasProperty(colorProperty)
                    ? material.GetColor(colorProperty)
                    : Color.white;
            }

            public void SetAlpha(float multiplier)
            {
                if (material == null || !material.HasProperty(colorProperty))
                {
                    return;
                }

                Color color = initialColor;
                color.a *= multiplier;
                material.SetColor(colorProperty, color);
            }
        }
    }
}
