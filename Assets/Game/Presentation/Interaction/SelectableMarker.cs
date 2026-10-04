using UnityEngine;

namespace Game.Presentation.Interaction
{
    [RequireComponent(typeof(Renderer))]
    public sealed class SelectableMarker : MonoBehaviour
    {
        [SerializeField] private Color normalColor = new Color(0.35f, 0.55f, 0.85f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.65f, 0.1f, 1f);

        private Renderer targetRenderer;
        private MaterialPropertyBlock properties;

        public bool IsSelected { get; private set; }

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            properties = new MaterialPropertyBlock();
            ApplyColor();
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
                properties = new MaterialPropertyBlock();
            }
            ApplyColor();
        }

        private void ApplyColor()
        {
            targetRenderer.GetPropertyBlock(properties);
            var color = IsSelected ? selectedColor : normalColor;
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            targetRenderer.SetPropertyBlock(properties);
        }
    }
}
