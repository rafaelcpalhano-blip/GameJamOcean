using UnityEngine;
using UnityEngine.Events;
using GameJamOcean.Localization;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public sealed class InteractionEvent3D : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Abrir upgrades do porto";
        [SerializeField] private string discoveryKey = "PortUpgrades";
        [SerializeField] private Vector3 promptOffset = new(0f, 0.5f, 0f);
        [SerializeField] private bool available = true;
        [SerializeField] private bool logInteraction = true;
        [SerializeField, Min(0f)] private float interactionCooldown = 0.3f;
        [Header("Future Panel / Other Actions")]
        [SerializeField] private UnityEvent onInteracted = new();
        private float nextInteractionTime;
        public string Prompt => discoveryKey == "PortUpgrades"
            ? LocalizationManager.Get("interaction.open_upgrades")
            : LocalizationManager.Get("interaction.interact");
        public UnityEvent OnInteracted => onInteracted;

        private void Awake()
        {
            InteractionPromptTarget2D target = GetComponent<InteractionPromptTarget2D>();
            if (target == null) target = gameObject.AddComponent<InteractionPromptTarget2D>();
            target.Configure(this, discoveryKey, promptOffset);
        }

        public bool CanInteract(GameObject interactor)
        {
            return available && isActiveAndEnabled && Time.time >= nextInteractionTime;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor)) return;
            nextInteractionTime = Time.time + interactionCooldown;
            if (logInteraction) Debug.Log($"{name}: {prompt} (evento On Interacted acionado).", this);
            onInteracted.Invoke();
        }

        public void SetAvailable(bool value) => available = value;
    }
}
