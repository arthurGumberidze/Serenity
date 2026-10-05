using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Resources;

namespace Game.Domain.Work
{
    public readonly struct WorkGroupId : IEquatable<WorkGroupId>, IComparable<WorkGroupId>
    {
        public WorkGroupId(StableEntityId value)
        {
            if (!value.IsValid) throw new ArgumentException("Valid group identity required.", nameof(value));
            Value = value;
        }

        public StableEntityId Value { get; }
        public bool IsValid => Value.IsValid;
        public static WorkGroupId NewId() => new WorkGroupId(StableEntityId.NewId());
        public int CompareTo(WorkGroupId other) => string.CompareOrdinal(Value.ToString(), other.Value.ToString());
        public bool Equals(WorkGroupId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is WorkGroupId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(WorkGroupId left, WorkGroupId right) => left.Equals(right);
        public static bool operator !=(WorkGroupId left, WorkGroupId right) => !left.Equals(right);
    }

    public readonly struct WorkGroupState
    {
        public WorkGroupState(WorkGroupId id, string displayName, StableEntityId[] memberIds,
            StableEntityId? commanderId)
        {
            if (!id.IsValid) throw new ArgumentException("Valid group identity required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 64)
                throw new ArgumentException("Group display name must contain 1-64 characters.", nameof(displayName));
            Id = id;
            DisplayName = displayName.Trim();
            MemberIds = memberIds ?? Array.Empty<StableEntityId>();
            CommanderId = commanderId;
        }

        public WorkGroupId Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<StableEntityId> MemberIds { get; }
        public StableEntityId? CommanderId { get; }
    }

    public sealed class WorkGroup
    {
        private readonly HashSet<StableEntityId> members = new HashSet<StableEntityId>();

        private WorkGroup(WorkGroupId id, string displayName)
        {
            if (!id.IsValid) throw new ArgumentException("Valid group identity required.", nameof(id));
            Id = id;
            Rename(displayName);
        }

        public WorkGroupId Id { get; }
        public string DisplayName { get; private set; }
        public StableEntityId? CommanderId { get; private set; }
        public int MemberCount => members.Count;
        public IReadOnlyList<StableEntityId> Members => members
            .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();

        public static WorkGroup CreateNew(string displayName) => new WorkGroup(WorkGroupId.NewId(), displayName);

        public static WorkGroup Restore(WorkGroupState state)
        {
            var group = new WorkGroup(state.Id, state.DisplayName);
            foreach (var member in state.MemberIds) group.AddMember(member);
            if (state.CommanderId.HasValue) group.SetCommander(state.CommanderId.Value);
            return group;
        }

        public void Rename(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 64)
                throw new ArgumentException("Group display name must contain 1-64 characters.", nameof(displayName));
            DisplayName = displayName.Trim();
        }

        public void AddMember(StableEntityId characterId)
        {
            if (!characterId.IsValid) throw new ArgumentException("Valid character identity required.", nameof(characterId));
            if (!members.Add(characterId))
                throw new InvalidOperationException("Character is already a member of this work group.");
        }

        public bool RemoveMember(StableEntityId characterId)
        {
            if (!members.Remove(characterId)) return false;
            if (CommanderId == characterId) CommanderId = null;
            return true;
        }

        public bool Contains(StableEntityId characterId) => members.Contains(characterId);

        public void SetCommander(StableEntityId characterId)
        {
            if (!members.Contains(characterId))
                throw new InvalidOperationException("The commander must be a member of the work group.");
            CommanderId = characterId;
        }

        public WorkGroupState CaptureState() => new WorkGroupState(Id, DisplayName, Members.ToArray(), CommanderId);
    }

    public sealed class WorkGroupRegistry
    {
        private readonly Dictionary<WorkGroupId, WorkGroup> groups = new Dictionary<WorkGroupId, WorkGroup>();
        private readonly Dictionary<StableEntityId, WorkGroupId> membership =
            new Dictionary<StableEntityId, WorkGroupId>();

        public int Count => groups.Count;
        public IReadOnlyList<WorkGroup> All => groups.Values.OrderBy(x => x.Id).ToArray();

        public void Add(WorkGroup group)
        {
            if (group == null) throw new ArgumentNullException(nameof(group));
            if (groups.ContainsKey(group.Id)) throw new InvalidOperationException("Work group identity already exists.");
            foreach (var member in group.Members)
                if (membership.ContainsKey(member))
                    throw new InvalidOperationException("A character may belong to only one canonical work group.");
            groups.Add(group.Id, group);
            foreach (var member in group.Members) membership.Add(member, group.Id);
        }

        public WorkGroup Get(WorkGroupId id) => groups.TryGetValue(id, out var group)
            ? group : throw new KeyNotFoundException("Work group not found.");

        public bool TryGet(WorkGroupId id, out WorkGroup group) => groups.TryGetValue(id, out group);

        public bool TryGetForMember(StableEntityId characterId, out WorkGroup group)
        {
            group = null;
            return membership.TryGetValue(characterId, out var id) && groups.TryGetValue(id, out group);
        }

        public void AddMember(WorkGroupId groupId, StableEntityId characterId)
        {
            if (membership.ContainsKey(characterId))
                throw new InvalidOperationException("A character may belong to only one canonical work group.");
            var group = Get(groupId);
            group.AddMember(characterId);
            membership.Add(characterId, groupId);
        }

        public bool RemoveMember(WorkGroupId groupId, StableEntityId characterId)
        {
            var group = Get(groupId);
            if (!group.RemoveMember(characterId)) return false;
            membership.Remove(characterId);
            return true;
        }
    }

    public readonly struct JobId : IEquatable<JobId>, IComparable<JobId>
    {
        public JobId(long value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public long Value { get; }
        public int CompareTo(JobId other) => Value.CompareTo(other.Value);
        public bool Equals(JobId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is JobId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(JobId left, JobId right) => left.Equals(right);
        public static bool operator !=(JobId left, JobId right) => !left.Equals(right);
    }

    public enum WorkJobType { Haul = 0, Move = 1 }
    public enum WorkPriority { Low = 0, Normal = 1, High = 2, Urgent = 3 }
    public enum WorkJobStatus { Queued = 0, Assigned = 1, Active = 2, Completed = 3, Cancelled = 4, Failed = 5 }
    public enum WorkTargetKind { WorldPosition = 0, Character = 1, WorldPile = 2, BuildingStorage = 3 }
    public enum WorkClaimState { None = 0, Reserved = 1, Carrying = 2, Released = 3 }

    public readonly struct WorkTarget : IEquatable<WorkTarget>
    {
        private WorkTarget(WorkTargetKind kind, StableEntityId entityId, WorldPosition position)
        {
            Kind = kind;
            EntityId = entityId;
            Position = position;
        }

        public WorkTargetKind Kind { get; }
        public StableEntityId EntityId { get; }
        public WorldPosition Position { get; }
        public bool HasEntity => Kind != WorkTargetKind.WorldPosition;

        public static WorkTarget At(WorldPosition position) =>
            new WorkTarget(WorkTargetKind.WorldPosition, default, position);

        public static WorkTarget ForEntity(WorkTargetKind kind, StableEntityId id)
        {
            if (kind == WorkTargetKind.WorldPosition) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!id.IsValid) throw new ArgumentException("Valid target identity required.", nameof(id));
            return new WorkTarget(kind, id, default);
        }

        public bool Equals(WorkTarget other) => Kind == other.Kind && EntityId == other.EntityId && Position.Equals(other.Position);
        public override bool Equals(object obj) => obj is WorkTarget other && Equals(other);
        public override int GetHashCode() => (((int)Kind * 397) ^ EntityId.GetHashCode()) * 397 ^ Position.GetHashCode();
        public static bool operator ==(WorkTarget left, WorkTarget right) => left.Equals(right);
        public static bool operator !=(WorkTarget left, WorkTarget right) => !left.Equals(right);
    }

    public sealed class WorkOrder
    {
        public WorkOrder(JobId id, WorkJobType type, WorkPriority priority, WorkTarget target,
            WorkTarget? destination, StableEntityId? assigneeId, WorkGroupId? groupId,
            ResourceId? resourceId, ResourceQuantity quantity, bool exclusiveTarget)
        {
            if (!Enum.IsDefined(typeof(WorkJobType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!Enum.IsDefined(typeof(WorkPriority), priority)) throw new ArgumentOutOfRangeException(nameof(priority));
            if (assigneeId.HasValue && !assigneeId.Value.IsValid) throw new ArgumentException("Assignee must be valid.", nameof(assigneeId));
            if (groupId.HasValue && !groupId.Value.IsValid) throw new ArgumentException("Group must be valid.", nameof(groupId));
            if (type == WorkJobType.Move && target.Kind != WorkTargetKind.WorldPosition)
                throw new ArgumentException("Move jobs require a world-position target.", nameof(target));
            if (type == WorkJobType.Haul && (!resourceId.HasValue || !resourceId.Value.IsValid || !quantity.IsPositive ||
                target.Kind != WorkTargetKind.WorldPile || !destination.HasValue ||
                destination.Value.Kind != WorkTargetKind.BuildingStorage))
                throw new ArgumentException("Haul jobs require source, destination, resource and positive quantity.");
            Id = id;
            Type = type;
            Priority = priority;
            Target = target;
            Destination = destination;
            AssigneeId = assigneeId;
            GroupId = groupId;
            ResourceId = resourceId;
            Quantity = quantity;
            ExclusiveTarget = exclusiveTarget;
            Status = assigneeId.HasValue ? WorkJobStatus.Assigned : WorkJobStatus.Queued;
        }

        public JobId Id { get; }
        public WorkJobType Type { get; }
        public WorkPriority Priority { get; private set; }
        public WorkTarget Target { get; }
        public WorkTarget? Destination { get; }
        public StableEntityId? AssigneeId { get; private set; }
        public WorkGroupId? GroupId { get; }
        public ResourceId? ResourceId { get; }
        public ResourceQuantity Quantity { get; }
        public bool ExclusiveTarget { get; }
        public WorkJobStatus Status { get; private set; }
        public WorkClaimState ClaimState { get; private set; }
        public bool IsTerminal => Status == WorkJobStatus.Completed || Status == WorkJobStatus.Cancelled || Status == WorkJobStatus.Failed;

        public void SetPriority(WorkPriority priority)
        {
            if (!Enum.IsDefined(typeof(WorkPriority), priority)) throw new ArgumentOutOfRangeException(nameof(priority));
            Priority = priority;
        }

        public bool AssignTo(StableEntityId characterId)
        {
            if (!characterId.IsValid) throw new ArgumentException("Valid assignee required.", nameof(characterId));
            if (Status != WorkJobStatus.Queued) return false;
            AssigneeId = characterId;
            Status = WorkJobStatus.Assigned;
            return true;
        }

        public bool Start()
        {
            if (Status == WorkJobStatus.Active) return true;
            if (Status != WorkJobStatus.Assigned) return false;
            Status = WorkJobStatus.Active;
            return true;
        }

        public bool RequeueAssigned()
        {
            if (IsTerminal) return false;
            Status = AssigneeId.HasValue ? WorkJobStatus.Assigned : WorkJobStatus.Queued;
            ClaimState = WorkClaimState.Released;
            return true;
        }

        public bool Finish(WorkJobStatus terminalStatus)
        {
            if (terminalStatus != WorkJobStatus.Completed && terminalStatus != WorkJobStatus.Cancelled &&
                terminalStatus != WorkJobStatus.Failed) throw new ArgumentOutOfRangeException(nameof(terminalStatus));
            if (IsTerminal) return false;
            Status = terminalStatus;
            ClaimState = WorkClaimState.Released;
            return true;
        }

        public void SetClaimState(WorkClaimState state)
        {
            if (!Enum.IsDefined(typeof(WorkClaimState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            ClaimState = state;
        }
    }
}
