using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJamOcean.Weapons
{
    [DisallowMultipleComponent]
    public sealed class AimCursor2D : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float size = 6f;
        [SerializeField, Min(2f)] private float armLength = 11f;
        [SerializeField, Min(1f)] private float gap = 5f;
        [SerializeField, Min(1f)] private float thickness = 2f;
        [SerializeField] private Color color = new(.9f, 1f, 1f, .95f);
        [SerializeField] private bool hideSystemCursor = true;

        private Texture2D dotTexture;
        private bool systemCursorHiddenByAim;

        private void OnEnable()
        {
            dotTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            dotTexture.SetPixel(0, 0, Color.white);
            dotTexture.Apply();

            UpdateSystemCursorVisibility();
        }

        private void Update()
        {
            // Browsers can restore the native cursor when the canvas regains focus.
            // Keep its visibility synchronized with the exact state that draws the aim.
            UpdateSystemCursorVisibility();
        }

        private void OnDisable()
        {
            if (systemCursorHiddenByAim)
            {
                Cursor.visible = true;
                systemCursorHiddenByAim = false;
            }

            if (dotTexture != null)
            {
                Destroy(dotTexture);
            }
        }

        private void OnGUI()
        {
            if (dotTexture == null || !IsAimActive())
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            float x = mousePosition.x;
            float y = Screen.height - mousePosition.y;
            Rect dotRect = new(x - size * .5f, y - size * .5f, size, size);

            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(dotRect, dotTexture);
            GUI.DrawTexture(new Rect(x - gap - armLength, y - thickness * .5f,
                armLength, thickness), dotTexture);
            GUI.DrawTexture(new Rect(x + gap, y - thickness * .5f,
                armLength, thickness), dotTexture);
            GUI.DrawTexture(new Rect(x - thickness * .5f, y - gap - armLength,
                thickness, armLength), dotTexture);
            GUI.DrawTexture(new Rect(x - thickness * .5f, y + gap,
                thickness, armLength), dotTexture);
            GUI.color = previousColor;
        }

        private void UpdateSystemCursorVisibility()
        {
            bool shouldHide = hideSystemCursor && IsAimActive();
            if (Cursor.visible == !shouldHide && systemCursorHiddenByAim == shouldHide)
            {
                return;
            }

            Cursor.visible = !shouldHide;
            systemCursorHiddenByAim = shouldHide;
        }

        private static bool IsAimActive()
        {
            return Mouse.current != null
                && !GameJamOcean.UI.GameMenus.BlocksGameplay
                && Time.timeScale > 0f;
        }

        private void OnValidate()
        {
            size = Mathf.Max(2f, size);
            armLength = Mathf.Max(2f, armLength);
            gap = Mathf.Max(1f, gap);
            thickness = Mathf.Max(1f, thickness);
        }
    }
}
