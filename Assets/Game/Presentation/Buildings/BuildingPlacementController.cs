using System;
using Game.Domain.Buildings;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Buildings;
using Game.Domain.Resources;
using Game.Simulation.Resources;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingPlacementController : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private LocalGameplayInputSource inputSource;
        [SerializeField] private WorldPointerRaycaster raycaster;
        [SerializeField] private BuildingPresentationCatalog catalog;
        [SerializeField, Min(0.1f)] private float gridSize = 1f;
        [SerializeField] private Vector3 gridOrigin;
        [SerializeField] private Color validColor = new Color(0.25f, 1f, 0.35f, 0.65f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.65f);
        [SerializeField] private bool completeImmediately = true;

        private BuildingPlacementService placementService;
        private BuildingPresentationSpawner spawner;
        private LocalInteractionMode interactionMode;
        private ConstructionFundingService construction;
        private Func<IEnumerable<InventoryOwner>> fundingSources;
        private BuildingDefinition activeDefinition;
        private BuildingOrientation orientation;
        private GridCoordinate previewCoordinate;
        private PlacementEvaluation previewEvaluation;
        private ConstructionEvaluation constructionEvaluation;
        private GameObject previewObject;
        private Renderer[] previewRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock propertyBlock;
        private bool subscribed;
        private bool exitModeAtEndOfFrame;

        public bool IsActive => activeDefinition != null;
        public bool HasPreview => previewObject != null;
        public bool IsPreviewValid => HasPreview && previewEvaluation.IsValid &&
            (construction == null || constructionEvaluation.IsValid);
        public ConstructionFailureReason LastConstructionFailure { get; private set; }
        public BuildingOrientation Orientation => orientation;
        public GridCoordinate PreviewCoordinate => previewCoordinate;
        public BuildingPresenter LastPlacedPresenter { get; private set; }
        public float GridSize => gridSize;

        public void Configure(LocalGameplayInputSource source, WorldPointerRaycaster worldRaycaster,
            BuildingPresentationCatalog presentationCatalog, float configuredGridSize, Vector3 origin)
        {
            inputSource = source;
            raycaster = worldRaycaster;
            catalog = presentationCatalog;
            gridSize = Mathf.Max(0.1f, configuredGridSize);
            gridOrigin = origin;
        }

        public void Initialize(BuildingPlacementService service, BuildingPresentationSpawner presentationSpawner,
            LocalInteractionMode mode, ConstructionFundingService constructionService = null,
            Func<IEnumerable<InventoryOwner>> fundingSourceProvider = null)
        {
            placementService = service ?? throw new ArgumentNullException(nameof(service));
            spawner = presentationSpawner ?? throw new ArgumentNullException(nameof(presentationSpawner));
            interactionMode = mode ?? throw new ArgumentNullException(nameof(mode));
            construction = constructionService;
            fundingSources = fundingSourceProvider;
            Subscribe();
        }

        public void StartPlacement(BuildingDefinitionId definitionId)
        {
            EnsureInitialized();
            CancelPlacement();
            activeDefinition = catalog.ResolveDefinition(definitionId);
            orientation = BuildingOrientation.North;
            interactionMode.EnterBuildingPlacement();
            CreatePreview();
        }

        public bool TryMovePreviewToWorldPoint(Vector3 worldPoint)
        {
            if (!IsActive) return false;
            var footprint = activeDefinition.Footprint.Rotate(orientation);
            previewCoordinate = new GridCoordinate(
                Mathf.RoundToInt((worldPoint.x - gridOrigin.x) / gridSize - (footprint.Width - 1) * 0.5f),
                Mathf.RoundToInt((worldPoint.z - gridOrigin.z) / gridSize - (footprint.Depth - 1) * 0.5f), 0);
            previewEvaluation = placementService.Evaluate(activeDefinition, previewCoordinate, orientation);
            constructionEvaluation = construction == null ? default : construction.Evaluate(activeDefinition,
                previewCoordinate, orientation, fundingSources());
            LastConstructionFailure = constructionEvaluation.Failure;
            if (previewObject != null)
            {
                previewObject.transform.SetPositionAndRotation(spawner.GridToWorld(previewCoordinate, activeDefinition, orientation),
                    Quaternion.Euler(0f, orientation.Degrees(), 0f));
                ApplyPreviewColor(IsPreviewValid ? validColor : invalidColor);
            }
            return IsPreviewValid;
        }

        public void RotatePreview()
        {
            if (!IsActive) return;
            var center = previewObject != null ? previewObject.transform.position : spawner.GridToWorld(previewCoordinate);
            orientation = orientation.RotateClockwise();
            TryMovePreviewToWorldPoint(center);
        }

        public Building ConfirmPlacement()
        {
            if (!IsActive || !IsPreviewValid) return null;
            Building building;
            if (construction != null)
            {
                BuildingPresenter staged = null;
                var result = construction.TryConfirm(activeDefinition, previewCoordinate, orientation,
                    fundingSources(), out building, candidate => staged = spawner.Spawn(candidate));
                LastConstructionFailure = result.Failure;
                if (!result.IsValid)
                {
                    if (staged != null) spawner.Despawn(staged);
                    TryMovePreviewToWorldPoint(spawner.GridToWorld(previewCoordinate, activeDefinition, orientation));
                    return null;
                }
                LastPlacedPresenter = staged;
            }
            else
            {
                building = placementService.Confirm(activeDefinition, previewCoordinate, orientation);
                if (completeImmediately) building.MarkCompleted();
                LastPlacedPresenter = spawner.Spawn(building);
            }
            DestroyPreview();
            activeDefinition = null;
            previewEvaluation = default;
            constructionEvaluation = default;
            exitModeAtEndOfFrame = true;
            return building;
        }

        public void CancelPlacement()
        {
            DestroyPreview();
            activeDefinition = null;
            previewEvaluation = default;
            constructionEvaluation = default;
            exitModeAtEndOfFrame = false;
            interactionMode?.ExitBuildingPlacement();
        }

        private void Update()
        {
            if (!IsActive || inputSource == null || raycaster == null) return;
            if (raycaster.TryGetWorldHit(inputSource.PointerPosition, out var hit) && hit.collider.GetComponentInParent<BuildableGround>() != null)
                TryMovePreviewToWorldPoint(hit.point);
            else
            {
                previewEvaluation = new PlacementEvaluation(false, PlacementFailureReason.OutsideBuildableBounds,
                    Array.Empty<GridCoordinate>());
                ApplyPreviewColor(invalidColor);
            }
        }

        private void LateUpdate()
        {
            if (!exitModeAtEndOfFrame) return;
            exitModeAtEndOfFrame = false;
            interactionMode?.ExitBuildingPlacement();
        }

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy()
        {
            Unsubscribe();
            if (previewObject != null) Destroy(previewObject);
        }

        private void DestroyPreview()
        {
            if (previewObject != null) Destroy(previewObject);
            previewObject = null;
            previewRenderers = Array.Empty<Renderer>();
        }

        private void Subscribe()
        {
            if (subscribed || inputSource == null || placementService == null) return;
            inputSource.BuildModeRequested += OnBuildModeRequested;
            inputSource.BuildConfirmed += OnBuildConfirmed;
            inputSource.BuildCancelled += OnBuildCancelled;
            inputSource.BuildRotated += OnBuildRotated;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || inputSource == null) return;
            inputSource.BuildModeRequested -= OnBuildModeRequested;
            inputSource.BuildConfirmed -= OnBuildConfirmed;
            inputSource.BuildCancelled -= OnBuildCancelled;
            inputSource.BuildRotated -= OnBuildRotated;
            subscribed = false;
        }

        private void OnBuildModeRequested()
        {
            if (IsActive) CancelPlacement();
            else StartPlacement(catalog.DefaultDefinitionId);
        }
        private void OnBuildConfirmed() => ConfirmPlacement();
        private void OnBuildCancelled() => CancelPlacement();
        private void OnBuildRotated() => RotatePreview();

        private void CreatePreview()
        {
            previewObject = Instantiate(catalog.ResolvePrefab(activeDefinition.Id));
            previewObject.name = "Building Placement Preview";
            foreach (var collider in previewObject.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var presenter in previewObject.GetComponentsInChildren<BuildingPresenter>(true)) presenter.enabled = false;
            SetLayerRecursively(previewObject, LayerMask.NameToLayer("Ignore Raycast"));
            previewRenderers = previewObject.GetComponentsInChildren<Renderer>(true);
            propertyBlock ??= new MaterialPropertyBlock();
            TryMovePreviewToWorldPoint(gridOrigin);
        }

        private void ApplyPreviewColor(Color color)
        {
            foreach (var renderer in previewRenderers)
            {
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColor, color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;
            foreach (Transform child in target.transform) SetLayerRecursively(child.gameObject, layer);
        }

        private void EnsureInitialized()
        {
            if (placementService == null || spawner == null || interactionMode == null || catalog == null)
                throw new InvalidOperationException("Building placement controller is not initialized.");
        }
    }
}
