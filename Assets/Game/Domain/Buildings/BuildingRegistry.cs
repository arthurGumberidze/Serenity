using System;
using System.Collections.Generic;

namespace Game.Domain.Buildings
{
    public sealed class BuildingRegistry
    {
        private readonly Dictionary<StableEntityId, Building> buildings = new Dictionary<StableEntityId, Building>();

        public int Count => buildings.Count;
        public IEnumerable<Building> All => buildings.Values;

        public void Add(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (!buildings.TryAdd(building.Id, building))
                throw new InvalidOperationException("A building with the same stable identity is already registered.");
        }

        public Building Get(StableEntityId id)
        {
            if (!buildings.TryGetValue(id, out var building)) throw new KeyNotFoundException("Building was not found.");
            return building;
        }

        public bool TryGet(StableEntityId id, out Building building) => buildings.TryGetValue(id, out building);
    }
}
