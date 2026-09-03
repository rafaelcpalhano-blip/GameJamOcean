using System;
using System.Collections;
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

        [Header("Attack")]
        [SerializeField] private EnemyAttackType attackType = EnemyAttackType.Melee;
        [SerializeField, Min(0f)] private float attackRange = 0.8f;
        [SerializeField, Min(0f)] private float attackDamage = 1f;
        [SerializeField, Min(0.01f)] private float attackCooldown = 1.25f;
        [SerializeField] private UnityEvent onAttack;
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
                enemyRigidbody.linearVelocity = Vector2.zero;
                TryAttack();
                UpdateMovementAnimation();
                return;
            }

            enemyRigidbody.linearVelocity = distance > stoppingDistance
                ? direction * movementSpeed
                : Vector2.zero;
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
