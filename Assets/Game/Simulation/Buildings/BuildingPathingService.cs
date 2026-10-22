using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;

namespace Game.Simulation.Buildings
{
    /// <summary>Projects canonical structure state into the shared placement/path-blocking grid.</summary>
    public sealed class BuildingPathingService
    {
        private static readonly BuildingDefinitionId GateDefinition = new BuildingDefinitionId("gate");
        private readonly BuildingRegistry buildings;
        private readonly BuildingOccupancyGrid occupancy;
        private readonly Func<BuildingDefinitionId, BuildingDefinition> definitions;
        private readonly HashSet<StableEntityId> openGates = new HashSet<StableEntityId>();
        private readonly HashSet<StableEntityId> releasedDestroyed = new HashSet<StableEntityId>();

        public BuildingPathingService(BuildingRegistry buildingRegistry, BuildingOccupancyGrid occupancyGrid,
            Func<BuildingDefinitionId, BuildingDefinition> definitionResolver)
        {
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            occupancy = occupancyGrid ?? throw new ArgumentNullException(nameof(occupancyGrid));
            definitions = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
        }

        public bool IsGateOpen(StableEntityId buildingId) => openGates.Contains(buildingId);

        public bool SetGateOpen(StableEntityId buildingId, bool open)
        {
            if (!buildings.TryGet(buildingId, out var building) || building.DefinitionId != GateDefinition ||
                !building.IsOperational) return false;
            var definition = definitions(building.DefinitionId);
            var cells = Cells(building, definition);
            if (open)
            {
                if (!openGates.Add(buildingId)) return true;
                occupancy.Release(cells, buildingId);
            }
            else
            {
                if (!openGates.Remove(buildingId)) return true;
                occupancy.Reserve(cells, buildingId, false);
            }
            return true;
        }

        public void Reconcile(Building building)
        {
            if (building == null) return;
            var definition = definitions(building.DefinitionId);
            var cells = Cells(building, definition);
            if (building.IsDestroyed)
            {
                if (releasedDestroyed.Add(building.Id)) occupancy.Release(cells, building.Id);
                openGates.Remove(building.Id);
                return;
            }
            releasedDestroyed.Remove(building.Id);
            if (building.DefinitionId == GateDefinition && openGates.Contains(building.Id))
                occupancy.Release(cells, building.Id);
        }

        public void RebindAfterLoad()
        {
            openGates.RemoveWhere(id => !buildings.TryGet(id, out var building) || !building.IsOperational ||
                building.DefinitionId != GateDefinition);
            releasedDestroyed.Clear();
            foreach (var building in buildings.All.OrderBy(x => x.Id)) Reconcile(building);
        }

        private static IEnumerable<GridCoordinate> Cells(Building building, BuildingDefinition definition) =>
            building.PlacementCells.Count == 0
                ? definition.Footprint.EnumerateCells(building.Coordinate, building.Orientation)
                : building.PlacementCells;
    }
}
