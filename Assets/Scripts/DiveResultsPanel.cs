using System;
using System.Collections.Generic;
using GameJamOcean.Diving;
using GameJamOcean.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamOcean.Flow
{
    public sealed class DiveResultsPanel : MonoBehaviour
    {
        private DiveSessionManager shownSession;
        private bool shownWon;
        private int shownTotal;
        private int displayedTotal;
        private TMP_Text titleText, progressText, smallChestText, finalChestText;
        private TMP_Text looseCoinsText, totalText, returnText;
        private readonly List<(TMP_Text text, int amount)> rewardTexts = new();

        public void Show(DiveSessionManager session, bool won, int total, Sprite coin, Action onContinue)
        {
            shownSession = session;
            shownWon = won;
            shownTotal = total;
            rewardTexts.Clear();
            LocalizationManager.LanguageChanged -= RefreshLocalizedTexts;
            LocalizationManager.LanguageChanged += RefreshLocalizedTexts;
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
            titleText = Text(content, "Title", new Vector2(560, 50), new Vector2(0, -25), "", 30, Color.white);
            GameJamOcean.UI.GameFontStyles.Apply(titleText, GameJamOcean.UI.GameFontRole.Display);
            progressText = Text(content, "Progress", new Vector2(560, 35), new Vector2(0, -80), "", 21, Color.white);
            smallChestText = Row(content, session.SmallChestIcon, "Small Chest", session.SecuredGold, -140);
            finalChestText = Row(content, session.FinalChestIcon, "Final Chest", session.FinalRewardGold, -210);
            looseCoinsText = Row(content, coin, "Loose Coins", session.CollectedCoinGold, -280);
            totalText = Text(content, "Total", new Vector2(560, 45), new Vector2(0, -365), "", 28, new Color(1, 0.85f, 0.1f));
            GameJamOcean.UI.GameFontStyles.Apply(totalText, GameJamOcean.UI.GameFontRole.Display);
            var button = GameJamOcean.UI.EditableUIFactory.CreateButton("Return", content,
                new Vector2(360, 55), new Vector2(0, -445), out TMP_Text buttonText,
                new Color(0.12f, 0.45f, 0.5f));
            GameJamOcean.UI.GameFontStyles.Apply(buttonText, GameJamOcean.UI.GameFontRole.General);
            returnText = buttonText;
            buttonText.fontSize = 23;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;
            buttonText.raycastTarget = false;
            button.onClick.AddListener(() => onContinue());
            RefreshLocalizedTexts();
            StartCoroutine(AnimateTotal());
        }

        private System.Collections.IEnumerator AnimateTotal()
        {
            shownTotal = Mathf.Max(0, shownTotal);
            if (shownTotal == 0) yield break;
            const float duration = 2f;
            GameJamOcean.Audio.GameAudio.Instance?.StartCoinCounting();
            float elapsed = 0f;
            int displayed = 0;
            while (elapsed < duration)
            {
                if (totalText == null)
                {
                    GameJamOcean.Audio.GameAudio.Instance?.StopCoinCounting();
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                int target = Mathf.Min(shownTotal, Mathf.FloorToInt(shownTotal * Mathf.Clamp01(elapsed / duration)));
                while (displayed < target) displayed++;
                displayedTotal = displayed;
                totalText.text = LocalizationManager.Get("dive_results.total", displayedTotal);
                yield return null;
            }
            displayedTotal = shownTotal;
            if (totalText != null) totalText.text = LocalizationManager.Get("dive_results.total", shownTotal);
            GameJamOcean.Audio.GameAudio.Instance?.StopCoinCounting();
        }

        private TMP_Text Row(Transform parent, Sprite icon, string description, int gold, float y)
        {
            if (icon != null)
            {
                var image = Rect(description + " icon", parent, new Vector2(48, 48), new Vector2(-235, y)).gameObject.AddComponent<Image>();
                image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
            }
            var label = Text(parent, description, new Vector2(300, 48), new Vector2(-40, y), description, 22, Color.white);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text reward = Text(parent, description + " gold", new Vector2(145, 48), new Vector2(200, y), LocalizationManager.Get("dive_results.reward", gold), 22, Color.yellow);
            GameJamOcean.UI.GameFontStyles.Apply(reward, GameJamOcean.UI.GameFontRole.Display);
            rewardTexts.Add((reward, gold));
            return label;
        }

        private void RefreshLocalizedTexts()
        {
            if (shownSession == null) return;
            if (titleText != null) titleText.text = LocalizationManager.Get(
                shownWon ? "dive_results.completed" : "dive_results.ended");
            if (progressText != null) progressText.text = LocalizationManager.Get("dive_results.enemies",
                shownSession.KilledEnemies, shownSession.TotalEnemies);
            if (smallChestText != null) smallChestText.text = LocalizationManager.Get("dive_results.small_chest",
                shownSession.OpenedSmallChests);
            if (finalChestText != null) finalChestText.text = LocalizationManager.Get("dive_results.final_chest",
                shownWon ? 1 : 0);
            if (looseCoinsText != null) looseCoinsText.text = LocalizationManager.Get("dive_results.loose_coins");
            if (totalText != null) totalText.text = LocalizationManager.Get("dive_results.total", displayedTotal);
            if (returnText != null) returnText.text = LocalizationManager.Get("dive_results.return_ocean");
            foreach ((TMP_Text text, int amount) reward in rewardTexts)
                if (reward.text != null)
                    reward.text.text = LocalizationManager.Get("dive_results.reward", reward.amount);
        }

        private void OnDestroy()
        {
            LocalizationManager.LanguageChanged -= RefreshLocalizedTexts;
            GameJamOcean.Audio.GameAudio.Instance?.StopCoinCounting();
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
