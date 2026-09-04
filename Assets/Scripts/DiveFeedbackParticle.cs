using TMPro;
using UnityEngine;

namespace GameJamOcean.Combat
{
    // Short-lived world-space feedback, independent of the enemy's lifetime.
    public sealed class DiveFeedbackParticle : MonoBehaviour
    {
        private static Sprite bubbleSprite;
        private SpriteRenderer bubble;
        private TMP_Text label;
        private float lifetime;
        private float age;
        private float phase;
        private Vector3 origin;
        private Color color;

        public static void SpawnDamage(Vector3 position, float amount, int sortingLayer, int sortingOrder, float duration = 0.8f)
        {
            SpawnNumber(position,
                amount.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
                new Color(1f, 0.9f, 0.35f), sortingLayer, sortingOrder, duration);
        }

        public static void SpawnPlayerHealthChange(Vector3 position, bool healing,
            int sortingLayer, int sortingOrder, float duration = 0.9f)
        {
            SpawnNumber(position, healing ? "+1" : "-1",
                healing ? new Color(.2f, 1f, .3f) : new Color(1f, .15f, .15f),
                sortingLayer, sortingOrder, duration);
        }

        private static void SpawnNumber(Vector3 position, string text, Color tint,
            int sortingLayer, int sortingOrder, float duration)
        {
            var go = new GameObject("Health number");
            go.transform.position = position + new Vector3(Random.Range(-0.15f, 0.15f), 0, -0.1f);
            var effect = go.AddComponent<DiveFeedbackParticle>();
            effect.label = go.AddComponent<TextMeshPro>();
            effect.label.font = TMP_Settings.defaultFontAsset;
            effect.label.text = text;
            effect.label.fontSize = 5;
            effect.label.alignment = TextAlignmentOptions.Center;
            effect.label.rectTransform.sizeDelta = new Vector2(3, 1);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sortingLayerID = sortingLayer;
            renderer.sortingOrder = sortingOrder + 20;
            effect.Initialize(duration, tint);
        }

        public static void SpawnBubble(Vector3 position, SpriteRenderer source, float duration)
        {
            if (bubbleSprite == null)
            {
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                texture.name = "Procedural dash bubble";
                texture.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[32 * 32];
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float radius = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                    float alpha = 1f - Mathf.SmoothStep(0.7f, 2.3f, Mathf.Abs(radius - 11.5f));
                    pixels[y * 32 + x] = new Color(1, 1, 1, alpha);
                }
                texture.SetPixels(pixels); texture.Apply();
                bubbleSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 64);
            }
            var go = new GameObject("Dash bubble");
            go.transform.position = position + (Vector3)(Random.insideUnitCircle * 0.18f);
            go.transform.localScale = Vector3.one * Random.Range(0.25f, 0.7f);
            var effect = go.AddComponent<DiveFeedbackParticle>();
            effect.bubble = go.AddComponent<SpriteRenderer>();
            effect.bubble.sprite = bubbleSprite;
            effect.bubble.sharedMaterial = source.sharedMaterial;
            effect.bubble.sortingLayerID = source.sortingLayerID;
            effect.bubble.sortingOrder = source.sortingOrder + 1;
            effect.Initialize(duration, new Color(0.7f, 0.95f, 1, 0.7f));
        }

        private void Initialize(float duration, Color tint)
        {
            lifetime = Mathf.Max(0.1f, duration); origin = transform.position;
            color = tint; phase = Random.value * 6.28f;
            if (bubble != null) bubble.color = color;
            if (label != null) label.color = color;
        }

        private void Update()
        {
            age += Time.deltaTime;
            float t = age / lifetime;
            if (t >= 1) { Destroy(gameObject); return; }
            transform.position = origin + new Vector3((Mathf.Sin(age * 3 + phase) - Mathf.Sin(phase)) * 0.12f, age * 0.7f, 0);
            Color faded = color; faded.a *= 1 - t;
            if (bubble != null) bubble.color = faded;
            if (label != null) label.color = faded;
        }
    }
}
