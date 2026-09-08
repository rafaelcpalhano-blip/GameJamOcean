using System;
using System.Collections;
using System.Collections.Generic;
using GameJamOcean.Combat;
using GameJamOcean.Diving;
using GameJamOcean.Player;
using GameJamOcean.Weapons;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Enemies
{
    public enum EnemyAttackType
    {
        Melee,
        Projectile
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Health))]
    public sealed class EnemyController2D : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float movementSpeed = 2f;
        [SerializeField, Min(0f)] private float stoppingDistance = 0.65f;
        [Header("Varied Pursuit and Spacing")]
        [SerializeField, Min(.1f)] private float separationRadius = 1.5f;
        [SerializeField, Range(0f, 3f)] private float separationStrength = 1.3f;
        [SerializeField, Range(0f, 1f)] private float weaveStrength = .55f;
        [SerializeField, Min(.1f)] private float steeringSharpness = 3f;
        [Header("Anti-camping Aggression")]
        [SerializeField, Min(1f)] private float massAttackDistance = 6f;
        [SerializeField, Min(1f)] private float distantSpeedMultiplier = 1.65f;
        [SerializeField, Range(0f, 1f)] private float finalWaveThreshold = .85f;
        [SerializeField, Min(1f)] private float finalWaveSpeedMultiplier = 1.3f;
        [Header("Isolation Speed Pressure")]
        [SerializeField, Min(1f)] private float isolationDistance = 7.5f;
        [SerializeField, Min(.1f)] private float isolationCheckInterval = 1f;
        [SerializeField, Min(.1f)] private float isolationSpeedBuffCooldown = 5f;
        [SerializeField, Min(.1f)] private float isolationSpeedBuffDuration = 4f;
        [SerializeField, Min(1f)] private float isolationSpeedMultiplier = 1.65f;
        [SerializeField, Min(1)] private int isolationEnemyCount = 5;
        private static readonly List<EnemyController2D> ActiveEnemies = new();
        private static float nextIsolationCheck;
        private float weavePhase, weaveFrequency;
        private GameJamOcean.World.MovementBounds2D movementBounds;
        private DiveSessionManager sessionManager;
        private float currentAggression = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveEnemies()
        {
            ActiveEnemies.Clear();
            nextIsolationCheck = 0f;
        }

        [Header("Attack")]
        [SerializeField] private EnemyAttackType attackType = EnemyAttackType.Melee;
        [SerializeField, Min(0f)] private float attackRange = 0.8f;
        [SerializeField, Min(0f)] private float attackDamage = 1f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1.25f;
        [SerializeField] private UnityEvent onAttack;
        [SerializeField] private AudioClip attackSound;
        [SerializeField, Range(0f, 2f)] private float attackSoundVolume = 1f;
        [SerializeField] private UnityEvent onEnemyDied;

        [Header("Projectile Attack")]
        [SerializeField] private EnemyProjectile2D projectilePrefab;
        [SerializeField] private Transform projectileSpawnPoint;
        [SerializeField, Min(0f)] private float projectileReleaseDelay = 0.2f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string idleStateName = "idle";
        [SerializeField] private string walkStateName = "walk";
        [SerializeField] private string attackStateName = "attack";
        [SerializeField] private string hurtStateName = "hurt";
        [SerializeField] private string deathStateName = "death";
        [SerializeField, Min(0f)] private float transitionDuration = 0.08f;
        [SerializeField, Min(0f)] private float attackAnimationDuration = 0.77f;
        [SerializeField, Min(0f)] private float hurtAnimationDuration = 0.35f;

        [Header("Death")]
        [SerializeField] private bool destroyOnDeath = true;
        [SerializeField, Min(0f)] private float destroyDelay = 0f;

        [Header("Visual")]
        [SerializeField] private bool showDamageNumbers = true;
        [SerializeField, Min(0.1f)] private float damageNumberLifetime = 0.8f;
        [SerializeField, Min(0f)] private float damageNumberHeight = 0.25f;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool spriteFacesRight = true;

        public event Action<EnemyController2D> EnemyDied;

        private Rigidbody2D enemyRigidbody;
        private Health enemyHealth;
        private Health targetHealth;
        private float nextAttackTime;
        private float animationLockedUntil;
        private int idleStateHash;
        private int walkStateHash;
        private int attackStateHash;
        private int hurtStateHash;
        private int deathStateHash;
        private int currentStateHash;
        private bool deathAnimationPlaying;
        private enum SpeciesProfile { Standard, AguaViva, PeixeEspada, Polvo, SereiaMaga, SereiaGuerreira }
        private SpeciesProfile speciesProfile;
        private bool warriorCharging;
        private float warriorSpecialUntil, warriorNextCharge;
        private int warriorOrbitSide = 1;
        [Header("Species Special Movement")]
        [SerializeField, Range(0f, 1f)] private float jellyfishDodgeChance = .65f;
        [SerializeField, Min(.05f)] private float jellyfishDodgeDuration = .32f;
        [SerializeField, Min(1f)] private float jellyfishDodgeSpeedMultiplier = 2.3f;
        [SerializeField, Min(.1f)] private float octopusDashCooldown = 2.4f;
        [SerializeField, Min(.05f)] private float octopusDashDuration = .65f;
        [SerializeField, Min(1f)] private float octopusDashSpeedMultiplier = 2.25f;
        private float jellyfishDodgeUntil;
        private Vector2 jellyfishDodgeDirection;
        private float octopusDashUntil, octopusNextDash;
        private int octopusOrbitSide = 1;
        [SerializeField, Min(.1f)] private float octopusLungeIntervalMin = 1.8f;
        [SerializeField, Min(.1f)] private float octopusLungeIntervalMax = 3.4f;
        [SerializeField, Min(.05f)] private float octopusLungeDuration = 1f;
        [SerializeField, Min(1f)] private float octopusLungeSpeedMultiplier = 2.6f;
        [SerializeField, Min(.05f)] private float octopusRetreatDuration = .34f;
        [SerializeField, Min(.05f)] private float octopusEscapeDuration = .42f;
        [SerializeField, Min(0f)] private float octopusKnockbackSpeed = 2.6f;
        [SerializeField, Min(0f)] private float warriorKnockbackSpeed = 4.5f;
        [SerializeField, Min(.05f)] private float knockbackDuration = .18f;
        private enum OctopusStrikePhase { Orbit, Charge, Retreat, Escape }
        private OctopusStrikePhase octopusStrikePhase;
        private float octopusStrikePhaseUntil, octopusNextLunge;
        private Vector2 octopusEscapeDirection;
        private float isolationSpeedBuffUntil;

        private void Awake()
        {
            enemyRigidbody = GetComponent<Rigidbody2D>();
            enemyHealth = GetComponent<Health>();
            speciesProfile = DetectSpecies(name);
            if (speciesProfile == SpeciesProfile.PeixeEspada) movementSpeed *= 1.35f;
            if (speciesProfile == SpeciesProfile.Polvo) movementSpeed *= 1.22f;
            movementBounds = FindFirstObjectByType<GameJamOcean.World.MovementBounds2D>();
            sessionManager = FindFirstObjectByType<DiveSessionManager>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            ConfigureRigidbody();
            CacheAnimationHashes();
        }

        private void OnEnable()
        {
            if (!ActiveEnemies.Contains(this)) ActiveEnemies.Add(this);
            weavePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            weaveFrequency = UnityEngine.Random.Range(.65f, 1.35f);
            octopusOrbitSide = UnityEngine.Random.value < .5f ? -1 : 1;
            octopusNextDash = Time.time + UnityEngine.Random.Range(.4f, 1.4f);
            octopusNextLunge = Time.time + UnityEngine.Random.Range(octopusLungeIntervalMin, octopusLungeIntervalMax);
            octopusStrikePhase = OctopusStrikePhase.Orbit;
            isolationSpeedBuffUntil = 0f;
            if (speciesProfile == SpeciesProfile.AguaViva)
                HarpoonLauncher2D.HarpoonFired += HandleHarpoonFired;
            enemyHealth.Damaged += HandleDamaged;
            enemyHealth.Died += HandleDeath;
        }

        private void Start()
        {
            FindTargetIfNeeded();
            ChangeAnimation(idleStateHash, 0f);
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
            if (speciesProfile == SpeciesProfile.AguaViva)
                HarpoonLauncher2D.HarpoonFired -= HandleHarpoonFired;
            enemyHealth.Damaged -= HandleDamaged;
            enemyHealth.Died -= HandleDeath;
        }

        private void FixedUpdate()
        {
            if (enemyHealth.IsDead)
            {
                enemyRigidbody.linearVelocity = Vector2.zero;
                return;
            }

            FindTargetIfNeeded();
            if (target == null || targetHealth == null || targetHealth.IsDead)
            {
                enemyRigidbody.linearVelocity = Vector2.zero;
                UpdateMovementAnimation();
                return;
            }

            TryStartGroupIsolationSpeedBuff((Vector2)target.position);

            Vector2 displacement = (Vector2)target.position - enemyRigidbody.position;
            float distance = displacement.magnitude;
            Vector2 direction = distance > Mathf.Epsilon
                ? displacement / distance
                : Vector2.zero;

            UpdateVisualDirection(direction.x);

            bool mageCanAttack = speciesProfile == SpeciesProfile.SereiaMaga && IsVisibleOnScreen();
            if ((distance <= attackRange || mageCanAttack)
                && (speciesProfile != SpeciesProfile.Polvo
                    || octopusStrikePhase == OctopusStrikePhase.Charge))
            {
                TryAttack();
            }

            Vector2 separation = Vector2.zero;
            float closestEnemyToPlayer = distance;
            foreach (var other in ActiveEnemies)
            {
                if (other == this || other == null || other.enemyHealth.IsDead) continue;
                closestEnemyToPlayer = Mathf.Min(closestEnemyToPlayer,
                    Vector2.Distance(other.enemyRigidbody.position, target.position));
                Vector2 away = enemyRigidbody.position - other.enemyRigidbody.position;
                float gap = away.magnitude;
                bool fellowOctopus = speciesProfile == SpeciesProfile.Polvo
                    && other.speciesProfile == SpeciesProfile.Polvo;
                float desiredGap = fellowOctopus ? separationRadius * 1.85f : separationRadius;
                if (gap >= desiredGap) continue;
                if (gap < .001f)
                {
                    // Stable opposite directions also separate perfectly overlapping spawns.
                    away = GetInstanceID() < other.GetInstanceID() ? Vector2.left : Vector2.right;
                }
                separation += away.normalized * (1f - gap / desiredGap)
                    * (fellowOctopus ? 2.2f : 1f);
            }
            float separationMultiplier = speciesProfile == SpeciesProfile.Polvo ? 1.8f : 1f;
            separation = Vector2.ClampMagnitude(separation, 1f) * separationStrength * separationMultiplier;
            float weave = Mathf.Sin(Time.time * weaveFrequency + weavePhase)
                + .3f * Mathf.Sin(Time.time * weaveFrequency * 1.73f + weavePhase);
            float approach = Mathf.Clamp01((distance - attackRange) / 2f);
            float speciesWeave = speciesProfile == SpeciesProfile.Polvo ? .65f : 1f;
            Vector2 tangent = new Vector2(-direction.y, direction.x);
            Vector2 sideways = tangent * (weave * weaveStrength * approach * speciesWeave);
            Vector2 pursuit = distance > Mathf.Max(stoppingDistance, attackRange) ? direction : Vector2.zero;
            float speedMultiplier = 1f;
            if (speciesProfile == SpeciesProfile.Polvo)
                ApplyOctopusMovement(distance, direction, tangent, ref pursuit, ref sideways, ref speedMultiplier);

            Vector2 special = Vector2.zero;
            if (speciesProfile == SpeciesProfile.AguaViva && Time.time < jellyfishDodgeUntil)
            {
                pursuit = Vector2.zero;
                sideways = Vector2.zero;
                special = jellyfishDodgeDirection;
                speedMultiplier = jellyfishDodgeSpeedMultiplier;
            }
            if (speciesProfile == SpeciesProfile.SereiaGuerreira)
                ApplyWarriorMovement(distance, direction, tangent, ref pursuit, ref sideways, ref speedMultiplier);

            // The rush starts only when the whole group has been left behind, not for
            // isolated enemies that happen to be spawning at the edge of the arena.
            float distancePressure = Mathf.InverseLerp(massAttackDistance,
                massAttackDistance * 1.75f, closestEnemyToPlayer);
            currentAggression = Mathf.Lerp(1f, distantSpeedMultiplier, distancePressure);
            if (sessionManager != null && sessionManager.TotalEnemies > 0
                && sessionManager.KilledEnemies >= Mathf.CeilToInt(sessionManager.TotalEnemies * finalWaveThreshold))
                currentAggression *= finalWaveSpeedMultiplier;
            speedMultiplier *= currentAggression;
            if (Time.time < isolationSpeedBuffUntil)
                speedMultiplier *= isolationSpeedMultiplier;

            Vector2 desired = Vector2.ClampMagnitude(pursuit + sideways + separation + special, 1f)
                * movementSpeed * speedMultiplier;
            float effectiveSteering = steeringSharpness;
            if (speciesProfile == SpeciesProfile.Polvo
                && octopusStrikePhase != OctopusStrikePhase.Orbit) effectiveSteering *= 4f;
            Vector2 velocity = Vector2.Lerp(enemyRigidbody.linearVelocity, desired,
                1f - Mathf.Exp(-effectiveSteering * Time.fixedDeltaTime));
            if (movementBounds != null)
            {
                Vector2 next = enemyRigidbody.position + velocity * Time.fixedDeltaTime;
                next = movementBounds.ClampPoint(next, Vector2.one * .2f);
                velocity = Vector2.ClampMagnitude((next - enemyRigidbody.position) / Time.fixedDeltaTime,
                    movementSpeed * speedMultiplier);
            }
            enemyRigidbody.linearVelocity = velocity;
            UpdateMovementAnimation();
        }

        private void TryStartGroupIsolationSpeedBuff(Vector2 playerPosition)
        {
            if (Time.time < nextIsolationCheck) return;
            nextIsolationCheck = Time.time + isolationCheckInterval;

            Vector2 center = Vector2.zero;
            int aliveCount = 0;
            var candidates = new List<EnemyController2D>(ActiveEnemies.Count);
            foreach (EnemyController2D enemy in ActiveEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.enemyHealth == null
                    || enemy.enemyHealth.IsDead || enemy.enemyRigidbody == null
                    || !enemy.enemyRigidbody.simulated || enemy.movementSpeed <= 0f) continue;
                center += enemy.enemyRigidbody.position;
                aliveCount++;
                if (Time.time >= enemy.isolationSpeedBuffUntil) candidates.Add(enemy);
            }
            if (aliveCount == 0 || Vector2.Distance(playerPosition, center / aliveCount) < isolationDistance)
                return;

            candidates.Sort((left, right) =>
            {
                int tier = left.SpeciesTier.CompareTo(right.SpeciesTier);
                return tier != 0 ? tier : left.GetInstanceID().CompareTo(right.GetInstanceID());
            });
            int count = Mathf.Min(Mathf.Max(1, isolationEnemyCount), candidates.Count);
            for (int index = 0; index < count; index++)
                candidates[index].BeginIsolationSpeedBuff(isolationSpeedBuffDuration);
            nextIsolationCheck = Time.time + isolationSpeedBuffCooldown;
        }

        private int SpeciesTier => speciesProfile == SpeciesProfile.Standard
            ? int.MaxValue : (int)speciesProfile;

        private void BeginIsolationSpeedBuff(float duration)
        {
            // The normal AI keeps steering. Only its final speed is temporarily multiplied.
            isolationSpeedBuffUntil = Mathf.Max(isolationSpeedBuffUntil,
                Time.time + Mathf.Max(.1f, duration));
        }

        private void TryAttack()
        {
            if (Time.time < nextAttackTime)
            {
                return;
            }

            nextAttackTime = Time.time + attackCooldown / Mathf.Max(1f, currentAggression);
            PlayActionAnimation(attackStateHash, attackAnimationDuration);
            onAttack?.Invoke();
            GameJamOcean.Audio.GameAudio.Instance?.PlayEffect(attackSound, attackSoundVolume);

            if (attackType == EnemyAttackType.Projectile)
            {
                Vector2 direction = ((Vector2)target.position - enemyRigidbody.position).normalized;
                const int shots = 1;
                StartCoroutine(LaunchProjectileAfterDelay(direction, shots));
                return;
            }

            float healthBefore = targetHealth.CurrentHealth;
            targetHealth.TakeDamage(attackDamage, gameObject);
            if (targetHealth.CurrentHealth >= healthBefore) return;
            float pushSpeed = speciesProfile == SpeciesProfile.SereiaGuerreira
                ? warriorKnockbackSpeed
                : speciesProfile == SpeciesProfile.Polvo ? octopusKnockbackSpeed : 0f;
            if (pushSpeed > 0f && target.TryGetComponent(out DiverController diver))
            {
                Vector2 pushDirection = ((Vector2)target.position - enemyRigidbody.position).normalized;
                diver.ApplyKnockback(pushDirection * pushSpeed, knockbackDuration);
            }
        }

        private IEnumerator LaunchProjectileAfterDelay(Vector2 direction, int shots)
        {
            if (projectileReleaseDelay > 0f)
            {
                yield return new WaitForSeconds(projectileReleaseDelay);
            }

            if (enemyHealth.IsDead || projectilePrefab == null || targetHealth == null || targetHealth.IsDead)
            {
                yield break;
            }

            for (int shot = 0; shot < shots; shot++)
            {
                if (enemyHealth.IsDead || targetHealth == null || targetHealth.IsDead) yield break;
                Vector2 origin = projectileSpawnPoint != null ? projectileSpawnPoint.position : transform.position;
                direction = ((Vector2)target.position - origin).normalized;
                EnemyProjectile2D projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
                projectile.Launch(direction, targetHealth, gameObject, attackDamage);
                if (shot + 1 < shots) yield return new WaitForSeconds(.18f);
            }
        }

        private bool IsVisibleOnScreen()
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            Vector3 viewport = camera.WorldToViewportPoint(transform.position);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;
        }

        private void HandleHarpoonFired(Vector2 origin, Vector2 direction)
        {
            if (enemyHealth == null || enemyHealth.IsDead || UnityEngine.Random.value > jellyfishDodgeChance) return;
            Vector2 incoming = enemyRigidbody.position - origin;
            float distance = incoming.magnitude;
            if (distance <= .01f || distance > 6f
                || Vector2.Dot(direction.normalized, incoming / distance) < .72f) return;
            Vector2 perpendicular = new(-direction.y, direction.x);
            // The side is intentionally unpredictable, so not every successful reaction is perfect.
            jellyfishDodgeDirection = perpendicular * (UnityEngine.Random.value < .5f ? -1f : 1f);
            jellyfishDodgeUntil = Time.time + jellyfishDodgeDuration;
        }

        private void ApplyOctopusMovement(float distance, Vector2 direction, Vector2 tangent,
            ref Vector2 pursuit, ref Vector2 sideways, ref float speedMultiplier)
        {
            if (octopusStrikePhase == OctopusStrikePhase.Charge
                && (Time.time >= octopusStrikePhaseUntil || distance <= attackRange))
            {
                octopusStrikePhase = OctopusStrikePhase.Retreat;
                octopusStrikePhaseUntil = Time.time + octopusRetreatDuration;
            }
            else if (octopusStrikePhase == OctopusStrikePhase.Retreat && Time.time >= octopusStrikePhaseUntil)
            {
                octopusStrikePhase = OctopusStrikePhase.Escape;
                octopusStrikePhaseUntil = Time.time + octopusEscapeDuration;
                float side = UnityEngine.Random.value < .5f ? -1f : 1f;
                float angle = UnityEngine.Random.Range(55f, 115f) * side;
                octopusEscapeDirection = Rotate(-direction, angle).normalized;
                octopusOrbitSide = side > 0f ? 1 : -1;
            }
            else if (octopusStrikePhase == OctopusStrikePhase.Escape && Time.time >= octopusStrikePhaseUntil)
            {
                octopusStrikePhase = OctopusStrikePhase.Orbit;
                octopusNextLunge = Time.time
                    + UnityEngine.Random.Range(octopusLungeIntervalMin, octopusLungeIntervalMax);
            }

            if (octopusStrikePhase == OctopusStrikePhase.Orbit
                && Time.time >= octopusNextLunge && distance <= 4.5f)
            {
                octopusStrikePhase = OctopusStrikePhase.Charge;
                octopusStrikePhaseUntil = Time.time + octopusLungeDuration;
                PlayActionAnimation(attackStateHash,
                    Mathf.Max(attackAnimationDuration, octopusLungeDuration));
            }

            if (octopusStrikePhase == OctopusStrikePhase.Orbit
                && Time.time >= octopusNextDash && distance <= 4.5f)
            {
                octopusDashUntil = Time.time + octopusDashDuration;
                octopusNextDash = octopusDashUntil + octopusDashCooldown;
                octopusOrbitSide *= -1;
            }

            sideways = Vector2.zero;
            if (octopusStrikePhase == OctopusStrikePhase.Charge)
            {
                // Close the whole remaining gap; normal melee contact applies the damage.
                pursuit = direction;
                speedMultiplier = octopusLungeSpeedMultiplier;
            }
            else if (octopusStrikePhase == OctopusStrikePhase.Retreat)
            {
                pursuit = -direction;
                speedMultiplier = octopusLungeSpeedMultiplier * .9f;
            }
            else if (octopusStrikePhase == OctopusStrikePhase.Escape)
            {
                pursuit = octopusEscapeDirection;
                speedMultiplier = octopusLungeSpeedMultiplier * .85f;
            }
            else if (Time.time < octopusDashUntil)
            {
                // A fast curved pass takes the octopus across the diver instead of piling up in front.
                pursuit = tangent * octopusOrbitSide + direction * .22f;
                speedMultiplier = octopusDashSpeedMultiplier;
            }
            else if (distance <= 4.5f)
            {
                pursuit = tangent * octopusOrbitSide + direction * (distance > 2.4f ? .3f : -.18f);
                speedMultiplier = 1.15f;
            }
        }

        private static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);
            return new Vector2(direction.x * cosine - direction.y * sine,
                direction.x * sine + direction.y * cosine);
        }

        private void ApplyWarriorMovement(float distance, Vector2 direction, Vector2 tangent,
            ref Vector2 pursuit, ref Vector2 sideways, ref float speedMultiplier)
        {
            if (warriorCharging && Time.time >= warriorSpecialUntil)
            {
                warriorCharging = false;
                warriorSpecialUntil = Time.time + 1.15f;
                warriorNextCharge = warriorSpecialUntil + .15f;
                warriorOrbitSide *= -1;
            }
            if (!warriorCharging && Time.time >= warriorSpecialUntil
                && Time.time >= warriorNextCharge && distance <= 4.5f)
            {
                warriorCharging = true;
                warriorSpecialUntil = Time.time + .65f;
            }

            sideways = Vector2.zero;
            if (warriorCharging)
            {
                pursuit = direction;
                speedMultiplier = 2f;
            }
            else if (Time.time < warriorSpecialUntil)
            {
                // Keep pressuring the diver between charges instead of waiting at orbit range.
                pursuit = direction * .55f + tangent * warriorOrbitSide * .45f;
                speedMultiplier = 1.35f;
            }
            else
            {
                pursuit = distance > attackRange * .85f ? direction : Vector2.zero;
                sideways *= .25f;
                speedMultiplier = 1.1f;
            }
        }

        private static SpeciesProfile DetectSpecies(string objectName)
        {
            string normalized = objectName.ToLowerInvariant().Replace(" ", "").Replace("(clone)", "");
            if (normalized.Contains("aguaviva")) return SpeciesProfile.AguaViva;
            if (normalized.Contains("peixeespada")) return SpeciesProfile.PeixeEspada;
            if (normalized.Contains("polvo")) return SpeciesProfile.Polvo;
            if (normalized.Contains("sereiamaga")) return SpeciesProfile.SereiaMaga;
            if (normalized.Contains("sereiaguerreira")) return SpeciesProfile.SereiaGuerreira;
            return SpeciesProfile.Standard;
        }

        private void UpdateMovementAnimation()
        {
            if (animator == null || deathAnimationPlaying || Time.time < animationLockedUntil)
            {
                return;
            }

            bool isMoving = enemyRigidbody.linearVelocity.sqrMagnitude > 0.01f;
            ChangeAnimation(isMoving ? walkStateHash : idleStateHash, transitionDuration);
        }

        private void PlayActionAnimation(int stateHash, float lockDuration)
        {
            if (animator == null || deathAnimationPlaying)
            {
                return;
            }

            animationLockedUntil = Time.time + lockDuration;
            ChangeAnimation(stateHash, transitionDuration, true);
        }

        private void ChangeAnimation(int stateHash, float fadeDuration, bool restart = false)
        {
            if (animator == null || stateHash == 0 || (!restart && currentStateHash == stateHash))
            {
                return;
            }

            currentStateHash = stateHash;
            animator.CrossFadeInFixedTime(stateHash, fadeDuration);
        }

        private void CacheAnimationHashes()
        {
            idleStateHash = GetAnimationHash(idleStateName);
            walkStateHash = GetAnimationHash(walkStateName);
            attackStateHash = GetAnimationHash(attackStateName);
            hurtStateHash = GetAnimationHash(hurtStateName);
            deathStateHash = GetAnimationHash(deathStateName);
        }

        private static int GetAnimationHash(string stateName)
        {
            return string.IsNullOrWhiteSpace(stateName)
                ? 0
                : Animator.StringToHash($"Base Layer.{stateName}");
        }

        private void FindTargetIfNeeded()
        {
            if (target != null)
            {
                targetHealth = target.GetComponentInParent<Health>();
                return;
            }

            DiverController diver = FindFirstObjectByType<DiverController>();
            if (diver == null)
            {
                return;
            }

            target = diver.transform;
            targetHealth = diver.GetComponent<Health>();
        }

        private void UpdateVisualDirection(float horizontalDirection)
        {
            if (spriteRenderer == null || Mathf.Abs(horizontalDirection) <= 0.01f)
            {
                return;
            }

            bool movingRight = horizontalDirection > 0f;
            spriteRenderer.flipX = spriteFacesRight ? !movingRight : movingRight;
        }

        private void HandleDamaged(Health health, GameObject source)
        {
            if (showDamageNumbers && spriteRenderer != null)
            {
                Vector3 top = spriteRenderer.bounds.center + Vector3.up * (spriteRenderer.bounds.extents.y + damageNumberHeight);
                DiveFeedbackParticle.SpawnDamage(top, health.LastDamageAmount, spriteRenderer.sortingLayerID, spriteRenderer.sortingOrder, damageNumberLifetime);
            }
            if (health.CurrentHealth > 0f)
            {
                PlayActionAnimation(hurtStateHash, hurtAnimationDuration);
            }
        }

        private void HandleDeath(Health health, GameObject source)
        {
            enemyRigidbody.linearVelocity = Vector2.zero;
            bool hasDeathAnimation = animator != null && deathStateHash != 0 && animator.HasState(0, deathStateHash);
            deathAnimationPlaying = hasDeathAnimation;
            animationLockedUntil = float.PositiveInfinity;
            if (hasDeathAnimation) ChangeAnimation(deathStateHash, transitionDuration, true);
            onEnemyDied?.Invoke();
            EnemyDied?.Invoke(this);

            if (destroyOnDeath)
            {
                if (hasDeathAnimation) Destroy(gameObject, destroyDelay);
                else StartCoroutine(FadeDeath(Mathf.Max(.3f, destroyDelay)));
            }
        }

        private IEnumerator FadeDeath(float duration)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>();
            foreach (Collider2D item in colliders) item.enabled = false;
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            Color[] colors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Color color = colors[i]; color.a *= alpha; renderers[i].color = color;
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        private void ConfigureRigidbody()
        {
            enemyRigidbody.gravityScale = 0f;
            enemyRigidbody.freezeRotation = true;
            enemyRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
            enemyRigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void OnValidate()
        {
            movementSpeed = Mathf.Max(0f, movementSpeed);
            stoppingDistance = Mathf.Max(0f, stoppingDistance);
            attackRange = Mathf.Max(stoppingDistance, attackRange);
            attackDamage = Mathf.Max(0f, attackDamage);
            attackCooldown = Mathf.Max(0.01f, attackCooldown);
            transitionDuration = Mathf.Max(0f, transitionDuration);
            attackAnimationDuration = Mathf.Max(0f, attackAnimationDuration);
            hurtAnimationDuration = Mathf.Max(0f, hurtAnimationDuration);
            projectileReleaseDelay = Mathf.Max(0f, projectileReleaseDelay);
            destroyDelay = Mathf.Max(0f, destroyDelay);
            isolationDistance = Mathf.Max(1f, isolationDistance);
            isolationCheckInterval = Mathf.Max(.1f, isolationCheckInterval);
            isolationSpeedBuffCooldown = Mathf.Max(.1f, isolationSpeedBuffCooldown);
            isolationSpeedBuffDuration = Mathf.Max(.1f, isolationSpeedBuffDuration);
            isolationSpeedMultiplier = Mathf.Max(1f, isolationSpeedMultiplier);
            isolationEnemyCount = Mathf.Max(1, isolationEnemyCount);

            CacheAnimationHashes();
        }
    }
}
