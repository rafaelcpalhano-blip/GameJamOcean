using System.Collections;
using GameJamOcean.Collectibles;
using GameJamOcean.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameJamOcean.UI
{
    public sealed class OceanGoldHUD : MonoBehaviour
    {
        private TMP_Text totalLabel, deltaLabel;
        private RectTransform coin;
        private Image coinImage;
        private SpriteRenderer goldSpinDriver;
        private Coroutine deltaAnimation;
        private GameProgress progress;

        public static void Ensure()
        {
            if (FindFirstObjectByType<OceanGoldHUD>() == null)
                new GameObject("Ocean Gold HUD").AddComponent<OceanGoldHUD>();
        }

        private void Awake()
        {
            Build();
            progress = GameProgress.Instance;
            if (progress == null) return;
            progress.TotalGoldChanged += OnGoldChanged;
            int pending = progress.ConsumePendingGoldDelta();
            if (pending != 0) ShowDelta(pending, progress.TotalGold);
            else totalLabel.text = progress.TotalGold.ToString();
        }

        private void Update()
        {
            if (goldSpinDriver != null && goldSpinDriver.sprite != null)
                coinImage.sprite = goldSpinDriver.sprite;
        }

        private void OnGoldChanged(int total)
        {
            int delta = progress.ConsumePendingGoldDelta();
            if (delta != 0) ShowDelta(delta, total);
            else totalLabel.text = total.ToString();
        }

        private void ShowDelta(int delta, int total)
        {
            if (deltaAnimation != null) StopCoroutine(deltaAnimation);
            deltaAnimation = StartCoroutine(AnimateDelta(delta, total));
        }

        private IEnumerator AnimateDelta(int delta, int finalTotal)
        {
            totalLabel.text = Mathf.Max(0, finalTotal - delta).ToString();
            deltaLabel.text = delta > 0 ? $"+{delta}" : delta.ToString();
            deltaLabel.color = delta > 0 ? new Color(.25f, 1f, .35f) : new Color(1f, .25f, .2f);
            RectTransform rect = deltaLabel.rectTransform;
            rect.anchoredPosition = new Vector2(72, -42);
            deltaLabel.alpha = 1f;
            float elapsed = 0f;
            while (elapsed < 1.15f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / 1.15f);
                rect.anchoredPosition = Vector2.Lerp(new Vector2(72, -42), new Vector2(72, -4), t);
                if (t > .75f) deltaLabel.alpha = 1f - (t - .75f) / .25f;
                yield return null;
            }
            totalLabel.text = finalTotal.ToString();
            deltaLabel.text = "";
            deltaAnimation = null;
        }

        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 150;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920,1080);
            gameObject.AddComponent<GraphicRaycaster>();
            RectTransform holder = new GameObject("Gold", typeof(RectTransform)).GetComponent<RectTransform>();
            holder.SetParent(transform, false); holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(1,1);
            holder.sizeDelta = new Vector2(240,90); holder.anchoredPosition = new Vector2(-32,-30);
            coin = new GameObject("Rotating Coin", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            coin.SetParent(holder, false); coin.anchorMin = coin.anchorMax = new Vector2(0,1); coin.pivot = new Vector2(.5f,.5f);
            coin.sizeDelta = new Vector2(52,52); coin.anchoredPosition = new Vector2(30,-30);
            coinImage = coin.GetComponent<Image>(); coinImage.sprite = CreateCoinSprite(); coinImage.color = Color.white;
            totalLabel = CreateText("Total", holder, new Vector2(145,48), new Vector2(72,-5), 32, Color.white);
            deltaLabel = CreateText("Delta", holder, new Vector2(145,35), new Vector2(72,-42), 24, Color.white);
            ConfigureGoldSpinDriver();
        }

        private void ConfigureGoldSpinDriver()
        {
            OceanGoldHUDSettings settings = Resources.Load<OceanGoldHUDSettings>("OceanGoldHUDSettings");
            if (settings == null || settings.goldSpinPrefab == null) return;
            GameObject driver = Instantiate(settings.goldSpinPrefab, transform);
            driver.name = "goldspin Animation Driver";
            driver.transform.localPosition = Vector3.one * 10000f;
            // This prefab is only an animation source for the HUD. GoldCollectible2D
            // requires its CircleCollider2D, so disabling the gameplay components is
            // safer than trying to remove them whenever OceanScene is loaded.
            foreach (GoldCollectible2D collectible in driver.GetComponentsInChildren<GoldCollectible2D>(true))
                collectible.enabled = false;
            foreach (Collider2D collider in driver.GetComponentsInChildren<Collider2D>(true))
                collider.enabled = false;
            foreach (Rigidbody2D body in driver.GetComponentsInChildren<Rigidbody2D>(true))
                body.simulated = false;
            goldSpinDriver = driver.GetComponentInChildren<SpriteRenderer>(true);
            if (goldSpinDriver != null) goldSpinDriver.enabled = false;
            Animator animator = driver.GetComponentInChildren<Animator>(true);
            if (animator != null) animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        private static TMP_Text CreateText(string name, Transform parent, Vector2 size, Vector2 position, float font, Color color)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false); text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0,1);
            text.rectTransform.pivot = new Vector2(0,1); text.rectTransform.sizeDelta = size; text.rectTransform.anchoredPosition = position;
            text.fontSize = font; text.color = color; text.alignment = TextAlignmentOptions.MidlineLeft; text.raycastTarget = false; return text;
        }

        private static Sprite CreateCoinSprite()
        {
            const int size = 64; var texture = new Texture2D(size,size,TextureFormat.RGBA32,false) { name = "Runtime Coin" };
            for (int y=0;y<size;y++) for (int x=0;x<size;x++)
            {
                float radius = Vector2.Distance(new Vector2(x,y), new Vector2(31.5f,31.5f));
                texture.SetPixel(x,y, radius < 25 ? (radius > 21 ? new Color(1,.48f,.02f,1) : Color.white) : Color.clear);
            }
            texture.Apply(); return Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),64);
        }

        private void OnDestroy()
        {
            if (progress != null) progress.TotalGoldChanged -= OnGoldChanged;
        }
    }
}
