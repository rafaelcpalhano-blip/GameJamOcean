using System.Collections;
using GameJamOcean.Diving;
using GameJamOcean.Interaction;
using GameJamOcean.Player;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace GameJamOcean.Rewards
{
    public enum ChestRewardType
    {
        Small,
        Final
    }

    public enum ChestMouseButton
    {
        Disabled,
        Left,
        Right
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class ChestRewardInteractable : MonoBehaviour, IInteractable
    {
        [Header("Chest")]
        [SerializeField] private ChestRewardType chestType = ChestRewardType.Small;
        [SerializeField] private string prompt = "Abrir baú";
        [SerializeField, Min(0)] private int minimumGold = 1;
        [SerializeField, Min(0)] private int maximumGold = 3;

        [Header("Interaction")]
        [SerializeField] private ChestMouseButton mouseButton = ChestMouseButton.Right;
        [SerializeField, Min(0f)] private float maximumInteractionDistance = 2f;
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private Transform player;

        [Header("Interaction Prompt")]
        [SerializeField] private Vector3 promptOffset = new(0f, 0.25f, 0f);

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string openTriggerName = "Open";

        [Header("Opening Reward Feedback")]
        [SerializeField] private Sprite rewardCoinSprite;
        [SerializeField, Min(1)] private int maximumVisualCoins = 18;
        [SerializeField, Min(0.1f)] private float rewardEffectDuration = 1.2f;
        [Header("Lifetime")]
        [SerializeField] private bool destroyAfterOpening = true;
        [SerializeField, Min(0f)] private float destroyDelay = 1f;

        [Header("References")]
        [SerializeField] private DiveSessionManager sessionManager;

        [Header("Events")]
        [SerializeField] private UnityEvent<int> onOpened;

        [Header("Runtime")]
        [SerializeField] private bool opened;
        [SerializeField] private int awardedGold;

        private BoxCollider2D chestCollider;

        public string Prompt => prompt;
        public bool IsOpened => opened;
        public ChestRewardType ChestType => chestType;

        private void Awake()
        {
            chestCollider = GetComponent<BoxCollider2D>();
            chestCollider.isTrigger = true;

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            FindReferencesIfNeeded();
            ConfigurePromptTarget();
        }

        private void OnEnable()
        {
            opened = false;
            awardedGold = 0;
            chestCollider.enabled = true;

            if (animator != null)
            {
                animator.enabled = false;
            }
        }

        private void Update()
        {
            if (opened || mouseButton == ChestMouseButton.Disabled || Mouse.current == null)
            {
                return;
            }

            bool pressed = mouseButton switch
            {
                ChestMouseButton.Left => Mouse.current.leftButton.wasPressedThisFrame,
                ChestMouseButton.Right => Mouse.current.rightButton.wasPressedThisFrame,
                _ => false
            };

            if (!pressed)
            {
                return;
            }

            FindReferencesIfNeeded();
            if (interactionCamera == null || player == null)
            {
                return;
            }

            Vector2 screenPosition = Mouse.current.position.ReadValue();
            Vector3 worldPosition = interactionCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, -interactionCamera.transform.position.z));

            if (chestCollider.OverlapPoint(worldPosition) && CanInteract(player.gameObject))
            {
                Interact(player.gameObject);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            if (opened || interactor == null || sessionManager == null)
            {
                return false;
            }

            if (sessionManager.SessionState is DiveSessionState.Lost or DiveSessionState.Won)
            {
                return false;
            }

            if (chestType == ChestRewardType.Final
                && sessionManager.SessionState != DiveSessionState.AwaitingFinalChest)
            {
                return false;
            }

            return maximumInteractionDistance <= 0f
                || Vector2.Distance(transform.position, interactor.transform.position)
                    <= maximumInteractionDistance;
        }

        public void Interact(GameObject interactor)
        {
            if (GameJamOcean.UI.GameMenus.BlocksGameplay) return;
            if (!CanInteract(interactor))
            {
                return;
            }

            opened = true;
            awardedGold = chestType == ChestRewardType.Final
                ? sessionManager.CalculateFinalChestReward()
                : Random.Range(minimumGold, maximumGold + 1);
            SpriteRenderer visual = GetComponentInChildren<SpriteRenderer>();
            Sprite chestIcon = visual != null ? visual.sprite : null;
            if (rewardCoinSprite == null)
                rewardCoinSprite = FindFirstObjectByType<GameJamOcean.Spawning.GoldSpawner2D>()?.CoinSprite;
            ChestRewardBurst.Spawn(transform.position + Vector3.up * 0.4f, rewardCoinSprite,
                awardedGold, maximumVisualCoins, rewardEffectDuration,
                visual != null ? visual.sortingLayerID : 0, visual != null ? visual.sortingOrder : 0);

            if (animator != null)
            {
                animator.enabled = true;
                animator.Rebind();
                animator.Update(0f);

                if (HasTriggerParameter(openTriggerName))
                {
                    animator.SetTrigger(openTriggerName);
                }
                else
                {
                    animator.Play(0, 0, 0f);
                    animator.Update(0f);
                }
            }

            if (chestType == ChestRewardType.Final)
            {
                sessionManager.SetFinalChestIcon(chestIcon);
                sessionManager.CollectFinalChest(awardedGold);
            }
            else
            {
                sessionManager.RecordSmallChest(awardedGold, chestIcon);
            }

            onOpened?.Invoke(awardedGold);

            if (destroyAfterOpening)
            {
                StartCoroutine(DestroyAfterDelay());
            }
        }

        private IEnumerator DestroyAfterDelay()
        {
            if (destroyDelay > 0f)
            {
                yield return new WaitForSeconds(destroyDelay);
            }

#if UNITY_EDITOR
            GameObject selectedObject = UnityEditor.Selection.activeGameObject;
            if (selectedObject != null
                && (selectedObject == gameObject || selectedObject.transform.IsChildOf(transform)))
            {
                UnityEditor.Selection.activeObject = null;
            }
#endif

            Destroy(gameObject);
        }

        private void FindReferencesIfNeeded()
        {
            if (sessionManager == null)
            {
                sessionManager = FindFirstObjectByType<DiveSessionManager>();
            }

            if (interactionCamera == null)
            {
                interactionCamera = Camera.main;
            }

            if (player == null)
            {
                DiverController diver = FindFirstObjectByType<DiverController>();
                if (diver != null)
                {
                    player = diver.transform;
                }
            }
        }

        private void ConfigurePromptTarget()
        {
            InteractionPromptTarget2D target = GetComponent<InteractionPromptTarget2D>();
            if (target == null)
            {
                target = gameObject.AddComponent<InteractionPromptTarget2D>();
            }

            string discoveryKey = chestType == ChestRewardType.Final
                ? "FinalChest"
                : "SmallChest";
            target.Configure(this, discoveryKey, promptOffset);
        }

        private bool HasTriggerParameter(string parameterName)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger
                    && parameter.name == parameterName)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnValidate()
        {
            minimumGold = Mathf.Max(0, minimumGold);
            maximumGold = Mathf.Max(minimumGold, maximumGold);
            maximumInteractionDistance = Mathf.Max(0f, maximumInteractionDistance);
            destroyDelay = Mathf.Max(0f, destroyDelay);
        }
    }
}
