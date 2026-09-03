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

        [Header("Interaction Prompt")]
        [SerializeField] private string discoveryKey = "GenericInteraction";
        [SerializeField] private Vector3 promptOffset = new(0f, 0.25f, 0f);

        [Header("Response")]
        [SerializeField] private bool logInteraction = true;
        [SerializeField] private UnityEvent onInteracted = new();

        public string Prompt => prompt;

        private void Awake()
        {
            InteractionPromptTarget2D target = GetComponent<InteractionPromptTarget2D>();
            if (target == null)
            {
                target = gameObject.AddComponent<InteractionPromptTarget2D>();
            }

            target.Configure(this, discoveryKey, promptOffset);
        }

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
