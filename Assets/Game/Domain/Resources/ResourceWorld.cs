using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;

namespace Game.Domain.Resources
{
    public sealed class ResourceInventoryRegistry
    {
        private readonly Dictionary<InventoryOwner, ResourceInventory> inventories = new Dictionary<InventoryOwner, ResourceInventory>();

        public IEnumerable<ResourceInventory> All => inventories.Values;
        public int Count => inventories.Count;

        public void Add(ResourceInventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!inventories.TryAdd(inventory.Owner, inventory))
                throw new InvalidOperationException("Inventory owner is already registered.");
        }

        public bool TryGet(InventoryOwner owner, out ResourceInventory inventory) => inventories.TryGetValue(owner, out inventory);
        public ResourceInventory Get(InventoryOwner owner) => inventories.TryGetValue(owner, out var inventory)
            ? inventory : throw new KeyNotFoundException("Inventory owner not found.");

        public bool Remove(InventoryOwner owner, ResourceInventory expected)
        {
            if (!inventories.TryGetValue(owner, out var current) || !ReferenceEquals(current, expected)) return false;
            return inventories.Remove(owner);
        }

        public ResourceInventoryState[] CaptureStates() => inventories.Values.Select(x => x.CaptureState())
            .OrderBy(x => x.Owner.Kind).ThenBy(x => x.Owner.Id.ToString(), StringComparer.Ordinal).ToArray();
    }

    public readonly struct WorldResourcePileState
    {
        public WorldResourcePileState(StableEntityId id, ResourceId resourceId, GridCoordinate coordinate)
        {
            if (!id.IsValid) throw new ArgumentException("Pile identity required.", nameof(id));
            if (!resourceId.IsValid) throw new ArgumentException("Resource ID required.", nameof(resourceId));
            Id = id;
            ResourceId = resourceId;
            Coordinate = coordinate;
        }

        public StableEntityId Id { get; }
        public ResourceId ResourceId { get; }
        public GridCoordinate Coordinate { get; }
    }

    // Quantity is owned only by the associated ResourceInventory, never by the pile entity or its view.
    public sealed class WorldResourcePile
    {
        private WorldResourcePile(WorldResourcePileState state) { State = state; }
        public WorldResourcePileState State { get; }
        public StableEntityId Id => State.Id;
        public ResourceId ResourceId => State.ResourceId;
        public GridCoordinate Coordinate => State.Coordinate;
        public InventoryOwner InventoryOwner => new InventoryOwner(InventoryOwnerKind.WorldPile, Id);

        public static WorldResourcePile CreateNew(ResourceId resourceId, GridCoordinate coordinate) =>
            Restore(new WorldResourcePileState(StableEntityId.NewId(), resourceId, coordinate));
        public static WorldResourcePile Restore(WorldResourcePileState state) => new WorldResourcePile(state);
    }
}
