using Game.Domain;
using Game.Presentation.Characters;
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
        public CharacterPresenter SelectedCharacterPresenter { get; private set; }
        public StableEntityId? SelectedCharacterId =>
            SelectedCharacterPresenter != null && SelectedCharacterPresenter.IsBound
                ? SelectedCharacterPresenter.CharacterId
                : (StableEntityId?)null;

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
            var characterPresenter = hit.collider.GetComponentInParent<CharacterPresenter>();
            SetSelection(marker, characterPresenter);
            return marker != null || characterPresenter != null;
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

        private void SetSelection(SelectableMarker marker, CharacterPresenter characterPresenter = null)
        {
            if (Selected == marker && SelectedCharacterPresenter == characterPresenter)
                return;
            if (Selected != null)
                Selected.SetSelected(false);
            Selected = marker;
            SelectedCharacterPresenter = characterPresenter;
            if (Selected != null)
                Selected.SetSelected(true);
        }
    }
}
