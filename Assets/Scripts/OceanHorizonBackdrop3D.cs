using System.Collections;
using GameJamOcean.Audio;
using GameJamOcean.Boat;
using GameJamOcean.Combat;
using GameJamOcean.Progression;
using GameJamOcean.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.World
{
    [DisallowMultipleComponent]
    public sealed class OceanHorizonBackdrop3D : MonoBehaviour
    {
        public static void ConfigureScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<OceanHorizonBackdrop3D>(true) != null) return;
            new GameObject("Fake Sky Horizon").AddComponent<OceanHorizonBackdrop3D>();
        }

        private void Awake()
        {
            transform.position = new Vector3(0f, 35f, 0f);
            CreateHorizonCylinder();
            CreateClouds();
        }

        private void CreateHorizonCylinder()
        {
            const int sides = 48;
            const float radius = 360f;
            const float height = 150f;
            var vertices = new Vector3[sides * 2];
            var triangles = new int[sides * 6];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i * 2] = new Vector3(Mathf.Cos(angle) * radius, -height * .5f, Mathf.Sin(angle) * radius);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(angle) * radius, height * .5f, Mathf.Sin(angle) * radius);
                int next = (i + 1) % sides;
                int t = i * 6;
                triangles[t] = i * 2; triangles[t + 1] = next * 2 + 1; triangles[t + 2] = i * 2 + 1;
                triangles[t + 3] = i * 2; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
            }
            GameObject sky = new("Blue Sky", typeof(MeshFilter), typeof(MeshRenderer));
            sky.transform.SetParent(transform, false);
            var mesh = new Mesh { name = "Fake Sky Horizon Mesh", vertices = vertices, triangles = triangles };
            mesh.RecalculateBounds(); mesh.RecalculateNormals();
            sky.GetComponent<MeshFilter>().sharedMesh = mesh;
            sky.GetComponent<MeshRenderer>().material = Unlit(new Color(.34f, .68f, .9f, 1f));
        }

        private void CreateClouds()
        {
            for (int group = 0; group < 10; group++)
            {
                float angle = group * Mathf.PI * 2f / 10f + .17f;
                Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 center = direction * 300f + Vector3.up * Random.Range(-1f, 24f);
                for (int puff = 0; puff < 3; puff++)
                {
                    GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    cloud.name = "Cloud Puff";
                    cloud.transform.SetParent(transform, false);
                    cloud.transform.localPosition = center + new Vector3((puff - 1) * 8f, puff == 1 ? 3f : 0f, 0f);
                    cloud.transform.localScale = new Vector3(14f, 4.5f, 5f);
                    Destroy(cloud.GetComponent<Collider>());
                    cloud.GetComponent<Renderer>().material = Unlit(new Color(1f, 1f, 1f, .78f));
                }
            }
        }

        private static Material Unlit(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            material.SetColor("_BaseColor", color);
            return material;
        }
    }

    public sealed class OceanVfxScene3D : MonoBehaviour
    {
        private static OceanAudioSettings settings;
        private int activeTornadoes;

        public static void ConfigureScene(Scene scene)
        {
            if (!scene.IsValid() || FindFirstObjectByType<OceanVfxScene3D>() != null) return;
            settings = Resources.Load<OceanAudioSettings>("OceanAudioSettings");
            if (settings == null) return;
            GameObject host = new("Ocean VFX System");
            SceneManager.MoveGameObjectToScene(host, scene);
            host.AddComponent<OceanVfxScene3D>().CreateTornadoRoutes();
        }

        public static void SpawnBoatImpact(Vector3 position, Vector3 normal)
        {
            settings ??= Resources.Load<OceanAudioSettings>("OceanAudioSettings");
            if (settings == null || settings.boatCollisionEffect == null) return;
            position.y += settings.boatCollisionWaterOffset;
            Quaternion rotation = normal.sqrMagnitude > .001f
                ? Quaternion.LookRotation(normal.normalized, Vector3.up) : Quaternion.identity;
            GameObject effect = Instantiate(settings.boatCollisionEffect, position, rotation);
            effect.transform.localScale *= settings.boatCollisionEffectScale;
            foreach (ParticleSystem particles in effect.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;
                main.loop = false;
                particles.Play(true);
            }
            Destroy(effect, Mathf.Max(.1f, settings.boatCollisionEffectLifetime));
        }

        private void CreateTornadoRoutes()
        {
            if (settings.tornadoPrefab == null) return;
            BoatController3D boat = FindFirstObjectByType<BoatController3D>();
            BoatWaterBounds3D water = boat != null ? boat.GetComponent<BoatWaterBounds3D>() : null;
            Bounds bounds = water != null ? water.NavigableBounds : default;
            if (bounds.size.x <= 0f || bounds.size.z <= 0f)
            {
                Vector3 center = boat != null ? boat.transform.position : Vector3.zero;
                bounds = new Bounds(center, new Vector3(100f, 1f, 100f));
            }
            float waterY = boat != null ? boat.transform.position.y : bounds.center.y;
            DiveSpawnExclusionCircle3D[] exclusions = FindObjectsByType<DiveSpawnExclusionCircle3D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int tornadoCount = Mathf.Max(1, settings.tornadoCount);
            int minimumActive = Mathf.Clamp(settings.tornadoMinimumActive, 1, tornadoCount);
            for (int i = 0; i < tornadoCount; i++)
            {
                Vector3[] points = new Vector3[3];
                for (int point = 0; point < points.Length; point++)
                    points[point] = RandomWaterPoint(bounds, waterY + settings.tornadoWaterOffset, exclusions);
                GameObject routeObject = new($"Tornado Route {i + 1}");
                routeObject.transform.SetParent(transform, false);
                routeObject.AddComponent<TornadoPatrol3D>().Configure(this, settings, points, i < minimumActive);
            }
        }

        private static Vector3 RandomWaterPoint(Bounds bounds, float y, DiveSpawnExclusionCircle3D[] exclusions)
        {
            Vector3 candidate = bounds.center;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                candidate = new Vector3(Random.Range(bounds.min.x, bounds.max.x), y,
                    Random.Range(bounds.min.z, bounds.max.z));
                bool blocked = false;
                foreach (DiveSpawnExclusionCircle3D exclusion in exclusions)
                    if (exclusion != null && exclusion.ContainsXZ(candidate)) { blocked = true; break; }
                if (!blocked) return candidate;
            }
            candidate.y = y;
            return candidate;
        }

        public void TornadoShown() => activeTornadoes++;

        public bool TryBeginTornadoHide()
        {
            int minimum = Mathf.Clamp(settings.tornadoMinimumActive, 1, Mathf.Max(1, settings.tornadoCount));
            if (activeTornadoes <= minimum) return false;
            activeTornadoes--;
            return true;
        }
    }

    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public sealed class TornadoPatrol3D : MonoBehaviour
    {
        private OceanAudioSettings settings;
        private OceanVfxScene3D manager;
        private Vector3[] points;
        private SphereCollider contact;
        private GameObject visual;
        private AudioSource tornadoAudio;
        private float nextDamageTime;
        private static TornadoPatrol3D captureOwner;
        private bool capturing;
        private float captureElapsed;
        private float captureStartRadius;
        private float captureStartAngle;
        private float captureDirection;
        private Quaternion captureRotation;

        private BoatController3D boat;
        private Rigidbody boatBody;
        private Health boatHealth;

        public void Configure(OceanVfxScene3D owner, OceanAudioSettings configuration, Vector3[] route,
            bool showImmediately)
        {
            manager = owner;
            settings = configuration;
            points = route;
            boat = FindFirstObjectByType<BoatController3D>();
            if (boat != null)
            {
                boatBody = boat.GetComponent<Rigidbody>();
                boatHealth = boat.GetComponent<Health>();
            }
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            contact = GetComponent<SphereCollider>();
            contact.isTrigger = true;
            contact.radius = settings.tornadoColliderRadius;
            contact.enabled = false;
            StartCoroutine(Patrol(showImmediately));
        }

        private IEnumerator Patrol(bool showImmediately)
        {
            if (!showImmediately)
                yield return new WaitForSeconds(Random.Range(0f, settings.tornadoInitialDelayMaximum));
            transform.position = points[0];
            ShowVisual();
            while (true)
            {
                for (int point = 1; point < points.Length; point++)
                    yield return MoveZigzag(transform.position, points[point]);

                while (capturing) yield return null;

                if (manager.TryBeginTornadoHide())
                {
                    yield return HideVisual();
                    float minimum = Mathf.Max(0f, settings.tornadoMinimumHiddenSeconds);
                    float maximum = Mathf.Max(minimum, settings.tornadoMaximumHiddenSeconds);
                    yield return new WaitForSeconds(Random.Range(minimum, maximum));
                    transform.position = points[0];
                    ShowVisual();
                }
                else
                    yield return MoveZigzag(transform.position, points[0]);
            }
        }

        private IEnumerator MoveZigzag(Vector3 start, Vector3 destination)
        {
            Vector3 direction = destination - start;
            float distance = direction.magnitude;
            if (distance <= .01f) yield break;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, direction.normalized);
            float progress = 0f;
            while (progress < 1f)
            {
                progress = Mathf.Min(1f, progress + settings.tornadoSpeed * Time.deltaTime / distance);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float wave = Mathf.Sin(progress * Mathf.PI * 2f * settings.tornadoZigzagCycles);
                transform.position = Vector3.Lerp(start, destination, progress)
                    + perpendicular * (wave * envelope * settings.tornadoZigzagAmplitude);
                yield return null;
            }
            transform.position = destination;
        }

        private void ShowVisual()
        {
            visual = Instantiate(settings.tornadoPrefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale *= settings.tornadoVisualScale;
            if (settings.tornadoSound != null)
            {
                tornadoAudio = visual.AddComponent<AudioSource>();
                tornadoAudio.clip = settings.tornadoSound;
                tornadoAudio.loop = true;
                tornadoAudio.playOnAwake = false;
                tornadoAudio.spatialBlend = 1f;
                tornadoAudio.rolloffMode = AudioRolloffMode.Logarithmic;
                tornadoAudio.minDistance = settings.tornadoSoundMinimumDistance;
                tornadoAudio.maxDistance = Mathf.Max(tornadoAudio.minDistance, settings.tornadoSoundMaximumDistance);
                tornadoAudio.volume = settings.tornadoSoundVolume
                    * (GameAudio.Instance != null ? GameAudio.Instance.EffectsVolume : 1f);
                tornadoAudio.Play();
            }
            contact.enabled = true;
            manager.TornadoShown();
        }

        private IEnumerator HideVisual()
        {
            contact.enabled = false;
            if (tornadoAudio != null) tornadoAudio.Stop();
            if (visual == null) yield break;
            foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(settings.tornadoDisappearanceFadeSeconds);
            if (visual != null) Destroy(visual);
            visual = null;
        }

        private void FixedUpdate()
        {
            if (tornadoAudio != null)
                tornadoAudio.volume = settings.tornadoSoundVolume
                    * (GameAudio.Instance != null ? GameAudio.Instance.EffectsVolume : 1f);
            if (capturing && (boatHealth == null || boatHealth.IsDead)) CancelCapture();
            if (!contact.enabled || boat == null || boatBody == null || boatBody.isKinematic ||
                boatHealth == null || boatHealth.IsDead || GameJamOcean.UI.GameMenus.BlocksGameplay) return;
            Vector3 toCenter = transform.position - boat.transform.position;
            toCenter.y = 0f;
            float distance = toCenter.magnitude;
            if (distance > settings.tornadoPullRadius) return;
            if (captureOwner != null && captureOwner != this) return;
            if (!capturing)
            {
                if (Time.time < nextDamageTime) return;
                if (distance > settings.tornadoSpiralCaptureRadius)
                {
                    boatBody.AddForce(toCenter.normalized * settings.tornadoPullAcceleration,
                        ForceMode.Acceleration);
                    return;
                }
                BeginSpiral(toCenter, distance);
            }
            UpdateSpiral();
        }

        private void BeginSpiral(Vector3 toCenter, float distance)
        {
            capturing = true;
            captureOwner = this;
            captureElapsed = 0f;
            captureStartRadius = Mathf.Max(distance, settings.tornadoCenterRadius);
            Vector3 fromCenter = -toCenter;
            captureStartAngle = Mathf.Atan2(fromCenter.z, fromCenter.x);
            captureDirection = Random.value < .5f ? -1f : 1f;
            captureRotation = boatBody.rotation;
            boatBody.linearVelocity = Vector3.zero;
            boatBody.angularVelocity = Vector3.zero;
        }

        private void UpdateSpiral()
        {
            captureElapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(captureElapsed / Mathf.Max(.1f, settings.tornadoSpiralDuration));
            float angle = captureStartAngle + captureDirection * Mathf.PI * 2f * progress;
            float radius = Mathf.Lerp(captureStartRadius, settings.tornadoCenterRadius * .25f,
                Mathf.SmoothStep(0f, 1f, progress));
            Vector3 center = transform.position;
            Vector3 target = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            target.y = boatBody.position.y;
            boatBody.linearVelocity = Vector3.zero;
            boatBody.angularVelocity = Vector3.zero;
            boatBody.MovePosition(target);
            boatBody.MoveRotation(Quaternion.AngleAxis(captureDirection * 360f * progress, Vector3.up)
                * captureRotation);
            if (progress < 1f) return;

            nextDamageTime = Time.time + settings.tornadoCenterCooldown;
            int boatLevel = GameProgress.HasInstance ? GameProgress.Instance.SelectedBoatLevel : 1;
            float damage = boatLevel == 2 ? settings.tornadoBoat2Damage
                : boatLevel >= 3 ? settings.tornadoBoat3Damage : settings.tornadoBoat1Damage;
            boatHealth.TakeDamage(damage, gameObject);
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            if (randomDirection.sqrMagnitude < .01f) randomDirection = Vector2.right;
            Vector3 push = new(randomDirection.x, 0f, randomDirection.y);
            boat.ApplyExternalDash(push, settings.tornadoLaunchSpeed,
                settings.tornadoLaunchControlLockSeconds);
            FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>()?.PlayCollisionImpact(push);
            capturing = false;
            if (captureOwner == this) captureOwner = null;
        }

        private void OnDisable()
        {
            CancelCapture();
        }

        private void CancelCapture()
        {
            capturing = false;
            if (captureOwner == this) captureOwner = null;
        }
    }
}
