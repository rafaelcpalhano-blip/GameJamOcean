using System.Collections;
using GameJamOcean.Boat;
using GameJamOcean.Spawning;
using GameJamOcean.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DivePointInteractable3D : MonoBehaviour, IInteractable
    {
        [SerializeField] private string sceneName = "DiveScene";
        [SerializeField] private bool available = true;

        [Header("Interaction Prompt")]
        [SerializeField] private string discoveryKey = "DiveBuoy";
        [SerializeField] private Vector3 promptOffset = new(0f, 0.4f, 0f);

        private bool isLoading;
        private BoatController3D transitioningBoat;
        private Rigidbody transitioningBody;

        public string Prompt => LocalizationManager.Get("interaction.enter_dive");

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
            FreezeBoatForDive(interactor);
            StartCoroutine(LoadDiveAfterJumpSound());
        }

        private void FreezeBoatForDive(GameObject interactor)
        {
            transitioningBoat = interactor != null ? interactor.GetComponentInParent<BoatController3D>() : null;
            if (transitioningBoat == null && interactor != null)
                transitioningBoat = interactor.transform.root.GetComponent<BoatController3D>();
            if (transitioningBoat == null) return;

            transitioningBody = transitioningBoat.GetComponent<Rigidbody>();
            transitioningBoat.enabled = false;
            if (transitioningBody == null) return;
            transitioningBody.linearVelocity = Vector3.zero;
            transitioningBody.angularVelocity = Vector3.zero;
            transitioningBody.isKinematic = true;
        }

        private IEnumerator LoadDiveAfterJumpSound()
        {
            GameJamOcean.Audio.GameAudio audio = GameJamOcean.Audio.GameAudio.Instance;
            audio?.PlayDiverWaterJump();
            float duration = audio != null ? audio.DiverWaterJumpDuration : 0f;
            if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        }
    }
}
