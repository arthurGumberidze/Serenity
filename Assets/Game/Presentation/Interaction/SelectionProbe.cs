using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Presentation.Buildings;
using Game.Presentation.Characters;
using Game.Presentation.Input;
using Game.Presentation.Resources;
using Game.Presentation.Production;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Presentation.Interaction
{
    public sealed class SelectionProbe : MonoBehaviour
    {
        [SerializeField] private LocalGameplayInputSource inputSource;
        [SerializeField] private WorldPointerRaycaster raycaster;

        private ILocalGameplayInput input;
        private LocalInteractionMode interactionMode;
        private readonly List<CharacterPresenter> selectedCharacters = new List<CharacterPresenter>();
        private readonly HashSet<StableEntityId> selectedCharacterIds = new HashSet<StableEntityId>();
        private StableWorldHitProxy hoveredProxy;
        private StableWorldHitProxy selectedProxy;
        private Vector2 dragStart;
        private Vector2 dragCurrent;
        private bool primaryGestureActive;
        private bool dragSelecting;
        private float nextSelectionRebindTime;
        private const float DragThreshold = 8f;

        public SelectableMarker Selected { get; private set; }
        public CharacterPresenter SelectedCharacterPresenter { get; private set; }
        public IReadOnlyList<CharacterPresenter> SelectedCharacterPresenters => selectedCharacters;
        public IReadOnlyList<StableEntityId> SelectedCharacterIds => selectedCharacterIds
            .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
        public WorldResourcePilePresenter SelectedWorldResourcePilePresenter { get; private set; }
        public BuildingPresenter SelectedBuildingPresenter { get; private set; }
        public ProductionSourcePresenter SelectedProductionSourcePresenter { get; private set; }
        public StableWorldHitProxy HoveredProxy => hoveredProxy;
        public StableEntityId? SelectedCharacterId => SelectedCharacterPresenter != null && SelectedCharacterPresenter.IsBound
            ? SelectedCharacterPresenter.CharacterId
            : selectedCharacterIds.OrderBy(x => x.ToString(), StringComparer.Ordinal).Cast<StableEntityId?>().LastOrDefault();

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
        private void OnDisable()
        {
            Unsubscribe();
            SetHoveredProxy(null);
            primaryGestureActive = false;
            dragSelecting = false;
        }

        private void Update()
        {
            if (input == null || raycaster == null) return;
            if (selectedCharacterIds.Count > 0 && Time.unscaledTime >= nextSelectionRebindTime)
            {
                nextSelectionRebindTime = Time.unscaledTime + 0.25f;
                RefreshSelectedPresentations();
            }
            RefreshHover(input.PointerPosition);
            var mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasPressedThisFrame &&
                (interactionMode == null || !interactionMode.IsBuildingPlacement) &&
                !raycaster.IsPointerBlockedByUi(input.PointerPosition))
            {
                primaryGestureActive = true;
                dragSelecting = false;
                dragStart = input.PointerPosition;
                dragCurrent = dragStart;
            }
            if (primaryGestureActive && mouse.leftButton.isPressed)
            {
                dragCurrent = input.PointerPosition;
                if ((dragCurrent - dragStart).sqrMagnitude >= DragThreshold * DragThreshold)
                    dragSelecting = true;
            }
            if (primaryGestureActive && mouse.leftButton.wasReleasedThisFrame)
            {
                dragCurrent = input.PointerPosition;
                if (dragSelecting) SelectWithinScreenRect(ScreenRect(dragStart, dragCurrent), input.IsAdditiveSelectionPressed);
                else TrySelectAt(dragCurrent, input.IsAdditiveSelectionPressed);
                primaryGestureActive = false;
                dragSelecting = false;
            }
        }

        private void OnGUI()
        {
            if (!dragSelecting) return;
            var a = WorldPointerRaycaster.ScreenToGuiPoint(dragStart);
            var b = WorldPointerRaycaster.ScreenToGuiPoint(dragCurrent);
            var rect = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            var previous = GUI.color;
            GUI.color = new Color(0.25f, 0.75f, 1f, 0.24f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.9f, 1f, 0.95f);
            GUI.Box(rect, GUIContent.none);
            GUI.color = previous;
        }

        public bool TrySelectAt(Vector2 pointerPosition) =>
            TrySelectAt(pointerPosition, input != null && input.IsAdditiveSelectionPressed);

        public bool TrySelectAt(Vector2 pointerPosition, bool additive)
        {
            if (raycaster == null) return false;
            if (raycaster.IsPointerBlockedByUi(pointerPosition)) return false;
            if (!raycaster.TryGetSelectableHit(pointerPosition, out var hit))
            {
                if (!additive) SetSelection(null);
                return false;
            }
            var marker = hit.collider.GetComponentInParent<SelectableMarker>();
            var proxy = hit.collider.GetComponentInParent<StableWorldHitProxy>();
            var characterPresenter = hit.collider.GetComponentInParent<CharacterPresenter>();
            var pilePresenter = hit.collider.GetComponentInParent<WorldResourcePilePresenter>();
            var buildingPresenter = hit.collider.GetComponentInParent<BuildingPresenter>();
            var productionPresenter = hit.collider.GetComponentInParent<ProductionSourcePresenter>();
            if (characterPresenter == null && selectedCharacterIds.Count > 0)
                return false;
            SetSelection(marker, characterPresenter, pilePresenter, buildingPresenter, productionPresenter, additive, proxy);
            return marker != null || characterPresenter != null || pilePresenter != null ||
                buildingPresenter != null || productionPresenter != null;
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
                selectedCharacterIds.Add(presenter.CharacterId);
                presenter.SetSelected(true);
            }
            SelectedCharacterPresenter = selectedCharacters.LastOrDefault();
        }

        public void SelectCharacterIds(IEnumerable<StableEntityId> characterIds)
        {
            if (characterIds == null) throw new ArgumentNullException(nameof(characterIds));
            ClearCharacterSelection();
            ClearNonCharacterSelection();
            foreach (var id in characterIds.OrderBy(x => x.ToString(), StringComparer.Ordinal))
                selectedCharacterIds.Add(id);
            RefreshSelectedPresentations();
        }

        public int SelectWithinScreenRect(Rect screenRect, bool additive = false)
        {
            if (raycaster == null || raycaster.WorldCamera == null) return 0;
            var candidates = FindObjectsByType<CharacterPresenter>(FindObjectsInactive.Exclude)
                .Where(x => x != null && x.IsBound)
                .Where(x =>
                {
                    var point = raycaster.WorldCamera.WorldToScreenPoint(x.transform.position + Vector3.up);
                    return point.z > 0f && screenRect.Contains(new Vector2(point.x, point.y));
                })
                .OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal).ToArray();
            if (!additive) ClearCharacterSelection();
            ClearNonCharacterSelection();
            foreach (var presenter in candidates)
            {
                if (selectedCharacters.Contains(presenter)) continue;
                selectedCharacters.Add(presenter);
                selectedCharacterIds.Add(presenter.CharacterId);
                presenter.SetSelected(true);
            }
            SelectedCharacterPresenter = selectedCharacters.LastOrDefault();
            return candidates.Length;
        }

        public void ClearSelection()
        {
            ClearCharacterSelection();
            ClearNonCharacterSelection();
        }

        private void Subscribe()
        {
            input ??= inputSource;
        }

        private void Unsubscribe()
        {
        }

        private void SetSelection(SelectableMarker marker, CharacterPresenter characterPresenter = null,
            WorldResourcePilePresenter pilePresenter = null, BuildingPresenter buildingPresenter = null,
            ProductionSourcePresenter productionPresenter = null, bool additive = false,
            StableWorldHitProxy proxy = null)
        {
            if (characterPresenter != null)
            {
                ClearNonCharacterSelection();
                if (!additive) ClearCharacterSelection();
                if (additive && selectedCharacterIds.Remove(characterPresenter.CharacterId))
                {
                    selectedCharacters.Remove(characterPresenter);
                    characterPresenter.SetSelected(false);
                }
                else if (!selectedCharacters.Contains(characterPresenter))
                {
                    selectedCharacters.Add(characterPresenter);
                    selectedCharacterIds.Add(characterPresenter.CharacterId);
                    characterPresenter.SetSelected(true);
                }
                SelectedCharacterPresenter = selectedCharacters.LastOrDefault();
                return;
            }

            ClearCharacterSelection();
            if (Selected == marker && SelectedWorldResourcePilePresenter == pilePresenter &&
                SelectedBuildingPresenter == buildingPresenter &&
                SelectedProductionSourcePresenter == productionPresenter) return;
            ClearNonCharacterSelection();
            Selected = marker;
            SelectedWorldResourcePilePresenter = pilePresenter;
            SelectedBuildingPresenter = buildingPresenter;
            SelectedProductionSourcePresenter = productionPresenter;
            selectedProxy = proxy;
            if (selectedProxy != null) selectedProxy.SetSelected(true);
            if (Selected != null) Selected.SetSelected(true);
            if (SelectedProductionSourcePresenter != null) SelectedProductionSourcePresenter.SetSelected(true);
        }

        public void SelectProductionSource(ProductionSourcePresenter presenter) =>
            SetSelection(null, productionPresenter: presenter,
                proxy: presenter != null ? presenter.GetComponent<StableWorldHitProxy>() : null);

        public void SelectWorldResourcePile(WorldResourcePilePresenter presenter) =>
            SetSelection(null, pilePresenter: presenter,
                proxy: presenter != null ? presenter.GetComponent<StableWorldHitProxy>() : null);

        private void ClearNonCharacterSelection()
        {
            if (Selected != null) Selected.SetSelected(false);
            if (SelectedProductionSourcePresenter != null) SelectedProductionSourcePresenter.SetSelected(false);
            if (selectedProxy != null) selectedProxy.SetSelected(false);
            Selected = null;
            SelectedWorldResourcePilePresenter = null;
            SelectedBuildingPresenter = null;
            SelectedProductionSourcePresenter = null;
            selectedProxy = null;
        }

        private void ClearCharacterSelection()
        {
            foreach (var presenter in selectedCharacters)
                if (presenter != null) presenter.SetSelected(false);
            selectedCharacters.Clear();
            selectedCharacterIds.Clear();
            SelectedCharacterPresenter = null;
        }

        public void RefreshSelectedPresentations()
        {
            var presenters = FindObjectsByType<CharacterPresenter>(FindObjectsInactive.Exclude)
                .Where(x => x != null && x.IsBound).ToArray();
            foreach (var presenter in selectedCharacters)
                if (presenter != null && !selectedCharacterIds.Contains(presenter.CharacterId)) presenter.SetSelected(false);
            selectedCharacters.Clear();
            foreach (var presenter in presenters)
            {
                var selected = selectedCharacterIds.Contains(presenter.CharacterId);
                presenter.SetSelected(selected);
                if (selected) selectedCharacters.Add(presenter);
            }
            selectedCharacters.Sort((left, right) => string.CompareOrdinal(
                left.CharacterId.ToString(), right.CharacterId.ToString()));
            SelectedCharacterPresenter = selectedCharacters.LastOrDefault();
        }

        private void RefreshHover(Vector2 pointerPosition)
        {
            if (raycaster.IsPointerBlockedByUi(pointerPosition) ||
                !raycaster.TryGetSelectableHit(pointerPosition, out var hit))
            {
                SetHoveredProxy(null);
                return;
            }
            SetHoveredProxy(hit.collider.GetComponentInParent<StableWorldHitProxy>());
        }

        private void SetHoveredProxy(StableWorldHitProxy proxy)
        {
            if (hoveredProxy == proxy) return;
            if (hoveredProxy != null) hoveredProxy.SetHovered(false);
            hoveredProxy = proxy;
            if (hoveredProxy != null) hoveredProxy.SetHovered(true);
        }

        private static Rect ScreenRect(Vector2 a, Vector2 b) => Rect.MinMaxRect(
            Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }
}
