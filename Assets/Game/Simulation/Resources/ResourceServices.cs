using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;

namespace Game.Simulation.Resources
{
    public enum TransferFailureReason { None = 0, MissingSource = 1, MissingDestination = 2, SelfTransfer = 3,
        InsufficientSource = 4, DestinationRejected = 5, InvalidQuantity = 6 }

    public sealed class ResourceTransferService
    {
        private readonly ResourceInventoryRegistry inventories;

        public ResourceTransferService(ResourceInventoryRegistry inventories) =>
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));

        public TransferFailureReason TransferExact(InventoryOwner sourceOwner, InventoryOwner destinationOwner,
            ResourceId resourceId, ResourceQuantity quantity)
        {
            if (!quantity.IsPositive) return TransferFailureReason.InvalidQuantity;
            if (sourceOwner == destinationOwner) return TransferFailureReason.SelfTransfer;
            if (!inventories.TryGet(sourceOwner, out var source)) return TransferFailureReason.MissingSource;
            if (!inventories.TryGet(destinationOwner, out var destination)) return TransferFailureReason.MissingDestination;
            if (!source.CanRemove(resourceId, quantity)) return TransferFailureReason.InsufficientSource;
            if (!destination.CanAdd(resourceId, quantity)) return TransferFailureReason.DestinationRejected;
            // Both preconditions are checked before either owner changes. These pure inventories have no callbacks.
            source.TryRemove(resourceId, quantity);
            if (destination.TryAdd(resourceId, quantity)) return TransferFailureReason.None;
            source.TryAdd(resourceId, quantity);
            throw new InvalidOperationException("Transfer destination changed during an owner-thread transaction.");
        }
    }

    public sealed class StorageService
    {
        private readonly ResourceInventoryRegistry inventories;
        private readonly ResourceCatalog resources;

        public StorageService(ResourceInventoryRegistry inventories, ResourceCatalog resources)
        {
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        }

        public ResourceInventory Attach(Building building, BuildingDefinition definition)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (definition == null || building.DefinitionId != definition.Id) throw new ArgumentException("Definition mismatch.", nameof(definition));
            if (definition.StorageCapacity == 0) return null;
            var inventory = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id),
                resources, definition.StorageCapacity, definition.StorageCategories);
            inventories.Add(inventory);
            return inventory;
        }
    }

    // A read model. It never stores a second mutable resource balance.
    public sealed class SettlementResourceView
    {
        private readonly ResourceInventoryRegistry inventories;
        public SettlementResourceView(ResourceInventoryRegistry inventories) =>
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));

        public ResourceQuantity GetStoredTotal(ResourceId resourceId)
        {
            long sum = 0;
            foreach (var inventory in inventories.All)
                if (inventory.Owner.Kind == InventoryOwnerKind.BuildingStorage)
                    sum = checked(sum + inventory.GetAmount(resourceId).Units);
            return new ResourceQuantity(sum);
        }
    }

    public sealed class WorldPileService
    {
        private readonly ResourceCatalog catalog;
        private readonly ResourceInventoryRegistry inventories;
        private readonly Dictionary<StableEntityId, WorldResourcePile> piles = new Dictionary<StableEntityId, WorldResourcePile>();

        public WorldPileService(ResourceCatalog catalog, ResourceInventoryRegistry inventories)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
        }

        public WorldResourcePile Create(ResourceId resourceId, ResourceQuantity initialQuantity, GridCoordinate coordinate)
            => Create(resourceId, initialQuantity, coordinate, null);

        public WorldResourcePile Create(ResourceId resourceId, ResourceQuantity initialQuantity,
            GridCoordinate coordinate, StableEntityId? stableId)
        {
            catalog.Get(resourceId);
            if (!initialQuantity.IsPositive) throw new ArgumentOutOfRangeException(nameof(initialQuantity));
            var pile = stableId.HasValue
                ? WorldResourcePile.Restore(new WorldResourcePileState(stableId.Value, resourceId, coordinate))
                : WorldResourcePile.CreateNew(resourceId, coordinate);
            var inventory = new ResourceInventory(pile.InventoryOwner, catalog, long.MaxValue);
            if (!inventory.TryAdd(resourceId, initialQuantity)) throw new InvalidOperationException("Unable to initialize pile.");
            inventories.Add(inventory);
            piles.Add(pile.Id, pile);
            return pile;
        }

        public WorldResourcePile RegisterExistingInventory(ResourceId resourceId, GridCoordinate coordinate,
            StableEntityId stableId)
        {
            catalog.Get(resourceId);
            var pile = WorldResourcePile.CreateWithId(stableId, resourceId, coordinate);
            if (!inventories.TryGet(pile.InventoryOwner, out _) || !piles.TryAdd(stableId, pile))
                throw new InvalidOperationException("Industrial pile requires one existing canonical inventory and unique identity.");
            return pile;
        }

        public IReadOnlyList<WorldResourcePile> SpillStorage(StableEntityId buildingId, GridCoordinate coordinate)
        {
            if (!buildingId.IsValid) throw new ArgumentException("Building identity required.", nameof(buildingId));
            var sourceOwner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, buildingId);
            if (!inventories.TryGet(sourceOwner, out var source) || source.TotalUnits == 0)
                return Array.Empty<WorldResourcePile>();
            var amounts = source.CaptureState().Amounts.OrderBy(x => x.ResourceId).ToArray();
            var staged = new List<(WorldResourcePile pile, ResourceInventory inventory, ResourceAmount amount)>();
            foreach (var amount in amounts)
            {
                var pileId = SpillPileId(buildingId, amount.ResourceId);
                if (piles.ContainsKey(pileId) || inventories.TryGet(new InventoryOwner(InventoryOwnerKind.WorldPile, pileId), out _))
                    throw new InvalidOperationException("A deterministic spill pile already exists before destruction commit.");
                var pile = WorldResourcePile.CreateWithId(pileId, amount.ResourceId, coordinate);
                var inventory = new ResourceInventory(pile.InventoryOwner, catalog, long.MaxValue);
                if (!inventory.TryAdd(amount.ResourceId, amount.Quantity))
                    throw new InvalidOperationException("Unable to stage a storage spill.");
                staged.Add((pile, inventory, amount));
            }
            var committed = new List<(WorldResourcePile pile, ResourceInventory inventory)>();
            try
            {
                foreach (var item in staged)
                {
                    inventories.Add(item.inventory);
                    piles.Add(item.pile.Id, item.pile);
                    committed.Add((item.pile, item.inventory));
                }
                foreach (var item in staged)
                    if (!source.TryRemove(item.amount.ResourceId, item.amount.Quantity))
                        throw new InvalidOperationException("Storage changed during the destruction transaction.");
                return staged.Select(x => x.pile).ToArray();
            }
            catch
            {
                foreach (var item in committed)
                {
                    piles.Remove(item.pile.Id);
                    inventories.Remove(item.inventory.Owner, item.inventory);
                }
                foreach (var item in staged)
                    if (!source.CanRemove(item.amount.ResourceId, item.amount.Quantity))
                        source.TryAdd(item.amount.ResourceId, item.amount.Quantity);
                throw;
            }
        }

        public WorldResourcePileState[] CaptureStates() => piles.Values.Select(x => x.State)
            .OrderBy(x => x.Id).ToArray();

        public void RestoreStates(IEnumerable<WorldResourcePileState> states)
        {
            var replacement = new Dictionary<StableEntityId, WorldResourcePile>();
            foreach (var state in states ?? throw new ArgumentNullException(nameof(states)))
            {
                catalog.Get(state.ResourceId);
                var pile = WorldResourcePile.Restore(state);
                if (!inventories.TryGet(pile.InventoryOwner, out var inventory) ||
                    inventory.CaptureState().Amounts.Any(x => x.Quantity.Units > 0 && x.ResourceId != state.ResourceId) ||
                    !replacement.TryAdd(pile.Id, pile))
                    throw new ArgumentException("World-pile state has no matching canonical inventory.", nameof(states));
            }
            piles.Clear();
            foreach (var pair in replacement.OrderBy(x => x.Key)) piles.Add(pair.Key, pair.Value);
        }

        public bool TryGet(StableEntityId id, out WorldResourcePile pile) => piles.TryGetValue(id, out pile);
        public IEnumerable<WorldResourcePile> All => piles.Values;
        // Empty piles remain as stable entities until a future explicit cleanup/destruction rule owns their lifecycle.

        private static StableEntityId SpillPileId(StableEntityId buildingId, ResourceId resourceId)
        {
            var first = 14695981039346656037UL;
            foreach (var value in buildingId.ToGuid().ToByteArray()) { first ^= value; first *= 1099511628211UL; }
            var second = 14695981039346656037UL;
            foreach (var value in resourceId.ToString()) { second ^= value; second *= 1099511628211UL; }
            second ^= 0x5532345350494c4cUL;
            return StableEntityId.FromDeterministicHash(first, second);
        }
    }
}
