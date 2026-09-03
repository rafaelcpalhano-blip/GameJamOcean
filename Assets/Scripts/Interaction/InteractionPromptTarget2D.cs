using System.Collections.Generic;
using UnityEngine;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptTarget2D : MonoBehaviour
    {
        private static readonly List<InteractionPromptTarget2D> Targets = new();

        [Header("Runtime Configuration")]
        [SerializeField] private string discoveryKey;
        [SerializeField] private Vector3 promptOffset = new(0f, 0.25f, 0f);
        [SerializeField] private MonoBehaviour interactableBehaviour;

        public static IReadOnlyList<InteractionPromptTarget2D> ActiveTargets => Targets;
        public string DiscoveryKey => discoveryKey;
        public IInteractable Interactable => interactableBehaviour as IInteractable;
        public bool IsAvailable => isActiveAndEnabled
            && Interactable != null
            && interactableBehaviour.isActiveAndEnabled;

        public Vector3 PromptWorldPosition
        {
            get
            {
                Renderer targetRenderer = GetComponentInChildren<Renderer>();
                if (targetRenderer != null)
                {
                    Bounds bounds = targetRenderer.bounds;
                    return new Vector3(bounds.center.x, bounds.max.y, transform.position.z) + promptOffset;
                }

                Collider2D targetCollider = GetComponentInChildren<Collider2D>();
                if (targetCollider != null)
                {
                    Bounds bounds = targetCollider.bounds;
                    return new Vector3(bounds.center.x, bounds.max.y, transform.position.z) + promptOffset;
                }

                return transform.position + promptOffset;
            }
        }

        public void Configure(IInteractable interactable, string key, Vector3 offset)
        {
            interactableBehaviour = interactable as MonoBehaviour;
            discoveryKey = string.IsNullOrWhiteSpace(key)
                ? interactableBehaviour.GetType().FullName
                : key.Trim();
            promptOffset = offset;
        }

        public bool Matches(IInteractable interactable)
        {
            return ReferenceEquals(Interactable, interactable);
        }

        private void OnEnable()
        {
            if (!Targets.Contains(this))
            {
                Targets.Add(this);
            }
        }

        private void OnDisable()
        {
            Targets.Remove(this);
        }
    }
}
