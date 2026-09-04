using System.Collections;
using GameJamOcean.Diving;
using GameJamOcean.Player;
using GameJamOcean.Progression;
using GameJamOcean.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameJamOcean.Flow
{
    [DisallowMultipleComponent]
    public sealed class DiveSessionFlowController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DiveSessionManager sessionManager;
        [SerializeField] private DiverController playerMovement;
        [SerializeField] private HarpoonLauncher2D playerWeapon;

        [Header("Ocean Scene / Delay Before Results")]
        [SerializeField] private string oceanSceneName = "OceanScene_3D";
        [SerializeField, Min(0f)] private float victoryReturnDelay = 1.5f;
        [SerializeField, Min(0f)] private float defeatReturnDelay = 1f;
        [Header("Results")]
        [SerializeField] private Sprite coinIcon;
        private bool showingResults;
        private bool loadingOcean;
        private float previousTimeScale;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLock;

        [Header("Runtime")]
        [SerializeField] private bool transitioning;
        [SerializeField] private bool completedSuccessfully;
        [SerializeField] private int committedGold;

        public bool CompletedSuccessfully => completedSuccessfully;
        public int CommittedGold => committedGold;

        private void Awake()
        {
            FindReferencesIfNeeded();
        }

        private void OnEnable()
        {
            FindReferencesIfNeeded();
            SubscribeToSession();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            UnsubscribeFromSession();
            RestoreResultsState();
        }

        private void SubscribeToSession()
        {
            if (sessionManager == null)
            {
                return;
            }

            sessionManager.SessionWon -= HandleVictory;
            sessionManager.SessionLost -= HandleDefeat;
            sessionManager.SessionWon += HandleVictory;
            sessionManager.SessionLost += HandleDefeat;
        }

        private void UnsubscribeFromSession()
        {
            if (sessionManager == null)
            {
                return;
            }

            sessionManager.SessionWon -= HandleVictory;
            sessionManager.SessionLost -= HandleDefeat;
        }

        private void HandleVictory()
        {
            if (transitioning)
            {
                return;
            }

            completedSuccessfully = true;
            committedGold = sessionManager.TotalCollectedGold;
            FinishSession(Mathf.Max(1.5f, victoryReturnDelay));
        }

        private void HandleDefeat()
        {
            if (transitioning)
            {
                return;
            }

            completedSuccessfully = false;
            committedGold = sessionManager.SecuredGold + sessionManager.CollectedCoinGold;
            FinishSession(defeatReturnDelay);
        }

        private void FinishSession(float returnDelay)
        {
            transitioning = true;
            DisablePlayerControl();
            GameProgress.Instance.AddGold(committedGold);
            StartCoroutine(ShowResultsAfterDelay(returnDelay));
        }

        private IEnumerator ShowResultsAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (coinIcon == null) coinIcon = FindFirstObjectByType<GameJamOcean.Spawning.GoldSpawner2D>()?.CoinSprite;
            previousTimeScale = Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            showingResults = true;
            Time.timeScale = 0;
            Cursor.visible = true; Cursor.lockState = CursorLockMode.None;
            var panel = gameObject.AddComponent<DiveResultsPanel>();
            panel.Show(sessionManager, completedSuccessfully, committedGold, coinIcon, ReturnToOcean);
        }

        private void RestoreResultsState()
        {
            if (!showingResults) return;
            showingResults = false;
            Time.timeScale = previousTimeScale;
            Cursor.visible = previousCursorVisible; Cursor.lockState = previousCursorLock;
        }

        private void ReturnToOcean()
        {
            if (loadingOcean) return;

            if (string.IsNullOrWhiteSpace(oceanSceneName))
            {
                Debug.LogError("Ocean scene name is empty.", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(oceanSceneName))
            { Debug.LogError($"Scene '{oceanSceneName}' is not in the build scene list.", this); return; }
            loadingOcean = true;
            RestoreResultsState();

#if UNITY_EDITOR
            UnityEditor.Selection.activeObject = null;
#endif

            SceneManager.LoadScene(oceanSceneName);
        }

        private void DisablePlayerControl()
        {
            if (playerMovement != null)
            {
                if (playerMovement.TryGetComponent<GameJamOcean.Combat.Health>(out var health)) health.DamageBlocked = true;
                if (playerMovement.TryGetComponent<GameJamOcean.Interaction.PlayerInteractor2D>(out var interactor)) interactor.enabled = false;
                playerMovement.enabled = false;
            }

            if (playerWeapon != null)
            {
                playerWeapon.enabled = false;
            }
        }

        private void FindReferencesIfNeeded()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }

            if (playerMovement == null)
            {
                playerMovement = FindFirstObjectByType<DiverController>();
            }

            if (playerWeapon == null && playerMovement != null)
            {
                playerWeapon = playerMovement.GetComponent<HarpoonLauncher2D>();
            }
        }

        private void OnValidate()
        {
            victoryReturnDelay = Mathf.Max(0f, victoryReturnDelay);
            defeatReturnDelay = Mathf.Max(0f, defeatReturnDelay);
        }
    }
}
