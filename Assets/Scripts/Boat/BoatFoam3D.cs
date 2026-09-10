using UnityEngine;
using UnityEngine.Rendering;

namespace GameJamOcean.Boat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoatController3D))]
    public sealed class BoatFoam3D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Material foamMaterial;
        [SerializeField] private Material contactFoamMaterial;
        [SerializeField] private Transform waterSurface;
        [Header("Waterline and Hull")]
        [SerializeField] private float surfaceOffset = 0.08f;
        [SerializeField] private Vector3 hullCenter;
        [SerializeField, Min(0.05f)] private float hullHalfWidth = 0.45f;
        [SerializeField, Min(0.05f)] private float hullHalfLength = 0.8f;
        [Header("Appearance")]
        [SerializeField] private Color foamColor = new(0.88f, 0.98f, 1f, 0.32f);
        [SerializeField] private Color wakeColor = new(0.48f, 0.82f, 1f, 0.42f);
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;
        [SerializeField, Min(0.02f)] private float particleSize = 0.25f;
        [SerializeField, Min(0.1f)] private float wakeLifetime = 1.8f;
        [SerializeField, Min(0f)] private float emissionRate = 25f;
        [SerializeField, Min(0.01f)] private float minimumSpeed = 0.15f;
        [SerializeField, Min(0f)] private float contactSurfaceOffset = 0.24f;
        [SerializeField, Min(0.02f)] private float contactBandWidth = 0.38f;
        [Header("Performance")]
        [SerializeField, Range(64, 512)] private int maximumParticles = 192;
        [SerializeField, Range(16, 48)] private int contactMeshSegments = 28;

        private BoatController3D boat;
        private Rigidbody body;
        private ParticleSystem particles;
        private Renderer waterRenderer;
        private MeshRenderer contactFoamRenderer;
        private Mesh contactFoamMesh;
        private MaterialPropertyBlock contactProperties;
        private float contactVisibility;
        private Vector3 previousCenter;
        private float emissionBudget;

        public void Configure(Material material, Material contactMaterial, Transform water, BoxCollider hull)
        {
            foamMaterial = material;
            contactFoamMaterial = contactMaterial;
            waterSurface = water;
            if (hull != null)
            {
                hullCenter = hull.center;
                hullHalfWidth = Mathf.Max(0.05f, hull.size.x * Mathf.Abs(transform.lossyScale.x) * 0.5f);
                hullHalfLength = Mathf.Max(0.05f, hull.size.z * Mathf.Abs(transform.lossyScale.z) * 0.5f);
            }
        }

        private void OnEnable()
        {
            boat = GetComponent<BoatController3D>();
            body = GetComponent<Rigidbody>();
            if (foamMaterial == null || waterSurface == null || body == null)
            {
                Debug.LogWarning("Configure boat foam material and water surface first.", this);
                enabled = false;
                return;
            }
            waterRenderer = waterSurface.GetComponent<Renderer>();
            if (particles == null) CreateParticles();
            if (contactFoamRenderer == null && contactFoamMaterial != null) CreateContactFoam();
            previousCenter = transform.TransformPoint(hullCenter);
            emissionBudget = 0f;
            particles.Play();
        }

        private void CreateParticles()
        {
            GameObject child = new("Boat Foam (Runtime)");
            child.transform.SetParent(transform, false);
            particles = child.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maximumParticles;
            main.startSpeed = 0f;
            main.gravityModifier = 0f;
            var emission = particles.emission;
            emission.enabled = false;
            var shape = particles.shape;
            shape.enabled = false;
            var colors = particles.colorOverLifetime;
            colors.enabled = true;
            Gradient fade = new();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.65f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colors.color = fade;
            var size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.6f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sharedMaterial = foamMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void CreateContactFoam()
        {
            GameObject child = new("Boat Contact Foam (Runtime)");
            child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = contactFoamMesh = BuildContactFoamMesh();
            contactFoamRenderer = child.AddComponent<MeshRenderer>();
            contactFoamRenderer.sharedMaterial = contactFoamMaterial;
            contactFoamRenderer.shadowCastingMode = ShadowCastingMode.Off;
            contactFoamRenderer.receiveShadows = false;
            contactFoamRenderer.lightProbeUsage = LightProbeUsage.Off;
            contactFoamRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            contactProperties = new MaterialPropertyBlock();
            contactFoamRenderer.enabled = false;
        }

        private Mesh BuildContactFoamMesh()
        {
            int segments = Mathf.Clamp(contactMeshSegments, 16, 48);
            const int rings = 4;
            Vector3[] vertices = new Vector3[segments * rings];
            Vector2[] uvs = new Vector2[vertices.Length];
            Color[] colors = new Color[vertices.Length];
            int[] triangles = new int[segments * (rings - 1) * 6];
            float[] padding = { -0.04f, contactBandWidth * .22f, contactBandWidth * .62f, contactBandWidth };
            Color[] ringColors =
            {
                new(.42f, .82f, 1f, .08f),
                new(foamColor.r, foamColor.g, foamColor.b, .78f),
                new(.82f, .95f, 1f, .42f),
                new(.45f, .82f, 1f, 0f)
            };

            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    float normalized = segment / (float)segments;
                    float angle = normalized * Mathf.PI * 2f;
                    float irregularity = Mathf.Sin(angle * 5f) * .025f + Mathf.Sin(angle * 9f + .7f) * .015f;
                    int index = ring * segments + segment;
                    vertices[index] = new Vector3(
                        Mathf.Cos(angle) * Mathf.Max(.05f, hullHalfWidth + padding[ring] + irregularity),
                        0f,
                        Mathf.Sin(angle) * Mathf.Max(.05f, hullHalfLength + padding[ring] + irregularity));
                    uvs[index] = new Vector2(normalized, ring / (float)(rings - 1));
                    colors[index] = ringColors[ring];
                }
            }

            int triangle = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    int inner = ring * segments + segment;
                    int innerNext = ring * segments + next;
                    int outer = (ring + 1) * segments + segment;
                    int outerNext = (ring + 1) * segments + next;
                    triangles[triangle++] = inner;
                    triangles[triangle++] = outerNext;
                    triangles[triangle++] = outer;
                    triangles[triangle++] = inner;
                    triangles[triangle++] = innerNext;
                    triangles[triangle++] = outerNext;
                }
            }

            Mesh mesh = new() { name = "Boat Contact Foam Mesh" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private float GetWaterTop()
        {
            return waterRenderer != null
                ? Mathf.Max(waterSurface.position.y, waterRenderer.bounds.max.y)
                : waterSurface.position.y;
        }

        private void UpdateContactFoam(Vector3 center, float waterTop, float speed)
        {
            if (contactFoamRenderer == null) return;
            bool moving = boat.enabled && intensity > 0f && speed >= minimumSpeed;
            contactVisibility = Mathf.MoveTowards(contactVisibility, moving ? intensity : 0f,
                Time.deltaTime * 4f);
            contactFoamRenderer.transform.SetPositionAndRotation(
                new Vector3(center.x, waterTop + contactSurfaceOffset, center.z),
                Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            contactFoamRenderer.enabled = contactVisibility > .005f;
            contactProperties ??= new MaterialPropertyBlock();
            contactFoamRenderer.GetPropertyBlock(contactProperties);
            contactProperties.SetFloat("_Opacity", Mathf.Clamp01(contactVisibility));
            contactFoamRenderer.SetPropertyBlock(contactProperties);
        }

        private void LateUpdate()
        {
            Vector3 center = transform.TransformPoint(hullCenter);
            if ((center - previousCenter).sqrMagnitude > 400f)
            {
                particles.Clear();
                previousCenter = center;
                emissionBudget = 0f;
            }
            float speed = boat.CurrentSpeed;
            float waterTop = GetWaterTop();
            UpdateContactFoam(center, waterTop, speed);
            if (speed < minimumSpeed || intensity <= 0f || !boat.enabled)
            {
                previousCenter = center;
                emissionBudget = 0f;
                return;
            }
            float normalizedSpeed = Mathf.Clamp01(speed / Mathf.Max(0.1f, boat.MaximumSpeed));
            // A small baseline makes the first foam visible immediately after the boat starts moving.
            float strength = Mathf.Lerp(0.35f, 1.7f, normalizedSpeed);
            emissionBudget += emissionRate * strength * intensity * Time.deltaTime;
            int count = Mathf.Min(32, Mathf.FloorToInt(emissionBudget));
            emissionBudget = Mathf.Min(1f, emissionBudget - count);
            Vector3 forward = boat.NavigationForward;
            if (Vector3.Dot(body.linearVelocity, forward) < 0f) forward = -forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 sample = Vector3.Lerp(previousCenter, center, (i + 1f) / count);
                sample.y = waterTop + surfaceOffset;
                float wakeSide = Random.value < 0.5f ? -1f : 1f;
                // Overlapping particles remain in world space to form a continuous textured wake.
                Emit(sample - forward * hullHalfLength
                    + right * Random.Range(-hullHalfWidth * .85f, hullHalfWidth * .85f),
                    right * wakeSide * 0.08f, wakeLifetime, 1.65f, strength, wakeColor);
            }
            previousCenter = center;
        }

        private void Emit(Vector3 position, Vector3 velocity, float lifetime, float size, float strength,
            Color baseColor)
        {
            Color color = baseColor;
            color.a *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(strength));
            var particle = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startLifetime = lifetime * Random.Range(0.8f, 1.1f),
                startSize = particleSize * size * Random.Range(0.7f, 1.3f),
                startColor = color,
                rotation = Random.Range(0f, 360f)
            };
            particles.Emit(particle, 1);
        }

        private void OnDisable()
        {
            if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (contactFoamRenderer != null) contactFoamRenderer.enabled = false;
            contactVisibility = 0f;
        }

        private void OnDestroy()
        {
            if (particles != null) Destroy(particles.gameObject);
            if (contactFoamMesh != null) Destroy(contactFoamMesh);
        }
    }
}
