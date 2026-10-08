using System;
using System.Collections.Generic;
using System.Linq;

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

        public bool Remove(Building expected)
        {
            if (expected == null || !buildings.TryGetValue(expected.Id, out var current) || !ReferenceEquals(current, expected))
                return false;
            return buildings.Remove(expected.Id);
        }

        public void ReplaceAll(IEnumerable<BuildingState> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            var replacement = new Dictionary<StableEntityId, Building>();
            foreach (var state in states)
            {
                var building = Building.Restore(state);
                if (!replacement.TryAdd(building.Id, building))
                    throw new ArgumentException("Building snapshot contains duplicate identities.", nameof(states));
            }
            buildings.Clear();
            foreach (var pair in replacement.OrderBy(x => x.Key)) buildings.Add(pair.Key, pair.Value);
        }
    }
}
