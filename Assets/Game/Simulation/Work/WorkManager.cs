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
        private long nextQueueOrder = 1;
        private long nextFrontOrder = -1;

        public WorkManager(CharacterRegistry characterRegistry, IWorkEligibilityPolicy eligibilityPolicy = null)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            eligibility = eligibilityPolicy ?? new AllowRegisteredCharacterWorkPolicy();
        }

        public WorkGroupRegistry Groups { get; } = new WorkGroupRegistry();
        public int JobCount => jobs.Count;
        public IReadOnlyList<WorkOrder> Jobs => jobs.Values.OrderBy(x => x.Id).ToArray();

        public WorkGroup CreateGroup(string displayName, IEnumerable<StableEntityId> memberIds,
            WorkGroupRole role = WorkGroupRole.General)
        {
            if (memberIds == null) throw new ArgumentNullException(nameof(memberIds));
            var group = WorkGroup.CreateNew(displayName, role);
            return AddGroup(group, memberIds);
        }

        public WorkGroup CreateGroup(WorkGroupId id, string displayName, IEnumerable<StableEntityId> memberIds,
            WorkGroupRole role)
        {
            if (memberIds == null) throw new ArgumentNullException(nameof(memberIds));
            return AddGroup(WorkGroup.CreateWithId(id, displayName, role), memberIds);
        }

        private WorkGroup AddGroup(WorkGroup group, IEnumerable<StableEntityId> memberIds)
        {
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

        public void RenameGroup(WorkGroupId groupId, string displayName) => Groups.Get(groupId).Rename(displayName);

        public void SetCommander(WorkGroupId groupId, StableEntityId characterId)
        {
            RequireCharacter(characterId);
            Groups.Get(groupId).SetCommander(characterId);
        }

        public WorkOrder CreateMoveJob(StableEntityId? assigneeId, WorldPosition target, WorkPriority priority,
            WorkGroupId? groupId = null, bool exclusiveTarget = false,
            WorkQueuePlacement queuePlacement = WorkQueuePlacement.RejectWhenBusy)
        {
            return Create(WorkJobType.Move, priority, WorkTarget.At(target), null, assigneeId, groupId,
                null, default, exclusiveTarget, queuePlacement);
        }

        public WorkOrder CreateHaulJob(StableEntityId? assigneeId, InventoryOwner source,
            InventoryOwner destination, ResourceId resourceId, ResourceQuantity quantity, WorkPriority priority,
            WorkGroupId? groupId = null, WorldPosition sourcePosition = default, bool exclusiveTarget = false,
            WorkQueuePlacement queuePlacement = WorkQueuePlacement.RejectWhenBusy)
        {
            if (source.Kind != InventoryOwnerKind.WorldPile)
                throw new ArgumentException("Manual haul source must be a world pile.", nameof(source));
            if (destination.Kind != InventoryOwnerKind.BuildingStorage)
                throw new ArgumentException("Manual haul destination must be building storage.", nameof(destination));
            return Create(WorkJobType.Haul, priority,
                WorkTarget.ForEntity(WorkTargetKind.WorldPile, source.Id, sourcePosition),
                WorkTarget.ForEntity(WorkTargetKind.BuildingStorage, destination.Id), assigneeId, groupId,
                resourceId, quantity, exclusiveTarget, queuePlacement);
        }

        public WorkOrder CreateDirectJob(WorkJobType type, StableEntityId? assigneeId, WorkTarget target,
            WorkPriority priority, WorkGroupId? groupId = null, bool exclusiveTarget = true,
            WorkQueuePlacement queuePlacement = WorkQueuePlacement.RejectWhenBusy)
        {
            if (type == WorkJobType.Move || type == WorkJobType.Haul)
                throw new ArgumentException("Use the specialized move/haul factory.", nameof(type));
            return Create(type, priority, target, null, assigneeId, groupId, null, default, exclusiveTarget,
                queuePlacement);
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

        public bool Assign(JobId jobId, StableEntityId characterId,
            WorkQueuePlacement queuePlacement = WorkQueuePlacement.RejectWhenBusy)
        {
            var job = Get(jobId);
            if (job.Status != WorkJobStatus.Queued) return false;
            RequireEligible(characterId, job.Type);
            if (queuePlacement == WorkQueuePlacement.RejectWhenBusy && HasOpenJob(characterId))
                throw new InvalidOperationException("A character cannot execute incompatible exclusive jobs simultaneously.");
            if (!job.AssignTo(characterId)) return false;
            job.SetQueueOrder(AllocateQueueOrder(queuePlacement));
            return true;
        }

        public bool TryAssignNext(StableEntityId characterId, out WorkOrder job)
        {
            RequireCharacter(characterId);
            job = jobs.Values.Where(x => x.Status == WorkJobStatus.Queued && IsEligible(characterId, x.Type))
                .OrderByDescending(x => x.Priority).ThenBy(x => x.Id).FirstOrDefault();
            return job != null && Assign(job.Id, characterId, WorkQueuePlacement.Tail);
        }

        public bool TryGetAssigned(StableEntityId characterId, out WorkOrder job)
        {
            job = jobs.Values.Where(x => x.AssigneeId == characterId &&
                    (x.Status == WorkJobStatus.Assigned || x.Status == WorkJobStatus.Active))
                .OrderByDescending(x => x.Status == WorkJobStatus.Active)
                .ThenBy(x => x.QueueOrder).ThenBy(x => x.Id).FirstOrDefault();
            return job != null;
        }

        public IReadOnlyList<WorkOrder> GetQueue(StableEntityId characterId) => jobs.Values
            .Where(x => x.AssigneeId == characterId && !x.IsTerminal)
            .OrderByDescending(x => x.Status == WorkJobStatus.Active)
            .ThenBy(x => x.QueueOrder).ThenBy(x => x.Id).ToArray();

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

        public bool Unassign(JobId id) => Get(id).Unassign();

        public void SetPriority(JobId id, WorkPriority priority) => Get(id).SetPriority(priority);

        public bool Complete(JobId id) => Finish(Get(id), WorkJobStatus.Completed);
        public bool Cancel(JobId id) => Finish(Get(id), WorkJobStatus.Cancelled);
        public bool Fail(JobId id) => Finish(Get(id), WorkJobStatus.Failed);

        public void SetClaimState(JobId id, WorkClaimState state) => SetClaimState(Get(id), state);

        public bool HasOpenJob(StableEntityId characterId) => jobs.Values.Any(x => x.AssigneeId == characterId && !x.IsTerminal);

        public bool IsTargetReserved(WorkTarget target) => exclusiveTargets.ContainsKey(target);

        public bool TryGetOpenJobForTarget(WorkTarget target, out WorkOrder job)
        {
            job = null;
            return exclusiveTargets.TryGetValue(target, out var id) && jobs.TryGetValue(id.Value, out job) && !job.IsTerminal;
        }

        public WorkSaveState CaptureState() => new WorkSaveState(nextJobId,
            Groups.All.Select(x => x.CaptureState()), Jobs.Select(Capture), nextQueueOrder, nextFrontOrder);

        public void RestoreState(WorkSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            jobs.Clear();
            exclusiveTargets.Clear();
            Groups.Clear();
            foreach (var groupState in state.Groups.OrderBy(x => x.Id))
            {
                var group = AddGroup(WorkGroup.CreateWithId(groupState.Id, groupState.DisplayName, groupState.Role),
                    groupState.MemberIds);
                if (groupState.CommanderId.HasValue) group.SetCommander(groupState.CommanderId.Value);
            }
            foreach (var saved in state.Jobs.OrderBy(x => x.Id))
            {
                if (saved.Id <= 0 || jobs.ContainsKey(saved.Id))
                    throw new InvalidOperationException("Work save contains a duplicate or invalid job identity.");
                WorkGroup group = null;
                if (saved.GroupId.HasValue)
                {
                    if (!Groups.TryGet(saved.GroupId.Value, out group))
                        throw new InvalidOperationException("Work save references an unknown group.");
                }
                if (saved.AssigneeId.HasValue)
                {
                    RequireEligible(saved.AssigneeId.Value, saved.Type);
                    if (saved.GroupId.HasValue && !group.Contains(saved.AssigneeId.Value))
                        throw new InvalidOperationException("Saved assignee is not a member of the job group.");
                }
                var restoredQueueOrder = saved.QueueOrder != 0 ? saved.QueueOrder : saved.Id;
                var order = new WorkOrder(new JobId(saved.Id), saved.Type, saved.Priority, saved.Target,
                    saved.Destination, saved.AssigneeId, saved.GroupId, saved.ResourceId, saved.Quantity,
                    saved.ExclusiveTarget, restoredQueueOrder);
                order.RestoreRuntime(saved.Status, saved.ClaimState);
                jobs.Add(saved.Id, order);
                if (saved.ExclusiveTarget && !order.IsTerminal)
                {
                    if (exclusiveTargets.ContainsKey(saved.Target))
                        throw new InvalidOperationException("Work save contains a conflicting exclusive target.");
                    exclusiveTargets.Add(saved.Target, order.Id);
                }
            }
            nextJobId = Math.Max(state.NextJobId, jobs.Count == 0 ? 1L : jobs.Keys.Max() + 1L);
            nextQueueOrder = Math.Max(state.NextQueueOrder,
                jobs.Values.Where(x => x.QueueOrder > 0).Select(x => x.QueueOrder + 1).DefaultIfEmpty(1).Max());
            nextFrontOrder = Math.Min(state.NextFrontOrder,
                jobs.Values.Where(x => x.QueueOrder < 0).Select(x => x.QueueOrder - 1).DefaultIfEmpty(-1).Min());
            var duplicate = jobs.Values.Where(x => x.Status == WorkJobStatus.Active && x.AssigneeId.HasValue)
                .GroupBy(x => x.AssigneeId.Value).FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null)
                throw new InvalidOperationException("Work save assigns multiple active jobs to one character.");
        }

        private WorkOrder Create(WorkJobType type, WorkPriority priority, WorkTarget target,
            WorkTarget? destination, StableEntityId? assigneeId, WorkGroupId? groupId,
            ResourceId? resourceId, ResourceQuantity quantity, bool exclusiveTarget,
            WorkQueuePlacement queuePlacement)
        {
            WorkGroup group = null;
            if (groupId.HasValue && !Groups.TryGet(groupId.Value, out group))
                throw new ArgumentException("Work group does not exist.", nameof(groupId));
            if (assigneeId.HasValue)
            {
                RequireEligible(assigneeId.Value, type);
                if (groupId.HasValue && !group.Contains(assigneeId.Value))
                    throw new InvalidOperationException("Group work may only be assigned to a member of that group.");
                if (queuePlacement == WorkQueuePlacement.RejectWhenBusy && HasOpenJob(assigneeId.Value))
                    throw new InvalidOperationException("A character cannot execute incompatible exclusive jobs simultaneously.");
            }
            if (exclusiveTarget && exclusiveTargets.ContainsKey(target))
                throw new InvalidOperationException("The exclusive work target is already reserved by another job.");
            var job = new WorkOrder(new JobId(nextJobId++), type, priority, target, destination,
                assigneeId, groupId, resourceId, quantity, exclusiveTarget,
                assigneeId.HasValue ? AllocateQueueOrder(queuePlacement) : 0);
            jobs.Add(job.Id.Value, job);
            if (exclusiveTarget) exclusiveTargets.Add(target, job.Id);
            return job;
        }

        private static WorkOrderState Capture(WorkOrder job) => new WorkOrderState(job.Id.Value,
            job.Type, job.Priority, job.Target, job.Destination, job.AssigneeId, job.GroupId,
            job.ResourceId, job.Quantity, job.ExclusiveTarget, job.Status, job.ClaimState, job.QueueOrder);

        private long AllocateQueueOrder(WorkQueuePlacement placement)
        {
            if (placement == WorkQueuePlacement.Front) return nextFrontOrder--;
            return nextQueueOrder++;
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
