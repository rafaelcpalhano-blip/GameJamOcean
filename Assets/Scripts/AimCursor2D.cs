using UnityEngine;
using UnityEngine.InputSystem;

namespace GameJamOcean.Weapons
{
    [DisallowMultipleComponent]
    public sealed class AimCursor2D : MonoBehaviour
    {
        [SerializeField, Min(2f)] private float size = 8f;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private bool hideSystemCursor = true;

        private Texture2D dotTexture;

        private void OnEnable()
        {
            dotTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            dotTexture.SetPixel(0, 0, Color.white);
            dotTexture.Apply();

            if (hideSystemCursor)
            {
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            Cursor.visible = true;

            if (dotTexture != null)
            {
                Destroy(dotTexture);
            }
        }

        private void OnGUI()
        {
            if (Mouse.current == null || dotTexture == null)
            {
                return;
            }

            Vector2 mousePosition = Mouse.current.position.ReadValue();
            Rect dotRect = new(
                mousePosition.x - size * 0.5f,
                Screen.height - mousePosition.y - size * 0.5f,
                size,
                size);

            Color previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(dotRect, dotTexture);
            GUI.color = previousColor;
        }

        private void OnValidate()
        {
            size = Mathf.Max(2f, size);
        }
    }
}
