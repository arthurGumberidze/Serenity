using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Domain.AI;
using Game.Domain.Work;
using Game.Simulation.Work;

namespace Game.Simulation.Buildings
{
    public enum BuildingLifecycleFailure
    {
        None, MissingBuilding, InactiveBuilding, Unsupported, NoDamage, InsufficientResources,
        InsufficientWork, DuplicateOperation, InvalidUpgrade
    }

    public readonly struct BuildingLifecycleResult
    {
        public BuildingLifecycleResult(BuildingLifecycleFailure failure, int integrityChanged = 0)
        { Failure = failure; IntegrityChanged = integrityChanged; }
        public BuildingLifecycleFailure Failure { get; }
        public int IntegrityChanged { get; }
        public bool Succeeded => Failure == BuildingLifecycleFailure.None;
    }

    /// <summary>Executes data-authored wear, repair, upgrade and demolition against existing authorities.</summary>
    public sealed class BuildingLifecycleService
    {
        private readonly BuildingRegistry buildings;
        private readonly Func<BuildingDefinitionId, BuildingDefinition> definitions;
        private readonly BuildingDamageService damage;
        private readonly ResourceInventoryRegistry inventories;
        private readonly WorldPileService piles;
        private readonly BuildingOccupancyGrid occupancy;
        private readonly ProductionService production;
        private WorkManager work;
        private Func<Building, WorldPosition> approachPosition;
        public Func<BuildingDefinition, bool> UpgradeTechnologyAvailable { get; set; }
        public Func<StableEntityId, Building, bool> UpgradeWorkerAllowed { get; set; }

        public WorkOrder UpgradeJob(StableEntityId id) => work?.Jobs.FirstOrDefault(x =>
            x.Type == WorkJobType.Upgrade && x.Target.EntityId == id && !x.IsTerminal);

        public BuildingUpgradeOption UpgradeOption(Building building) =>
            definitions(building.DefinitionId).Upgrades?.Options.SingleOrDefault();

        public BuildingLifecycleResult RequestUpgrade(StableEntityId id)
        {
            if (!buildings.TryGet(id, out var building)) return Fail(BuildingLifecycleFailure.MissingBuilding);
            if (!building.IsOperational) return Fail(BuildingLifecycleFailure.InactiveBuilding);
            if (work == null || approachPosition == null) return Fail(BuildingLifecycleFailure.Unsupported);
            if (UpgradeJob(id) != null || DemolitionJob(id) != null) return Fail(BuildingLifecycleFailure.DuplicateOperation);
            var option = UpgradeOption(building);
            if (option == null) return Fail(BuildingLifecycleFailure.InvalidUpgrade);
            var target = definitions(option.TargetDefinitionId);
            if (UpgradeTechnologyAvailable != null && !UpgradeTechnologyAvailable(target))
                return Fail(BuildingLifecycleFailure.Unsupported);
            if (!TryPlan(option.Cost, UpgradeFunding(building), out _)) return Fail(BuildingLifecycleFailure.InsufficientResources);
            work.CreateDirectJob(WorkJobType.Upgrade, null,
                WorkTarget.ForEntity(WorkTargetKind.Building,id,approachPosition(building)),WorkPriority.High);
            BuildingChanged?.Invoke(building);
            return new BuildingLifecycleResult(BuildingLifecycleFailure.None);
        }

        private IEnumerable<InventoryOwner> UpgradeFunding(Building building) => inventories.All
            .Where(x => x.Owner.Kind == InventoryOwnerKind.WorldPile ||
                x.Owner.Kind == InventoryOwnerKind.BuildingStorage && buildings.TryGet(x.Owner.Id,out var owner) &&
                owner.IsOperational && owner.OwnerSideId == building.OwnerSideId)
            .OrderBy(x=>x.Owner.Id).Select(x=>x.Owner);

        public bool AdvanceUpgrade(WorkOrder job, TimeSpan delta, long calendarTick)
        {
            if (job == null || job.Type != WorkJobType.Upgrade || job.Status != WorkJobStatus.Active ||
                !job.AssigneeId.HasValue || UpgradeJob(job.Target.EntityId) != job) return false;
            if (!buildings.TryGet(job.Target.EntityId,out var building) || !building.IsOperational)
            { work.Fail(job.Id); return false; }
            if(UpgradeWorkerAllowed!=null&&!UpgradeWorkerAllowed(job.AssigneeId.Value,building))
            {work.Fail(job.Id);return false;}
            var option=UpgradeOption(building);
            if(option==null){work.Fail(job.Id);return false;}
            if(UpgradeTechnologyAvailable!=null && !UpgradeTechnologyAvailable(definitions(option.TargetDefinitionId)))return false;
            // Costs are consumed atomically at completion, so cancelling never loses resources.
            // If another job spent the stock, this work waits without inventing materials.
            if(!TryPlan(option.Cost,UpgradeFunding(building),out _))return false;
            job.AdvanceProgress(delta);
            if(job.ProgressTicks<option.WorkRequiredTicks)return false;
            return Upgrade(building.Id,option.TargetDefinitionId,StableEntityId.FromDeterministicHash(0x55504752414445UL,(ulong)job.Id.Value),
                TimeSpan.FromTicks(job.ProgressTicks),calendarTick,UpgradeFunding(building)).Succeeded;
        }

        public void ConfigureWork(WorkManager manager, Func<Building, WorldPosition> approach)
        { work = manager ?? throw new ArgumentNullException(nameof(manager)); approachPosition = approach; }

        public WorkOrder DemolitionJob(StableEntityId id) => work?.Jobs.FirstOrDefault(x =>
            x.Type == WorkJobType.Demolish && x.Target.EntityId == id && !x.IsTerminal);

        public long DemolitionRequiredTicks(Building building) =>
            Math.Max(TimeSpan.FromMinutes(10).Ticks, definitions(building.DefinitionId)
                .ForSegmentLength(building.PlacementCells.Count).Construction.WorkRequiredTicks / 2);

        public bool AdvanceDemolition(WorkOrder job, TimeSpan delta, long calendarTick)
        {
            if (job == null || job.Type != WorkJobType.Demolish || job.Status != WorkJobStatus.Active ||
                !job.AssigneeId.HasValue || DemolitionJob(job.Target.EntityId) != job) return false;
            if (!buildings.TryGet(job.Target.EntityId, out var building) || !building.IsOperational)
            { work.Fail(job.Id); return false; }
            job.AdvanceProgress(delta);
            if (job.ProgressTicks < DemolitionRequiredTicks(building)) return false;
            return CompleteDemolition(building.Id, calendarTick).Succeeded;
        }

        private readonly struct Deduction
        {
            public Deduction(ResourceInventory inventory, ResourceAmount amount)
            { Inventory = inventory; Amount = amount; }
            public ResourceInventory Inventory { get; }
            public ResourceAmount Amount { get; }
        }

        public BuildingLifecycleService(BuildingRegistry buildingRegistry,
            Func<BuildingDefinitionId, BuildingDefinition> definitionResolver,
            BuildingDamageService damageService, ResourceInventoryRegistry inventoryRegistry,
            WorldPileService pileService, BuildingOccupancyGrid occupancyGrid,
            ProductionService productionService = null)
        {
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            definitions = definitionResolver ?? throw new ArgumentNullException(nameof(definitionResolver));
            damage = damageService ?? throw new ArgumentNullException(nameof(damageService));
            inventories = inventoryRegistry ?? throw new ArgumentNullException(nameof(inventoryRegistry));
            piles = pileService ?? throw new ArgumentNullException(nameof(pileService));
            occupancy = occupancyGrid ?? throw new ArgumentNullException(nameof(occupancyGrid));
            production = productionService;
        }

        public event Action<Building> BuildingChanged;
        public event Action<StableEntityId> BuildingDemolished;

        public int AdvanceWearTo(long targetCalendarTick)
        {
            if (targetCalendarTick < 0) throw new ArgumentOutOfRangeException(nameof(targetCalendarTick));
            // Calendar age never damages structures, including definitions from older saves.
            return 0;
        }

        public BuildingLifecycleResult Repair(StableEntityId buildingId, StableEntityId operationId,
            TimeSpan work, long calendarTick, IEnumerable<InventoryOwner> fundingSources)
        {
            if (!buildings.TryGet(buildingId, out var building)) return Fail(BuildingLifecycleFailure.MissingBuilding);
            if (!building.IsOperational) return Fail(BuildingLifecycleFailure.InactiveBuilding);
            if (building.HasProcessedDamage(operationId)) return Fail(BuildingLifecycleFailure.DuplicateOperation);
            if (building.CurrentIntegrity >= building.MaxIntegrity) return Fail(BuildingLifecycleFailure.NoDamage);
            var spec = definitions(building.DefinitionId).Maintenance;
            if (spec == null) return Fail(BuildingLifecycleFailure.Unsupported);
            if (work.Ticks < spec.RepairWorkRequiredTicks) return Fail(BuildingLifecycleFailure.InsufficientWork);
            if (!TryPlan(spec.RepairCost, fundingSources, out var deductions))
                return Fail(BuildingLifecycleFailure.InsufficientResources);
            Commit(deductions);
            var repaired = building.Repair(operationId, building.MaxIntegrity - building.CurrentIntegrity, calendarTick);
            if (repaired <= 0) { Rollback(deductions); return Fail(BuildingLifecycleFailure.DuplicateOperation); }
            BuildingChanged?.Invoke(building);
            return new BuildingLifecycleResult(BuildingLifecycleFailure.None, repaired);
        }

        public BuildingLifecycleResult Upgrade(StableEntityId buildingId, BuildingDefinitionId targetId,
            StableEntityId operationId, TimeSpan work, long calendarTick,
            IEnumerable<InventoryOwner> fundingSources)
        {
            if (!buildings.TryGet(buildingId, out var building)) return Fail(BuildingLifecycleFailure.MissingBuilding);
            if (!building.IsOperational) return Fail(BuildingLifecycleFailure.InactiveBuilding);
            if (building.HasProcessedDamage(operationId)) return Fail(BuildingLifecycleFailure.DuplicateOperation);
            var source = definitions(building.DefinitionId);
            var option = source.Upgrades?.Options.SingleOrDefault(x => x.TargetDefinitionId == targetId);
            if (option == null) return Fail(BuildingLifecycleFailure.InvalidUpgrade);
            if (work.Ticks < option.WorkRequiredTicks) return Fail(BuildingLifecycleFailure.InsufficientWork);
            if (!TryPlan(option.Cost, fundingSources, out var deductions))
                return Fail(BuildingLifecycleFailure.InsufficientResources);
            var target = definitions(targetId);
            Commit(deductions);
            if (!building.UpgradeTo(operationId, target, calendarTick))
            { Rollback(deductions); return Fail(BuildingLifecycleFailure.DuplicateOperation); }
            BuildingChanged?.Invoke(building);
            return new BuildingLifecycleResult(BuildingLifecycleFailure.None);
        }

        public BuildingLifecycleResult Demolish(StableEntityId buildingId, long calendarTick)
        {
            if (!buildings.TryGet(buildingId, out var building)) return Fail(BuildingLifecycleFailure.MissingBuilding);
            if (!building.IsOperational) return Fail(BuildingLifecycleFailure.InactiveBuilding);
            if (definitions(building.DefinitionId).Maintenance?.CanDemolish != true || work == null || approachPosition == null)
                return Fail(BuildingLifecycleFailure.Unsupported);
            if (DemolitionJob(buildingId) != null || UpgradeJob(buildingId) != null) return Fail(BuildingLifecycleFailure.DuplicateOperation);
            work.CreateDirectJob(WorkJobType.Demolish, null,
                WorkTarget.ForEntity(WorkTargetKind.Building, buildingId, approachPosition(building)), WorkPriority.High);
            return new BuildingLifecycleResult(BuildingLifecycleFailure.None);
        }

        private BuildingLifecycleResult CompleteDemolition(StableEntityId buildingId, long calendarTick)
        {
            if (!buildings.TryGet(buildingId, out var building)) return Fail(BuildingLifecycleFailure.MissingBuilding);
            if (!building.IsOperational) return Fail(BuildingLifecycleFailure.InactiveBuilding);
            var definition = definitions(building.DefinitionId);
            if (definition.Maintenance == null || !definition.Maintenance.CanDemolish)
                return Fail(BuildingLifecycleFailure.Unsupported);
            production?.CancelTasksForSite(building.Id);
            var storageOwner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id);
            if (inventories.TryGet(storageOwner, out var storage))
            {
                piles.SpillStorage(building.Id, building.Coordinate);
                inventories.Remove(storageOwner, storage);
            }
            foreach (var amount in definition.ForSegmentLength(building.PlacementCells.Count).Construction.Cost)
            {
                var returned = amount.Quantity.Units * definition.Maintenance.DemolitionReturnPerThousand / 1000L;
                if (returned > 0) piles.Create(amount.ResourceId, new ResourceQuantity(returned), building.Coordinate);
            }
            occupancy.Release(building.PlacementCells.Count == 0
                ? definition.Footprint.EnumerateCells(building.Coordinate, building.Orientation)
                : building.PlacementCells, building.Id);
            if (!buildings.Remove(building)) throw new InvalidOperationException("Building changed during demolition.");
            BuildingDemolished?.Invoke(building.Id);
            return new BuildingLifecycleResult(BuildingLifecycleFailure.None);
        }

        private bool TryPlan(IReadOnlyList<ResourceAmount> costs, IEnumerable<InventoryOwner> owners,
            out List<Deduction> deductions)
        {
            deductions = new List<Deduction>();
            var sources = (owners ?? Array.Empty<InventoryOwner>()).Distinct()
                .Select(owner => inventories.TryGet(owner, out var inventory) ? inventory : null)
                .Where(x => x != null).ToArray();
            foreach (var cost in costs)
            {
                var remaining = cost.Quantity.Units;
                foreach (var source in sources)
                {
                    var take = Math.Min(remaining, source.GetAmount(cost.ResourceId).Units);
                    if (take > 0) deductions.Add(new Deduction(source,
                        new ResourceAmount(cost.ResourceId, new ResourceQuantity(take))));
                    remaining -= take;
                    if (remaining == 0) break;
                }
                if (remaining > 0) return false;
            }
            return true;
        }

        private static void Commit(IReadOnlyList<Deduction> deductions)
        {
            var committed = 0;
            try
            {
                for (; committed < deductions.Count; committed++)
                    if (!deductions[committed].Inventory.TryRemove(deductions[committed].Amount.ResourceId,
                            deductions[committed].Amount.Quantity))
                        throw new InvalidOperationException("Lifecycle funding changed during commit.");
            }
            catch
            {
                for (var i = committed - 1; i >= 0; i--)
                    deductions[i].Inventory.TryAdd(deductions[i].Amount.ResourceId, deductions[i].Amount.Quantity);
                throw;
            }
        }

        private static void Rollback(IReadOnlyList<Deduction> deductions)
        {
            for (var i = deductions.Count - 1; i >= 0; i--)
                if (!deductions[i].Inventory.TryAdd(deductions[i].Amount.ResourceId, deductions[i].Amount.Quantity))
                    throw new InvalidOperationException("Could not roll back lifecycle funding.");
        }

        private static BuildingLifecycleResult Fail(BuildingLifecycleFailure failure) => new BuildingLifecycleResult(failure);

        private static StableEntityId OperationId(StableEntityId buildingId, string stream, long tick)
        {
            var first = 14695981039346656037UL;
            foreach (var value in buildingId.ToGuid().ToByteArray()) { first ^= value; first *= 1099511628211UL; }
            foreach (var value in stream) { first ^= value; first *= 1099511628211UL; }
            first ^= unchecked((ulong)tick); first *= 1099511628211UL;
            var second = first ^ 0x9e3779b97f4a7c15UL;
            return StableEntityId.FromDeterministicHash(first, second);
        }
    }
}
