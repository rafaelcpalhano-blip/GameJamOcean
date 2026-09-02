using UnityEngine;
using UnityEngine.Events;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class InteractionEvent : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] private string prompt = "Interagir";
        [SerializeField] private bool available = true;
        [SerializeField] private bool oneShot;

        [Header("Response")]
        [SerializeField] private bool logInteraction = true;
        [SerializeField] private UnityEvent onInteracted = new();

        public string Prompt => prompt;

        public bool CanInteract(GameObject interactor)
        {
            return available && isActiveAndEnabled;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (logInteraction)
            {
                Debug.Log($"{interactor.name}: {prompt} -> {name}", this);
            }

            onInteracted.Invoke();

            if (oneShot)
            {
                available = false;
            }
        }

        public void SetAvailable(bool value)
        {
            available = value;
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                prompt = "Interagir";
            }
        }
    }
}
