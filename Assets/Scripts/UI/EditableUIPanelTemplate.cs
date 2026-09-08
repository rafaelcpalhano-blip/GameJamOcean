using UnityEngine;

namespace GameJamOcean.UI
{
    [DisallowMultipleComponent]
    public sealed class EditableUIPanelTemplate : MonoBehaviour
    {
        [SerializeField] private RectTransform runtimeContent;
        public RectTransform RuntimeContent => runtimeContent;
        public void Configure(RectTransform content) => runtimeContent = content;
    }
}
