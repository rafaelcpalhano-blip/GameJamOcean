using System;
using System.Collections;
using System.Collections.Generic;
using GameJamOcean.Combat;
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
        private static readonly List<EnemyController2D> ActiveEnemies = new();
        private float weavePhase, weaveFrequency;
        private GameJamOcean.World.MovementBounds2D movementBounds;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveEnemies() => ActiveEnemies.Clear();

        [Header("Attack")]
        [SerializeField] private EnemyAttackType attackType = EnemyAttackType.Melee;
        [SerializeField, Min(0f)] private float attackRange = 0.8f;
        [SerializeField, Min(0f)] private float attackDamage = 1f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1.25f;
        [SerializeField] private UnityEvent onAttack;
        [SerializeField] private AudioClip attackSound;
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

        private void Awake()
        {
            enemyRigidbody = GetComponent<Rigidbody2D>();
            enemyHealth = GetComponent<Health>();
            movementBounds = FindFirstObjectByType<GameJamOcean.World.MovementBounds2D>();

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

            Vector2 displacement = (Vector2)target.position - enemyRigidbody.position;
            float distance = displacement.magnitude;
            Vector2 direction = distance > Mathf.Epsilon
                ? displacement / distance
                : Vector2.zero;

            UpdateVisualDirection(direction.x);

            if (distance <= attackRange)
            {
                TryAttack();
            }

            Vector2 separation = Vector2.zero;
            foreach (var other in ActiveEnemies)
            {
                if (other == this || other == null || other.enemyHealth.IsDead) continue;
                Vector2 away = enemyRigidbody.position - other.enemyRigidbody.position;
                float gap = away.magnitude;
                if (gap >= separationRadius) continue;
                if (gap < .001f)
                {
                    // Stable opposite directions also separate perfectly overlapping spawns.
                    away = GetInstanceID() < other.GetInstanceID() ? Vector2.left : Vector2.right;
                }
                separation += away.normalized * (1f - gap / separationRadius);
            }
            separation = Vector2.ClampMagnitude(separation, 1f) * separationStrength;
            float weave = Mathf.Sin(Time.time * weaveFrequency + weavePhase)
                + .3f * Mathf.Sin(Time.time * weaveFrequency * 1.73f + weavePhase);
            float approach = Mathf.Clamp01((distance - attackRange) / 2f);
            Vector2 sideways = new Vector2(-direction.y, direction.x) * (weave * weaveStrength * approach);
            Vector2 pursuit = distance > Mathf.Max(stoppingDistance, attackRange) ? direction : Vector2.zero;
            Vector2 desired = Vector2.ClampMagnitude(pursuit + sideways + separation, 1f) * movementSpeed;
            Vector2 velocity = Vector2.Lerp(enemyRigidbody.linearVelocity, desired,
                1f - Mathf.Exp(-steeringSharpness * Time.fixedDeltaTime));
            if (movementBounds != null)
            {
                Vector2 next = enemyRigidbody.position + velocity * Time.fixedDeltaTime;
                next = movementBounds.ClampPoint(next, Vector2.one * .2f);
                velocity = Vector2.ClampMagnitude((next - enemyRigidbody.position) / Time.fixedDeltaTime, movementSpeed);
            }
            enemyRigidbody.linearVelocity = velocity;
            UpdateMovementAnimation();
        }

        private void TryAttack()
        {
            if (Time.time < nextAttackTime)
            {
                return;
            }

            nextAttackTime = Time.time + attackCooldown;
            PlayActionAnimation(attackStateHash, attackAnimationDuration);
            onAttack?.Invoke();
            GameJamOcean.Audio.GameAudio.Instance?.PlayEffect(attackSound);

            if (attackType == EnemyAttackType.Projectile)
            {
                Vector2 direction = ((Vector2)target.position - enemyRigidbody.position).normalized;
                StartCoroutine(LaunchProjectileAfterDelay(direction));
                return;
            }

            targetHealth.TakeDamage(attackDamage, gameObject);
        }

        private IEnumerator LaunchProjectileAfterDelay(Vector2 direction)
        {
            if (projectileReleaseDelay > 0f)
            {
                yield return new WaitForSeconds(projectileReleaseDelay);
            }

            if (enemyHealth.IsDead || projectilePrefab == null || targetHealth == null || targetHealth.IsDead)
            {
                yield break;
            }

            Vector2 origin = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : transform.position;
            EnemyProjectile2D projectile = Instantiate(
                projectilePrefab,
                origin,
                Quaternion.identity);
            projectile.Launch(direction, targetHealth, gameObject, attackDamage);
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
            deathAnimationPlaying = true;
            animationLockedUntil = float.PositiveInfinity;
            ChangeAnimation(deathStateHash, transitionDuration, true);
            onEnemyDied?.Invoke();
            EnemyDied?.Invoke(this);

            if (destroyOnDeath)
            {
                Destroy(gameObject, destroyDelay);
            }
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

            CacheAnimationHashes();
        }
    }
}
