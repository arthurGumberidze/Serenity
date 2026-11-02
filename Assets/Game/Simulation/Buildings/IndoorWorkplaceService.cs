using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Simulation.AI;

namespace Game.Simulation.Buildings
{
    /// <summary>Arrival authority shared by research and barracks. Travel never counts as indoor work.</summary>
    public sealed class IndoorWorkplaceService
    {
        public static readonly BuildingDefinitionId ResearchHouse = new BuildingDefinitionId("research_house");
        private sealed class Visit
        {
            public StableEntityId Building;
            public WorldPosition Entrance;
            public ITier1MovementDriver Movement;
            public bool Arrived;
        }
        private readonly BuildingRegistry buildings;
        private readonly CombatSideId side;
        private readonly Func<Building, WorldPosition> entrance;
        private readonly Dictionary<StableEntityId, Visit> visits = new Dictionary<StableEntityId, Visit>();
        public IndoorWorkplaceService(BuildingRegistry buildings, CombatSideId side,
            Func<Building, WorldPosition> entrance)
        {
            this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            this.side = side;
            this.entrance = entrance ?? throw new ArgumentNullException(nameof(entrance));
        }
        public bool Operational(StableEntityId id) => buildings.TryGet(id, out var b) && b.IsOperational && b.OwnerSideId == side;
        public bool HasResearchHouse => buildings.All.Any(b => b.IsOperational && b.OwnerSideId == side && b.DefinitionId.FamilyId == ResearchHouse);
        public StableEntityId FindResearchHouse(WorldPosition from) => buildings.All
            .Where(b => b.IsOperational && b.OwnerSideId == side && b.DefinitionId.FamilyId == ResearchHouse)
            .OrderBy(b => entrance(b).DistanceSquared(from)).ThenBy(b => b.Id).Select(b => b.Id).FirstOrDefault();
        public bool IsInside(StableEntityId worker) => visits.TryGetValue(worker, out var visit) &&
            visit.Arrived && Operational(visit.Building) && visit.Movement.IsAvailable &&
            visit.Movement.CurrentPosition.DistanceSquared(visit.Entrance) <= 1f;
        public bool IsInside(StableEntityId worker, StableEntityId building) =>
            IsInside(worker) && visits[worker].Building == building;
        public bool IsResearchingInside(StableEntityId worker) => IsInside(worker) &&
            buildings.Get(visits[worker].Building).DefinitionId.FamilyId == ResearchHouse;
        public bool Travel(StableEntityId worker, StableEntityId building, ITier1MovementDriver movement)
        {
            if (!Operational(building) || !movement.IsAvailable) { Release(worker); return false; }
            if (!visits.TryGetValue(worker, out var visit) || visit.Building != building || visit.Movement != movement)
            {
                Release(worker);
                visit = new Visit { Building = building, Movement = movement, Entrance = entrance(buildings.Get(building)) };
                visits[worker] = visit;
                movement.MoveTo(visit.Entrance, .35f);
            }
            if (IsInside(worker)) return true;
            visit.Arrived = false;
            if (movement.State == MovementState.Arrived && movement.CurrentPosition.DistanceSquared(visit.Entrance) <= 1f)
            {
                visit.Arrived = true;
                movement.Stop();
                return true;
            }
            if (movement.State == MovementState.Idle || movement.State == MovementState.Failed)
                movement.MoveTo(visit.Entrance, .35f);
            return false;
        }
        public void Release(StableEntityId worker)
        {
            if (visits.TryGetValue(worker, out var visit)) { visit.Movement.Stop(); visits.Remove(worker); }
        }
        public void Clear() { foreach (var id in visits.Keys.ToArray()) Release(id); }
    }
}
