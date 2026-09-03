using GameJamOcean.Boat;
using GameJamOcean.Spawning;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DivePointInteractable3D : MonoBehaviour, IInteractable
    {
        [SerializeField] private string prompt = "Entrar na fase de mergulho";
        [SerializeField] private string sceneName = "DiveScene";
        [SerializeField] private bool available = true;

        [Header("Interaction Prompt")]
        [SerializeField] private string discoveryKey = "DiveBuoy";
        [SerializeField] private Vector3 promptOffset = new(0f, 0.4f, 0f);

        private bool isLoading;

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
                Debug.LogError($"The dive scene '{sceneName}' is not in Build Settings.", this);
                return;
            }

            isLoading = true;
            OceanReturnState3D.Save(interactor.transform.root);
            GetComponent<SpawnedDivePoint3D>()?.NotifySelected();
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
    }
}
