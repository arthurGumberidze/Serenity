using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Simulation.Buildings;

namespace Game.Simulation.Resources
{
    public enum ConstructionFailureReason { None = 0, InvalidPlacement = 1, InsufficientResources = 2,
        FundingSourceMissing = 3, CommitFailed = 4 }

    public readonly struct ConstructionEvaluation
    {
        public ConstructionEvaluation(PlacementEvaluation placement, ConstructionFailureReason failure,
            ResourceId missingResource)
        {
            Placement = placement;
            Failure = failure;
            MissingResource = missingResource;
        }

        public PlacementEvaluation Placement { get; }
        public ConstructionFailureReason Failure { get; }
        public ResourceId MissingResource { get; }
        public bool IsValid => Placement.IsValid && Failure == ConstructionFailureReason.None;
    }

    public sealed class ConstructionFundingService
    {
        private readonly BuildingPlacementService placement;
        private readonly ResourceInventoryRegistry inventories;
        private readonly ResourceCatalog resources;
        private readonly StorageService storage;

        private readonly struct Deduction
        {
            public Deduction(ResourceInventory source, ResourceId id, ResourceQuantity quantity)
            { Source = source; Id = id; Quantity = quantity; }
            public ResourceInventory Source { get; }
            public ResourceId Id { get; }
            public ResourceQuantity Quantity { get; }
        }

        public ConstructionFundingService(BuildingPlacementService placement, ResourceInventoryRegistry inventories,
            ResourceCatalog resources, StorageService storage)
        {
            this.placement = placement ?? throw new ArgumentNullException(nameof(placement));
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public ConstructionEvaluation Evaluate(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation, IEnumerable<InventoryOwner> fundingSources)
        {
            var spatial = placement.Evaluate(definition, coordinate, orientation);
            if (!spatial.IsValid) return new ConstructionEvaluation(spatial, ConstructionFailureReason.InvalidPlacement, default);
            var funding = Plan(definition, fundingSources, out _, out var missing);
            return new ConstructionEvaluation(spatial, funding, missing);
        }

        public ConstructionEvaluation TryConfirm(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation, IEnumerable<InventoryOwner> fundingSources, out Building building,
            Action<Building> afterPlacement = null)
        {
            building = null;
            var result = Evaluate(definition, coordinate, orientation, fundingSources);
            if (!result.IsValid) return result;
            // Replan immediately before mutation. All writes below run on the one simulation owner thread.
            var funding = Plan(definition, fundingSources, out var deductions, out var missing);
            if (funding != ConstructionFailureReason.None)
                return new ConstructionEvaluation(result.Placement, funding, missing);
            Building candidate = null;
            ResourceInventory attachedStorage = null;
            var charged = new List<Deduction>();
            try
            {
                candidate = placement.Confirm(definition, coordinate, orientation);
                attachedStorage = storage.Attach(candidate, definition);
                foreach (var deduction in deductions)
                {
                    if (!deduction.Source.TryRemove(deduction.Id, deduction.Quantity))
                        throw new InvalidOperationException("Funding changed during construction transaction.");
                    charged.Add(deduction);
                }
                afterPlacement?.Invoke(candidate); // deterministic post-charge failure injection and presentation boundary
                candidate.MarkCompleted(); // U09 temporary instant-completion slice; worker delivery is future work.
                building = candidate;
                return result;
            }
            catch
            {
                for (var i = charged.Count - 1; i >= 0; i--)
                    if (!charged[i].Source.TryAdd(charged[i].Id, charged[i].Quantity))
                        throw new InvalidOperationException("Failed to restore construction funding.");
                if (attachedStorage != null) inventories.Remove(attachedStorage.Owner, attachedStorage);
                if (candidate != null) placement.Rollback(candidate, definition);
                return new ConstructionEvaluation(result.Placement, ConstructionFailureReason.CommitFailed, default);
            }
        }

        private ConstructionFailureReason Plan(BuildingDefinition definition, IEnumerable<InventoryOwner> fundingSources,
            out List<Deduction> deductions, out ResourceId missingResource)
        {
            deductions = new List<Deduction>();
            missingResource = default;
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var owners = (fundingSources ?? throw new ArgumentNullException(nameof(fundingSources))).Distinct().ToArray();
            var sources = new List<ResourceInventory>();
            foreach (var owner in owners)
            {
                if (!inventories.TryGet(owner, out var inventory)) return ConstructionFailureReason.FundingSourceMissing;
                sources.Add(inventory);
            }
            foreach (var cost in definition.ConstructionCost)
            {
                resources.Get(cost.ResourceId);
                var remaining = cost.Quantity.Units;
                foreach (var source in sources)
                {
                    var available = source.GetAmount(cost.ResourceId).Units;
                    var take = Math.Min(available, remaining);
                    if (take > 0) deductions.Add(new Deduction(source, cost.ResourceId, new ResourceQuantity(take)));
                    remaining -= take;
                    if (remaining == 0) break;
                }
                if (remaining != 0)
                {
                    missingResource = cost.ResourceId;
                    return ConstructionFailureReason.InsufficientResources;
                }
            }
            return ConstructionFailureReason.None;
        }
    }
}
