using System.Collections;
using GameJamOcean.Boat;
using GameJamOcean.CameraSystem;
using GameJamOcean.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamOcean.Progression
{
    public sealed class EndGameCelebration3D : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float sightseeingSeconds = 10f;
        [SerializeField, Min(.25f)] private float fadeSeconds = 1.5f;
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
            GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineTemporarilyMuted(true);

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
                yield return null;
            }
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(elapsed / fadeSeconds);
                yield return null;
            }
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            // The message has its own short lifetime, but the achievement panorama
            // continues until the camera has completed one full revolution.
            while (cameraFollow != null && !cameraFollow.EndGameOrbitCompleted)
                yield return null;

            cameraFollow?.EndEndGameOrbit();
            if (boat != null && boatWasEnabled) boat.enabled = true;
            GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineTemporarilyMuted(false);
            GameJamOcean.UI.GameMenus.EndGameActive = false;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            GameJamOcean.Audio.GameAudio.Instance?.SetBoatEngineTemporarilyMuted(false);
            GameJamOcean.UI.GameMenus.EndGameActive = false;
        }

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
            rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(820, 220);
            panel.GetComponent<Image>().color = new Color(.015f, .07f, .1f, .72f);

            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleObject.transform.SetParent(panel.transform, false);
            RectTransform titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, .68f); titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(35f, 0f); titleRect.offsetMax = new Vector2(-35f, -12f);
            TextMeshProUGUI title = titleObject.GetComponent<TextMeshProUGUI>();
            title.text = "PARABÉNS, CAPITÃO!";
            title.fontSize = 28f;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            GameFontStyles.Apply(title, GameFontRole.Display);

            var bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyObject.transform.SetParent(panel.transform, false);
            RectTransform bodyRect = bodyObject.GetComponent<RectTransform>();
            bodyRect.anchorMin = Vector2.zero; bodyRect.anchorMax = new Vector2(1f, .7f);
            bodyRect.offsetMin = new Vector2(35f, 18f); bodyRect.offsetMax = new Vector2(-35f, 0f);
            TextMeshProUGUI body = bodyObject.GetComponent<TextMeshProUGUI>();
            body.text = "Você ajudou a transformar a Ilha do Campeche em um lugar cheio de vida, esperança e novos começos.\n"
                + "O farol agora brilha por todos que chamam esta ilha de lar.";
            body.fontSize = 24f;
            body.enableAutoSizing = true;
            body.fontSizeMin = 20f;
            body.fontSizeMax = 24f;
            body.alignment = TextAlignmentOptions.Center;
            body.color = Color.white;
            GameFontStyles.Apply(body, GameFontRole.General);
        }
    }
}
