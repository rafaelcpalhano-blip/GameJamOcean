using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameJamOcean.Boat;

namespace GameJamOcean.World
{
    public static class OceanRockSetup
    {
        private static bool IsRock(Transform t) => t.name.Equals("Rock", StringComparison.OrdinalIgnoreCase)
            || t.name.Equals("Rocks", StringComparison.OrdinalIgnoreCase)
            || t.name.StartsWith("Rock (", StringComparison.OrdinalIgnoreCase)
            || t.name.StartsWith("Rock(Clone)", StringComparison.OrdinalIgnoreCase);

        public static void Configure(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var rock in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsRock(rock)) continue;
                bool nested = false;
                for (var parent = rock.parent; parent != null; parent = parent.parent)
                    if (IsRock(parent)) { nested = true; break; }
                if (nested) continue;
                var colliders = rock.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                {
                    foreach (var mesh in rock.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mesh.sharedMesh == null) continue;
                        // Local bounds do not require CPU-readable vertices or runtime mesh cooking.
                        // Attach on the mesh object so its rotation/scale also applies to the box.
                        Bounds bounds = mesh.sharedMesh.bounds;
                        var collider = mesh.gameObject.AddComponent<BoxCollider>();
                        collider.center = bounds.center;
                        collider.size = new Vector3(
                            Mathf.Max(.01f, bounds.size.x),
                            Mathf.Max(.01f, bounds.size.y),
                            Mathf.Max(.01f, bounds.size.z));
                    }
                    colliders = rock.GetComponentsInChildren<Collider>(true);
                }
                if (colliders.Length == 0) { Debug.LogWarning($"Rocha sem malha/colisor: {rock.name}", rock); continue; }
                var clock = rock.GetComponent<BoatDamageObstacle3D>() ?? rock.gameObject.AddComponent<BoatDamageObstacle3D>();
                clock.ConfigureRock(clock);
                foreach (var collider in colliders)
                {
                    var damage = collider.GetComponent<BoatDamageObstacle3D>() ?? collider.gameObject.AddComponent<BoatDamageObstacle3D>();
                    damage.ConfigureRock(clock);
                }
            }
        }
    }

    /// <summary>Keeps imported wind prefabs intact while making scene instances easier to read.</summary>
    [DisallowMultipleComponent]
    public sealed class WindVfxEnhancer3D : MonoBehaviour
    {
        [Header("Wind visibility")]
        [SerializeField, Min(.1f)] private float particleAmount = 3f;
        [SerializeField, Min(.1f)] private float visibility = 2.5f;
        private bool applied;
        private readonly List<Material> webMaterials = new();

        public static void ConfigureScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                bool supported = candidate.name.StartsWith("VFX_Wind_Flows_01")
                    || candidate.name.StartsWith("VFX_Wind_Flows_Trails_01");
                if (!supported || candidate.GetComponentInParent<WindVfxEnhancer3D>() != null) continue;
                candidate.gameObject.AddComponent<WindVfxEnhancer3D>();
            }
        }

        private void Start()
        {
            if (applied) return;
            applied = true;
            GetVisibleHeightRange(out float waterHeight, out float highestBuilding);
            foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            {
                var emission = particles.emission;
                ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
                rate.curveMultiplier *= particleAmount;
                emission.rateOverTime = rate;

                var main = particles.main;
                main.useUnscaledTime = true;
                main.maxParticles = Mathf.CeilToInt(main.maxParticles * particleAmount);
                ParticleSystem.MinMaxGradient color = main.startColor;
                Color minimum = color.colorMin;
                Color maximum = color.colorMax;
                minimum.a = Mathf.Clamp01(minimum.a * visibility);
                maximum.a = Mathf.Clamp01(maximum.a * visibility);
                color.colorMin = minimum;
                color.colorMax = maximum;
                main.startColor = color;

                var shape = particles.shape;
                if (shape.enabled && shape.shapeType == ParticleSystemShapeType.Box)
                {
                    float centerHeight = (waterHeight + highestBuilding) * .5f;
                    Vector3 bottomLocal = particles.transform.InverseTransformPoint(
                        new Vector3(particles.transform.position.x, waterHeight, particles.transform.position.z));
                    Vector3 topLocal = particles.transform.InverseTransformPoint(
                        new Vector3(particles.transform.position.x, highestBuilding, particles.transform.position.z));
                    Vector3 shapePosition = shape.position;
                    shapePosition.y = (bottomLocal.y + topLocal.y) * .5f;
                    shape.position = shapePosition;
                    Vector3 shapeScale = shape.scale;
                    shapeScale.y = Mathf.Max(.5f, Mathf.Abs(topLocal.y - bottomLocal.y));
                    shape.scale = shapeScale;
                }
                ApplyWebSafeMaterial(particles);
                if (!particles.isPlaying) particles.Play(true);
            }
        }

        private void ApplyWebSafeMaterial(ParticleSystem particles)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            Material template = Resources.Load<Material>("WindWebFallback");
            if (renderer == null || template == null) return;
            Material original = renderer.sharedMaterial;
            Material webMaterial = new Material(template) { name = "Wind URP Web Material (Runtime)" };
            Texture texture = null;
            if (original != null)
            {
                if (original.HasProperty("_MainTexture")) texture = original.GetTexture("_MainTexture");
                if (texture == null && original.HasProperty("_CutoutTexture")) texture = original.GetTexture("_CutoutTexture");
                if (texture == null && original.HasProperty("_OpacityTexture")) texture = original.GetTexture("_OpacityTexture");
            }
            if (texture != null) webMaterial.SetTexture("_BaseMap", texture);
            renderer.sharedMaterial = webMaterial;
            webMaterials.Add(webMaterial);
#endif
        }

        private void OnDestroy()
        {
            foreach (Material material in webMaterials)
                if (material != null) Destroy(material);
            webMaterials.Clear();
        }

        private static void GetVisibleHeightRange(out float waterHeight, out float highestBuilding)
        {
            waterHeight = 0f;
            bool foundWater = false;
            float highest = float.NegativeInfinity;

            foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!foundWater && candidate.name == "Water"
                    && candidate.TryGetComponent<Renderer>(out var waterRenderer))
                {
                    waterHeight = waterRenderer.bounds.center.y;
                    foundWater = true;
                }

                if (candidate.name != "Aldeias") continue;
                foreach (Renderer building in candidate.GetComponentsInChildren<Renderer>(true))
                    if (building is not ParticleSystemRenderer)
                        highest = Mathf.Max(highest, building.bounds.max.y);
            }

            if (!foundWater)
            {
                BoatController3D boat = FindFirstObjectByType<BoatController3D>();
                if (boat != null) waterHeight = boat.transform.position.y;
            }
            highestBuilding = float.IsNegativeInfinity(highest) ? waterHeight + 10f : highest;
            highestBuilding = Mathf.Max(waterHeight + .5f, highestBuilding);
        }
    }
}
