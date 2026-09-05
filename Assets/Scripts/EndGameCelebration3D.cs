using System.Collections;
using GameJamOcean.Boat;
using GameJamOcean.CameraSystem;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GameJamOcean.Progression
{
    public sealed class EndGameCelebration3D : MonoBehaviour
    {
        [SerializeField, Min(5f)] private float sightseeingSeconds = 30f;
        [SerializeField, Min(.5f)] private float fadeSeconds = 4f;
        private CanvasGroup group;
        private CameraFollow3D cameraFollow;
        private BoatController3D boat;
        private bool boatWasEnabled;

        public static void Show(GameObject finalVillage)
        {
            if (FindFirstObjectByType<EndGameCelebration3D>() != null) return;
            var host = new GameObject("End Game Celebration");
            var celebration = host.AddComponent<EndGameCelebration3D>();
            celebration.Begin(finalVillage);
        }

        private void Begin(GameObject finalVillage)
        {
            GameJamOcean.UI.GameMenus.EndGameActive = true;
            BuildUI();
            cameraFollow = FindFirstObjectByType<CameraFollow3D>();
            boat = FindFirstObjectByType<BoatController3D>();
            boatWasEnabled = boat != null && boat.enabled;
            if (boat != null) boat.enabled = false;

            Vector3 center = finalVillage != null ? finalVillage.transform.position : Vector3.zero;
            if (finalVillage != null)
            {
                Renderer[] renderers = finalVillage.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                    center = bounds.center;
                }
            }
            cameraFollow?.BeginEndGameOrbit(center);
            StartCoroutine(Sequence());
        }

        private IEnumerator Sequence()
        {
            float sightseeing = 0f;
            while (sightseeing < sightseeingSeconds)
            {
                sightseeing += Time.unscaledDeltaTime;
                bool keyboardSkip = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
                bool mouseSkip = Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame
                    || Mouse.current.rightButton.wasPressedThisFrame);
                // Avoid consuming the same click that bought the final upgrade.
                if (sightseeing > 1f && (keyboardSkip || mouseSkip)) break;
                yield return null;
            }
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }
            cameraFollow?.EndEndGameOrbit();
            if (boat != null && boatWasEnabled) boat.enabled = true;
            GameJamOcean.UI.GameMenus.EndGameActive = false;
            Destroy(gameObject);
        }

        private void OnDestroy() => GameJamOcean.UI.GameMenus.EndGameActive = false;

        private void BuildUI()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            gameObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();

            var panel = new GameObject("Message", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .72f); rect.anchorMax = new Vector2(.5f, .72f);
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(820, 190);
            panel.GetComponent<Image>().color = new Color(.015f, .07f, .1f, .72f);
            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(panel.transform, false);
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(35, 22); textRect.offsetMax = new Vector2(-35, -22);
            TextMeshProUGUI label = text.GetComponent<TextMeshProUGUI>();
            label.text = "PARABÉNS, CAPITÃO!\nA aldeia alcançou seu nível máximo. Obrigado por devolver vida e esperança a esta ilha!";
            label.fontSize = 30; label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
        }
    }
}
