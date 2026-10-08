using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.Military
{
    public readonly struct MilitaryUnitId : IEquatable<MilitaryUnitId>, IComparable<MilitaryUnitId>
    {
        public MilitaryUnitId(StableEntityId value)
        {
            if (!value.IsValid) throw new ArgumentException("Military unit ID must be valid.", nameof(value));
            Value = value;
        }

        public StableEntityId Value { get; }
        public bool IsValid => Value.IsValid;
        public static MilitaryUnitId NewId() => new MilitaryUnitId(StableEntityId.NewId());
        public static MilitaryUnitId Parse(string text) => new MilitaryUnitId(StableEntityId.Parse(text));
        public int CompareTo(MilitaryUnitId other) => Value.CompareTo(other.Value);
        public bool Equals(MilitaryUnitId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is MilitaryUnitId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(MilitaryUnitId left, MilitaryUnitId right) => left.Equals(right);
        public static bool operator !=(MilitaryUnitId left, MilitaryUnitId right) => !left.Equals(right);
    }

    public readonly struct MilitarySideId : IEquatable<MilitarySideId>, IComparable<MilitarySideId>
    {
        public MilitarySideId(string value)
        {
            if (!IsCanonical(value)) throw new ArgumentException("Military side ID must be canonical lowercase text.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => IsCanonical(Value);
        public int CompareTo(MilitarySideId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(MilitarySideId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MilitarySideId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(MilitarySideId left, MilitarySideId right) => left.Equals(right);
        public static bool operator !=(MilitarySideId left, MilitarySideId right) => !left.Equals(right);

        private static bool IsCanonical(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
            value == value.Trim() && value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_');
    }

    public enum MilitaryUnitMemberRole { Soldier = 1 }
    public enum MilitaryUnitMemberStatus { Active = 1, Removed = 2 }
    public enum MilitaryUnitStatus { Ready = 1, Engaged = 2, Leaderless = 3, Disbanded = 4 }
    public enum MilitaryUnitRepresentationMode { Detailed = 1, Aggregate = 2 }
    public enum MilitaryUnitOrderKind { Idle = 1, Move = 2, Hold = 3, Engage = 4 }

    public sealed class MilitaryUnitMemberState
    {
        public MilitaryUnitMemberState(StableEntityId characterId, MilitaryUnitMemberRole role,
            MilitaryUnitMemberStatus status, ulong joinedSequence, ulong? removedSequence = null)
        {
            if (!characterId.IsValid) throw new ArgumentException("Roster member ID required.", nameof(characterId));
            if (!Enum.IsDefined(typeof(MilitaryUnitMemberRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (!Enum.IsDefined(typeof(MilitaryUnitMemberStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if ((status == MilitaryUnitMemberStatus.Removed) != removedSequence.HasValue)
                throw new ArgumentException("Removed roster members require a removal sequence.", nameof(removedSequence));
            if (removedSequence.HasValue && removedSequence.Value < joinedSequence)
                throw new ArgumentOutOfRangeException(nameof(removedSequence));
            CharacterId = characterId;
            Role = role;
            Status = status;
            JoinedSequence = joinedSequence;
            RemovedSequence = removedSequence;
        }

        public StableEntityId CharacterId { get; }
        public MilitaryUnitMemberRole Role { get; }
        public MilitaryUnitMemberStatus Status { get; }
        public ulong JoinedSequence { get; }
        public ulong? RemovedSequence { get; }
        public bool IsActive => Status == MilitaryUnitMemberStatus.Active;
    }

    public sealed class MilitaryUnitOrderState
    {
        public MilitaryUnitOrderState(MilitaryUnitOrderKind kind, WorldPosition target,
            bool hasTarget, MilitaryUnitId? targetUnitId, ulong sequence)
        {
            if (!Enum.IsDefined(typeof(MilitaryUnitOrderKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == MilitaryUnitOrderKind.Move && !hasTarget)
                throw new ArgumentException("Move orders require a target position.", nameof(hasTarget));
            if (kind != MilitaryUnitOrderKind.Move && hasTarget)
                throw new ArgumentException("Only Move orders may contain a target position.", nameof(hasTarget));
            if (kind == MilitaryUnitOrderKind.Engage && !targetUnitId.HasValue)
                throw new ArgumentException("Engage orders require a target unit.", nameof(targetUnitId));
            if (kind != MilitaryUnitOrderKind.Engage && targetUnitId.HasValue)
                throw new ArgumentException("Only Engage orders may contain a target unit.", nameof(targetUnitId));
            Kind = kind;
            Target = target;
            HasTarget = hasTarget;
            TargetUnitId = targetUnitId;
            Sequence = sequence;
        }

        public MilitaryUnitOrderKind Kind { get; }
        public WorldPosition Target { get; }
        public bool HasTarget { get; }
        public MilitaryUnitId? TargetUnitId { get; }
        public ulong Sequence { get; }

        public static MilitaryUnitOrderState Idle(ulong sequence = 0) =>
            new MilitaryUnitOrderState(MilitaryUnitOrderKind.Idle, default, false, null, sequence);
    }

    /// <summary>Detached canonical unit state. Character health and biography remain in Character/U18.</summary>
    public sealed class MilitaryUnitState
    {
        public MilitaryUnitState(MilitaryUnitId id, MilitarySideId ownerSideId, StableEntityId? captainId,
            IEnumerable<MilitaryUnitMemberState> roster, MilitaryUnitStatus status,
            MilitaryUnitRepresentationMode representation, string abstractLocationId,
            int moraleSummary, int cohesion, MilitaryUnitOrderState order,
            StableEntityId? activeEncounterId, ulong revision)
        {
            if (!id.IsValid) throw new ArgumentException("Military unit ID required.", nameof(id));
            if (!ownerSideId.IsValid) throw new ArgumentException("Military side ID required.", nameof(ownerSideId));
            if (!Enum.IsDefined(typeof(MilitaryUnitStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(typeof(MilitaryUnitRepresentationMode), representation))
                throw new ArgumentOutOfRangeException(nameof(representation));
            if (moraleSummary < 0 || moraleSummary > 1000) throw new ArgumentOutOfRangeException(nameof(moraleSummary));
            if (cohesion < 0 || cohesion > 1000) throw new ArgumentOutOfRangeException(nameof(cohesion));
            if (abstractLocationId == null || abstractLocationId.Length > 128 || abstractLocationId != abstractLocationId.Trim())
                throw new ArgumentException("Abstract location must be trimmed and at most 128 characters.", nameof(abstractLocationId));
            var members = (roster ?? throw new ArgumentNullException(nameof(roster))).ToArray();
            if (members.Length == 0) throw new ArgumentException("A military unit requires roster history.", nameof(roster));
            if (members.Any(x => x == null)) throw new ArgumentException("Roster members cannot be null.", nameof(roster));
            members = members.OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal).ToArray();
            if (members.Select(x => x.CharacterId).Distinct().Count() != members.Length)
                throw new ArgumentException("Roster member IDs must be unique.", nameof(roster));
            if (captainId.HasValue && !members.Any(x => x.CharacterId == captainId.Value && x.IsActive))
                throw new ArgumentException("Captain must be an active roster member.", nameof(captainId));
            if ((status == MilitaryUnitStatus.Engaged) != activeEncounterId.HasValue)
                throw new ArgumentException("Engaged status and active encounter link must agree.", nameof(activeEncounterId));
            if (status == MilitaryUnitStatus.Leaderless && captainId.HasValue)
                throw new ArgumentException("A leaderless unit cannot have a captain.", nameof(captainId));
            var hasActiveMembers = members.Any(x => x.IsActive);
            if ((status == MilitaryUnitStatus.Disbanded) == hasActiveMembers)
                throw new ArgumentException("Disbanded status must match an empty active roster.", nameof(status));
            if (status == MilitaryUnitStatus.Ready && !captainId.HasValue)
                throw new ArgumentException("A ready unit requires a captain.", nameof(captainId));
            Id = id;
            OwnerSideId = ownerSideId;
            CaptainId = captainId;
            Roster = Array.AsReadOnly(members);
            Status = status;
            Representation = representation;
            AbstractLocationId = abstractLocationId;
            MoraleSummary = moraleSummary;
            Cohesion = cohesion;
            Order = order ?? throw new ArgumentNullException(nameof(order));
            ActiveEncounterId = activeEncounterId;
            Revision = revision;
        }

        public MilitaryUnitId Id { get; }
        public MilitarySideId OwnerSideId { get; }
        public StableEntityId? CaptainId { get; }
        public IReadOnlyList<MilitaryUnitMemberState> Roster { get; }
        public MilitaryUnitStatus Status { get; }
        public MilitaryUnitRepresentationMode Representation { get; }
        public string AbstractLocationId { get; }
        public int MoraleSummary { get; }
        public int Cohesion { get; }
        public MilitaryUnitOrderState Order { get; }
        public StableEntityId? ActiveEncounterId { get; }
        public ulong Revision { get; }
        public IReadOnlyList<StableEntityId> ActiveMemberIds => Roster.Where(x => x.IsActive)
            .Select(x => x.CharacterId).ToArray();
    }

    public sealed class MilitaryUnitSaveState
    {
        public MilitaryUnitSaveState(IEnumerable<MilitaryUnitState> units)
        {
            var values = (units ?? throw new ArgumentNullException(nameof(units))).ToArray();
            if (values.Any(x => x == null)) throw new ArgumentException("Saved units cannot be null.", nameof(units));
            values = values.OrderBy(x => x.Id).ToArray();
            if (values.Select(x => x.Id).Distinct().Count() != values.Length)
                throw new ArgumentException("Saved military unit IDs must be unique.", nameof(units));
            Units = Array.AsReadOnly(values);
        }

        public IReadOnlyList<MilitaryUnitState> Units { get; }
    }
}
