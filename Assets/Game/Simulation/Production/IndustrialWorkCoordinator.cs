using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.Work;

namespace Game.Simulation.Production
{
    /// <summary>Dispatches ordinary saved work orders. The quarry service and inventories own all progress/stock.</summary>
    public sealed class IndustrialWorkCoordinator
    {
        private readonly IndustrialExtractionService industry;
        private readonly ResourceInventoryRegistry inventories;
        private readonly WorkManager work;
        private readonly Func<IEnumerable<StableEntityId>> residents;
        private readonly Func<StableEntityId, bool> available;
        private readonly Func<StableEntityId, WorldPosition> position;
        private readonly Func<StableEntityId, bool> storageAvailable;
        private long nextDispatchTick;
        private long lastTick = -1;

        public IndustrialWorkCoordinator(IndustrialExtractionService industry, ResourceInventoryRegistry inventories,
            WorkManager work, Func<IEnumerable<StableEntityId>> residents, Func<StableEntityId, bool> available,
            Func<StableEntityId, WorldPosition> position, Func<StableEntityId, bool> storageAvailable)
        {
            this.industry = industry ?? throw new ArgumentNullException(nameof(industry));
            this.inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
            this.work = work ?? throw new ArgumentNullException(nameof(work));
            this.residents = residents ?? throw new ArgumentNullException(nameof(residents));
            this.available = available ?? throw new ArgumentNullException(nameof(available));
            this.position = position ?? throw new ArgumentNullException(nameof(position));
            this.storageAvailable = storageAvailable ?? throw new ArgumentNullException(nameof(storageAvailable));
        }

        public void Advance(long calendarTick)
        {
            if (calendarTick < lastTick) nextDispatchTick = calendarTick;
            lastTick = calendarTick;
            if (calendarTick < nextDispatchTick) return;
            nextDispatchTick = checked(calendarTick + TimeSpan.TicksPerMinute);
            var roster = residents().Distinct().ToArray();
            foreach (var zone in industry.Zones.Where(value => value.FacilityBuilt))
            {
                if (!storageAvailable(zone.StorageId)) continue;
                var source = new InventoryOwner(InventoryOwnerKind.WorldPile, zone.Id);
                var destination = new InventoryOwner(InventoryOwnerKind.BuildingStorage, zone.StorageId);
                if (!inventories.TryGet(source, out var output) || !inventories.TryGet(destination, out var storage)) continue;
                var definition = industry.GetDefinition(zone.DefinitionId);
                var open = work.OpenJobs.Where(value => value.Target.EntityId == zone.Id).ToArray();
                var candidates = roster.Where(id => available(id) && !work.HasOpenJob(id))
                    .OrderBy(id => position(id).DistanceSquared(zone.Position)).ThenBy(id => id).ToList();
                // Reserve one resident for other settlement work; never create imaginary workers.
                var slots = Math.Max(0, Math.Min(definition.WorkerCapacity, roster.Length - 1) - open.Length);
                foreach (var amount in definition.OutputsPerCycle)
                {
                    if (slots == 0 || candidates.Count == 0) break;
                    if (open.Any(value => value.Type == WorkJobType.Haul && value.ResourceId == amount.ResourceId)) continue;
                    var carrier = candidates.FirstOrDefault(id => inventories.TryGet(inventories.WorkerOwner(id),
                        out var carried) && carried.Capacity > carried.TotalUnits);
                    if (!carrier.IsValid) continue;
                    var carrierInventory = inventories.Get(inventories.WorkerOwner(carrier));
                    var units = Math.Min(Math.Min(8L, carrierInventory.Capacity - carrierInventory.TotalUnits),
                        Math.Min(output.GetAmount(amount.ResourceId).Units, storage.Capacity - storage.TotalUnits));
                    if (units <= 0 || !storage.CanAdd(amount.ResourceId, new ResourceQuantity(units))) continue;
                    work.CreateHaulJob(carrier, source, destination, amount.ResourceId,
                        new ResourceQuantity(units), WorkPriority.Normal, sourcePosition: zone.Position);
                    candidates.Remove(carrier); slots--;
                }
                // Backpressure: stop extraction until the designated warehouse can accept another batch.
                if (zone.Remaining == 0 || definition.OutputsPerCycle.Any(amount =>
                        !storage.CanAdd(amount.ResourceId, amount.Quantity) ||
                        output.GetAmount(amount.ResourceId).Units >= definition.WorkerCapacity * 8L)) continue;
                foreach (var id in candidates.Take(slots))
                    work.CreateDirectJob(WorkJobType.Mine, id,
                        WorkTarget.ForEntity(WorkTargetKind.WorldPile, zone.Id, zone.Position),
                        WorkPriority.Normal, exclusiveTarget: false);
            }
        }
    }
}
