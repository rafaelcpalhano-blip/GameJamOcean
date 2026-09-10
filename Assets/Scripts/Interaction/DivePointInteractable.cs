using UnityEngine;
using UnityEngine.SceneManagement;
using GameJamOcean.Localization;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DivePointInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Entrar na fase de farm";
        [SerializeField] private string sceneName = "DiveScene";
        [SerializeField] private bool available = true;

        [Header("Interaction Prompt")]
        [SerializeField] private string discoveryKey = "DiveBuoy";
        [SerializeField] private Vector3 promptOffset = new(0f, 0.25f, 0f);

        private bool isLoading;

        public string Prompt => LocalizationManager.Get("interaction.enter_dive");

        private void Awake()
        {
            ConfigurePromptTarget();
        }

        public bool CanInteract(GameObject interactor)
        {
            return available && !isLoading && isActiveAndEnabled;
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(sceneName)
                || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError(
                    $"The dive scene '{sceneName}' is not available in the build settings.",
                    this);
                return;
            }

            isLoading = true;
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }

        public void SetAvailable(bool value)
        {
            available = value;
        }

        private void ConfigurePromptTarget()
        {
            InteractionPromptTarget2D target = GetComponent<InteractionPromptTarget2D>();
            if (target == null)
            {
                target = gameObject.AddComponent<InteractionPromptTarget2D>();
            }

            target.Configure(this, discoveryKey, promptOffset);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                prompt = "Entrar na fase de farm";
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                sceneName = "DiveScene";
            }
        }
    }
}
