using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamOcean.UI
{
    public enum EditableUIPanelKind { MainMenu, PauseMenu, UpgradePanel, DiveResultsPanel, TutorialPanel }

    [CreateAssetMenu(menuName = "GameJamOcean/UI/Editable UI Settings")]
    public sealed class EditableUISettings : ScriptableObject
    {
        [Header("Editable Panel Prefabs")]
        public GameObject mainMenuPrefab;
        public GameObject pauseMenuPrefab;
        public GameObject upgradePanelPrefab;
        public GameObject diveResultsPanelPrefab;
        public GameObject tutorialPanelPrefab;
        [Header("Main Menu Logo")]
        public Sprite mainMenuLogo;
        [Header("Shared Controls")]
        public GameObject buttonPrefab;
        [Header("Button Audio")]
        public AudioClip buttonClickSound;
        [Range(0f, 2f)] public float buttonClickVolume = 1f;
        public AudioClip buttonHoverSound;
        [Range(0f, 2f)] public float buttonHoverVolume = .6f;
        [Header("Upgrade Panel Button Audio")]
        [Tooltip("Clique exclusivo dos botões Vida, Velocidade, Arpão e Conserto.")]
        public AudioClip cashbackButtonClickSound;
        [Range(0f, 1f)] public float cashbackButtonClickVolume = 1f;
        [Header("Game Cursor")]
        public Texture2D gameCursor;
        public Vector2 cursorHotspot = Vector2.zero;

        public GameObject Panel(EditableUIPanelKind kind) => kind switch
        {
            EditableUIPanelKind.MainMenu => mainMenuPrefab,
            EditableUIPanelKind.PauseMenu => pauseMenuPrefab,
            EditableUIPanelKind.UpgradePanel => upgradePanelPrefab,
            EditableUIPanelKind.DiveResultsPanel => diveResultsPanelPrefab,
            EditableUIPanelKind.TutorialPanel => tutorialPanelPrefab,
            _ => null
        };
    }

    public static class EditableUIFactory
    {
        private static EditableUISettings settings;
        public static EditableUISettings Settings => settings != null
            ? settings : settings = Resources.Load<EditableUISettings>("EditableUISettings");

        public static RectTransform CreatePanel(EditableUIPanelKind kind, Transform parent,
            Vector2 fallbackSize, Color fallbackColor, string fallbackName, out RectTransform content)
        {
            GameObject template = Settings != null ? Settings.Panel(kind) : null;
            RectTransform panel;
            if (template != null)
            {
                panel = Object.Instantiate(template, parent, false).GetComponent<RectTransform>();
                if (panel == null)
                    panel = template.AddComponent<RectTransform>();
            }
            else
            {
                panel = new GameObject(fallbackName, typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image)).GetComponent<RectTransform>();
                panel.SetParent(parent, false);
                panel.sizeDelta = fallbackSize;
                panel.GetComponent<Image>().color = fallbackColor;
            }
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * .5f;
            panel.anchoredPosition = Vector2.zero;
            Image background = panel.GetComponent<Image>();
            if (background == null) background = panel.gameObject.AddComponent<Image>();

            EditableUIPanelTemplate marker = panel.GetComponent<EditableUIPanelTemplate>();
            content = marker != null ? marker.RuntimeContent : null;
            if (content == null)
            {
                Transform found = panel.Find("Runtime Content");
                content = found != null ? found as RectTransform : null;
            }
            if (content == null)
            {
                content = new GameObject("Runtime Content", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(panel, false);
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.offsetMin = content.offsetMax = Vector2.zero;
                content.pivot = Vector2.one * .5f;
            }
            return panel;
        }

        public static Button CreateButton(string name, Transform parent, Vector2 size,
            Vector2 position, out TMP_Text text, Color fallbackColor)
        {
            GameObject template = Settings != null ? Settings.buttonPrefab : null;
            RectTransform rect;
            Button button;
            if (template != null)
            {
                GameObject instance = Object.Instantiate(template, parent, false);
                instance.name = name;
                rect = instance.GetComponent<RectTransform>();
                button = instance.GetComponent<Button>();
            }
            else
            {
                rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(Button)).GetComponent<RectTransform>();
                rect.SetParent(parent, false);
                Image image = rect.GetComponent<Image>();
                image.color = fallbackColor;
                button = rect.GetComponent<Button>();
                button.targetGraphic = image;
            }
            if (rect == null) rect = button.gameObject.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            if (button == null) button = rect.gameObject.AddComponent<Button>();
            if (button.targetGraphic == null)
                button.targetGraphic = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            if (button.GetComponent<UIButtonAudioFeedback>() == null)
                button.gameObject.AddComponent<UIButtonAudioFeedback>();
            text = button.GetComponentInChildren<TMP_Text>(true);
            if (text == null)
            {
                RectTransform labelRect = new GameObject("Label", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                text = labelRect.GetComponent<TextMeshProUGUI>();
                text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white;
                text.raycastTarget = false;
            }
            return button;
        }
    }
}
