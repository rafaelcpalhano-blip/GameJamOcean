using System;
using GameJamOcean.Diving;
using GameJamOcean.Player;
using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Collectibles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class GoldCollectible2D : MonoBehaviour
    {
        [Header("Reward")]
        [SerializeField, Min(1)] private int goldValue = 1;

        [Header("Collection Effect")]
        [SerializeField] private GameObject collectionEffectPrefab;

        [Header("References")]
        [SerializeField] private DiveSessionManager sessionManager;

        [Header("Events")]
        [SerializeField] private UnityEvent onCollected;

        [Header("Runtime")]
        [SerializeField] private bool collected;

        public event Action<GoldCollectible2D> Collected;

        private CircleCollider2D collectibleCollider;

        private void Awake()
        {
            collectibleCollider = GetComponent<CircleCollider2D>();
            collectibleCollider.isTrigger = true;

            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || other.GetComponentInParent<DiverController>() == null)
            {
                return;
            }

            Collect();
        }

        private void Collect()
        {
            if (sessionManager == null
                || sessionManager.SessionState is DiveSessionState.NotStarted
                    or DiveSessionState.Lost
                    or DiveSessionState.Won)
            {
                return;
            }

            collected = true;
            collectibleCollider.enabled = false;
            sessionManager.AddCollectedCoinGold(goldValue);

            if (collectionEffectPrefab != null)
            {
                Instantiate(collectionEffectPrefab, transform.position, Quaternion.identity);
            }

            onCollected?.Invoke();
            Collected?.Invoke(this);

#if UNITY_EDITOR
            GameObject selectedObject = UnityEditor.Selection.activeGameObject;
            if (selectedObject != null
                && (selectedObject == gameObject || selectedObject.transform.IsChildOf(transform)))
            {
                UnityEditor.Selection.activeObject = null;
            }
#endif

            Destroy(gameObject);
        }

        private void OnValidate()
        {
            goldValue = Mathf.Max(1, goldValue);
        }
    }
}
