using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class DivePointInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Entrar na fase de farm";
        [SerializeField] private string sceneName = "DiveScene";
        [SerializeField] private bool available = true;

        private bool isLoading;

        public string Prompt => prompt;

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
