using Game.Domain;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Resources;
using UnityEngine;

namespace Game.Presentation.Interaction
{
    public sealed class SelectionProbe : MonoBehaviour
    {
        [SerializeField] private LocalGameplayInputSource inputSource;
        [SerializeField] private WorldPointerRaycaster raycaster;

        private ILocalGameplayInput input;
        private LocalInteractionMode interactionMode;
        public SelectableMarker Selected { get; private set; }
        public CharacterPresenter SelectedCharacterPresenter { get; private set; }
        public WorldResourcePilePresenter SelectedWorldResourcePilePresenter { get; private set; }
        public BuildingPresenter SelectedBuildingPresenter { get; private set; }
        public StableEntityId? SelectedCharacterId =>
            SelectedCharacterPresenter != null && SelectedCharacterPresenter.IsBound
                ? SelectedCharacterPresenter.CharacterId
                : (StableEntityId?)null;

        public void Configure(LocalGameplayInputSource source, WorldPointerRaycaster worldRaycaster,
            LocalInteractionMode mode = null)
        {
            Unsubscribe();
            inputSource = source;
            raycaster = worldRaycaster;
            input = source;
            interactionMode = mode;
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
            var pilePresenter = hit.collider.GetComponentInParent<WorldResourcePilePresenter>();
            var buildingPresenter = hit.collider.GetComponentInParent<BuildingPresenter>();
            SetSelection(marker, characterPresenter, pilePresenter, buildingPresenter);
            return marker != null || characterPresenter != null || pilePresenter != null || buildingPresenter != null;
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
            if (interactionMode != null && interactionMode.IsBuildingPlacement)
                return;
            TrySelectAt(input.PointerPosition);
        }

        private void SetSelection(SelectableMarker marker, CharacterPresenter characterPresenter = null,
            WorldResourcePilePresenter pilePresenter = null, BuildingPresenter buildingPresenter = null)
        {
            if (Selected == marker && SelectedCharacterPresenter == characterPresenter &&
                SelectedWorldResourcePilePresenter == pilePresenter && SelectedBuildingPresenter == buildingPresenter)
                return;
            if (Selected != null)
                Selected.SetSelected(false);
            Selected = marker;
            SelectedCharacterPresenter = characterPresenter;
            SelectedWorldResourcePilePresenter = pilePresenter;
            SelectedBuildingPresenter = buildingPresenter;
            if (Selected != null)
                Selected.SetSelected(true);
        }
    }
}
