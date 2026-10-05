using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.Buildings;

namespace Game.Simulation.Buildings
{
    public enum PlacementFailureReason
    {
        None = 0,
        OutsideBuildableBounds = 1,
        Occupied = 2,
        UnsupportedPlacementMode = 3
    }

    public readonly struct PlacementEvaluation
    {
        public PlacementEvaluation(bool isValid, PlacementFailureReason failureReason, IReadOnlyList<GridCoordinate> cells)
        {
            IsValid = isValid;
            FailureReason = failureReason;
            Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        }

        public bool IsValid { get; }
        public PlacementFailureReason FailureReason { get; }
        public IReadOnlyList<GridCoordinate> Cells { get; }
    }

    public sealed class BuildingOccupancyGrid
    {
        private readonly Dictionary<GridCoordinate, StableEntityId> occupied = new Dictionary<GridCoordinate, StableEntityId>();

        public int OccupiedCellCount => occupied.Count;
        public bool IsOccupied(GridCoordinate coordinate) => occupied.ContainsKey(coordinate);

        public void Reserve(IEnumerable<GridCoordinate> cells, StableEntityId buildingId, bool allowsOverlap)
        {
            if (!buildingId.IsValid) throw new ArgumentException("A valid building ID is required.", nameof(buildingId));
            var materialized = new List<GridCoordinate>(cells ?? throw new ArgumentNullException(nameof(cells)));
            if (!allowsOverlap)
                foreach (var cell in materialized)
                    if (occupied.ContainsKey(cell)) throw new InvalidOperationException("A requested building cell is already occupied.");
            foreach (var cell in materialized)
                if (!allowsOverlap) occupied.Add(cell, buildingId);
        }
    }

    public sealed class BuildingPlacementService
    {
        private readonly BuildingRegistry registry;
        private readonly BuildingOccupancyGrid occupancy;
        private readonly BuildingGridBounds bounds;

        public BuildingPlacementService(BuildingRegistry registry, BuildingOccupancyGrid occupancy, BuildingGridBounds bounds)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.occupancy = occupancy ?? throw new ArgumentNullException(nameof(occupancy));
            this.bounds = bounds;
        }

        public PlacementEvaluation Evaluate(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (definition.PlacementMode != BuildingPlacementMode.Grid)
                return new PlacementEvaluation(false, PlacementFailureReason.UnsupportedPlacementMode, Array.Empty<GridCoordinate>());
            var cells = new List<GridCoordinate>(definition.Footprint.EnumerateCells(coordinate, orientation));
            foreach (var cell in cells)
                if (!bounds.Contains(cell)) return new PlacementEvaluation(false, PlacementFailureReason.OutsideBuildableBounds, cells);
            if (!definition.AllowsOverlap)
                foreach (var cell in cells)
                    if (occupancy.IsOccupied(cell)) return new PlacementEvaluation(false, PlacementFailureReason.Occupied, cells);
            return new PlacementEvaluation(true, PlacementFailureReason.None, cells);
        }

        public Building Confirm(BuildingDefinition definition, GridCoordinate coordinate, BuildingOrientation orientation)
        {
            var evaluation = Evaluate(definition, coordinate, orientation);
            if (!evaluation.IsValid)
                throw new InvalidOperationException("Building placement is invalid: " + evaluation.FailureReason);
            var building = Building.CreateNew(definition.Id, coordinate, orientation);
            occupancy.Reserve(evaluation.Cells, building.Id, definition.AllowsOverlap);
            registry.Add(building);
            return building;
        }
    }
}
