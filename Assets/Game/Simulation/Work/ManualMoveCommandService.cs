using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Work;
using Game.Simulation.Tiers;

namespace Game.Simulation.Work
{
    public enum ManualMoveCommandStatus
    {
        Created = 0,
        TierUnavailable = 1,
        AlreadyHasWork = 2,
        Rejected = 3
    }

    public readonly struct ManualMoveCommandEntry
    {
        public ManualMoveCommandEntry(StableEntityId characterId, ManualMoveCommandStatus status,
            WorldPosition target, WorkOrder job = null)
        {
            CharacterId = characterId;
            Status = status;
            Target = target;
            Job = job;
        }

        public StableEntityId CharacterId { get; }
        public ManualMoveCommandStatus Status { get; }
        public WorldPosition Target { get; }
        public WorkOrder Job { get; }
    }

    public sealed class ManualMoveCommandResult
    {
        public ManualMoveCommandResult(IEnumerable<ManualMoveCommandEntry> entries)
        {
            Entries = (entries ?? throw new ArgumentNullException(nameof(entries))).ToArray();
            Jobs = Entries.Where(x => x.Job != null).Select(x => x.Job).ToArray();
        }

        public IReadOnlyList<ManualMoveCommandEntry> Entries { get; }
        public IReadOnlyList<WorkOrder> Jobs { get; }
        public int CreatedCount => Jobs.Count;
        public int UnavailableCount => Entries.Count - CreatedCount;
    }

    /// <summary>
    /// Pure command boundary for player-directed movement. It creates U11 work and never changes representation tier.
    /// </summary>
    public sealed class ManualMoveCommandService
    {
        public const float DefaultFormationSpacing = 1.5f;

        private readonly WorkManager work;
        private readonly ICharacterTierLookup tiers;
        private readonly float formationSpacing;

        public ManualMoveCommandService(WorkManager workManager, ICharacterTierLookup tierLookup,
            float spacing = DefaultFormationSpacing)
        {
            work = workManager ?? throw new ArgumentNullException(nameof(workManager));
            tiers = tierLookup ?? throw new ArgumentNullException(nameof(tierLookup));
            if (spacing <= 0f || float.IsNaN(spacing) || float.IsInfinity(spacing))
                throw new ArgumentOutOfRangeException(nameof(spacing));
            formationSpacing = spacing;
        }

        public ManualMoveCommandResult IssueForGroup(WorkGroupId groupId, WorldPosition target,
            WorkPriority priority)
        {
            var group = work.Groups.Get(groupId);
            return Issue(group.Members, target, priority, groupId);
        }

        public ManualMoveCommandResult Issue(IEnumerable<StableEntityId> characterIds, WorldPosition target,
            WorkPriority priority, WorkGroupId? groupId = null)
        {
            if (characterIds == null) throw new ArgumentNullException(nameof(characterIds));
            var ordered = characterIds.Where(x => x.IsValid).Distinct()
                .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();
            var statuses = new Dictionary<StableEntityId, ManualMoveCommandStatus>();
            var available = new List<StableEntityId>();
            foreach (var id in ordered)
            {
                if (!tiers.TryGetTier(id, out var tier) || tier != CharacterSimulationTier.Tier1)
                {
                    statuses[id] = ManualMoveCommandStatus.TierUnavailable;
                    continue;
                }
                if (work.HasOpenJob(id))
                {
                    statuses[id] = ManualMoveCommandStatus.AlreadyHasWork;
                    continue;
                }
                available.Add(id);
            }

            var formationTargets = BuildFormationTargets(target, available.Count, formationSpacing);
            var jobs = new Dictionary<StableEntityId, WorkOrder>();
            for (var i = 0; i < available.Count; i++)
            {
                var id = available[i];
                try
                {
                    jobs[id] = work.CreateMoveJob(id, formationTargets[i], priority, groupId);
                    statuses[id] = ManualMoveCommandStatus.Created;
                }
                catch (ArgumentException)
                {
                    statuses[id] = ManualMoveCommandStatus.Rejected;
                }
                catch (InvalidOperationException)
                {
                    statuses[id] = ManualMoveCommandStatus.Rejected;
                }
            }

            var entries = new List<ManualMoveCommandEntry>(ordered.Length);
            foreach (var id in ordered)
            {
                jobs.TryGetValue(id, out var job);
                var entryTarget = job != null ? job.Target.Position : target;
                entries.Add(new ManualMoveCommandEntry(id, statuses[id], entryTarget, job));
            }
            return new ManualMoveCommandResult(entries);
        }

        public static IReadOnlyList<WorldPosition> BuildFormationTargets(WorldPosition center, int count,
            float spacing = DefaultFormationSpacing)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (spacing <= 0f || float.IsNaN(spacing) || float.IsInfinity(spacing))
                throw new ArgumentOutOfRangeException(nameof(spacing));
            if (count == 0) return Array.Empty<WorldPosition>();

            var columns = (int)Math.Ceiling(Math.Sqrt(count));
            var rows = (int)Math.Ceiling(count / (double)columns);
            var targets = new List<WorldPosition>(count);
            for (var row = 0; row < rows; row++)
            {
                var rowCount = Math.Min(columns, count - row * columns);
                var z = (row - (rows - 1) * 0.5f) * spacing;
                for (var column = 0; column < rowCount; column++)
                {
                    var x = (column - (rowCount - 1) * 0.5f) * spacing;
                    targets.Add(new WorldPosition(center.X + x, center.Y, center.Z + z));
                }
            }
            return targets;
        }
    }
}
