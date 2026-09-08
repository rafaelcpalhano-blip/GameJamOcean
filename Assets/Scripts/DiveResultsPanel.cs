using System;
using GameJamOcean.Diving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamOcean.Flow
{
    public sealed class DiveResultsPanel : MonoBehaviour
    {
        public void Show(DiveSessionManager session, bool won, int total, Sprite coin, Action onContinue)
        {
            var canvasObject = new GameObject("Dive Results Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 2000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            var shade = Rect("Dim", canvas.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.gameObject.AddComponent<Image>().color = new Color(0, 0.025f, 0.07f, 0.72f);
            var panel = GameJamOcean.UI.EditableUIFactory.CreatePanel(
                GameJamOcean.UI.EditableUIPanelKind.DiveResultsPanel, canvas.transform,
                new Vector2(620, 580), new Color(0.025f, 0.12f, 0.18f, 0.98f),
                "Results", out RectTransform content);
            panel.sizeDelta = new Vector2(620, 580);
            TMP_Text title = Text(content, "Title", new Vector2(560, 50), new Vector2(0, -25), won ? "MERGULHO CONCLUÍDO!" : "FIM DO MERGULHO", 30, Color.white);
            GameJamOcean.UI.GameFontStyles.Apply(title, GameJamOcean.UI.GameFontRole.Display);
            Text(content, "Progress", new Vector2(560, 35), new Vector2(0, -80),
                $"Inimigos derrotados: {session.KilledEnemies}/{session.TotalEnemies}", 21, Color.white);
            Row(content, session.SmallChestIcon, $"Baú P ×{session.OpenedSmallChests}", session.SecuredGold, -140);
            Row(content, session.FinalChestIcon, won ? "Baú G ×1" : "Baú G ×0", session.FinalRewardGold, -210);
            Row(content, coin, "Moedas soltas", session.CollectedCoinGold, -280);
            TMP_Text totalLabel = Text(content, "Total", new Vector2(560, 45), new Vector2(0, -365),
                "TOTAL RECEBIDO: 0 OURO", 28, new Color(1, 0.85f, 0.1f));
            GameJamOcean.UI.GameFontStyles.Apply(totalLabel, GameJamOcean.UI.GameFontRole.Display);
            StartCoroutine(AnimateTotal(totalLabel, total));
            var button = GameJamOcean.UI.EditableUIFactory.CreateButton("Return", content,
                new Vector2(360, 55), new Vector2(0, -445), out TMP_Text buttonText,
                new Color(0.12f, 0.45f, 0.5f));
            GameJamOcean.UI.GameFontStyles.Apply(buttonText, GameJamOcean.UI.GameFontRole.General);
            buttonText.text = "VOLTAR AO OCEANO";
            buttonText.fontSize = 23;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            buttonText.raycastTarget = false;
            button.onClick.AddListener(() => onContinue());
        }

        private static System.Collections.IEnumerator AnimateTotal(TMP_Text label, int total)
        {
            total = Mathf.Max(0, total);
            if (total == 0) yield break;
            const float duration = 2f;
            GameJamOcean.Audio.GameAudio.Instance?.StartCoinCounting();
            float elapsed = 0f;
            int displayed = 0;
            while (elapsed < duration)
            {
                if (label == null)
                {
                    GameJamOcean.Audio.GameAudio.Instance?.StopCoinCounting();
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                int target = Mathf.Min(total, Mathf.FloorToInt(total * Mathf.Clamp01(elapsed / duration)));
                while (displayed < target) displayed++;
                label.text = $"TOTAL RECEBIDO: {displayed} OURO";
                yield return null;
            }
            if (label != null) label.text = $"TOTAL RECEBIDO: {total} OURO";
            GameJamOcean.Audio.GameAudio.Instance?.StopCoinCounting();
        }

        private static void Row(Transform parent, Sprite icon, string description, int gold, float y)
        {
            if (icon != null)
            {
                var image = Rect(description + " icon", parent, new Vector2(48, 48), new Vector2(-235, y)).gameObject.AddComponent<Image>();
                image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
            }
            var label = Text(parent, description, new Vector2(300, 48), new Vector2(-40, y), description, 22, Color.white);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text reward = Text(parent, description + " gold", new Vector2(145, 48), new Vector2(200, y), $"+{gold} ouro", 22, Color.yellow);
            GameJamOcean.UI.GameFontStyles.Apply(reward, GameJamOcean.UI.GameFontRole.Display);
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static TMP_Text Text(Transform parent, string name, Vector2 size, Vector2 pos, string content, float fontSize, Color tint)
        {
            var text = Rect(name, parent, size, pos).gameObject.AddComponent<TextMeshProUGUI>();
            GameJamOcean.UI.GameFontStyles.Apply(text, GameJamOcean.UI.GameFontRole.General);
            text.text = content; text.fontSize = fontSize;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(10f, fontSize * .72f); text.fontSizeMax = fontSize;
            text.alignment = TextAlignmentOptions.Center; text.color = tint; text.raycastTarget = false;
            return text;
        }
    }
}
