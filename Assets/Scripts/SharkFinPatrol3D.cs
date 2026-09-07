using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using GameJamOcean.Boat;
using GameJamOcean.Combat;

namespace GameJamOcean.World
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SharkFinPatrol3D : MonoBehaviour
    {
        [Header("Local patrol (world units)")]
        [SerializeField, Min(1f)] private float territoryRadius = 6f;
        [SerializeField, Min(.1f)] private float swimSpeed = 1.9f;
        [SerializeField, Min(1f)] private float turnSpeed = 45f;
        [SerializeField, Min(.05f)] private float turnSmoothTime = .4f;
        [SerializeField] private Vector3[] patrolPoints = new Vector3[4];
        [Header("Submerging (never above starting height)")]
        [SerializeField, Min(0f)] private float diveDepth = 2f;
        [SerializeField, Min(.1f)] private float depthSpeed = 1.4f;
        private float fullDiveDepth;
        [Header("Charge")]
        [SerializeField, Min(.1f)] private float detectionRange = 15f;
        [SerializeField, Min(1f)] private float pursuitRadius = 12f;
        [SerializeField, Min(1f)] private float chargeTurnSpeed = 90f;
        [SerializeField, Min(.1f)] private float chargeSpeed = 6f;
        [SerializeField, Min(.1f)] private float chargeDuration = 5.5f;
        [SerializeField, Min(0f)] private float impactPushSpeed = 1.5f;
        [SerializeField, Min(.1f)] private float attackCooldown = 6f;
        [SerializeField, Range(0, 100)] private float damagePercent = 30f;
        private Vector3 home, direction, destination;
        private Quaternion initialRotation;
        private float depth, desiredDepth, nextDive, nextAttack, chargeUntil;
        private int pointIndex = -1;
        private bool charging, chargeLaunched;
        private float turnVelocity;
        private BoatController3D boat;
        private Health health;
        private Rigidbody sharkBody;
        private float aimUntil, recoveryUntil;
        private float nextContactDamageTime;
        [Header("Physical collision")]
        [SerializeField, Min(.1f)] private float bodyMass = 2f;
        [SerializeField, Min(.05f)] private float aimDuration = .25f;
        [SerializeField, Min(.1f)] private float cargoBoatSlideDuration = .65f;
        [SerializeField, Min(.1f)] private float cargoBoatSlideSpeed = 5.5f;
        [Header("Appearance")]
        [SerializeField] private Color finColor = new Color(.3f, .34f, .38f, 1f);
        private BoatWaterBounds3D water;
        private GameJamOcean.Spawning.DiveSpawnExclusionCircle3D[] exclusions;
        private float forcedSlideUntil;
        private Vector3 forcedSlideDirection;

        public static void ConfigureScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (!IsFin(t.name) || t.GetComponentInParent<SharkFinPatrol3D>() != null) continue;
                t.gameObject.AddComponent<SharkFinPatrol3D>();
            }
        }
        private static bool IsFin(string value) => value.Replace("_", " ").ToLowerInvariant().Contains("shark fin");
        private void Start()
        {
            home = transform.position;
            ApplyFinColor();
            fullDiveDepth = diveDepth;
            foreach (var visual in GetComponentsInChildren<Renderer>())
                fullDiveDepth = Mathf.Max(fullDiveDepth, visual.bounds.max.y - home.y + .35f);
            initialRotation = transform.rotation;
            direction = Vector3.forward;
            ConfigurePhysics();
            boat = FindFirstObjectByType<BoatController3D>();
            if (boat != null) { health = boat.GetComponent<Health>(); water = boat.GetComponent<BoatWaterBounds3D>(); }
            exclusions = FindObjectsByType<GameJamOcean.Spawning.DiveSpawnExclusionCircle3D>(FindObjectsSortMode.None);
            patrolPoints = new Vector3[4];
            float phase = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < 4; i++)
            {
                float angle = phase + i * Mathf.PI * .5f;
                Vector3 point = home + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * territoryRadius * Random.Range(.45f, .75f);
                for (int n = 0; n < 8 && !Allowed(point); n++) point = Vector3.Lerp(point, home, .5f);
                patrolPoints[i] = Allowed(point) ? point : home;
            }
            ChoosePoint();
            nextDive = Time.time + Random.Range(1f, 4f);
            nextAttack = Time.time + 2f;
        }

        private void ApplyFinColor()
        {
            var properties = new MaterialPropertyBlock();
            foreach (Renderer visual in GetComponentsInChildren<Renderer>(true))
            {
                visual.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", finColor);
                properties.SetColor("_Color", finColor);
                visual.SetPropertyBlock(properties);
                properties.Clear();
            }
        }
        private bool Allowed(Vector3 p, bool pursuing = false)
        {
            float radius = pursuing ? Mathf.Max(territoryRadius, pursuitRadius) : territoryRadius;
            if (Flat(p - home).sqrMagnitude > radius * radius) return false;
            if (water != null)
            {
                Bounds b = water.NavigableBounds;
                if (b.size.x > 0 && (p.x < b.min.x || p.x > b.max.x || p.z < b.min.z || p.z > b.max.z)) return false;
            }
            if (exclusions != null) foreach (var zone in exclusions)
                if (zone != null && zone.ContainsXZ(p)) return false;
            return true;
        }
        private void ChoosePoint()
        {
            int next = Random.Range(0, 3);
            if (next >= pointIndex && pointIndex >= 0) next++;
            pointIndex = next;
            destination = patrolPoints[pointIndex];
        }

        private void ConfigurePhysics()
        {
            // Use a primitive collider: safe in Web builds, with no runtime mesh cooking.
            Bounds bounds = new Bounds(transform.position, Vector3.one);
            bool found = false;
            foreach (var visual in GetComponentsInChildren<Renderer>())
            {
                if (!found) { bounds = visual.bounds; found = true; }
                else bounds.Encapsulate(visual.bounds);
            }
            foreach (var old in GetComponentsInChildren<Collider>()) old.enabled = false;
            // Unity's missing-component wrappers are not CLR null: do not use ?? here.
            if (!TryGetComponent<Rigidbody>(out sharkBody))
                sharkBody = gameObject.AddComponent<Rigidbody>();
            sharkBody.isKinematic = false;
            sharkBody.useGravity = false;
            sharkBody.mass = bodyMass;
            sharkBody.constraints = RigidbodyConstraints.FreezeRotation;
            sharkBody.interpolation = RigidbodyInterpolation.Interpolate;
            sharkBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            sharkBody.solverIterations = 12;
            sharkBody.solverVelocityIterations = 8;
            // Fit in the model's local space, including imported scale and orientation.
            var localBounds = new Bounds(transform.InverseTransformPoint(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                localBounds.Encapsulate(transform.InverseTransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            var solid = gameObject.AddComponent<BoxCollider>();
            solid.center = localBounds.center;
            solid.size = new Vector3(Mathf.Max(.1f, localBounds.size.x), Mathf.Max(.1f, localBounds.size.y), Mathf.Max(.1f, localBounds.size.z));
            solid.isTrigger = false;
        }

        private void FixedUpdate()
        {
            if (sharkBody == null || patrolPoints == null || patrolPoints.Length != 4) return;
            float dt = Time.fixedDeltaTime;
            Vector3 position = sharkBody.position;
            if (Time.time < forcedSlideUntil)
            {
                Vector3 targetVelocity = forcedSlideDirection * cargoBoatSlideSpeed;
                targetVelocity.y = Mathf.Clamp((home.y - position.y) / dt, -depthSpeed, depthSpeed);
                sharkBody.linearVelocity = Vector3.Lerp(sharkBody.linearVelocity, targetVelocity,
                    1f - Mathf.Exp(-8f * dt));
                return;
            }
            if (Time.time < recoveryUntil)
            {
                // Preserve the solver's horizontal response without continuing to rise after surfacing.
                Vector3 recoveryVelocity = sharkBody.linearVelocity;
                recoveryVelocity.y = Mathf.Clamp((home.y - depth - position.y) / dt, -depthSpeed, depthSpeed);
                sharkBody.linearVelocity = recoveryVelocity;
                return;
            }
            bool canAttack = !GameJamOcean.UI.GameMenus.BlocksGameplay && boat != null
                && boat.isActiveAndEnabled && health != null && !health.IsDead;
            if (!charging && canAttack && Time.time >= nextAttack
                && Flat(boat.transform.position - position).sqrMagnitude <= detectionRange * detectionRange
                && Allowed(boat.transform.position, true))
            {
                charging = true; chargeLaunched = false;
                aimUntil = Time.time + aimDuration;
                // Allow a smooth half-turn and surfacing before starting the actual chase timer.
                chargeUntil = aimUntil + 360f / Mathf.Max(1f, chargeTurnSpeed)
                    + fullDiveDepth / (depthSpeed * 4f) + turnSmoothTime * 4f;
                desiredDepth = 0;
                nextDive = Time.time + chargeDuration + Random.Range(3f, 7f);
                destination = boat.transform.position; destination.y = home.y;
                nextAttack = Time.time + attackCooldown;
            }
            if (charging && (!canAttack || Time.time >= chargeUntil || !Allowed(boat.transform.position, true)))
            {
                charging = false;
                nextAttack = Time.time + attackCooldown;
                ChoosePoint();
            }
            if (charging) { destination = boat.transform.position; destination.y = home.y; }
            if (!charging && Flat(destination - position).sqrMagnitude < .5f) ChoosePoint();
            Vector3 desired = Flat(destination - position).normalized;
            if (desired.sqrMagnitude > 0)
            {
                float heading = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float targetHeading = Mathf.Atan2(desired.x, desired.z) * Mathf.Rad2Deg;
                heading = Mathf.SmoothDampAngle(heading, targetHeading, ref turnVelocity,
                    turnSmoothTime, charging ? chargeTurnSpeed : turnSpeed, dt);
                direction = new Vector3(Mathf.Sin(heading * Mathf.Deg2Rad), 0, Mathf.Cos(heading * Mathf.Deg2Rad));
            }
            float alignment = Mathf.Clamp01(Vector3.Dot(direction, desired));
            if (charging && !chargeLaunched && Time.time >= aimUntil && depth <= .15f
                && alignment >= Mathf.Cos(15f * Mathf.Deg2Rad))
            {
                chargeLaunched = true;
                chargeUntil = Time.time + chargeDuration;
            }
            // Turn gently before accelerating; slow down on sharp corrections rather than circling at full speed.
            float speed = charging ? (chargeLaunched ? chargeSpeed * alignment * alignment : 0f)
                : swimSpeed * alignment;
            Vector3 velocity = direction * speed;
            // The wider envelope also lets a finished chase return to its original four patrol points.
            if (!Allowed(position + velocity * dt, true))
            {
                velocity = Vector3.zero;
                if (charging) nextAttack = Time.time + attackCooldown;
                charging = false;
                ChoosePoint();
            }
            if (!charging && desiredDepth == 0f && depth <= .01f && Time.time >= nextDive)
            {
                desiredDepth = fullDiveDepth;
            }
            depth = Mathf.MoveTowards(depth, charging ? 0 : desiredDepth, depthSpeed * dt * (charging ? 4 : 1));
            if (!charging && desiredDepth > 0f && depth >= desiredDepth - .001f)
            {
                // Each complete dive is followed by a return to the original surface height.
                desiredDepth = 0;
                nextDive = Time.time + fullDiveDepth / Mathf.Max(.1f, depthSpeed) + Random.Range(3f, 7f);
            }
            velocity.y = Mathf.Clamp((home.y - Mathf.Max(0, depth) - position.y) / dt, -depthSpeed * 4, depthSpeed * 4);
            sharkBody.linearVelocity = velocity;
            sharkBody.MoveRotation(Quaternion.LookRotation(direction, Vector3.up) * initialRotation);
        }

        private void OnCollisionEnter(Collision collision) => HandleContact(collision);
        private void OnCollisionStay(Collision collision) => HandleContact(collision);
        private void HandleContact(Collision collision)
        {
            var collidedBoat = collision.collider.GetComponentInParent<BoatController3D>();
            bool boatHit = collidedBoat != null && collidedBoat == boat;
            if (!boatHit)
            {
                if (charging) { charging = false; recoveryUntil = Time.time + .5f; ChoosePoint(); }
                return;
            }
            if (GameJamOcean.UI.GameMenus.BlocksGameplay || boat == null || !boat.isActiveAndEnabled
                || health == null || health.IsDead
                || Time.time < nextContactDamageTime) return;
            charging = false;
            recoveryUntil = Time.time + .5f;
            nextAttack = Time.time + attackCooldown;
            nextContactDamageTime = Time.time + attackCooldown;
            ChoosePoint();
            BoatVisualUpgrade3D visual = boat.GetComponent<BoatVisualUpgrade3D>();
            float appliedDamagePercent = visual != null ? visual.SharkDamagePercent : damagePercent;
            health.TakeDamage(health.MaximumHealth * appliedDamagePercent / 100f, gameObject);
            var body = boat.GetComponent<Rigidbody>();
            Vector3 push = body != null ? Flat(body.position - sharkBody.position).normalized : direction;
            if (push.sqrMagnitude < .001f) push = direction;
            FindFirstObjectByType<GameJamOcean.CameraSystem.CameraFollow3D>()
                ?.PlayCollisionImpact(push);
            boat.GetComponent<BoatWaterMotion3D>()?.PlayCollisionImpact(push);

            bool cargoBoat = visual != null && visual.CurrentVesselLevel == 3;
            Vector3 boatForward = Flat(boat.NavigationForward).normalized;
            Vector3 boatToShark = Flat(sharkBody.position - boat.transform.position).normalized;
            bool frontalImpact = boatToShark.sqrMagnitude > .001f
                && Vector3.Dot(boatForward, boatToShark) >= .45f;
            if (cargoBoat && frontalImpact)
            {
                boat.RejectCollisionRecoil();
                Vector3 boatRight = Vector3.Cross(Vector3.up, boatForward).normalized;
                float sideSign = Vector3.Dot(boatToShark, boatRight) >= 0f ? 1f : -1f;
                forcedSlideDirection = boatRight * sideSign;
                forcedSlideUntil = Time.time + cargoBoatSlideDuration;
                recoveryUntil = forcedSlideUntil + .25f;
                StartCoroutine(AllowCargoBoatPass(GetComponent<Collider>(), collision.collider));
                return;
            }
            if (!health.IsDead && body != null && !body.isKinematic)
            {
                body.AddForce(push * impactPushSpeed, ForceMode.VelocityChange);
            }
        }

        private IEnumerator AllowCargoBoatPass(Collider sharkCollider, Collider boatCollider)
        {
            if (sharkCollider == null || boatCollider == null) yield break;
            Physics.IgnoreCollision(sharkCollider, boatCollider, true);
            yield return new WaitForSeconds(cargoBoatSlideDuration);
            if (sharkCollider != null && boatCollider != null)
                Physics.IgnoreCollision(sharkCollider, boatCollider, false);
        }
        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            Gizmos.color = Color.cyan;
            foreach (var p in patrolPoints) Gizmos.DrawWireSphere(p, .3f);
        }
    }
}
