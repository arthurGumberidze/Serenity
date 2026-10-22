using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.World;

namespace Game.Simulation.Buildings
{
    public enum PlacementFailureReason
    {
        None, OutsideBuildableBounds, Occupied, UnsupportedPlacementMode, TerrainBlocked,
        InvalidShape, MissingSettlement, OutsideOwnedTerritory, NoReachableApproach,
        ShorelineRequired, GroundwaterRequired
    }

    public readonly struct BuildingPlacementRequest
    {
        public BuildingPlacementRequest(BuildingDefinition definition, GridCoordinate anchor,
            BuildingOrientation orientation, SettlementId? settlementId = null,
            IEnumerable<GridCoordinate> authoredCells = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Anchor = anchor;
            Orientation = orientation;
            SettlementId = settlementId;
            AuthoredCells = Array.AsReadOnly((authoredCells ?? Array.Empty<GridCoordinate>()).ToArray());
        }
        public BuildingDefinition Definition { get; }
        public GridCoordinate Anchor { get; }
        public BuildingOrientation Orientation { get; }
        public SettlementId? SettlementId { get; }
        public IReadOnlyList<GridCoordinate> AuthoredCells { get; }
    }

    public readonly struct PlacementRuleContext
    {
        public PlacementRuleContext(BuildingPlacementRequest request, IReadOnlyList<GridCoordinate> cells,
            IReadOnlyList<GridCoordinate> approachCells)
        { Request = request; Cells = cells; ApproachCells = approachCells; }
        public BuildingPlacementRequest Request { get; }
        public IReadOnlyList<GridCoordinate> Cells { get; }
        public IReadOnlyList<GridCoordinate> ApproachCells { get; }
    }

    public readonly struct PlacementEvaluation
    {
        public PlacementEvaluation(bool isValid, PlacementFailureReason failureReason,
            IReadOnlyList<GridCoordinate> cells, IReadOnlyList<GridCoordinate> approachCells = null)
        {
            IsValid = isValid; FailureReason = failureReason;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
            ApproachCells = approachCells ?? Array.Empty<GridCoordinate>();
        }
        public bool IsValid { get; }
        public PlacementFailureReason FailureReason { get; }
        public IReadOnlyList<GridCoordinate> Cells { get; }
        public IReadOnlyList<GridCoordinate> ApproachCells { get; }
    }

    public readonly struct GridTerritoryRegion
    {
        public GridTerritoryRegion(int minX, int minZ, int maxX, int maxZ, int level = 0)
        {
            if (maxX < minX || maxZ < minZ) throw new ArgumentOutOfRangeException(nameof(maxX));
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ; Level = level;
        }
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }
        public int Level { get; }
        public bool Contains(GridCoordinate cell) => cell.Level == Level && cell.X >= MinX && cell.X <= MaxX &&
            cell.Z >= MinZ && cell.Z <= MaxZ;
    }

    public sealed class SettlementTerritoryRegistry
    {
        private readonly Dictionary<SettlementId, List<GridTerritoryRegion>> regions =
            new Dictionary<SettlementId, List<GridTerritoryRegion>>();
        public void Replace(SettlementId id, IEnumerable<GridTerritoryRegion> territory)
        {
            if (!id.IsValid) throw new ArgumentException("Settlement identity is required.", nameof(id));
            var value = (territory ?? throw new ArgumentNullException(nameof(territory))).ToList();
            if (value.Count == 0) throw new ArgumentException("Territory cannot be empty.", nameof(territory));
            regions[id] = value;
        }
        public bool Contains(SettlementId id, GridCoordinate cell) =>
            regions.TryGetValue(id, out var value) && value.Any(region => region.Contains(cell));
    }

    public sealed class BuildingOccupancyGrid
    {
        private readonly Dictionary<GridCoordinate, StableEntityId> occupied = new Dictionary<GridCoordinate, StableEntityId>();
        public int OccupiedCellCount => occupied.Count;
        public bool IsOccupied(GridCoordinate coordinate) => occupied.ContainsKey(coordinate);
        public void Reserve(IEnumerable<GridCoordinate> cells, StableEntityId buildingId, bool allowsOverlap)
        {
            if (!buildingId.IsValid) throw new ArgumentException("A valid building ID is required.", nameof(buildingId));
            var value = new List<GridCoordinate>(cells ?? throw new ArgumentNullException(nameof(cells)));
            if (!allowsOverlap && value.Any(IsOccupied))
                throw new InvalidOperationException("A requested building cell is already occupied.");
            if (!allowsOverlap) foreach (var cell in value) occupied.Add(cell, buildingId);
        }
        public void Release(IEnumerable<GridCoordinate> cells, StableEntityId buildingId)
        {
            foreach (var cell in cells ?? throw new ArgumentNullException(nameof(cells)))
                if (occupied.TryGetValue(cell, out var owner) && owner == buildingId) occupied.Remove(cell);
        }
        public void Clear() => occupied.Clear();
    }

    public sealed class BuildingPlacementService
    {
        private const int MaximumAuthoredCells = 4096;
        private readonly BuildingRegistry registry;
        private readonly BuildingOccupancyGrid occupancy;
        private readonly BuildingGridBounds bounds;
        private Func<IReadOnlyList<GridCoordinate>, bool> environmentAllows;
        private Func<PlacementRuleContext, PlacementFailureReason> ruleEvaluator;
        private SettlementTerritoryRegistry territories;

        public BuildingPlacementService(BuildingRegistry registry, BuildingOccupancyGrid occupancy, BuildingGridBounds bounds)
        { this.registry = registry ?? throw new ArgumentNullException(nameof(registry)); this.occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy)); this.bounds = bounds; }
        public void ConfigureEnvironment(Func<IReadOnlyList<GridCoordinate>, bool> allows) => environmentAllows = allows ?? throw new ArgumentNullException(nameof(allows));
        public void ConfigureRuleEvaluator(Func<PlacementRuleContext, PlacementFailureReason> evaluator) => ruleEvaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        public void ConfigureTerritories(SettlementTerritoryRegistry registry) => territories = registry ?? throw new ArgumentNullException(nameof(registry));
        public PlacementEvaluation Evaluate(BuildingDefinition definition, GridCoordinate coordinate, BuildingOrientation orientation) =>
            Evaluate(new BuildingPlacementRequest(definition, coordinate, orientation));

        public PlacementEvaluation Evaluate(BuildingPlacementRequest request)
        {
            var definition = request.Definition;
            if (definition.PlacementMode != BuildingPlacementMode.Grid) return Invalid(PlacementFailureReason.UnsupportedPlacementMode);
            if (!TryResolveCells(request, out var cells)) return Invalid(PlacementFailureReason.InvalidShape);
            if (cells.Any(cell => !bounds.Contains(cell))) return Invalid(PlacementFailureReason.OutsideBuildableBounds, cells);
            if (!definition.AllowsOverlap && cells.Any(occupancy.IsOccupied)) return Invalid(PlacementFailureReason.Occupied, cells);
            var constraints = definition.Placement.Constraints;
            if ((constraints & BuildingPlacementConstraint.OwnedTerritory) != 0)
            {
                if (!request.SettlementId.HasValue) return Invalid(PlacementFailureReason.MissingSettlement, cells);
                if (territories == null || cells.Any(cell => !territories.Contains(request.SettlementId.Value, cell)))
                    return Invalid(PlacementFailureReason.OutsideOwnedTerritory, cells);
            }
            var approach = ApproachCells(cells).Where(bounds.Contains).Where(cell => !occupancy.IsOccupied(cell))
                .Where(cell => (constraints & BuildingPlacementConstraint.OwnedTerritory) == 0 || territories.Contains(request.SettlementId.Value, cell)).ToArray();
            if ((constraints & BuildingPlacementConstraint.ReachableApproach) != 0 && approach.Length == 0)
                return Invalid(PlacementFailureReason.NoReachableApproach, cells, approach);
            if (environmentAllows != null && !environmentAllows(cells)) return Invalid(PlacementFailureReason.TerrainBlocked, cells, approach);
            if (ruleEvaluator != null)
            {
                var failure = ruleEvaluator(new PlacementRuleContext(request, cells, approach));
                if (failure != PlacementFailureReason.None) return Invalid(failure, cells, approach);
            }
            else
            {
                if ((constraints & BuildingPlacementConstraint.Shoreline) != 0) return Invalid(PlacementFailureReason.ShorelineRequired, cells, approach);
                if ((constraints & BuildingPlacementConstraint.Groundwater) != 0) return Invalid(PlacementFailureReason.GroundwaterRequired, cells, approach);
            }
            return new PlacementEvaluation(true, PlacementFailureReason.None, cells, approach);
        }

        public Building Confirm(BuildingDefinition definition, GridCoordinate coordinate, BuildingOrientation orientation,
            CombatSideId ownerSideId = default, StableEntityId? stableId = null) => Confirm(new BuildingPlacementRequest(definition, coordinate, orientation), ownerSideId, stableId);
        public Building Confirm(BuildingPlacementRequest request, CombatSideId ownerSideId = default, StableEntityId? stableId = null)
        {
            var evaluation = Evaluate(request);
            if (!evaluation.IsValid) throw new InvalidOperationException("Building placement is invalid: " + evaluation.FailureReason);
            var owner = ownerSideId.IsValid ? ownerSideId : new CombatSideId("player");
            var building = Building.CreateNew(request.Definition, request.Anchor, request.Orientation, owner,
                evaluation.Cells, request.SettlementId, stableId);
            occupancy.Reserve(evaluation.Cells, building.Id, request.Definition.AllowsOverlap);
            try { registry.Add(building); } catch { occupancy.Release(evaluation.Cells, building.Id); throw; }
            return building;
        }
        public void Rollback(Building building, BuildingDefinition definition)
        {
            if (building == null || definition == null || building.DefinitionId != definition.Id) throw new ArgumentException("Matching building and definition required.");
            if (!registry.Remove(building)) throw new InvalidOperationException("Building transaction is not registered.");
            occupancy.Release(building.PlacementCells.Count == 0 ? definition.Footprint.EnumerateCells(building.Coordinate, building.Orientation) : building.PlacementCells, building.Id);
        }

        private static PlacementEvaluation Invalid(PlacementFailureReason reason, IReadOnlyList<GridCoordinate> cells = null,
            IReadOnlyList<GridCoordinate> approach = null) => new PlacementEvaluation(false, reason, cells ?? Array.Empty<GridCoordinate>(), approach);
        private static bool TryResolveCells(BuildingPlacementRequest request, out IReadOnlyList<GridCoordinate> cells)
        {
            if (request.Definition.Placement.SpatialForm == BuildingSpatialForm.Structure)
            {
                cells = request.AuthoredCells.Count == 0 ? request.Definition.Footprint.EnumerateCells(request.Anchor, request.Orientation).ToArray() : Array.Empty<GridCoordinate>();
                return request.AuthoredCells.Count == 0;
            }
            var authored = request.AuthoredCells;
            if (authored.Count == 0)
            {
                cells = request.Definition.Placement.SpatialForm == BuildingSpatialForm.Segment
                    ? new[] { request.Anchor }
                    : request.Definition.Footprint.EnumerateCells(request.Anchor, request.Orientation).ToArray();
                return true;
            }
            if (authored.Count > MaximumAuthoredCells || authored[0] != request.Anchor || authored.Any(x => x.Level != request.Anchor.Level) || authored.Distinct().Count() != authored.Count)
            { cells = Array.Empty<GridCoordinate>(); return false; }
            if (request.Definition.Placement.SpatialForm == BuildingSpatialForm.Segment)
            {
                for (var i = 1; i < authored.Count; i++) if (!AreCardinalNeighbours(authored[i - 1], authored[i])) { cells = Array.Empty<GridCoordinate>(); return false; }
                cells = authored.ToArray(); return true;
            }
            var remaining = new HashSet<GridCoordinate>(authored); var queue = new Queue<GridCoordinate>();
            queue.Enqueue(authored[0]); remaining.Remove(authored[0]);
            while (queue.Count > 0) foreach (var neighbour in Neighbours(queue.Dequeue())) if (remaining.Remove(neighbour)) queue.Enqueue(neighbour);
            cells = authored.OrderBy(x => x.X).ThenBy(x => x.Z).ToArray(); return remaining.Count == 0;
        }
        private static GridCoordinate[] ApproachCells(IReadOnlyList<GridCoordinate> cells)
        {
            var shape = new HashSet<GridCoordinate>(cells);
            return cells.SelectMany(Neighbours).Where(x => !shape.Contains(x)).Distinct().OrderBy(x => x.X).ThenBy(x => x.Z).ThenBy(x => x.Level).ToArray();
        }
        private static IEnumerable<GridCoordinate> Neighbours(GridCoordinate cell)
        {
            yield return new GridCoordinate(cell.X - 1, cell.Z, cell.Level); yield return new GridCoordinate(cell.X, cell.Z - 1, cell.Level);
            yield return new GridCoordinate(cell.X, cell.Z + 1, cell.Level); yield return new GridCoordinate(cell.X + 1, cell.Z, cell.Level);
        }
        private static bool AreCardinalNeighbours(GridCoordinate a, GridCoordinate b) => a.Level == b.Level && Math.Abs(a.X - b.X) + Math.Abs(a.Z - b.Z) == 1;
    }
}
