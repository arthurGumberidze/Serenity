using Game.Presentation.Input;
using UnityEngine;

namespace Game.Presentation.Interaction
{
    public sealed class SelectionProbe : MonoBehaviour
    {
        [SerializeField] private LocalGameplayInputSource inputSource;
        [SerializeField] private WorldPointerRaycaster raycaster;

        private ILocalGameplayInput input;
        public SelectableMarker Selected { get; private set; }

        public void Configure(LocalGameplayInputSource source, WorldPointerRaycaster worldRaycaster)
        {
            Unsubscribe();
            inputSource = source;
            raycaster = worldRaycaster;
            input = source;
            Subscribe();
        }

        private void Awake()
        {
            input = inputSource;
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public bool TrySelectAt(Vector2 pointerPosition)
        {
            if (raycaster == null || !raycaster.TryGetWorldHit(pointerPosition, out var hit))
            {
                SetSelection(null);
                return false;
            }
            var marker = hit.collider.GetComponentInParent<SelectableMarker>();
            SetSelection(marker);
            return marker != null;
        }

        private void Subscribe()
        {
            input ??= inputSource;
            if (input == null)
                return;
            input.PrimaryClicked -= OnPrimaryClicked;
            input.PrimaryClicked += OnPrimaryClicked;
        }

        private void Unsubscribe()
        {
            if (input != null)
                input.PrimaryClicked -= OnPrimaryClicked;
        }

        private void OnPrimaryClicked()
        {
            TrySelectAt(input.PointerPosition);
        }

        private void SetSelection(SelectableMarker marker)
        {
            if (Selected == marker)
                return;
            if (Selected != null)
                Selected.SetSelected(false);
            Selected = marker;
            if (Selected != null)
                Selected.SetSelected(true);
        }
    }
}
