using System;
using System.Collections;
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
        [Header("Player Attraction")]
        [SerializeField, Min(.1f)] private float attractionRadius = .7f;
        [SerializeField, Min(.1f)] private float attractionSpeed = 6f;
        [SerializeField, Min(.1f)] private float attractionAcceleration = 14f;

        [Header("Events")]
        [SerializeField] private UnityEvent onCollected;

        [Header("Runtime")]
        [SerializeField] private bool collected;

        public event Action<GoldCollectible2D> Collected;

        private CircleCollider2D collectibleCollider;
        private Transform player;
        private float currentAttractionSpeed;
        private Coroutine fading;

        private void Awake()
        {
            collectibleCollider = GetComponent<CircleCollider2D>();
            collectibleCollider.isTrigger = true;

            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }
            DiverController diver = FindFirstObjectByType<DiverController>();
            player = diver != null ? diver.transform : null;
        }

        private void Update()
        {
            if (collected || Time.timeScale <= 0f) return;
            if (player == null)
            {
                DiverController diver = FindFirstObjectByType<DiverController>();
                player = diver != null ? diver.transform : null;
            }
            if (player == null) return;
            Vector3 delta = player.position - transform.position;
            delta.z = 0f;
            if (delta.sqrMagnitude > attractionRadius * attractionRadius)
            {
                currentAttractionSpeed = 0f;
                return;
            }
            currentAttractionSpeed = Mathf.MoveTowards(currentAttractionSpeed,
                attractionSpeed, attractionAcceleration * Time.deltaTime);
            transform.position = Vector3.MoveTowards(transform.position, player.position,
                currentAttractionSpeed * Time.deltaTime);
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

        public void FadeOutAndDestroy(float duration)
        {
            if (collected || fading != null) return;
            collected = true;
            collectibleCollider.enabled = false;
            fading = StartCoroutine(FadeOut(Mathf.Max(.1f, duration)));
        }

        private IEnumerator FadeOut(float duration)
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
            Color[] initialColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) initialColors[i] = renderers[i].color;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Color color = initialColors[i];
                    color.a *= alpha;
                    renderers[i].color = color;
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnValidate()
        {
            goldValue = Mathf.Max(1, goldValue);
        }
    }
}
