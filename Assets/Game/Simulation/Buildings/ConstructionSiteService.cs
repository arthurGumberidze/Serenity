using System;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Simulation.Resources;

namespace Game.Simulation.Buildings
{
    /// <summary>Owns construction-buffer transactions; delivered stock remains in the shared inventory registry.</summary>
    public sealed class ConstructionSiteService
    {
        private readonly ResourceCatalog resources;
        private readonly ResourceInventoryRegistry inventories;
        private readonly ResourceTransferService transfers;

        public ConstructionSiteService(ResourceCatalog resourceCatalog, ResourceInventoryRegistry inventoryRegistry)
        {
            resources = resourceCatalog ?? throw new ArgumentNullException(nameof(resourceCatalog));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            transfers = new ResourceTransferService(inventories);
        }

        public ResourceInventory Initialize(Building building, BuildingDefinition definition, SettlementId settlementId)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (inventories.TryGet(building.ConstructionBufferOwner, out _))
                throw new InvalidOperationException("Construction buffer is already registered.");
            building.InitializeConstruction(definition, settlementId);
            var capacity = checked(definition.Construction.Cost.Sum(x => x.Quantity.Units));
            // A construction contract may legitimately contain hides, food or other authored
            // categories. Restrict the buffer to the exact categories used by this building,
            // rather than silently rejecting every non-"Construction" material.
            var acceptedCategories = definition.Construction.Cost
                .Select(x => resources.Get(x.ResourceId).Category)
                .Distinct()
                .ToArray();
            var buffer = new ResourceInventory(building.ConstructionBufferOwner, resources, capacity,
                acceptedCategories);
            inventories.Add(buffer);
            RefreshReadiness(building);
            return buffer;
        }

        public TransferFailureReason Deliver(Building building, InventoryOwner source, ResourceId resourceId,
            ResourceQuantity quantity)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var contract = building.ConstructionMaterials.SingleOrDefault(x => x.ResourceId == resourceId);
            if (contract.RemainingUnits <= 0 ||
                !inventories.TryGet(building.ConstructionBufferOwner, out var buffer) ||
                quantity.Units > contract.RemainingUnits - buffer.GetAmount(resourceId).Units)
                return TransferFailureReason.DestinationRejected;
            var result = transfers.TransferExact(source, building.ConstructionBufferOwner, resourceId, quantity);
            if (result == TransferFailureReason.None) RefreshReadiness(building);
            return result;
        }

        public bool CommitReadyStage(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (!inventories.TryGet(building.ConstructionBufferOwner, out var buffer))
                throw new InvalidOperationException("Construction buffer is missing.");
            var cost = building.NextStageMaterialCost();
            if (cost.Any(x => !buffer.CanRemove(x.ResourceId, x.Quantity)))
            {
                building.SetNextStageReady(false);
                return false;
            }
            foreach (var amount in cost) buffer.TryRemove(amount.ResourceId, amount.Quantity);
            building.InstallNextStageMaterials(cost);
            RefreshReadiness(building);
            return true;
        }

        public void NotifyDelivery(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            RefreshReadiness(building);
            while (building.ConstructionPhase == ConstructionPhase.ReadyForWork && CommitReadyStage(building)) { }
        }

        public void Cancel(Building building, WorldPileService piles)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (piles == null) throw new ArgumentNullException(nameof(piles));
            if (building.IsOperational) throw new InvalidOperationException("Completed buildings cannot be cancelled.");
            if (inventories.TryGet(building.ConstructionBufferOwner, out var buffer))
            {
                foreach (var amount in buffer.CaptureState().Amounts)
                {
                    if (!amount.Quantity.IsPositive) continue;
                    piles.Create(amount.ResourceId, amount.Quantity, building.Coordinate);
                    if (!buffer.TryRemove(amount.ResourceId, amount.Quantity))
                        throw new InvalidOperationException("Construction buffer changed during cancellation.");
                }
                inventories.Remove(buffer.Owner, buffer);
            }
            foreach (var material in building.ConstructionMaterials.Where(x => x.InstalledUnits > 0))
                piles.Create(material.ResourceId, new ResourceQuantity(material.InstalledUnits), building.Coordinate);
            building.CancelConstruction();
        }

        public void RebindRestored(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (building.ConstructionPhase == ConstructionPhase.LegacyPlanned ||
                building.ConstructionPhase == ConstructionPhase.Completed ||
                building.ConstructionPhase == ConstructionPhase.Cancelled) return;
            if (!inventories.TryGet(building.ConstructionBufferOwner, out var buffer))
                throw new InvalidOperationException("Restored construction site is missing its material buffer.");
            var requiredCapacity = checked(building.ConstructionMaterials.Sum(x => x.RequiredUnits));
            if (buffer.Capacity < requiredCapacity)
                throw new InvalidOperationException("Restored construction buffer is smaller than its material contract.");
            RefreshReadiness(building);
        }

        private void RefreshReadiness(Building building)
        {
            if (building.ConstructionPhase == ConstructionPhase.Building ||
                building.ConstructionPhase == ConstructionPhase.Completed ||
                building.ConstructionPhase == ConstructionPhase.Cancelled) return;
            if (!inventories.TryGet(building.ConstructionBufferOwner, out var buffer))
                throw new InvalidOperationException("Construction buffer is missing.");
            building.SetNextStageReady(building.NextStageMaterialCost()
                .All(x => buffer.CanRemove(x.ResourceId, x.Quantity)));
        }
    }
}
