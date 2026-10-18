using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.AI;
using Game.Domain.Combat;
using Game.Domain.Resources;
using Game.Domain.World;

namespace Game.Domain.Military
{
    public enum MilitaryRank
    {
        Recruit = 1,
        Soldier = 2,
        Veteran = 3,
        Elite = 4
    }

    public sealed class MilitaryTrainingLevelDefinition
    {
        public MilitaryTrainingLevelDefinition(MilitaryRank rank, long durationTicks,
            int combatSkillLevel, IEnumerable<ResourceAmount> cost)
        {
            if (!Enum.IsDefined(typeof(MilitaryRank), rank)) throw new ArgumentOutOfRangeException(nameof(rank));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (combatSkillLevel < 0 || combatSkillLevel > 100)
                throw new ArgumentOutOfRangeException(nameof(combatSkillLevel));
            var values = (cost ?? throw new ArgumentNullException(nameof(cost))).ToArray();
            if (values.Any(x => !x.Quantity.IsPositive) ||
                values.Select(x => x.ResourceId).Distinct().Count() != values.Length)
                throw new ArgumentException("Training cost must be positive and unique.", nameof(cost));
            Rank = rank;
            DurationTicks = durationTicks;
            CombatSkillLevel = combatSkillLevel;
            Cost = Array.AsReadOnly(values.OrderBy(x => x.ResourceId).ToArray());
        }

        public MilitaryRank Rank { get; }
        public long DurationTicks { get; }
        public int CombatSkillLevel { get; }
        public IReadOnlyList<ResourceAmount> Cost { get; }
    }

    public sealed class MilitaryTrainingCatalog
    {
        private readonly IReadOnlyDictionary<MilitaryRank, MilitaryTrainingLevelDefinition> definitions;

        public MilitaryTrainingCatalog(IEnumerable<MilitaryTrainingLevelDefinition> values)
        {
            var copy = (values ?? throw new ArgumentNullException(nameof(values))).ToArray();
            if (copy.Length == 0 || copy.Any(x => x == null) ||
                copy.Select(x => x.Rank).Distinct().Count() != copy.Length)
                throw new ArgumentException("Training levels must be non-empty and unique.", nameof(values));
            var expected = Enum.GetValues(typeof(MilitaryRank)).Cast<MilitaryRank>().OrderBy(x => x).ToArray();
            if (!copy.Select(x => x.Rank).OrderBy(x => x).SequenceEqual(expected))
                throw new ArgumentException("Training catalog must define every approved rank.", nameof(values));
            definitions = copy.ToDictionary(x => x.Rank);
        }

        public MilitaryTrainingLevelDefinition Get(MilitaryRank rank) => definitions.TryGetValue(rank, out var value)
            ? value : throw new KeyNotFoundException("Unknown military rank: " + rank);

        public IReadOnlyList<MilitaryTrainingLevelDefinition> All => definitions.Values.OrderBy(x => x.Rank).ToArray();

        public bool TryGetNext(MilitaryRank? current, out MilitaryTrainingLevelDefinition value)
        {
            var next = current.HasValue ? (int)current.Value + 1 : (int)MilitaryRank.Recruit;
            if (!Enum.IsDefined(typeof(MilitaryRank), next)) { value = null; return false; }
            value = Get((MilitaryRank)next);
            return true;
        }

        public static MilitaryTrainingCatalog CreateStoneAge() => new MilitaryTrainingCatalog(new[]
        {
            Level(MilitaryRank.Recruit, 4, 20, 2, 1),
            Level(MilitaryRank.Soldier, 8, 40, 4, 2),
            Level(MilitaryRank.Veteran, 14, 65, 7, 4),
            Level(MilitaryRank.Elite, 22, 90, 11, 7)
        });

        private static MilitaryTrainingLevelDefinition Level(MilitaryRank rank, int gameHours,
            int skill, long food, long wood) => new MilitaryTrainingLevelDefinition(rank,
            TimeSpan.FromHours(gameHours).Ticks, skill, new[]
            {
                new ResourceAmount(new ResourceId("berries"), new ResourceQuantity(food)),
                new ResourceAmount(new ResourceId("wood_log"), new ResourceQuantity(wood))
            });
    }

    public enum MilitaryTrainingStartFailure
    {
        None = 0,
        MissingCharacter = 1,
        NotAdult = 2,
        Incapacitated = 3,
        MissingBarracks = 4,
        BarracksInactive = 5,
        WrongOwner = 6,
        AlreadyTraining = 7,
        MaximumRank = 8,
        InsufficientResources = 9
    }

    public sealed class MilitaryTrainingRecordState
    {
        public MilitaryTrainingRecordState(StableEntityId characterId, StableEntityId barracksId,
            CombatSideId sideId, MilitaryRank rank, long startTick, long completionTick,
            bool completed, ulong sequence)
        {
            if (!characterId.IsValid || !barracksId.IsValid || !sideId.IsValid)
                throw new ArgumentException("Training references are required.");
            if (!Enum.IsDefined(typeof(MilitaryRank), rank)) throw new ArgumentOutOfRangeException(nameof(rank));
            // Diagnostic graduates are canonical instantaneous completions; scheduled training remains strictly later.
            if (startTick < 0 || completionTick < startTick || sequence == 0)
                throw new ArgumentOutOfRangeException(nameof(completionTick));
            CharacterId = characterId; BarracksId = barracksId; SideId = sideId; Rank = rank;
            StartTick = startTick; CompletionTick = completionTick; Completed = completed; Sequence = sequence;
        }
        public StableEntityId CharacterId { get; }
        public StableEntityId BarracksId { get; }
        public CombatSideId SideId { get; }
        public MilitaryRank Rank { get; }
        public long StartTick { get; }
        public long CompletionTick { get; }
        public bool Completed { get; }
        public ulong Sequence { get; }
    }

    public sealed class MilitaryTrainingSaveState
    {
        public MilitaryTrainingSaveState(IEnumerable<MilitaryTrainingRecordState> records, ulong sequence,
            long lastProcessedTick)
        {
            var values = (records ?? Enumerable.Empty<MilitaryTrainingRecordState>()).ToArray();
            if (lastProcessedTick < 0 || values.Any(x => x == null || x.Sequence > sequence ||
                x.StartTick > lastProcessedTick || x.Completed && x.CompletionTick > lastProcessedTick) ||
                values.Select(x => x.Sequence).Distinct().Count() != values.Length ||
                values.GroupBy(x => x.CharacterId).Any(x => x.Count(v => !v.Completed) > 1))
                throw new ArgumentException("Military training history is invalid.", nameof(records));
            Records = Array.AsReadOnly(values.OrderBy(x => x.Sequence).ToArray());
            Sequence = sequence; LastProcessedTick = lastProcessedTick;
        }
        public IReadOnlyList<MilitaryTrainingRecordState> Records { get; }
        public ulong Sequence { get; }
        public long LastProcessedTick { get; }
    }

    public enum MilitaryDirectTargetKind
    {
        EnemyMilitary = 1,
        EnemyMilitaryBuilding = 2,
        Area = 3,
        Civilian = 4
    }

    public enum MilitaryAttackDisabledReason
    {
        None = 0,
        NoAttackers = 1,
        AttackerIsCivilian = 2,
        MixedOrWrongSide = 3,
        FriendlyTarget = 4,
        CivilianDirectTargetForbidden = 5,
        NonMilitaryBuilding = 6,
        TargetMissing = 7,
        NoPhysicalPath = 8,
        UnitUnavailable = 9
    }

    public readonly struct MilitaryDirectTarget
    {
        public MilitaryDirectTarget(MilitaryDirectTargetKind kind, WorldPosition position,
            StableEntityId entityId = default)
        {
            if (!Enum.IsDefined(typeof(MilitaryDirectTargetKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind != MilitaryDirectTargetKind.Area && !entityId.IsValid)
                throw new ArgumentException("Entity target requires a stable ID.", nameof(entityId));
            Kind = kind; Position = position; EntityId = entityId;
        }
        public MilitaryDirectTargetKind Kind { get; }
        public WorldPosition Position { get; }
        public StableEntityId EntityId { get; }
    }

    public readonly struct MilitaryAttackAvailability
    {
        public MilitaryAttackAvailability(bool enabled, MilitaryAttackDisabledReason reason)
        { Enabled = enabled; Reason = reason; }
        public bool Enabled { get; }
        public MilitaryAttackDisabledReason Reason { get; }
    }

    public static class MilitaryDirectTargetRules
    {
        public static MilitaryAttackAvailability Evaluate(bool hasAttackers, bool allAttackersMilitary,
            bool sameAttackerSide, MilitaryDirectTargetKind targetKind, bool targetExists,
            bool hostile, bool militaryBuilding, bool physicalPath)
        {
            if (!hasAttackers) return Disabled(MilitaryAttackDisabledReason.NoAttackers);
            if (!allAttackersMilitary) return Disabled(MilitaryAttackDisabledReason.AttackerIsCivilian);
            if (!sameAttackerSide) return Disabled(MilitaryAttackDisabledReason.MixedOrWrongSide);
            if (!targetExists) return Disabled(MilitaryAttackDisabledReason.TargetMissing);
            if (targetKind == MilitaryDirectTargetKind.Civilian)
                return Disabled(MilitaryAttackDisabledReason.CivilianDirectTargetForbidden);
            if (targetKind != MilitaryDirectTargetKind.Area && !hostile)
                return Disabled(MilitaryAttackDisabledReason.FriendlyTarget);
            if (targetKind == MilitaryDirectTargetKind.EnemyMilitaryBuilding && !militaryBuilding)
                return Disabled(MilitaryAttackDisabledReason.NonMilitaryBuilding);
            if (!physicalPath) return Disabled(MilitaryAttackDisabledReason.NoPhysicalPath);
            return new MilitaryAttackAvailability(true, MilitaryAttackDisabledReason.None);
        }

        private static MilitaryAttackAvailability Disabled(MilitaryAttackDisabledReason reason) =>
            new MilitaryAttackAvailability(false, reason);
    }

    public sealed class MilitaryAttackOrderState
    {
        public MilitaryAttackOrderState(ulong sequence, MilitaryUnitId attackerUnitId,
            MilitaryDirectTarget target, long issuedTick, bool completed)
        {
            if (sequence == 0 || !attackerUnitId.IsValid || issuedTick < 0)
                throw new ArgumentException("Attack order identity is invalid.");
            Sequence = sequence; AttackerUnitId = attackerUnitId; Target = target;
            IssuedTick = issuedTick; Completed = completed;
        }
        public ulong Sequence { get; }
        public MilitaryUnitId AttackerUnitId { get; }
        public MilitaryDirectTarget Target { get; }
        public long IssuedTick { get; }
        public bool Completed { get; }
    }

    public enum SettlementCaptureClockStatus { Idle = 0, Advancing = 1, Frozen = 2, Completed = 3 }

    public sealed class SettlementCaptureOutcomeState
    {
        public SettlementCaptureOutcomeState(CombatSideId winnerSideId, long seed,
            IEnumerable<StableEntityId> joinedSoldierIds, IEnumerable<StableEntityId> executedSoldierIds,
            IEnumerable<StableEntityId> transferredBuildingIds, IEnumerable<StableEntityId> transferredResidentIds,
            long completionTick, StableEntityId resolutionId)
        {
            if (!winnerSideId.IsValid || !resolutionId.IsValid || completionTick < 0)
                throw new ArgumentException("Capture outcome identity is invalid.");
            WinnerSideId = winnerSideId; Seed = seed;
            JoinedSoldierIds = Copy(joinedSoldierIds); ExecutedSoldierIds = Copy(executedSoldierIds);
            TransferredBuildingIds = Copy(transferredBuildingIds); TransferredResidentIds = Copy(transferredResidentIds);
            if (JoinedSoldierIds.Intersect(ExecutedSoldierIds).Any())
                throw new ArgumentException("A soldier cannot both join and be executed.");
            CompletionTick = completionTick; ResolutionId = resolutionId;
        }
        public CombatSideId WinnerSideId { get; }
        public long Seed { get; }
        public IReadOnlyList<StableEntityId> JoinedSoldierIds { get; }
        public IReadOnlyList<StableEntityId> ExecutedSoldierIds { get; }
        public IReadOnlyList<StableEntityId> TransferredBuildingIds { get; }
        public IReadOnlyList<StableEntityId> TransferredResidentIds { get; }
        public long CompletionTick { get; }
        public StableEntityId ResolutionId { get; }
        private static IReadOnlyList<StableEntityId> Copy(IEnumerable<StableEntityId> source) => Array.AsReadOnly(
            (source ?? Enumerable.Empty<StableEntityId>()).Distinct().OrderBy(x => x).ToArray());
    }

    public sealed class SettlementCaptureState
    {
        public SettlementCaptureState(SettlementId settlementId, CombatSideId defenderSideId,
            CombatSideId? attackerSideId, StableEntityId mainBuildingId, StableEntityId? activeBarracksId,
            long progressTicks, long requiredTicks, long lastProcessedTick, SettlementCaptureClockStatus status,
            long seed, SettlementCaptureOutcomeState outcome = null)
        {
            if (!settlementId.IsValid || !defenderSideId.IsValid || !mainBuildingId.IsValid)
                throw new ArgumentException("Capture references are required.");
            if (attackerSideId.HasValue && !attackerSideId.Value.IsValid) throw new ArgumentException(nameof(attackerSideId));
            if (activeBarracksId.HasValue && !activeBarracksId.Value.IsValid) throw new ArgumentException(nameof(activeBarracksId));
            if (progressTicks < 0 || requiredTicks <= 0 || progressTicks > requiredTicks || lastProcessedTick < 0)
                throw new ArgumentOutOfRangeException(nameof(progressTicks));
            if (!Enum.IsDefined(typeof(SettlementCaptureClockStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if ((status == SettlementCaptureClockStatus.Completed) != (outcome != null))
                throw new ArgumentException("Completed capture state and outcome must agree.");
            SettlementId = settlementId; DefenderSideId = defenderSideId; AttackerSideId = attackerSideId;
            MainBuildingId = mainBuildingId; ActiveBarracksId = activeBarracksId; ProgressTicks = progressTicks;
            RequiredTicks = requiredTicks; LastProcessedTick = lastProcessedTick; Status = status; Seed = seed; Outcome = outcome;
        }
        public SettlementId SettlementId { get; }
        public CombatSideId DefenderSideId { get; }
        public CombatSideId? AttackerSideId { get; }
        public StableEntityId MainBuildingId { get; }
        public StableEntityId? ActiveBarracksId { get; }
        public long ProgressTicks { get; }
        public long RequiredTicks { get; }
        public long LastProcessedTick { get; }
        public SettlementCaptureClockStatus Status { get; }
        public long Seed { get; }
        public SettlementCaptureOutcomeState Outcome { get; }
    }

    public sealed class MilitaryCampaignSaveState
    {
        public MilitaryCampaignSaveState(MilitaryTrainingSaveState training,
            IEnumerable<SettlementCaptureState> captures,
            IEnumerable<MilitaryAttackOrderState> attackOrders = null, ulong attackSequence = 0)
        {
            Training = training ?? throw new ArgumentNullException(nameof(training));
            var values = (captures ?? Enumerable.Empty<SettlementCaptureState>()).ToArray();
            if (values.Any(x => x == null) || values.Select(x => x.SettlementId).Distinct().Count() != values.Length)
                throw new ArgumentException("Capture states must be unique.", nameof(captures));
            Captures = Array.AsReadOnly(values.OrderBy(x => x.SettlementId).ToArray());
            var orders = (attackOrders ?? Enumerable.Empty<MilitaryAttackOrderState>()).ToArray();
            if (orders.Any(x => x == null || x.Sequence > attackSequence) ||
                orders.Select(x => x.Sequence).Distinct().Count() != orders.Length)
                throw new ArgumentException("Attack order history is invalid.", nameof(attackOrders));
            AttackOrders = Array.AsReadOnly(orders.OrderBy(x => x.Sequence).ToArray());
            AttackSequence = attackSequence;
        }
        public MilitaryTrainingSaveState Training { get; }
        public IReadOnlyList<SettlementCaptureState> Captures { get; }
        public IReadOnlyList<MilitaryAttackOrderState> AttackOrders { get; }
        public ulong AttackSequence { get; }
    }
}
