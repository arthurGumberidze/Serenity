using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;

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
        private readonly Dictionary<StableEntityId, GateAccessMode> gateModes = new Dictionary<StableEntityId, GateAccessMode>();
        private readonly Dictionary<StableEntityId, StableEntityId> detainedTravelers = new Dictionary<StableEntityId, StableEntityId>();
        private readonly HashSet<StableEntityId> allowedTravelersOnce = new HashSet<StableEntityId>();
        private readonly HashSet<StableEntityId> releasedDestroyed = new HashSet<StableEntityId>();

        public BuildingPathingService(BuildingRegistry buildingRegistry, BuildingOccupancyGrid occupancyGrid,
            Func<BuildingDefinitionId, BuildingDefinition> definitionResolver)
        {
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            occupancy = occupancyGrid ?? throw new ArgumentNullException(nameof(occupancyGrid));
            definitions = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
        }

        public event Action<StableEntityId,bool> GateChanged;
        public bool IsGateOpen(StableEntityId buildingId) => openGates.Contains(buildingId);
        public IReadOnlyDictionary<StableEntityId, StableEntityId> DetainedTravelers => detainedTravelers;
        public Func<CombatSideId, CombatSideId, bool> AreAllied { get; set; }

        public GateAccessMode GetGateAccessMode(StableEntityId buildingId) =>
            gateModes.TryGetValue(buildingId, out var mode) ? mode : GateAccessMode.Closed;

        public bool SetGateAccessMode(StableEntityId buildingId, GateAccessMode mode)
        {
            if (!Enum.IsDefined(typeof(GateAccessMode), mode) ||
                !buildings.TryGet(buildingId, out var building) || building.DefinitionId != GateDefinition ||
                !building.IsOperational) return false;
            gateModes[buildingId] = mode;
            return SetGateOpen(buildingId, mode == GateAccessMode.Open || mode == GateAccessMode.AlliesOnly);
        }

        public GatePassageResult EvaluatePassage(StableEntityId buildingId, StableEntityId travelerId,
            CombatSideId? travelerSide)
        {
            if (!travelerId.IsValid || !buildings.TryGet(buildingId, out var gate) ||
                gate.DefinitionId != GateDefinition || !gate.IsOperational) return GatePassageResult.Denied;
            if (allowedTravelersOnce.Remove(travelerId)) return GatePassageResult.Allowed;
            switch (GetGateAccessMode(buildingId))
            {
                case GateAccessMode.Open:
                    return GatePassageResult.Allowed;
                case GateAccessMode.Closed:
                case GateAccessMode.Nobody:
                    return GatePassageResult.Denied;
                case GateAccessMode.AlliesOnly:
                    if (travelerSide.HasValue && (travelerSide.Value == gate.OwnerSideId ||
                        (AreAllied?.Invoke(gate.OwnerSideId, travelerSide.Value) ?? false)))
                        return GatePassageResult.Allowed;
                    if (travelerSide.HasValue) return GatePassageResult.Denied;
                    detainedTravelers[travelerId] = buildingId;
                    return GatePassageResult.DetainedAwaitingPlayer;
                default:
                    throw new InvalidOperationException("Unsupported gate access mode.");
            }
        }

        public GatePassageResult ResolveDetention(StableEntityId travelerId, bool allowPass)
        {
            if (!detainedTravelers.Remove(travelerId)) return GatePassageResult.Denied;
            if (allowPass) allowedTravelersOnce.Add(travelerId);
            return allowPass ? GatePassageResult.Allowed : GatePassageResult.Denied;
        }

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
            GateChanged?.Invoke(buildingId,open);
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
                gateModes.Remove(building.Id);
                foreach (var traveler in detainedTravelers.Where(x => x.Value == building.Id).Select(x => x.Key).ToArray())
                    detainedTravelers.Remove(traveler);
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
            foreach (var id in gateModes.Keys.Where(id => !buildings.TryGet(id, out var building) ||
                         !building.IsOperational || building.DefinitionId != GateDefinition).ToArray()) gateModes.Remove(id);
            detainedTravelers.Clear();
            allowedTravelersOnce.Clear();
            releasedDestroyed.Clear();
            foreach (var building in buildings.All.OrderBy(x => x.Id)) Reconcile(building);
        }

        private static IEnumerable<GridCoordinate> Cells(Building building, BuildingDefinition definition) =>
            building.PlacementCells.Count == 0
                ? definition.Footprint.EnumerateCells(building.Coordinate, building.Orientation)
                : building.PlacementCells;
    }
}
