using TMPro;
using UnityEngine;

namespace GameJamOcean.Rewards
{
    // Decorative only: never instantiate GoldCollectible2D or award gold here.
    public sealed class ChestRewardBurst : MonoBehaviour
    {
        private float elapsed, duration;
        private TMP_Text label;
        private SpriteRenderer[] coins;
        private Vector3[] velocities;
        public static void Spawn(Vector3 position, Sprite sprite, int amount, int cap, float lifetime, int layer, int order)
        {
            var root = new GameObject("Chest reward feedback");
            root.transform.position = position;
            var effect = root.AddComponent<ChestRewardBurst>();
            effect.duration = Mathf.Max(0.1f, lifetime);
            var textObject = new GameObject("Gold amount");
            textObject.transform.SetParent(root.transform, false);
            effect.label = textObject.AddComponent<TextMeshPro>();
            effect.label.font = TMP_Settings.defaultFontAsset;
            effect.label.text = $"+{amount}";
            effect.label.fontSize = 7;
            effect.label.alignment = TextAlignmentOptions.Center;
            effect.label.rectTransform.sizeDelta = new Vector2(5, 1);
            var textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.sortingLayerID = layer; textRenderer.sortingOrder = order + 32;
            effect.label.color = Color.yellow;
            int count = sprite != null ? Mathf.Clamp(amount, 0, cap) : 0;
            effect.coins = new SpriteRenderer[count]; effect.velocities = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var coin = new GameObject("Visual coin");
                coin.transform.SetParent(root.transform, false);
                coin.transform.localScale = Vector3.one * (0.2f / Mathf.Max(0.01f, sprite.bounds.size.y));
                var renderer = coin.AddComponent<SpriteRenderer>(); renderer.sprite = sprite;
                renderer.sortingLayerID = layer; renderer.sortingOrder = order + 30;
                effect.coins[i] = renderer;
                effect.velocities[i] = new Vector3(Random.Range(-1.3f, 1.3f), Random.Range(1.7f, 3f), 0);
            }
        }
        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { Destroy(gameObject); return; }
            float alpha = 1f - Mathf.SmoothStep(0.4f, 1f, elapsed / duration);
            label.transform.localPosition = new Vector3(0, 0.6f + elapsed * 0.8f, -0.1f);
            label.color = new Color(1, 0.9f, 0, alpha);
            for (int i = 0; i < coins.Length; i++)
            {
                coins[i].transform.localPosition = velocities[i] * elapsed + Vector3.down * (0.9f * elapsed * elapsed);
                coins[i].color = new Color(1, 1, 1, alpha);
            }
        }
    }
}
