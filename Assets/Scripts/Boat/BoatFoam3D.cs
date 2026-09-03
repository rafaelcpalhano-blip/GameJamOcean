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
        [SerializeField] private Transform waterSurface;
        [Header("Waterline and Hull")]
        [SerializeField] private float surfaceOffset = 0.08f;
        [SerializeField] private Vector3 hullCenter;
        [SerializeField, Min(0.05f)] private float hullHalfWidth = 0.45f;
        [SerializeField, Min(0.05f)] private float hullHalfLength = 0.8f;
        [Header("Appearance")]
        [SerializeField] private Color foamColor = new(0.88f, 0.98f, 1f, 0.32f);
        [SerializeField, Range(0f, 2f)] private float intensity = 1f;
        [SerializeField, Min(0.02f)] private float particleSize = 0.25f;
        [SerializeField, Min(0.1f)] private float wakeLifetime = 1.8f;
        [SerializeField, Min(0.1f)] private float hullFoamLifetime = 0.65f;
        [SerializeField, Min(0f)] private float emissionRate = 25f;
        [SerializeField, Min(0.01f)] private float minimumSpeed = 0.15f;

        private BoatController3D boat;
        private Rigidbody body;
        private ParticleSystem particles;
        private Vector3 previousCenter;
        private float emissionBudget;

        public void Configure(Material material, Transform water, BoxCollider hull)
        {
            foamMaterial = material;
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
            if (particles == null) CreateParticles();
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
            main.maxParticles = 1024;
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
            if (speed < minimumSpeed || intensity <= 0f || !boat.enabled)
            {
                previousCenter = center;
                emissionBudget = 0f;
                return;
            }
            float strength = Mathf.Clamp(speed / Mathf.Max(0.1f, boat.MaximumSpeed), 0f, 1.7f);
            emissionBudget += emissionRate * strength * intensity * Time.deltaTime;
            int count = Mathf.Min(32, Mathf.FloorToInt(emissionBudget));
            emissionBudget = Mathf.Min(1f, emissionBudget - count);
            Vector3 forward = boat.NavigationForward;
            if (Vector3.Dot(body.linearVelocity, forward) < 0f) forward = -forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            for (int i = 0; i < count; i++)
            {
                Vector3 sample = Vector3.Lerp(previousCenter, center, (i + 1f) / count);
                sample.y = waterSurface.position.y + surfaceOffset;
                float side = Random.value < 0.5f ? -1f : 1f;
                // Both sides of the leading hull and a widening wake behind the stern.
                Emit(sample + right * (side * hullHalfWidth)
                    + forward * Random.Range(-0.2f, 0.8f) * hullHalfLength,
                    right * side * 0.08f, hullFoamLifetime, 0.75f, strength);
                Emit(sample - forward * hullHalfLength
                    + right * Random.Range(-hullHalfWidth, hullHalfWidth),
                    right * side * 0.12f, wakeLifetime, 1f, strength);
            }
            previousCenter = center;
        }

        private void Emit(Vector3 position, Vector3 velocity, float lifetime, float size, float strength)
        {
            Color color = foamColor;
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
        }

        private void OnDestroy()
        {
            if (particles != null) Destroy(particles.gameObject);
        }
    }
}
