using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor2D : MonoBehaviour
    {
        private const int MaximumNearbyColliders = 16;

        [Header("Input")]
        [Tooltip("Optional. When empty, F and the gamepad north button are used.")]
        [SerializeField] private InputActionReference interactAction;

        [Header("Detection")]
        [SerializeField] private Transform interactionOrigin;
        [SerializeField, Min(0.1f)] private float interactionRadius = 1.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private Camera interactionCamera;

        private readonly Collider2D[] nearbyColliders = new Collider2D[MaximumNearbyColliders];
        private readonly Collider2D[] clickedColliders = new Collider2D[MaximumNearbyColliders];
        private ContactFilter2D contactFilter;
        private InputAction activeAction;
        private InputAction fallbackAction;
        private bool enabledActiveAction;
        private IInteractable currentInteractable;

        public event Action<IInteractable> FocusChanged;

        public IInteractable CurrentInteractable => currentInteractable;
        public string CurrentPrompt => currentInteractable?.Prompt ?? string.Empty;

        private void Awake()
        {
            if (interactionOrigin == null)
            {
                interactionOrigin = transform;
            }

            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }

            ConfigureContactFilter();
        }

        private void OnEnable()
        {
            activeAction = interactAction != null ? interactAction.action : CreateFallbackAction();
            activeAction.performed += OnInteractPerformed;

            enabledActiveAction = !activeAction.enabled;
            if (enabledActiveAction)
            {
                activeAction.Enable();
            }
        }

        private void OnDisable()
        {
            SetCurrentInteractable(null);

            if (activeAction == null)
            {
                return;
            }

            activeAction.performed -= OnInteractPerformed;
            if (enabledActiveAction)
            {
                activeAction.Disable();
            }

            enabledActiveAction = false;
            activeAction = null;
        }

        private void OnDestroy()
        {
            fallbackAction?.Dispose();
        }

        private void Update()
        {
            FindClosestInteractable();
            HandleLeftClick();
        }

        private void HandleLeftClick()
        {
            if (Mouse.current == null
                || !Mouse.current.leftButton.wasPressedThisFrame
                || interactionCamera == null
                || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = interactionCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -interactionCamera.transform.position.z));

            int count = Physics2D.OverlapPoint(worldPosition, contactFilter, clickedColliders);
            Vector2 origin = interactionOrigin.position;

            for (int index = 0; index < count; index++)
            {
                Collider2D clickedCollider = clickedColliders[index];
                IInteractable candidate = FindInteractable(clickedCollider);
                if (candidate == null
                    || !candidate.CanInteract(gameObject)
                    || ((Vector2)clickedCollider.ClosestPoint(origin) - origin).sqrMagnitude
                        > interactionRadius * interactionRadius)
                {
                    continue;
                }

                candidate.Interact(gameObject);
                return;
            }
        }

        private InputAction CreateFallbackAction()
        {
            if (fallbackAction != null)
            {
                return fallbackAction;
            }

            fallbackAction = new InputAction("Interact", InputActionType.Button);
            fallbackAction.AddBinding("<Keyboard>/f");
            fallbackAction.AddBinding("<Gamepad>/buttonNorth");
            return fallbackAction;
        }

        private void ConfigureContactFilter()
        {
            contactFilter = new ContactFilter2D();
            contactFilter.SetLayerMask(interactionLayers);
            contactFilter.useTriggers = true;
        }

        private void FindClosestInteractable()
        {
            Vector2 origin = interactionOrigin.position;
            int count = Physics2D.OverlapCircle(
                origin,
                interactionRadius,
                contactFilter,
                nearbyColliders);

            IInteractable closest = null;
            float closestDistance = float.PositiveInfinity;

            for (int index = 0; index < count; index++)
            {
                Collider2D nearbyCollider = nearbyColliders[index];
                IInteractable candidate = FindInteractable(nearbyCollider);
                if (candidate == null || !candidate.CanInteract(gameObject))
                {
                    continue;
                }

                float distance = ((Vector2)nearbyCollider.ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance >= closestDistance)
                {
                    continue;
                }

                closest = candidate;
                closestDistance = distance;
            }

            SetCurrentInteractable(closest);
        }

        private static IInteractable FindInteractable(Collider2D nearbyCollider)
        {
            MonoBehaviour[] behaviours = nearbyCollider.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable)
                {
                    return interactable;
                }
            }

            return null;
        }

        private void SetCurrentInteractable(IInteractable value)
        {
            if (ReferenceEquals(currentInteractable, value))
            {
                return;
            }

            currentInteractable = value;
            FocusChanged?.Invoke(currentInteractable);
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (currentInteractable == null)
            {
                FindClosestInteractable();
            }

            if (currentInteractable != null && currentInteractable.CanInteract(gameObject))
            {
                currentInteractable.Interact(gameObject);
            }
        }

        private void OnValidate()
        {
            interactionRadius = Mathf.Max(0.1f, interactionRadius);
            ConfigureContactFilter();
        }

        private void OnDrawGizmosSelected()
        {
            Transform originTransform = interactionOrigin != null ? interactionOrigin : transform;
            Gizmos.color = currentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(originTransform.position, interactionRadius);
        }
    }
}
