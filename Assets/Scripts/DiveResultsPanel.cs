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
            var panel = Rect("Results", canvas.transform, new Vector2(620, 530), Vector2.zero);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.one * 0.5f;
            panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.12f, 0.18f, 0.98f);
            Text(panel, "Title", new Vector2(560, 50), new Vector2(0, -25), won ? "MERGULHO CONCLUÍDO!" : "FIM DO MERGULHO", 30, Color.white);
            Text(panel, "Progress", new Vector2(560, 35), new Vector2(0, -80),
                $"Inimigos derrotados: {session.KilledEnemies}/{session.TotalEnemies}", 21, Color.white);
            float y = -140;
            if (session.OpenedSmallChests > 0)
            {
                Row(panel, session.SmallChestIcon, $"Baús pequenos ×{session.OpenedSmallChests}", session.SecuredGold, y);
                y -= 70;
            }
            if (won)
            {
                Row(panel, session.FinalChestIcon, "Baú final ×1", session.FinalRewardGold, y);
                y -= 70;
            }
            if (session.CollectedCoinGold > 0)
                Row(panel, coin, "Moedas coletadas", session.CollectedCoinGold, y);
            Text(panel, "Total", new Vector2(560, 45), new Vector2(0, -365), $"TOTAL RECEBIDO: {total} OURO", 28, new Color(1, 0.85f, 0.1f));
            var buttonRect = Rect("Return", panel, new Vector2(360, 55), new Vector2(0, -445));
            var image = buttonRect.gameObject.AddComponent<Image>(); image.color = new Color(0.12f, 0.45f, 0.5f);
            var button = buttonRect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            Text(buttonRect, "Label", new Vector2(350, 50), Vector2.zero, "VOLTAR AO OCEANO", 23, Color.white);
            button.onClick.AddListener(() => onContinue());
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
            Text(parent, description + " gold", new Vector2(145, 48), new Vector2(200, y), $"+{gold} ouro", 22, Color.yellow);
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
            text.font = TMP_Settings.defaultFontAsset; text.text = content; text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center; text.color = tint; text.raycastTarget = false;
            return text;
        }
    }
}
