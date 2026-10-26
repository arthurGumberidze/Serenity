using System;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Presentation.Input;
using Game.Presentation.Interaction;
using Game.Simulation.Buildings;
using Game.Domain.Resources;
using Game.Simulation.Resources;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Game.Presentation.Environment;

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
        [SerializeField] private Color clearanceColor = new Color(1f, 0.62f, 0.12f, 0.7f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.65f);
        [SerializeField] private bool completeImmediately = true;

        private BuildingPlacementService placementService;
        private BuildingPresentationSpawner spawner;
        private LocalInteractionMode interactionMode;
        private ConstructionFundingService construction;
        private ConstructionClearanceCoordinator clearance;
        private Func<IEnumerable<InventoryOwner>> fundingSources;
        private BuildingDefinition activeDefinition;
        private BuildingOrientation orientation;
        private GridCoordinate previewCoordinate;
        private PlacementEvaluation previewEvaluation;
        private ConstructionEvaluation constructionEvaluation;
        private GameObject previewObject;
        private BuildingGridPreview gridPreview;
        private Renderer[] previewRenderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock propertyBlock;
        private bool subscribed;
        private bool exitModeAtEndOfFrame;
        private P102WorldPlacementSurface connectedWorldSurface;
        private PlacementSurfaceEvaluation surfaceEvaluation;
        private IReadOnlyList<GridCoordinate> authoredPlacementCells = Array.Empty<GridCoordinate>();
        private GridCoordinate zoneDragAnchor;
        private bool zoneDragActive;
        private int lastCancelledFrame = -1;
        private Vector3 lastPreviewPoint;
        private float nextPreviewRefresh;
        private int shoreRotationOffset;
        private const int GardenMinimumSide = 2;
        private const int GardenMaximumSide = 8;

        public bool IsActive => activeDefinition != null;
        public bool WasCancelledThisFrame => lastCancelledFrame == Time.frameCount;
        public bool HasPreview => previewObject != null;
        public bool IsPreviewValid => HasPreview && previewEvaluation.IsValid &&
            (construction == null || constructionEvaluation.IsValid);
        public ConstructionFailureReason LastConstructionFailure { get; private set; }
        public PlacementFailureReason LastPlacementFailure => previewEvaluation.FailureReason;
        public BuildingOrientation Orientation => orientation;
        public GridCoordinate PreviewCoordinate => previewCoordinate;
        public BuildingDefinitionId? ActiveDefinitionId => activeDefinition?.Id;
        public BuildingPresenter LastPlacedPresenter { get; private set; }
        public float GridSize => gridSize;
        public BuildingPlacementVisualState VisualState => !IsPreviewValid
            ? BuildingPlacementVisualState.Blocked
            : surfaceEvaluation.RequiresClearance
                ? BuildingPlacementVisualState.RequiresClearance
                : BuildingPlacementVisualState.Free;
        public IReadOnlyList<StableEntityId> PreviewClearanceSourceIds =>
            surfaceEvaluation.ClearableSources ?? Array.Empty<StableEntityId>();
        public string SurfaceBlockedReason => surfaceEvaluation.BlockedReason;
        public IReadOnlyList<StableEntityId> LastPlacedClearanceSourceIds { get; private set; } =
            Array.Empty<StableEntityId>();
        public Func<Vector3, (Vector3 point, BuildingOrientation orientation)?> ShoreSnap { get; set; }
        public event Action<Building, BuildingPresenter> BuildingPlaced;
        public event Action BuildMenuRequested;

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

        public void ConfigureClearance(ConstructionClearanceCoordinator coordinator) =>
            clearance = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

        public void StartPlacement(BuildingDefinitionId definitionId)
        {
            TryStartPlacement(definitionId);
        }

        public bool TryStartPlacement(BuildingDefinitionId definitionId)
        {
            EnsureInitialized();
            CancelPlacement();
            var definition = catalog.ResolveDefinition(definitionId);
            if (construction != null && !construction.IsBuildingUnlocked(definition.Id))
            {
                LastConstructionFailure = ConstructionFailureReason.LockedByResearch;
                return false;
            }
            activeDefinition = definition;
            LastConstructionFailure = ConstructionFailureReason.None;
            orientation = BuildingOrientation.North;
            shoreRotationOffset = 0;
            interactionMode.EnterBuildingPlacement();
            CreatePreview();
            return true;
        }

        public bool TryMovePreviewToWorldPoint(Vector3 worldPoint)
        {
            if (!IsActive) return false;
            if (activeDefinition.Id == new BuildingDefinitionId("fishing_camp"))
            {
                var snapped = ShoreSnap?.Invoke(worldPoint);
                if (snapped.HasValue)
                {
                    worldPoint = snapped.Value.point; orientation = snapped.Value.orientation;
                    for (var turn = 0; turn < shoreRotationOffset; turn++) orientation = orientation.RotateClockwise();
                }
            }
            if (zoneDragActive)
            {
                authoredPlacementCells = IsDragSegment() ? CreateSegmentCells(zoneDragAnchor, WorldToCell(worldPoint)) :
                    CreateRectangularZoneCells(zoneDragAnchor, WorldToCell(worldPoint), GardenMinimumSide, GardenMaximumSide);
                return EvaluatePreview(authoredPlacementCells[0], authoredPlacementCells, worldPoint);
            }
            connectedWorldSurface ??= FindAnyObjectByType<P102WorldPlacementSurface>();
            var footprint = activeDefinition.Footprint.Rotate(orientation);
            previewCoordinate = new GridCoordinate(
                Mathf.RoundToInt((worldPoint.x - gridOrigin.x) / gridSize - (footprint.Width - 1) * 0.5f),
                Mathf.RoundToInt((worldPoint.z - gridOrigin.z) / gridSize - (footprint.Depth - 1) * 0.5f), 0);
            authoredPlacementCells = Array.Empty<GridCoordinate>();
            return EvaluatePreview(previewCoordinate, authoredPlacementCells, worldPoint);
        }

        private bool EvaluatePreview(GridCoordinate coordinate, IReadOnlyList<GridCoordinate> authoredCells,
            Vector3 terrainPoint)
        {
            connectedWorldSurface ??= FindAnyObjectByType<P102WorldPlacementSurface>();
            previewCoordinate = coordinate;
            var request = new BuildingPlacementRequest(activeDefinition, coordinate, orientation, null, authoredCells);
            previewEvaluation = placementService.Evaluate(request);
            constructionEvaluation = construction == null ? default : construction.Evaluate(activeDefinition,
                coordinate, orientation, fundingSources(), authoredCells);
            if (construction != null) previewEvaluation = constructionEvaluation.Placement;
            surfaceEvaluation = connectedWorldSurface == null
                ? new PlacementSurfaceEvaluation(previewEvaluation.IsValid, null)
                : connectedWorldSurface.EvaluateFootprint(previewEvaluation.Cells, gridSize, gridOrigin,
                    activeDefinition.Id.ToString() == "fishing_camp" ? 3f : (float?)null, activeDefinition.Id.ToString()=="gate",
                    activeDefinition.Id.ToString() == "fishing_camp");
            if (previewEvaluation.IsValid && !surfaceEvaluation.CanPlace)
                previewEvaluation = new PlacementEvaluation(false, PlacementFailureReason.TerrainBlocked,
                    previewEvaluation.Cells, previewEvaluation.ApproachCells);
            LastConstructionFailure = constructionEvaluation.Failure;
            if (previewObject != null)
            {
                var position = authoredCells.Count > 0 ? spawner.GridToWorld(authoredCells) :
                    spawner.GridToWorld(coordinate, activeDefinition, orientation);
                previewObject.transform.SetPositionAndRotation(position,
                    Quaternion.Euler(0f, orientation.Degrees(), 0f));
                previewObject.GetComponent<BuildingZoneVisual>()?.Render(
                    authoredCells.Count > 0 ? authoredCells : previewEvaluation.Cells, gridSize);
                previewRenderers = previewObject.GetComponentsInChildren<Renderer>(true);
                var state = VisualState;
                ApplyPreviewColor(state == BuildingPlacementVisualState.Free ? validColor :
                    state == BuildingPlacementVisualState.RequiresClearance ? clearanceColor : invalidColor);
                gridPreview?.Render(previewEvaluation.Cells, state);
            }
            return IsPreviewValid;
        }

        public void RotatePreview()
        {
            if (!IsActive) return;
            if (zoneDragActive) return; // An active drag owns its world-grid direction.
            var center = previewObject != null ? previewObject.transform.position : spawner.GridToWorld(previewCoordinate);
            orientation = orientation.RotateClockwise();
            if (activeDefinition.Id.ToString() == "fishing_camp") shoreRotationOffset = (shoreRotationOffset + 1) % 4;
            TryMovePreviewToWorldPoint(center);
        }

        public Building ConfirmPlacement()
        {
            if (!IsActive) return null;
            // Recheck blockers and the complete footprint at commit time too.
            var cells = authoredPlacementCells;
            var center = cells.Count > 0 ? spawner.GridToWorld(cells) :
                spawner.GridToWorld(previewCoordinate, activeDefinition, orientation);
            EvaluatePreview(previewCoordinate, cells, center);
            if (!IsPreviewValid) return null;
            Building building;
            if (construction != null)
            {
                BuildingPresenter staged = null;
                var clearanceSources = PreviewClearanceSourceIds.ToArray();
                var result = construction.TryConfirm(activeDefinition, previewCoordinate, orientation,
                    fundingSources(), out building, candidate => staged = spawner.Spawn(candidate),
                    authoredCells: cells, deferLogistics: clearanceSources.Length > 0);
                LastConstructionFailure = result.Failure;
                if (!result.IsValid)
                {
                    if (staged != null) spawner.Despawn(staged);
                    EvaluatePreview(previewCoordinate, cells, center);
                    return null;
                }
                LastPlacedPresenter = staged;
                LastPlacedClearanceSourceIds = clearanceSources;
            }
            else
            {
                building = placementService.Confirm(new BuildingPlacementRequest(activeDefinition,
                    previewCoordinate, orientation, authoredCells: cells));
                if (completeImmediately) building.MarkCompleted();
                LastPlacedPresenter = spawner.Spawn(building);
                LastPlacedClearanceSourceIds = Array.Empty<StableEntityId>();
            }
            DestroyPreview();
            activeDefinition = null;
            previewEvaluation = default;
            constructionEvaluation = default;
            surfaceEvaluation = default;
            authoredPlacementCells = Array.Empty<GridCoordinate>();
            zoneDragActive = false;
            surfaceEvaluation = default;
            exitModeAtEndOfFrame = true;
            BuildingPlaced?.Invoke(building, LastPlacedPresenter);
            return building;
        }

        public void CancelPlacement()
        {
            if (IsActive) lastCancelledFrame = Time.frameCount;
            DestroyPreview();
            activeDefinition = null;
            previewEvaluation = default;
            constructionEvaluation = default;
            authoredPlacementCells = Array.Empty<GridCoordinate>();
            zoneDragActive = false;
            exitModeAtEndOfFrame = false;
            interactionMode?.ExitBuildingPlacement();
        }

        public bool CancelConstruction(Building building)
        {
            EnsureInitialized();
            if (building == null || building.IsOperational || construction == null) return false;
            var definition = catalog.ResolveDefinition(building.DefinitionId);
            clearance?.Cancel(building.Id);
            if (!construction.Cancel(building, definition)) return false;
            if (spawner.TryGetPresenter(building.Id, out var presenter)) spawner.Despawn(presenter);
            return true;
        }

        private void Update()
        {
            if (!IsActive || inputSource == null || raycaster == null) return;
            if (raycaster.TryGetGroundHit(inputSource.PointerPosition, out var hit))
            {
                // Revalidate stationary previews periodically; confirmation always validates synchronously.
                if (Time.unscaledTime >= nextPreviewRefresh || (lastPreviewPoint - hit.point).sqrMagnitude > .25f)
                {
                    lastPreviewPoint = hit.point;
                    nextPreviewRefresh = Time.unscaledTime + .12f;
                    TryMovePreviewToWorldPoint(hit.point);
                }
            }
            else
            {
                previewEvaluation = new PlacementEvaluation(false, PlacementFailureReason.OutsideBuildableBounds,
                    Array.Empty<GridCoordinate>());
                ApplyPreviewColor(invalidColor);
                gridPreview?.Render(Array.Empty<GridCoordinate>(), BuildingPlacementVisualState.Blocked);
            }
            if (zoneDragActive && Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            {
                zoneDragActive = false;
                ConfirmPlacement();
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
            BuildingPlaced = null;
            BuildMenuRequested = null;
            DestroyPreview();
        }

        private void DestroyPreview()
        {
            if (previewObject != null) Destroy(previewObject);
            if (gridPreview != null) Destroy(gridPreview.gameObject);
            previewObject = null;
            gridPreview = null;
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
            if (IsActive)
            {
                CancelPlacement();
                if (BuildMenuRequested == null) return;
            }
            if (BuildMenuRequested != null) BuildMenuRequested.Invoke();
            else if (!IsActive) StartPlacement(catalog.DefaultDefinitionId);
        }
        private void OnBuildConfirmed()
        {
            if (inputSource != null && raycaster != null && raycaster.IsPointerBlockedByUi(inputSource.PointerPosition))
                return;
            if ((IsVariableGarden() || IsDragSegment()) && raycaster != null && inputSource != null &&
                raycaster.TryGetGroundHit(inputSource.PointerPosition, out var hit))
            {
                zoneDragAnchor = WorldToCell(hit.point);
                zoneDragActive = true;
                authoredPlacementCells = IsDragSegment() ? CreateSegmentCells(zoneDragAnchor, zoneDragAnchor) :
                    CreateRectangularZoneCells(zoneDragAnchor, zoneDragAnchor, GardenMinimumSide, GardenMaximumSide);
                EvaluatePreview(authoredPlacementCells[0], authoredPlacementCells, hit.point);
                return;
            }
            ConfirmPlacement();
        }
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
            var gridObject = new GameObject("Building footprint preview");
            SetLayerRecursively(gridObject, LayerMask.NameToLayer("Ignore Raycast"));
            gridPreview = gridObject.AddComponent<BuildingGridPreview>();
            gridPreview.Initialize(gridSize, spawner.GridToWorld);
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

        private bool IsVariableGarden() => activeDefinition != null &&
            activeDefinition.Id == new BuildingDefinitionId("primitive_garden");

        private bool IsDragSegment() => activeDefinition != null &&
            (activeDefinition.Id == new BuildingDefinitionId("palisade") ||
             activeDefinition.Id == new BuildingDefinitionId("primitive_path"));

        public static IReadOnlyList<GridCoordinate> CreateSegmentCells(GridCoordinate anchor,
            GridCoordinate pointer, int maximumCells = 32)
        {
            if (maximumCells < 1) throw new ArgumentOutOfRangeException(nameof(maximumCells));
            var dx = pointer.X - anchor.X;
            var dz = pointer.Z - anchor.Z;
            var alongX = Math.Abs(dx) >= Math.Abs(dz);
            var length = Math.Min(maximumCells, Math.Abs(alongX ? dx : dz) + 1);
            var direction = Math.Sign(alongX ? dx : dz);
            var cells = new List<GridCoordinate>(length);
            for (var i = 0; i < length; i++) cells.Add(new GridCoordinate(
                anchor.X + (alongX ? i * direction : 0), anchor.Z + (alongX ? 0 : i * direction), anchor.Level));
            return cells;
        }

        private GridCoordinate WorldToCell(Vector3 worldPoint) => new GridCoordinate(
            Mathf.RoundToInt((worldPoint.x - gridOrigin.x) / gridSize),
            Mathf.RoundToInt((worldPoint.z - gridOrigin.z) / gridSize), 0);

        public static IReadOnlyList<GridCoordinate> CreateRectangularZoneCells(GridCoordinate anchor,
            GridCoordinate pointer, int minimumSide = GardenMinimumSide, int maximumSide = GardenMaximumSide)
        {
            if (minimumSide < 1 || maximumSide < minimumSide) throw new ArgumentOutOfRangeException(nameof(minimumSide));
            static int ClampedEnd(int start, int end, int min, int max)
            {
                var direction = end < start ? -1 : 1;
                var distance = Mathf.Clamp(Mathf.Abs(end - start) + 1, min, max);
                return start + direction * (distance - 1);
            }
            var endX = ClampedEnd(anchor.X, pointer.X, minimumSide, maximumSide);
            var endZ = ClampedEnd(anchor.Z, pointer.Z, minimumSide, maximumSide);
            var minX = Math.Min(anchor.X, endX);
            var maxX = Math.Max(anchor.X, endX);
            var minZ = Math.Min(anchor.Z, endZ);
            var maxZ = Math.Max(anchor.Z, endZ);
            var result = new List<GridCoordinate>((maxX - minX + 1) * (maxZ - minZ + 1));
            for (var x = minX; x <= maxX; x++)
                for (var z = minZ; z <= maxZ; z++)
                    result.Add(new GridCoordinate(x, z, anchor.Level));
            // PlacementService requires the authored anchor to be the first item.
            result.Remove(anchor);
            result.Insert(0, anchor);
            return result;
        }
    }
}
