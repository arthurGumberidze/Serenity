using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;

namespace Game.Simulation.Work
{
    public interface IWorkEligibilityPolicy
    {
        bool CanPerform(Character character, WorkJobType jobType);
    }

    public sealed class AllowRegisteredCharacterWorkPolicy : IWorkEligibilityPolicy
    {
        public bool CanPerform(Character character, WorkJobType jobType) =>
            character != null && character.LifeState == CharacterLifeState.Alive;
    }

    /// <summary>Session-owned canonical group and work-order coordinator. It has no frame loop.</summary>
    public sealed class WorkManager
    {
        private readonly CharacterRegistry characters;
        private readonly IWorkEligibilityPolicy eligibility;
        private readonly Dictionary<long, WorkOrder> jobs = new Dictionary<long, WorkOrder>();
        private readonly Dictionary<WorkTarget, JobId> exclusiveTargets = new Dictionary<WorkTarget, JobId>();
        private long nextJobId = 1;

        public WorkManager(CharacterRegistry characterRegistry, IWorkEligibilityPolicy eligibilityPolicy = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            eligibility = eligibilityPolicy ?? new AllowRegisteredCharacterWorkPolicy();
        }

        public WorkGroupRegistry Groups { get; } = new WorkGroupRegistry();
        public int JobCount => jobs.Count;
        public IReadOnlyList<WorkOrder> Jobs => jobs.Values.OrderBy(x => x.Id).ToArray();

        public WorkGroup CreateGroup(string displayName, IEnumerable<StableEntityId> memberIds)
        {
            if (memberIds == null) throw new ArgumentNullException(nameof(memberIds));
            var group = WorkGroup.CreateNew(displayName);
            foreach (var memberId in memberIds.OrderBy(x => x.ToString(), StringComparer.Ordinal))
            {
                RequireCharacter(memberId);
                group.AddMember(memberId);
            }
            Groups.Add(group);
            return group;
        }

        public void AddMember(WorkGroupId groupId, StableEntityId characterId)
        {
            RequireCharacter(characterId);
            Groups.AddMember(groupId, characterId);
        }

        public bool RemoveMember(WorkGroupId groupId, StableEntityId characterId) =>
            Groups.RemoveMember(groupId, characterId);

        public WorkOrder CreateMoveJob(StableEntityId? assigneeId, WorldPosition target, WorkPriority priority,
            WorkGroupId? groupId = null, bool exclusiveTarget = false)
        {
            return Create(WorkJobType.Move, priority, WorkTarget.At(target), null, assigneeId, groupId,
                null, default, exclusiveTarget);
        }

        public WorkOrder CreateHaulJob(StableEntityId? assigneeId, InventoryOwner source,
            InventoryOwner destination, ResourceId resourceId, ResourceQuantity quantity, WorkPriority priority,
            WorkGroupId? groupId = null)
        {
            if (source.Kind != InventoryOwnerKind.WorldPile)
                throw new ArgumentException("Manual haul source must be a world pile.", nameof(source));
            if (destination.Kind != InventoryOwnerKind.BuildingStorage)
                throw new ArgumentException("Manual haul destination must be building storage.", nameof(destination));
            return Create(WorkJobType.Haul, priority,
                WorkTarget.ForEntity(WorkTargetKind.WorldPile, source.Id),
                WorkTarget.ForEntity(WorkTargetKind.BuildingStorage, destination.Id), assigneeId, groupId,
                resourceId, quantity, false);
        }

        public IReadOnlyList<WorkOrder> CreateGroupMoveJobs(WorkGroupId groupId, WorldPosition target,
            WorkPriority priority)
        {
            var group = Groups.Get(groupId);
            var result = new List<WorkOrder>();
            foreach (var memberId in group.Members)
            {
                if (!IsEligible(memberId, WorkJobType.Move) || HasOpenJob(memberId)) continue;
                result.Add(CreateMoveJob(memberId, target, priority, groupId));
            }
            return result;
        }

        public IReadOnlyList<WorkOrder> CreateGroupHaulJobs(WorkGroupId groupId, InventoryOwner source,
            InventoryOwner destination, ResourceId resourceId, ResourceQuantity quantityPerWorker,
            WorkPriority priority)
        {
            var group = Groups.Get(groupId);
            var result = new List<WorkOrder>();
            foreach (var memberId in group.Members)
            {
                if (!IsEligible(memberId, WorkJobType.Haul) || HasOpenJob(memberId)) continue;
                result.Add(CreateHaulJob(memberId, source, destination, resourceId, quantityPerWorker,
                    priority, groupId));
            }
            return result;
        }

        public bool Assign(JobId jobId, StableEntityId characterId)
        {
            var job = Get(jobId);
            if (job.Status != WorkJobStatus.Queued) return false;
            RequireEligible(characterId, job.Type);
            if (HasOpenJob(characterId))
                throw new InvalidOperationException("A character cannot execute incompatible exclusive jobs simultaneously.");
            return job.AssignTo(characterId);
        }

        public bool TryAssignNext(StableEntityId characterId, out WorkOrder job)
        {
            RequireCharacter(characterId);
            job = jobs.Values.Where(x => x.Status == WorkJobStatus.Queued && IsEligible(characterId, x.Type))
                .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).FirstOrDefault();
            return job != null && Assign(job.Id, characterId);
        }

        public bool TryGetAssigned(StableEntityId characterId, out WorkOrder job)
        {
            job = jobs.Values.Where(x => x.AssigneeId == characterId &&
                    (x.Status == WorkJobStatus.Assigned || x.Status == WorkJobStatus.Active))
                .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).FirstOrDefault();
            return job != null;
        }

        public WorkOrder Get(JobId id) => jobs.TryGetValue(id.Value, out var job)
            ? job : throw new KeyNotFoundException("Work order not found.");

        public IReadOnlyList<WorkOrder> Capture(WorkJobStatus status) => jobs.Values
            .Where(x => x.Status == status).OrderByDescending(x => x.Priority).ThenBy(x => x.Id).ToArray();

        public bool Start(JobId id)
        {
            var job = Get(id);
            return job.Start();
        }

        public bool RequeueAssigned(JobId id)
        {
            var job = Get(id);
            return job.RequeueAssigned();
        }

        public bool Complete(JobId id) => Finish(Get(id), WorkJobStatus.Completed);
        public bool Cancel(JobId id) => Finish(Get(id), WorkJobStatus.Cancelled);
        public bool Fail(JobId id) => Finish(Get(id), WorkJobStatus.Failed);

        public void SetClaimState(JobId id, WorkClaimState state) => SetClaimState(Get(id), state);

        public bool HasOpenJob(StableEntityId characterId) => jobs.Values.Any(x => x.AssigneeId == characterId && !x.IsTerminal);

        private WorkOrder Create(WorkJobType type, WorkPriority priority, WorkTarget target,
            WorkTarget? destination, StableEntityId? assigneeId, WorkGroupId? groupId,
            ResourceId? resourceId, ResourceQuantity quantity, bool exclusiveTarget)
        {
            WorkGroup group = null;
            if (groupId.HasValue && !Groups.TryGet(groupId.Value, out group))
                throw new ArgumentException("Work group does not exist.", nameof(groupId));
            if (assigneeId.HasValue)
            {
                RequireEligible(assigneeId.Value, type);
                if (groupId.HasValue && !group.Contains(assigneeId.Value))
                    throw new InvalidOperationException("Group work may only be assigned to a member of that group.");
                if (HasOpenJob(assigneeId.Value))
                    throw new InvalidOperationException("A character cannot execute incompatible exclusive jobs simultaneously.");
            }
            if (exclusiveTarget && exclusiveTargets.ContainsKey(target))
                throw new InvalidOperationException("The exclusive work target is already reserved by another job.");
            var job = new WorkOrder(new JobId(nextJobId++), type, priority, target, destination,
                assigneeId, groupId, resourceId, quantity, exclusiveTarget);
            jobs.Add(job.Id.Value, job);
            if (exclusiveTarget) exclusiveTargets.Add(target, job.Id);
            return job;
        }

        private bool Finish(WorkOrder job, WorkJobStatus terminalStatus)
        {
            if (job.IsTerminal) return false;
            if (!job.Finish(terminalStatus)) return false;
            if (job.ExclusiveTarget) exclusiveTargets.Remove(job.Target);
            return true;
        }

        private static void SetClaimState(WorkOrder job, WorkClaimState state)
        {
            job.SetClaimState(state);
        }

        private void RequireCharacter(StableEntityId characterId)
        {
            if (!characterId.IsValid || !characters.TryGet(characterId, out _))
                throw new InvalidOperationException("Work assignment requires a registered canonical character.");
        }

        private bool IsEligible(StableEntityId characterId, WorkJobType type) =>
            characterId.IsValid && characters.TryGet(characterId, out var character) && eligibility.CanPerform(character, type);

        private void RequireEligible(StableEntityId characterId, WorkJobType type)
        {
            RequireCharacter(characterId);
            if (!characters.TryGet(characterId, out var character) || !eligibility.CanPerform(character, type))
                throw new InvalidOperationException("Character is not eligible for this work type.");
        }
    }
}
