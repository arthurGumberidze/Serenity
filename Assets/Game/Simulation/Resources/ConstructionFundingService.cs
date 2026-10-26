using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Simulation.Buildings;
using Game.Domain.Research;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Simulation.Work;
using Game.Simulation.AI;

namespace Game.Simulation.Resources
{
    public enum ConstructionFailureReason { None = 0, InvalidPlacement = 1, InsufficientResources = 2,
        FundingSourceMissing = 3, CommitFailed = 4, LockedByResearch = 5 }

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
        private readonly IResearchUnlockView unlocks;
        private ConstructionSiteService sites;
        private WorkManager work;
        private WorldPileService piles;
        private SettlementId settlementId;
        private HaulClaimRegistry haulClaims;
        private const long DeliveryBatchUnits = 8;
        public bool RequiresWorker { get; set; }
        public TimeSpan MinimumBuildTime { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan BuildTimePerResource { get; set; } = TimeSpan.FromMinutes(2);

        private readonly struct Deduction
        {
            public Deduction(ResourceInventory source, ResourceId id, ResourceQuantity quantity)
            { Source = source; Id = id; Quantity = quantity; }
            public ResourceInventory Source { get; }
            public ResourceId Id { get; }
            public ResourceQuantity Quantity { get; }
        }

        public ConstructionFundingService(BuildingPlacementService placement, ResourceInventoryRegistry inventories,
            ResourceCatalog resources, StorageService storage, IResearchUnlockView researchUnlocks = null)
        {
            this.placement = placement ?? throw new ArgumentNullException(nameof(placement));
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            unlocks = researchUnlocks;
        }

        public bool IsBuildingUnlocked(BuildingDefinitionId definitionId) =>
            unlocks == null || unlocks.IsBuildingUnlocked(definitionId);

        public void ConfigureSettlement(SettlementId settlement)
        {
            if (!settlement.IsValid) throw new ArgumentException("Settlement identity is required.", nameof(settlement));
            settlementId = settlement;
        }

        public void ConfigureLogistics(ConstructionSiteService constructionSites, WorkManager workManager,
            WorldPileService worldPiles, SettlementId settlement, HaulClaimRegistry claims = null)
        {
            sites = constructionSites ?? throw new ArgumentNullException(nameof(constructionSites));
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            piles = worldPiles ?? throw new ArgumentNullException(nameof(worldPiles));
            if (!settlement.IsValid) throw new ArgumentException("Settlement identity is required.", nameof(settlement));
            settlementId = settlement;
            haulClaims = claims;
        }

        public ConstructionEvaluation Evaluate(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation, IEnumerable<InventoryOwner> fundingSources,
            IEnumerable<GridCoordinate> authoredCells = null)
        {
            var request = new BuildingPlacementRequest(definition, coordinate, orientation,
                settlementId.IsValid ? (SettlementId?)settlementId : null, authoredCells);
            var spatial = placement.Evaluate(request);
            if (!spatial.IsValid) return new ConstructionEvaluation(spatial, ConstructionFailureReason.InvalidPlacement, default);
            if (!IsBuildingUnlocked(definition.Id))
                return new ConstructionEvaluation(spatial, ConstructionFailureReason.LockedByResearch, default);
            var funding = Plan(definition.ForSegmentLength(request.AuthoredCells.Count), fundingSources, out _, out var missing);
            return new ConstructionEvaluation(spatial, funding, missing);
        }

        public ConstructionEvaluation TryConfirm(BuildingDefinition definition, GridCoordinate coordinate,
            BuildingOrientation orientation, IEnumerable<InventoryOwner> fundingSources, out Building building,
            Action<Building> afterPlacement = null, Game.Domain.Combat.CombatSideId ownerSideId = default,
            StableEntityId? stableId = null, IEnumerable<GridCoordinate> authoredCells = null,
            bool deferLogistics = false)
        {
            building = null;
            var result = Evaluate(definition, coordinate, orientation, fundingSources, authoredCells);
            if (!result.IsValid) return result;
            definition = definition.ForSegmentLength(authoredCells?.Count() ?? 0);
            // Replan immediately before mutation. All writes below run on the one simulation owner thread.
            var funding = Plan(definition, fundingSources, out var deductions, out var missing);
            if (funding != ConstructionFailureReason.None)
                return new ConstructionEvaluation(result.Placement, funding, missing);
            Building candidate = null;
            ResourceInventory attachedStorage = null;
            var charged = new List<Deduction>();
            var deliveryJobs = new List<JobId>();
            try
            {
                var request = new BuildingPlacementRequest(definition, coordinate, orientation,
                    settlementId.IsValid ? (SettlementId?)settlementId : null, authoredCells);
                candidate = placement.Confirm(request, ownerSideId, stableId);
                var physicalLogistics = sites != null && work != null && piles != null && settlementId.IsValid;
                if (physicalLogistics) sites.Initialize(candidate, definition, settlementId);
                if (RequiresWorker) candidate.RequireConstructionWork(TimeSpan.FromTicks(Math.Max(MinimumBuildTime.Ticks,
                    checked(definition.ConstructionCost.Sum(x => x.Quantity.Units) * BuildTimePerResource.Ticks))));
                attachedStorage = storage.Attach(candidate, definition);
                if (physicalLogistics)
                {
                    if (!deferLogistics)
                        CreateDeliveryJobs(candidate, deductions, WorkPriority.High, deliveryJobs);
                }
                else
                {
                    foreach (var deduction in deductions)
                    {
                        if (!deduction.Source.TryRemove(deduction.Id, deduction.Quantity))
                            throw new InvalidOperationException("Funding changed during construction transaction.");
                        charged.Add(deduction);
                    }
                }
                afterPlacement?.Invoke(candidate); // deterministic transaction/presentation boundary
                if (!physicalLogistics && !RequiresWorker) candidate.MarkCompleted(); // Historical simulations retain their contract.
                building = candidate;
                return result;
            }
            catch
            {
                foreach (var jobId in deliveryJobs) work?.Cancel(jobId);
                for (var i = charged.Count - 1; i >= 0; i--)
                    if (!charged[i].Source.TryAdd(charged[i].Id, charged[i].Quantity))
                        throw new InvalidOperationException("Failed to restore construction funding.");
                if (attachedStorage != null) inventories.Remove(attachedStorage.Owner, attachedStorage);
                if (candidate != null)
                {
                    if (inventories.TryGet(candidate.ConstructionBufferOwner, out var buffer))
                        inventories.Remove(buffer.Owner, buffer);
                    placement.Rollback(candidate, definition);
                }
                return new ConstructionEvaluation(result.Placement, ConstructionFailureReason.CommitFailed, default);
            }
        }

        public bool Cancel(Building building, BuildingDefinition definition)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (sites == null || work == null || piles == null || building.IsOperational) return false;
            foreach (var order in work.Jobs.Where(x => !x.IsTerminal &&
                ((x.Type == WorkJobType.Build && x.Target.EntityId == building.Id) ||
                 (x.Type == WorkJobType.Haul && x.Destination.HasValue &&
                  x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer &&
                  x.Destination.Value.EntityId == building.Id))).ToArray())
                work.Cancel(order.Id);
            sites.Cancel(building, piles);
            var storageOwner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            if (inventories.TryGet(storageOwner, out var attachedStorage))
                inventories.Remove(storageOwner, attachedStorage);
            placement.Rollback(building, definition);
            return true;
        }

        public bool TryScheduleDeliveries(Building building, BuildingDefinition definition,
            IEnumerable<InventoryOwner> fundingSources, WorkPriority priority)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (building.IsOperational || sites == null || work == null || piles == null || !settlementId.IsValid)
                return false;
            if (work.Jobs.Any(x => !x.IsTerminal && x.Type == WorkJobType.Haul && x.Destination.HasValue &&
                                   x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer &&
                                   x.Destination.Value.EntityId == building.Id)) return true;
            inventories.TryGet(building.ConstructionBufferOwner,out var buffer);
            var missing = building.ConstructionMaterials.Select(x=>(id:x.ResourceId,
                units:Math.Max(0,x.RemainingUnits-(buffer?.GetAmount(x.ResourceId).Units ?? 0))))
                .Where(x=>x.units>0).Select(x=>new ResourceAmount(x.id,new ResourceQuantity(x.units))).ToArray();
            var result = Plan(definition, fundingSources, out var deductions, out _, missing);
            if (result != ConstructionFailureReason.None) return false;
            CreateDeliveryJobs(building, deductions, priority, null);
            return true;
        }

        private void CreateDeliveryJobs(Building building, IEnumerable<Deduction> deductions,
            WorkPriority priority, ICollection<JobId> created)
        {
            foreach (var deduction in deductions)
            {
                haulClaims?.ReleaseReservedAtSource(deduction.Source.Owner, deduction.Id,
                    deduction.Quantity.Units);
                var remaining = deduction.Quantity.Units;
                while (remaining > 0)
                {
                    var quantity = new ResourceQuantity(Math.Min(DeliveryBatchUnits, remaining));
                    var order = work.CreateHaulJob(null, deduction.Source.Owner,
                        building.ConstructionBufferOwner, deduction.Id, quantity, priority);
                    created?.Add(order.Id);
                    remaining -= quantity.Units;
                }
            }
        }

        private ConstructionFailureReason Plan(BuildingDefinition definition, IEnumerable<InventoryOwner> fundingSources,
            out List<Deduction> deductions, out ResourceId missingResource, IReadOnlyList<ResourceAmount> remainingCost = null)
        {
            deductions = new List<Deduction>();
            missingResource = default;
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var owners = (fundingSources ?? throw new ArgumentNullException(nameof(fundingSources))).Distinct().ToArray();
            var sources = new List<ResourceInventory>();
            foreach (var owner in owners)
            {
                if (sites != null && owner.Kind != InventoryOwnerKind.WorldPile &&
                    owner.Kind != InventoryOwnerKind.BuildingStorage && owner.Kind != InventoryOwnerKind.ProductionOutput) continue;
                if (!inventories.TryGet(owner, out var inventory)) return ConstructionFailureReason.FundingSourceMissing;
                sources.Add(inventory);
            }
            foreach (var cost in remainingCost ?? definition.ConstructionCost)
            {
                resources.Get(cost.ResourceId);
                var remaining = cost.Quantity.Units;
                foreach (var source in sources)
                {
                    var reserved = work == null ? 0L : work.Jobs.Where(x => !x.IsTerminal && x.Type == WorkJobType.Haul &&
                        x.ResourceId == cost.ResourceId && x.Target.EntityId == source.Owner.Id &&
                        ((source.Owner.Kind == InventoryOwnerKind.WorldPile && x.Target.Kind == WorkTargetKind.WorldPile) ||
                         (source.Owner.Kind == InventoryOwnerKind.BuildingStorage &&
                          x.Target.Kind == WorkTargetKind.BuildingStorage)) && x.Destination.HasValue &&
                        x.Destination.Value.Kind == WorkTargetKind.ConstructionBuffer).Sum(x => x.Quantity.Units);
                    var available = Math.Max(0, source.GetAmount(cost.ResourceId).Units - reserved);
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
