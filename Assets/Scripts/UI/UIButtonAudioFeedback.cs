using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GameJamOcean.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonAudioFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler
    {
        private Button button;
        private Image image;
        private EditableUISettings settings;
        private Color colorBeforeHover;
        private bool hoverTintApplied;

        private void Awake()
        {
            settings = Resources.Load<EditableUISettings>("EditableUISettings");
            button = GetComponent<Button>();
            image = GetComponent<Image>();
            button.onClick.AddListener(PlayClick);
        }

        private void PlayClick()
        {
            if (settings == null || button == null || !button.interactable) return;
            GameJamOcean.Audio.GameAudio.Instance?.PlayUIButtonClick(
                settings.buttonClickSound, settings.buttonClickVolume);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (settings == null || button == null || !button.interactable) return;
            if (image != null)
            {
                colorBeforeHover = image.color;
                image.color = new Color(colorBeforeHover.r, colorBeforeHover.g * .94f,
                    colorBeforeHover.b * .72f, colorBeforeHover.a);
                hoverTintApplied = true;
            }
            GameJamOcean.Audio.GameAudio.Instance?.PlayUIButtonHover(
                settings.buttonHoverSound, settings.buttonHoverVolume);
        }

        public void OnPointerExit(PointerEventData eventData) => RemoveHoverTint();

        public void OnPointerDown(PointerEventData eventData) => RemoveHoverTint();

        private void OnDisable() => RemoveHoverTint();

        private void RemoveHoverTint()
        {
            if (!hoverTintApplied || image == null) return;
            image.color = colorBeforeHover;
            hoverTintApplied = false;
        }
    }
}
