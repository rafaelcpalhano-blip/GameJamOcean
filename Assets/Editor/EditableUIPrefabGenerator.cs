#if UNITY_EDITOR
using System.IO;
using GameJamOcean.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamOcean.EditorTools
{
    [InitializeOnLoad]
    public static class EditableUIPrefabGenerator
    {
        private const string PrefabFolder = "Assets/Prefabs/UI";
        private const string SettingsPath = "Assets/Resources/EditableUISettings.asset";

        static EditableUIPrefabGenerator()
        {
            EditorApplication.delayCall += EnsureAssets;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.delayCall += EnsureAssets;
            };
        }

        [MenuItem("Tools/Game Jam Ocean/UI/Generate Editable UI Prefabs")]
        public static void EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureFolder("Assets/Prefabs", "UI");
            EnsureFolder("Assets", "Resources");

            EditableUISettings settings = AssetDatabase.LoadAssetAtPath<EditableUISettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<EditableUISettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.mainMenuPrefab = EnsurePanel("MainMenu", new Vector2(570, 520), new Color(.02f, .09f, .14f, .24f));
            settings.pauseMenuPrefab = EnsurePanel("PauseMenu", new Vector2(570, 520), new Color(.02f, .09f, .14f, .24f));
            settings.upgradePanelPrefab = EnsurePanel("UpgradePanel", new Vector2(900, 800), new Color(.025f, .10f, .14f, .72f));
            settings.diveResultsPanelPrefab = EnsurePanel("DiveResultsPanel", new Vector2(620, 530), new Color(.025f, .12f, .18f, .98f));
            settings.tutorialPanelPrefab = EnsurePanel("TutorialPanel", new Vector2(570, 520), new Color(.02f, .09f, .14f, .88f));
            settings.buttonPrefab = EnsureButton();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static GameObject EnsurePanel(string name, Vector2 size, Color color)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(EditableUIPanelTemplate));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
            rect.sizeDelta = size;
            root.GetComponent<Image>().color = color;
            var content = new GameObject("Runtime Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(rect, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = content.offsetMax = Vector2.zero;
            root.GetComponent<EditableUIPanelTemplate>().Configure(content);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject EnsureButton()
        {
            string path = $"{PrefabFolder}/Button.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(Button), typeof(UIButtonAudioFeedback));
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(440, 52);
            Image image = root.GetComponent<Image>();
            image.color = new Color(.1f, .37f, .43f, 1f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
            label.SetParent(root.transform, false);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = label.offsetMax = Vector2.zero;
            TMP_Text text = label.GetComponent<TextMeshProUGUI>();
            text.text = "BOTÃO";
            text.fontSize = 23;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
