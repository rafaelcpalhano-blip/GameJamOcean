using System.Collections;
using System.Collections.Generic;
using GameJamOcean.Collectibles;
using GameJamOcean.Diving;
using GameJamOcean.World;
using UnityEngine;

namespace GameJamOcean.Spawning
{
    [DisallowMultipleComponent]
    public sealed class GoldSpawner2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DiveSessionManager sessionManager;
        [SerializeField] private MovementBounds2D spawnArea;
        [SerializeField] private GoldCollectible2D goldPrefab;
        [SerializeField] private Transform spawnedGoldParent;

        [Header("Amount Per Dive")]
        [SerializeField, Min(0)] private int initialAmount = 20;
        [SerializeField, Min(1)] private int maximumAmount = 50;

        [Header("Timed Spawn")]
        [SerializeField, Min(0.05f)] private float spawnInterval = 2f;
        [SerializeField, Min(1)] private int amountPerInterval = 1;

        [Header("Position Validation")]
        [SerializeField, Min(0f)] private float edgePadding = 0.6f;
        [SerializeField, Min(0f)] private float minimumGoldSeparation = 0.6f;
        [SerializeField, Min(1)] private int positionAttempts = 30;
        [SerializeField, Min(0f)] private float obstacleCheckRadius = 0.25f;
        [SerializeField] private LayerMask blockingLayers;
        [Header("End of Dive")]
        [SerializeField, Min(.1f)] private float uncollectedFadeDuration = .75f;

        [Header("Runtime")]
        [SerializeField] private int totalSpawned;
        [SerializeField] private int currentlyAlive;
        [SerializeField] private int totalCollected;

        private readonly List<GoldCollectible2D> activeGold = new();
        public Sprite CoinSprite => goldPrefab != null ? goldPrefab.GetComponentInChildren<SpriteRenderer>()?.sprite : null;

        private void Awake()
        {
            FindReferencesIfNeeded();
        }

        private void OnEnable()
        {
            FindReferencesIfNeeded();
            if (sessionManager != null) sessionManager.FinalChestReady += FadeUncollectedGold;
        }

        private void OnDisable()
        {
            if (sessionManager != null) sessionManager.FinalChestReady -= FadeUncollectedGold;
        }

        private IEnumerator Start()
        {
            yield return null;

            FindReferencesIfNeeded();
            if (!IsConfigured())
            {
                yield break;
            }

            int initialSpawnCount = Mathf.Min(initialAmount, maximumAmount);
            for (int index = 0; index < initialSpawnCount; index++)
            {
                TrySpawnGold();
            }

            while (totalSpawned < maximumAmount)
            {
                yield return new WaitForSeconds(spawnInterval);

                if (sessionManager == null
                    || sessionManager.SessionState != DiveSessionState.Running)
                {
                    yield break;
                }

                int amountToSpawn = Mathf.Min(amountPerInterval, maximumAmount - totalSpawned);
                for (int index = 0; index < amountToSpawn; index++)
                {
                    TrySpawnGold();
                }
            }
        }

        public bool TrySpawnGold()
        {
            if (!IsConfigured() || totalSpawned >= maximumAmount)
            {
                return false;
            }

            if (!TryFindPosition(out Vector2 spawnPosition))
            {
                return false;
            }

            GoldCollectible2D collectible = Instantiate(
                goldPrefab,
                spawnPosition,
                Quaternion.identity,
                spawnedGoldParent);
            collectible.Collected += HandleGoldCollected;
            activeGold.Add(collectible);
            totalSpawned++;
            currentlyAlive++;
            return true;
        }

        private bool TryFindPosition(out Vector2 result)
        {
            Bounds bounds = spawnArea.Bounds;
            float minimumX = bounds.min.x + edgePadding;
            float maximumX = bounds.max.x - edgePadding;
            float minimumY = bounds.min.y + edgePadding;
            float maximumY = bounds.max.y - edgePadding;

            if (minimumX > maximumX || minimumY > maximumY)
            {
                result = bounds.center;
                return false;
            }

            RemoveMissingReferences();

            for (int attempt = 0; attempt < positionAttempts; attempt++)
            {
                Vector2 candidate = new(
                    Random.Range(minimumX, maximumX),
                    Random.Range(minimumY, maximumY));

                if (IsTooCloseToGold(candidate) || IsBlocked(candidate))
                {
                    continue;
                }

                result = candidate;
                return true;
            }

            result = default;
            return false;
        }

        private bool IsTooCloseToGold(Vector2 candidate)
        {
            float minimumDistanceSquared = minimumGoldSeparation * minimumGoldSeparation;
            foreach (GoldCollectible2D collectible in activeGold)
            {
                if (collectible != null
                    && ((Vector2)collectible.transform.position - candidate).sqrMagnitude
                        < minimumDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBlocked(Vector2 candidate)
        {
            return blockingLayers.value != 0
                && Physics2D.OverlapCircle(candidate, obstacleCheckRadius, blockingLayers) != null;
        }

        private void HandleGoldCollected(GoldCollectible2D collectible)
        {
            collectible.Collected -= HandleGoldCollected;
            activeGold.Remove(collectible);
            currentlyAlive = Mathf.Max(0, currentlyAlive - 1);
            totalCollected++;
        }

        private void FadeUncollectedGold()
        {
            RemoveMissingReferences();
            foreach (GoldCollectible2D collectible in activeGold)
                if (collectible != null) collectible.FadeOutAndDestroy(uncollectedFadeDuration);
            activeGold.Clear();
            currentlyAlive = 0;
        }

        private void RemoveMissingReferences()
        {
            activeGold.RemoveAll(item => item == null);
            currentlyAlive = activeGold.Count;
        }

        private bool IsConfigured()
        {
            if (sessionManager != null && spawnArea != null && goldPrefab != null)
            {
                return true;
            }

            Debug.LogWarning(
                $"{nameof(GoldSpawner2D)} on '{name}' needs Session Manager, Spawn Area and Gold Prefab.",
                this);
            return false;
        }

        private void FindReferencesIfNeeded()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }

            if (spawnArea == null)
            {
                spawnArea = FindFirstObjectByType<MovementBounds2D>();
            }
        }

        private void OnValidate()
        {
            initialAmount = Mathf.Max(0, initialAmount);
            maximumAmount = Mathf.Max(1, maximumAmount);
            initialAmount = Mathf.Min(initialAmount, maximumAmount);
            spawnInterval = Mathf.Max(0.05f, spawnInterval);
            amountPerInterval = Mathf.Max(1, amountPerInterval);
            edgePadding = Mathf.Max(0f, edgePadding);
            minimumGoldSeparation = Mathf.Max(0f, minimumGoldSeparation);
            positionAttempts = Mathf.Max(1, positionAttempts);
            obstacleCheckRadius = Mathf.Max(0f, obstacleCheckRadius);
            uncollectedFadeDuration = Mathf.Max(.1f, uncollectedFadeDuration);
        }

        private void OnDrawGizmosSelected()
        {
            MovementBounds2D area = spawnArea != null
                ? spawnArea
                : FindFirstObjectByType<MovementBounds2D>();
            if (area == null)
            {
                return;
            }

            Bounds bounds = area.Bounds;
            Vector3 size = bounds.size - new Vector3(edgePadding * 2f, edgePadding * 2f, 0f);
            size.x = Mathf.Max(0f, size.x);
            size.y = Mathf.Max(0f, size.y);
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.9f);
            Gizmos.DrawWireCube(bounds.center, size);
        }
    }
}
