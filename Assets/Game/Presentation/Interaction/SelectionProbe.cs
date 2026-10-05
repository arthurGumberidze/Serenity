using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<CharacterPresenter> selectedCharacters = new List<CharacterPresenter>();

        public SelectableMarker Selected { get; private set; }
        public CharacterPresenter SelectedCharacterPresenter { get; private set; }
        public IReadOnlyList<CharacterPresenter> SelectedCharacterPresenters => selectedCharacters;
        public IReadOnlyList<StableEntityId> SelectedCharacterIds => selectedCharacters
            .Where(x => x != null && x.IsBound).Select(x => x.CharacterId)
            .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
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

        private void Awake() => input = inputSource;
        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();

        public bool TrySelectAt(Vector2 pointerPosition) =>
            TrySelectAt(pointerPosition, input != null && input.IsAdditiveSelectionPressed);

        public bool TrySelectAt(Vector2 pointerPosition, bool additive)
        {
            if (raycaster == null || !raycaster.TryGetWorldHit(pointerPosition, out var hit))
            {
                if (!additive) SetSelection(null);
                return false;
            }
            var marker = hit.collider.GetComponentInParent<SelectableMarker>();
            var characterPresenter = hit.collider.GetComponentInParent<CharacterPresenter>();
            var pilePresenter = hit.collider.GetComponentInParent<WorldResourcePilePresenter>();
            var buildingPresenter = hit.collider.GetComponentInParent<BuildingPresenter>();
            SetSelection(marker, characterPresenter, pilePresenter, buildingPresenter, additive);
            return marker != null || characterPresenter != null || pilePresenter != null || buildingPresenter != null;
        }

        public void SelectCharacters(IEnumerable<CharacterPresenter> presenters)
        {
            if (presenters == null) throw new ArgumentNullException(nameof(presenters));
            ClearCharacterSelection();
            ClearNonCharacterSelection();
            foreach (var presenter in presenters.Where(x => x != null && x.IsBound)
                         .OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal))
            {
                if (selectedCharacters.Contains(presenter)) continue;
                selectedCharacters.Add(presenter);
                presenter.SetSelected(true);
            }
            SelectedCharacterPresenter = selectedCharacters.LastOrDefault();
        }

        private void Subscribe()
        {
            input ??= inputSource;
            if (input == null) return;
            input.PrimaryClicked -= OnPrimaryClicked;
            input.PrimaryClicked += OnPrimaryClicked;
        }

        private void Unsubscribe()
        {
            if (input != null) input.PrimaryClicked -= OnPrimaryClicked;
        }

        private void OnPrimaryClicked()
        {
            if (interactionMode != null && interactionMode.IsBuildingPlacement) return;
            TrySelectAt(input.PointerPosition);
        }

        private void SetSelection(SelectableMarker marker, CharacterPresenter characterPresenter = null,
            WorldResourcePilePresenter pilePresenter = null, BuildingPresenter buildingPresenter = null,
            bool additive = false)
        {
            if (characterPresenter != null)
            {
                ClearNonCharacterSelection();
                if (!additive) ClearCharacterSelection();
                if (additive && selectedCharacters.Remove(characterPresenter))
                {
                    characterPresenter.SetSelected(false);
                }
                else if (!selectedCharacters.Contains(characterPresenter))
                {
                    selectedCharacters.Add(characterPresenter);
                    characterPresenter.SetSelected(true);
                }
                SelectedCharacterPresenter = selectedCharacters.LastOrDefault();
                return;
            }

            ClearCharacterSelection();
            if (Selected == marker && SelectedWorldResourcePilePresenter == pilePresenter &&
                SelectedBuildingPresenter == buildingPresenter) return;
            ClearNonCharacterSelection();
            Selected = marker;
            SelectedWorldResourcePilePresenter = pilePresenter;
            SelectedBuildingPresenter = buildingPresenter;
            if (Selected != null) Selected.SetSelected(true);
        }

        private void ClearNonCharacterSelection()
        {
            if (Selected != null) Selected.SetSelected(false);
            Selected = null;
            SelectedWorldResourcePilePresenter = null;
            SelectedBuildingPresenter = null;
        }

        private void ClearCharacterSelection()
        {
            foreach (var presenter in selectedCharacters)
                if (presenter != null) presenter.SetSelected(false);
            selectedCharacters.Clear();
            SelectedCharacterPresenter = null;
        }
    }
}
