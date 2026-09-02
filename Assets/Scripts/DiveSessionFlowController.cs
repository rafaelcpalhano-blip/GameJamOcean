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

        [Header("Return To Ocean")]
        [SerializeField] private string oceanSceneName = "OceanScene";
        [SerializeField, Min(0f)] private float victoryReturnDelay = 1.5f;
        [SerializeField, Min(0f)] private float defeatReturnDelay = 1f;

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
            UnsubscribeFromSession();
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
            FinishSession(victoryReturnDelay);
        }

        private void HandleDefeat()
        {
            if (transitioning)
            {
                return;
            }

            completedSuccessfully = false;
            committedGold = sessionManager.SecuredGold;
            FinishSession(defeatReturnDelay);
        }

        private void FinishSession(float returnDelay)
        {
            transitioning = true;
            DisablePlayerControl();
            GameProgress.Instance.AddGold(committedGold);
            StartCoroutine(ReturnToOceanAfterDelay(returnDelay));
        }

        private IEnumerator ReturnToOceanAfterDelay(float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSecondsRealtime(delay);
            }

            if (string.IsNullOrWhiteSpace(oceanSceneName))
            {
                Debug.LogError("Ocean scene name is empty.", this);
                transitioning = false;
                yield break;
            }

            SceneManager.LoadScene(oceanSceneName);
        }

        private void DisablePlayerControl()
        {
            if (playerMovement != null)
            {
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
