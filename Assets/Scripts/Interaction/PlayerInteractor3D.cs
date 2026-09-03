using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor3D : MonoBehaviour
    {
        private const int MaximumHits = 24;

        [Header("Input")]
        [SerializeField] private InputActionReference interactAction;

        [Header("Detection")]
        [SerializeField] private Transform interactionOrigin;
        [SerializeField, Min(0.1f)] private float interactionRadius = 2.5f;
        [SerializeField] private LayerMask interactionLayers = ~0;
        [SerializeField] private Camera interactionCamera;

        private readonly Collider[] nearbyColliders = new Collider[MaximumHits];
        private readonly RaycastHit[] pointerHits = new RaycastHit[MaximumHits];
        private InputAction activeAction;
        private InputAction fallbackAction;
        private bool enabledActiveAction;
        private IInteractable currentInteractable;

        public event Action<IInteractable> FocusChanged;
        public IInteractable CurrentInteractable => currentInteractable;

        private void Awake()
        {
            interactionOrigin ??= transform;
            interactionCamera ??= Camera.main;
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
            if (activeAction != null)
            {
                activeAction.performed -= OnInteractPerformed;
                if (enabledActiveAction)
                {
                    activeAction.Disable();
                }
            }

            activeAction = null;
            enabledActiveAction = false;
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

        public void Configure(Camera targetCamera, Transform origin, LayerMask layers)
        {
            interactionCamera = targetCamera;
            interactionOrigin = origin != null ? origin : transform;
            interactionLayers = layers;
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

        private void FindClosestInteractable()
        {
            Vector3 origin = interactionOrigin.position;
            int count = Physics.OverlapSphereNonAlloc(
                origin,
                interactionRadius,
                nearbyColliders,
                interactionLayers,
                QueryTriggerInteraction.Collide);

            IInteractable closest = null;
            float closestDistance = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                Collider nearbyCollider = nearbyColliders[index];
                IInteractable candidate = FindInteractable(nearbyCollider);
                if (candidate == null || !candidate.CanInteract(gameObject))
                {
                    continue;
                }

                float distance = (nearbyCollider.ClosestPoint(origin) - origin).sqrMagnitude;
                if (distance < closestDistance)
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }

            SetCurrentInteractable(closest);
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

            Ray ray = interactionCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            int count = Physics.RaycastNonAlloc(
                ray,
                pointerHits,
                interactionCamera.farClipPlane,
                interactionLayers,
                QueryTriggerInteraction.Collide);
            Vector3 origin = interactionOrigin.position;

            for (int index = 0; index < count; index++)
            {
                Collider clickedCollider = pointerHits[index].collider;
                IInteractable candidate = FindInteractable(clickedCollider);
                if (candidate == null
                    || !candidate.CanInteract(gameObject)
                    || (clickedCollider.ClosestPoint(origin) - origin).sqrMagnitude
                        > interactionRadius * interactionRadius)
                {
                    continue;
                }

                candidate.Interact(gameObject);
                return;
            }
        }

        private static IInteractable FindInteractable(Collider targetCollider)
        {
            MonoBehaviour[] behaviours = targetCollider.GetComponentsInParent<MonoBehaviour>(true);
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
            FocusChanged?.Invoke(value);
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            FindClosestInteractable();
            if (currentInteractable != null && currentInteractable.CanInteract(gameObject))
            {
                currentInteractable.Interact(gameObject);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform origin = interactionOrigin != null ? interactionOrigin : transform;
            Gizmos.color = currentInteractable != null ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(origin.position, interactionRadius);
        }
    }
}
