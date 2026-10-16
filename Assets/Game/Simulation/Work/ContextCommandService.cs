using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Simulation.AI;

namespace Game.Simulation.Work
{
    public readonly struct ContextCommandEntry
    {
        public ContextCommandEntry(StableEntityId characterId, ContextActionAvailability availability,
            WorkOrder order = null)
        {
            CharacterId = characterId;
            Availability = availability;
            Order = order;
        }

        public StableEntityId CharacterId { get; }
        public ContextActionAvailability Availability { get; }
        public WorkOrder Order { get; }
    }

    public sealed class ContextCommandResult
    {
        public ContextCommandResult(IEnumerable<ContextCommandEntry> entries)
        {
            Entries = (entries ?? throw new ArgumentNullException(nameof(entries))).ToArray();
        }

        public IReadOnlyList<ContextCommandEntry> Entries { get; }
        public IReadOnlyList<WorkOrder> Orders => Entries.Where(x => x.Order != null).Select(x => x.Order).ToArray();
    }

    public sealed class ContextCommandService
    {
        private readonly WorkManager work;
        private readonly Tier1AiScheduler scheduler;

        public ContextCommandService(WorkManager workManager, Tier1AiScheduler aiScheduler)
        {
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            scheduler = aiScheduler ?? throw new ArgumentNullException(nameof(aiScheduler));
        }

        public ContextCommandResult Issue(IEnumerable<StableEntityId> characterIds, ContextActionKind action,
            ContextCommandTarget target, bool append,
            Func<StableEntityId, ContextActionFacts> factsProvider,
            InventoryOwner? haulSource = null, InventoryOwner? haulDestination = null,
            ResourceQuantity haulQuantity = default)
        {
            if (characterIds == null) throw new ArgumentNullException(nameof(characterIds));
            if (factsProvider == null) throw new ArgumentNullException(nameof(factsProvider));
            var placement = append ? WorkQueuePlacement.Tail : WorkQueuePlacement.Front;
            var priority = append ? WorkPriority.High : WorkPriority.Immediate;
            var entries = new List<ContextCommandEntry>();
            foreach (var id in characterIds.Where(x => x.IsValid).Distinct()
                         .OrderBy(x => x.ToString(), StringComparer.Ordinal))
            {
                var availability = ContextActionRules.Evaluate(action, target, factsProvider(id));
                if (!availability.Enabled)
                {
                    entries.Add(new ContextCommandEntry(id, availability));
                    continue;
                }

                if (!append) scheduler.TryInterruptForDirectWork(id);
                var order = CreateOrder(id, action, target, priority, placement,
                    haulSource, haulDestination, haulQuantity);
                entries.Add(new ContextCommandEntry(id, availability, order));
            }
            return new ContextCommandResult(entries);
        }

        private WorkOrder CreateOrder(StableEntityId characterId, ContextActionKind action,
            ContextCommandTarget target, WorkPriority priority, WorkQueuePlacement placement,
            InventoryOwner? haulSource, InventoryOwner? haulDestination, ResourceQuantity haulQuantity)
        {
            if (action == ContextActionKind.Move)
                return work.CreateMoveJob(characterId, target.Position, priority,
                    queuePlacement: placement);
            if (action == ContextActionKind.Haul)
            {
                if (!haulSource.HasValue || !haulDestination.HasValue || !target.ResourceId.HasValue ||
                    !haulQuantity.IsPositive)
                    throw new InvalidOperationException("Haul context requires canonical source, destination and quantity.");
                return work.CreateHaulJob(characterId, haulSource.Value, haulDestination.Value,
                    target.ResourceId.Value, haulQuantity, priority, sourcePosition: target.Position,
                    queuePlacement: placement);
            }
            return work.CreateDirectJob(JobType(action), characterId, ToWorkTarget(target), priority,
                exclusiveTarget: false, queuePlacement: placement);
        }

        private static WorkJobType JobType(ContextActionKind action)
        {
            switch (action)
            {
                case ContextActionKind.Inspect: return WorkJobType.Inspect;
                case ContextActionKind.Gather: return WorkJobType.Gather;
                case ContextActionKind.Mine: return WorkJobType.Mine;
                case ContextActionKind.Chop: return WorkJobType.Chop;
                case ContextActionKind.Build: return WorkJobType.Build;
                case ContextActionKind.Guard: return WorkJobType.Guard;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static WorkTarget ToWorkTarget(ContextCommandTarget target)
        {
            switch (target.Kind)
            {
                case ContextTargetKind.Character:
                    return WorkTarget.ForEntity(WorkTargetKind.Character, target.EntityId, target.Position);
                case ContextTargetKind.ResourcePile:
                    return WorkTarget.ForEntity(WorkTargetKind.WorldPile, target.EntityId, target.Position);
                case ContextTargetKind.ResourceSource:
                    return WorkTarget.ForEntity(WorkTargetKind.ProductionSite, target.EntityId, target.Position);
                case ContextTargetKind.Building:
                    return WorkTarget.ForEntity(WorkTargetKind.Building, target.EntityId, target.Position);
                case ContextTargetKind.Area:
                case ContextTargetKind.Ground:
                    return WorkTarget.At(target.Position);
                default:
                    throw new ArgumentOutOfRangeException(nameof(target));
            }
        }
    }
}
