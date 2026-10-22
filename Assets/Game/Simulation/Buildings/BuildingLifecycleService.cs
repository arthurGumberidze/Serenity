using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Simulation.Production;
using Game.Simulation.Resources;

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
        private long lastWearTick;

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
            var changes = 0;
            foreach (var building in buildings.All.Where(x => x.IsOperational).OrderBy(x => x.Id).ToArray())
            {
                var spec = definitions(building.DefinitionId).Maintenance;
                if (spec == null || spec.WearIntervalTicks <= 0 || spec.WearPerInterval <= 0) continue;
                var first = checked((lastWearTick / spec.WearIntervalTicks + 1L) * spec.WearIntervalTicks);
                for (var tick = first; tick <= targetCalendarTick && !building.IsDestroyed;
                     tick = checked(tick + spec.WearIntervalTicks))
                {
                    var eventId = OperationId(building.Id, "wear", tick);
                    var result = damage.ApplyDamage(building.Id, eventId, spec.WearPerInterval,
                        BuildingDamageType.Generic, tick);
                    if (result.Succeeded && result.Applied > 0) changes++;
                }
            }
            lastWearTick = targetCalendarTick;
            return changes;
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
            foreach (var amount in definition.Construction.Cost)
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
