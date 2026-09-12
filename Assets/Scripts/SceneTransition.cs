using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameJamOcean.Flow
{
    /// <summary>Persistent, unscaled fade shared by every gameplay scene change.</summary>
    [DefaultExecutionOrder(-3000)]
    public sealed class SceneTransition : MonoBehaviour
    {
        private const float FadeOutSeconds = 0.45f;
        private const float FadeInSeconds = 0.55f;

        private static SceneTransition instance;
        private CanvasGroup fadeGroup;
        private bool loadingScene;

        public static bool IsTransitioning => instance != null && instance.loadingScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (instance == null)
                new GameObject("Scene Transition").AddComponent<SceneTransition>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateOverlay();
        }

        private IEnumerator Start()
        {
            // Also gives the first loaded scene a gentle reveal.
            yield return null;
            yield return FadeTo(0f, FadeInSeconds);
            SetInputBlocked(false);
        }

        public static bool LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;
            Bootstrap();
            if (instance.loadingScene) return false;
            instance.StopAllCoroutines();
            instance.loadingScene = true;
            instance.StartCoroutine(instance.LoadSceneRoutine(sceneName));
            return true;
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            SetInputBlocked(true);
            yield return FadeTo(1f, FadeOutSeconds);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"Could not begin loading scene '{sceneName}'.", this);
                yield return FadeTo(0f, FadeInSeconds);
                SetInputBlocked(false);
                loadingScene = false;
                yield break;
            }

            while (!operation.isDone) yield return null;
            // Let the destination scene create its camera and runtime UI behind black.
            yield return null;
            yield return FadeTo(0f, FadeInSeconds);
            SetInputBlocked(false);
            loadingScene = false;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = fadeGroup.alpha;
            if (duration <= 0f)
            {
                fadeGroup.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                fadeGroup.alpha = Mathf.Lerp(start, target,
                    progress * progress * (3f - 2f * progress));
                yield return null;
            }

            fadeGroup.alpha = target;
        }

        private void CreateOverlay()
        {
            GameObject canvasObject = new("Scene Fade Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            Canvas fadeCanvas = canvasObject.GetComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            fadeGroup = canvasObject.GetComponent<CanvasGroup>();
            fadeGroup.alpha = 1f;

            GameObject shadeObject = new("Black", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            RectTransform shade = shadeObject.GetComponent<RectTransform>();
            shade.SetParent(canvasObject.transform, false);
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = Vector2.zero;
            shade.offsetMax = Vector2.zero;
            Image image = shadeObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
        }

        private void SetInputBlocked(bool blocked)
        {
            fadeGroup.blocksRaycasts = blocked;
            fadeGroup.interactable = blocked;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
