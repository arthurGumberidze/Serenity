using System;
using System.Collections.Generic;
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
        {
            catalog.Get(resourceId);
            if (!initialQuantity.IsPositive) throw new ArgumentOutOfRangeException(nameof(initialQuantity));
            var pile = WorldResourcePile.CreateNew(resourceId, coordinate);
            var inventory = new ResourceInventory(pile.InventoryOwner, catalog, long.MaxValue);
            if (!inventory.TryAdd(resourceId, initialQuantity)) throw new InvalidOperationException("Unable to initialize pile.");
            inventories.Add(inventory);
            piles.Add(pile.Id, pile);
            return pile;
        }

        public bool TryGet(StableEntityId id, out WorldResourcePile pile) => piles.TryGetValue(id, out pile);
        public IEnumerable<WorldResourcePile> All => piles.Values;
        // Empty piles remain as stable entities until a future explicit cleanup/destruction rule owns their lifecycle.
    }
}
