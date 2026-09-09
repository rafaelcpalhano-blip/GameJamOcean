using System.Collections;
using System.Collections.Generic;
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
        private Bounds navigableBounds;
        private DiveSpawnExclusionCircle3D[] tornadoExclusions;
        private readonly List<TornadoPatrol3D> tornadoControllers = new();

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
            navigableBounds = bounds;
            tornadoExclusions = FindObjectsByType<DiveSpawnExclusionCircle3D>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int tornadoCount = Mathf.Max(1, settings.tornadoCount);
            int minimumActive = Mathf.Clamp(settings.tornadoMinimumActive, 1, tornadoCount);
            for (int i = 0; i < tornadoCount; i++)
            {
                Vector3[] points = CreateRandomRoute(waterY + settings.tornadoWaterOffset);
                GameObject routeObject = new($"Tornado Route {i + 1}");
                routeObject.transform.SetParent(transform, false);
                TornadoPatrol3D controller = routeObject.AddComponent<TornadoPatrol3D>();
                tornadoControllers.Add(controller);
                controller.Configure(this, settings, points, i < minimumActive);
            }
            StartCoroutine(TargetDivePointsPeriodically());
        }

        private IEnumerator TargetDivePointsPeriodically()
        {
            while (true)
            {
                float minimum = Mathf.Max(.1f, settings.tornadoBuoyTargetMinimumInterval);
                float maximum = Mathf.Max(minimum, settings.tornadoBuoyTargetMaximumInterval);
                yield return new WaitForSeconds(Random.Range(minimum, maximum));
                while (!TryAssignDivePointTarget()) yield return new WaitForSeconds(1f);
            }
        }

        private bool TryAssignDivePointTarget()
        {
            List<TornadoPatrol3D> availableTornadoes = new();
            foreach (TornadoPatrol3D tornado in tornadoControllers)
                if (tornado != null && tornado.IsVisible && !tornado.HasDivePointTarget)
                    availableTornadoes.Add(tornado);
            List<SpawnedDivePoint3D> availablePoints = new();
            foreach (SpawnedDivePoint3D point in SpawnedDivePoint3D.ActivePoints)
            {
                if (point == null || !point.IsAvailableForTornado) continue;
                bool alreadyTargeted = false;
                foreach (TornadoPatrol3D tornado in tornadoControllers)
                    if (tornado != null && tornado.TargetedDivePoint == point) { alreadyTargeted = true; break; }
                if (!alreadyTargeted) availablePoints.Add(point);
            }
            if (availableTornadoes.Count == 0 || availablePoints.Count == 0) return false;
            SpawnedDivePoint3D selectedPoint = availablePoints[Random.Range(0, availablePoints.Count)];
            TornadoPatrol3D selectedTornado = availableTornadoes[0];
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < availableTornadoes.Count; index++)
            {
                float distance = (availableTornadoes[index].transform.position - selectedPoint.transform.position)
                    .sqrMagnitude;
                if (distance >= nearestDistance || !SegmentIsClear(availableTornadoes[index].transform.position,
                        selectedPoint.transform.position, settings.tornadoIslandClearance + .25f)) continue;
                nearestDistance = distance;
                selectedTornado = availableTornadoes[index];
            }
            if (float.IsPositiveInfinity(nearestDistance)) return false;
            return selectedTornado.TryTargetDivePoint(selectedPoint);
        }

        private Vector3[] CreateRandomRoute(float y)
        {
            float safeMargin = settings.tornadoIslandClearance + settings.tornadoZigzagAmplitude;
            for (int routeAttempt = 0; routeAttempt < 48; routeAttempt++)
            {
                Vector3[] route = { RandomWaterPoint(y, safeMargin), RandomWaterPoint(y, safeMargin),
                    RandomWaterPoint(y, safeMargin) };
                if (SegmentIsClear(route[0], route[1], safeMargin)
                    && SegmentIsClear(route[1], route[2], safeMargin)
                    && SegmentIsClear(route[2], route[0], safeMargin)) return route;
            }
            return new[] { RandomWaterPoint(y, safeMargin), RandomWaterPoint(y, safeMargin),
                RandomWaterPoint(y, safeMargin) };
        }

        private Vector3 RandomWaterPoint(float y, float clearance)
        {
            Vector3 candidate = navigableBounds.center;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                candidate = new Vector3(Random.Range(navigableBounds.min.x, navigableBounds.max.x), y,
                    Random.Range(navigableBounds.min.z, navigableBounds.max.z));
                if (PositionIsClear(candidate, clearance)) return candidate;
            }
            candidate.y = y;
            return ConstrainTornadoPosition(candidate);
        }

        private bool PositionIsClear(Vector3 candidate, float clearance)
        {
            foreach (DiveSpawnExclusionCircle3D exclusion in tornadoExclusions)
            {
                if (exclusion == null) continue;
                Vector2 delta = new(candidate.x - exclusion.transform.position.x,
                    candidate.z - exclusion.transform.position.z);
                float radius = exclusion.Radius + clearance;
                if (delta.sqrMagnitude < radius * radius) return false;
            }
            return true;
        }

        private bool SegmentIsClear(Vector3 start, Vector3 end, float clearance)
        {
            Vector2 a = new(start.x, start.z);
            Vector2 b = new(end.x, end.z);
            Vector2 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            foreach (DiveSpawnExclusionCircle3D exclusion in tornadoExclusions)
            {
                if (exclusion == null) continue;
                Vector2 center = new(exclusion.transform.position.x, exclusion.transform.position.z);
                float t = lengthSquared > .001f ? Mathf.Clamp01(Vector2.Dot(center - a, segment) / lengthSquared) : 0f;
                float radius = exclusion.Radius + clearance;
                if ((a + segment * t - center).sqrMagnitude < radius * radius) return false;
            }
            return true;
        }

        public Vector3 ConstrainTornadoPosition(Vector3 candidate)
        {
            candidate.x = Mathf.Clamp(candidate.x, navigableBounds.min.x, navigableBounds.max.x);
            candidate.z = Mathf.Clamp(candidate.z, navigableBounds.min.z, navigableBounds.max.z);
            foreach (DiveSpawnExclusionCircle3D exclusion in tornadoExclusions)
            {
                if (exclusion == null) continue;
                Vector2 delta = new(candidate.x - exclusion.transform.position.x,
                    candidate.z - exclusion.transform.position.z);
                float radius = exclusion.Radius + settings.tornadoIslandClearance;
                if (delta.sqrMagnitude >= radius * radius) continue;
                if (delta.sqrMagnitude < .001f) delta = Vector2.right;
                delta = delta.normalized * radius;
                candidate.x = exclusion.transform.position.x + delta.x;
                candidate.z = exclusion.transform.position.z + delta.y;
            }
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
        private AudioSource tornadoAudioBoost;
        private float nextDamageTime;
        private static TornadoPatrol3D captureOwner;
        private bool capturing;
        private float captureElapsed;
        private float captureStartRadius;
        private float captureStartAngle;
        private float captureDirection;
        private Quaternion captureRotation;
        private int activeBuoyCaptures;
        private SpawnedDivePoint3D targetedDivePoint;
        private float targetedDivePointSpeed;

        private BoatController3D boat;
        private Rigidbody boatBody;
        private Health boatHealth;
        public bool IsVisible => contact != null && contact.enabled;
        public bool HasDivePointTarget => targetedDivePoint != null;
        public SpawnedDivePoint3D TargetedDivePoint => targetedDivePoint;

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
            ConfigureSpatialAudio();
            StartCoroutine(Patrol(showImmediately));
        }

        private void ConfigureSpatialAudio()
        {
            tornadoAudio = gameObject.AddComponent<AudioSource>();
            tornadoAudioBoost = gameObject.AddComponent<AudioSource>();
            ConfigureSpatialSource(tornadoAudio);
            ConfigureSpatialSource(tornadoAudioBoost);
        }

        private void ConfigureSpatialSource(AudioSource source)
        {
            source.clip = settings.tornadoSound;
            source.loop = true;
            source.playOnAwake = false;
            // Tornado attenuation is intentionally boat-relative. Using Unity's global
            // AudioListener here would measure from the elevated follow camera instead.
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        private void RefreshTornadoAudioVolume()
        {
            float attenuation = 0f;
            if (boat != null)
            {
                Vector3 offset = boat.transform.position - transform.position;
                float horizontalDistance = new Vector2(offset.x, offset.z).magnitude;
                float minimumDistance = Mathf.Max(0f, settings.tornadoSoundMinimumDistance);
                float maximumDistance = Mathf.Max(minimumDistance + .01f,
                    settings.tornadoSoundMaximumDistance);
                attenuation = horizontalDistance <= minimumDistance ? 1f
                    : horizontalDistance >= maximumDistance ? 0f
                    : 1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(minimumDistance, maximumDistance, horizontalDistance));
            }
            float configuredVolume = settings.tornadoSoundVolume * attenuation
                * (GameAudio.Instance != null ? GameAudio.Instance.EffectsVolume : 1f);
            if (tornadoAudio != null) tornadoAudio.volume = Mathf.Clamp01(configuredVolume);
            if (tornadoAudioBoost != null) tornadoAudioBoost.volume = Mathf.Clamp01(configuredVolume - 1f);
        }

        public bool TryTargetDivePoint(SpawnedDivePoint3D point)
        {
            if (!IsVisible || targetedDivePoint != null || point == null || !point.IsAvailableForTornado)
                return false;
            targetedDivePoint = point;
            Vector3 delta = point.transform.position - transform.position;
            delta.y = 0f;
            targetedDivePointSpeed = Mathf.Max(settings.tornadoSpeed,
                delta.magnitude / Mathf.Max(.1f, settings.tornadoBuoyTargetMaximumTravelTime));
            return true;
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

                if (targetedDivePoint != null) yield return MoveTowardTargetedDivePoint();
                while (capturing || activeBuoyCaptures > 0) yield return null;

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
                if (targetedDivePoint != null)
                {
                    yield return MoveTowardTargetedDivePoint();
                    yield break;
                }
                progress = Mathf.Min(1f, progress + settings.tornadoSpeed * Time.deltaTime / distance);
                float envelope = Mathf.Sin(progress * Mathf.PI);
                float wave = Mathf.Sin(progress * Mathf.PI * 2f * settings.tornadoZigzagCycles);
                Vector3 candidate = Vector3.Lerp(start, destination, progress)
                    + perpendicular * (wave * envelope * settings.tornadoZigzagAmplitude);
                transform.position = manager.ConstrainTornadoPosition(candidate);
                yield return null;
            }
            transform.position = destination;
        }

        private IEnumerator MoveTowardTargetedDivePoint()
        {
            float weaveTime = 0f;
            while (targetedDivePoint != null && targetedDivePoint.IsAvailableForTornado)
            {
                Vector3 target = targetedDivePoint.transform.position;
                target.y = transform.position.y;
                Vector3 direction = target - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude <= settings.tornadoBuoyCaptureRadius
                    * settings.tornadoBuoyCaptureRadius) break;
                weaveTime += Time.deltaTime;
                Vector3 stepDirection = direction.normalized;
                Vector3 sideways = Vector3.Cross(Vector3.up, stepDirection)
                    * (Mathf.Sin(weaveTime * 2f) * .2f);
                Vector3 candidate = transform.position
                    + (stepDirection + sideways).normalized * targetedDivePointSpeed * Time.deltaTime;
                transform.position = manager.ConstrainTornadoPosition(candidate);
                yield return null;
            }
            targetedDivePoint = null;
        }

        private void ShowVisual()
        {
            visual = Instantiate(settings.tornadoPrefab, transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale *= settings.tornadoVisualScale;
            if (tornadoAudio != null && tornadoAudio.clip != null)
            {
                RefreshTornadoAudioVolume();
                tornadoAudio.Play();
                if (tornadoAudioBoost != null && tornadoAudioBoost.volume > 0f) tornadoAudioBoost.Play();
            }
            contact.enabled = true;
            manager.TornadoShown();
        }

        private IEnumerator HideVisual()
        {
            contact.enabled = false;
            if (tornadoAudio != null) tornadoAudio.Stop();
            if (tornadoAudioBoost != null) tornadoAudioBoost.Stop();
            if (visual == null) yield break;
            foreach (ParticleSystem particles in visual.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(settings.tornadoDisappearanceFadeSeconds);
            if (visual != null) Destroy(visual);
            visual = null;
        }

        private void FixedUpdate()
        {
            RefreshTornadoAudioVolume();
            if (tornadoAudio != null && tornadoAudio.isPlaying && tornadoAudioBoost != null
                && tornadoAudioBoost.volume > 0f && !tornadoAudioBoost.isPlaying)
            {
                tornadoAudioBoost.timeSamples = tornadoAudio.timeSamples;
                tornadoAudioBoost.Play();
            }
            if (capturing && (boatHealth == null || boatHealth.IsDead)) CancelCapture();
            if (!contact.enabled || GameJamOcean.UI.GameMenus.BlocksGameplay) return;
            TryCaptureNearbyDivePoints();
            if (boat == null || boatBody == null || boatBody.isKinematic
                || boatHealth == null || boatHealth.IsDead) return;
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

        private void TryCaptureNearbyDivePoints()
        {
            float radiusSquared = settings.tornadoBuoyCaptureRadius * settings.tornadoBuoyCaptureRadius;
            var activePoints = SpawnedDivePoint3D.ActivePoints;
            for (int index = activePoints.Count - 1; index >= 0; index--)
            {
                SpawnedDivePoint3D point = activePoints[index];
                if (point == null) continue;
                Vector3 delta = point.transform.position - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= radiusSquared && point.TryBeginTornadoCapture())
                    StartCoroutine(CaptureAndThrowDivePoint(point));
            }
        }

        private IEnumerator CaptureAndThrowDivePoint(SpawnedDivePoint3D point)
        {
            activeBuoyCaptures++;
            Transform buoy = point.transform;
            Vector3 offset = buoy.position - transform.position;
            float startRadius = Mathf.Max(new Vector2(offset.x, offset.z).magnitude, .25f);
            float startAngle = Mathf.Atan2(offset.z, offset.x);
            float directionSign = Random.value < .5f ? -1f : 1f;
            float startY = buoy.position.y;
            Quaternion startRotation = buoy.rotation;
            float duration = Mathf.Max(.1f, settings.tornadoBuoySpiralDuration);
            float elapsed = 0f;
            while (elapsed < duration && point != null)
            {
                elapsed += Time.fixedDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float angle = startAngle + directionSign * Mathf.PI * 2f * progress;
                float radius = Mathf.Lerp(startRadius, .3f, Mathf.SmoothStep(0f, 1f, progress));
                Vector3 center = transform.position;
                buoy.position = center + new Vector3(Mathf.Cos(angle) * radius,
                    Mathf.Lerp(startY - center.y, .35f, progress), Mathf.Sin(angle) * radius);
                buoy.rotation = Quaternion.AngleAxis(directionSign * 360f * progress, Vector3.up) * startRotation;
                yield return new WaitForFixedUpdate();
            }

            if (point != null)
            {
                Vector2 randomDirection = Random.insideUnitCircle.normalized;
                if (randomDirection.sqrMagnitude < .01f) randomDirection = Vector2.right;
                Vector3 velocity = new(randomDirection.x * settings.tornadoBuoyLaunchSpeed,
                    settings.tornadoBuoyLaunchUpSpeed, randomDirection.y * settings.tornadoBuoyLaunchSpeed);
                elapsed = 0f;
                float flightTime = Mathf.Max(.1f, settings.tornadoBuoyDestructionDelay);
                while (elapsed < flightTime && point != null)
                {
                    float dt = Time.fixedDeltaTime;
                    elapsed += dt;
                    buoy.position += velocity * dt;
                    velocity += Physics.gravity * dt;
                    buoy.Rotate(Vector3.up, directionSign * 360f * dt, Space.World);
                    yield return new WaitForFixedUpdate();
                }
                if (point != null) point.NotifyDestroyedByTornado();
            }
            activeBuoyCaptures = Mathf.Max(0, activeBuoyCaptures - 1);
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
