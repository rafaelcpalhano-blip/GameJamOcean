using TMPro;
using UnityEngine;

namespace GameJamOcean.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptUI2D : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInteractor2D playerInteractor;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private TMP_Text promptText;

        [Header("Messages")]
        [SerializeField] private string firstTimeMessage = "Press F or Click";
        [SerializeField] private string nearbyMessage = "[ F ]";
        [SerializeField, Min(0.5f)] private float firstTimeDuration = 4f;

        [Header("Visibility")]
        [SerializeField, Range(0f, 0.25f)] private float viewportMargin = 0.03f;

        private RectTransform promptRect;
        private Canvas parentCanvas;
        private InteractionPromptTarget2D tutorialTarget;
        private float tutorialEndTime;

        private void Awake()
        {
            FindReferencesIfNeeded();
            Hide();
        }

        private void Update()
        {
            FindReferencesIfNeeded();
            if (promptText == null || worldCamera == null)
            {
                return;
            }

            if (tutorialTarget != null)
            {
                if (tutorialTarget.IsAvailable
                    && Time.unscaledTime < tutorialEndTime
                    && IsOnScreen(tutorialTarget.PromptWorldPosition))
                {
                    Show(tutorialTarget, firstTimeMessage);
                    return;
                }

                tutorialTarget = null;
            }

            InteractionPromptTarget2D newTutorial = FindUndiscoveredVisibleTarget();
            if (newTutorial != null)
            {
                tutorialTarget = newTutorial;
                tutorialEndTime = Time.unscaledTime + firstTimeDuration;
                InteractionDiscoveryStore.MarkSeen(newTutorial.DiscoveryKey);
                Show(newTutorial, firstTimeMessage);
                return;
            }

            InteractionPromptTarget2D nearbyTarget = FindTarget(playerInteractor?.CurrentInteractable);
            if (nearbyTarget != null && nearbyTarget.IsAvailable)
            {
                Show(nearbyTarget, nearbyMessage);
                return;
            }

            Hide();
        }

        [ContextMenu("Reset Interaction Tutorials")]
        public void ResetInteractionTutorials()
        {
            InteractionDiscoveryStore.ResetAll();
            tutorialTarget = null;
            Hide();
        }

        private InteractionPromptTarget2D FindUndiscoveredVisibleTarget()
        {
            InteractionPromptTarget2D bestTarget = null;
            float bestDistance = float.PositiveInfinity;
            var targets = InteractionPromptTarget2D.ActiveTargets;

            for (int index = 0; index < targets.Count; index++)
            {
                InteractionPromptTarget2D candidate = targets[index];
                if (candidate == null
                    || !candidate.IsAvailable
                    || InteractionDiscoveryStore.HasSeen(candidate.DiscoveryKey)
                    || !IsOnScreen(candidate.PromptWorldPosition))
                {
                    continue;
                }

                Vector3 viewportPoint = worldCamera.WorldToViewportPoint(candidate.PromptWorldPosition);
                float distance = ((Vector2)viewportPoint - new Vector2(0.5f, 0.5f)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestTarget = candidate;
                    bestDistance = distance;
                }
            }

            return bestTarget;
        }

        private static InteractionPromptTarget2D FindTarget(IInteractable interactable)
        {
            if (interactable == null)
            {
                return null;
            }

            var targets = InteractionPromptTarget2D.ActiveTargets;
            for (int index = 0; index < targets.Count; index++)
            {
                InteractionPromptTarget2D target = targets[index];
                if (target != null && target.Matches(interactable))
                {
                    return target;
                }
            }

            return null;
        }

        private bool IsOnScreen(Vector3 worldPosition)
        {
            Vector3 viewportPoint = worldCamera.WorldToViewportPoint(worldPosition);
            return viewportPoint.z > 0f
                && viewportPoint.x >= viewportMargin
                && viewportPoint.x <= 1f - viewportMargin
                && viewportPoint.y >= viewportMargin
                && viewportPoint.y <= 1f - viewportMargin;
        }

        private void Show(InteractionPromptTarget2D target, string message)
        {
            promptText.text = message;
            promptText.enabled = true;

            Vector2 screenPosition = worldCamera.WorldToScreenPoint(target.PromptWorldPosition);
            if (parentCanvas == null || parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                promptRect.position = screenPosition;
                return;
            }

            RectTransform canvasRect = parentCanvas.transform as RectTransform;
            Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceCamera
                ? parentCanvas.worldCamera
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPosition, eventCamera, out Vector2 localPosition))
            {
                promptRect.anchoredPosition = localPosition;
            }
        }

        private void Hide()
        {
            if (promptText != null)
            {
                promptText.enabled = false;
            }
        }

        private void FindReferencesIfNeeded()
        {
            if (playerInteractor == null)
            {
                playerInteractor = FindFirstObjectByType<PlayerInteractor2D>();
            }

            if (worldCamera == null)
            {
                worldCamera = Camera.main;
            }

            if (promptText == null)
            {
                promptText = GetComponent<TMP_Text>();
            }

            if (promptText != null)
            {
                promptRect = promptText.rectTransform;
                parentCanvas = promptText.GetComponentInParent<Canvas>();
            }
        }

        private void OnValidate()
        {
            firstTimeDuration = Mathf.Max(0.5f, firstTimeDuration);
        }
    }
}
