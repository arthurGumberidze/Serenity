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
        Private = 1,
        Sergeant = 2,
        WarrantOfficer = 3,
        Lieutenant = 4,
        Captain = 5,
        Major = 6,
        LieutenantColonel = 7,
        Colonel = 8,
        ColonelGeneral = 9,
        Generalissimo = 10,

        // Source-compatible aliases for saves/tests created before the ten-rank design.
        Recruit = Private,
        Soldier = Sergeant,
        Veteran = WarrantOfficer,
        Elite = Lieutenant
    }

    public sealed class MilitaryTrainingLevelDefinition
    {
        public MilitaryTrainingLevelDefinition(MilitaryRank rank, long durationTicks,
            int combatSkillLevel, IEnumerable<ResourceAmount> cost,
            int healthBonus = 0, int damagePerThousand = 1000,
            long minimumServiceTicks = 0, int requiredBattleExperience = 0)
        {
            if (!Enum.IsDefined(typeof(MilitaryRank), rank)) throw new ArgumentOutOfRangeException(nameof(rank));
            if (durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            if (combatSkillLevel < 0 || combatSkillLevel > 100)
                throw new ArgumentOutOfRangeException(nameof(combatSkillLevel));
            if (healthBonus < 0 || damagePerThousand < 1000 || damagePerThousand > 2000)
                throw new ArgumentOutOfRangeException(nameof(healthBonus));
            if (minimumServiceTicks < 0) throw new ArgumentOutOfRangeException(nameof(minimumServiceTicks));
            if (requiredBattleExperience < 0 || requiredBattleExperience > 100)
                throw new ArgumentOutOfRangeException(nameof(requiredBattleExperience));
            var values = (cost ?? throw new ArgumentNullException(nameof(cost))).ToArray();
            if (values.Any(x => !x.Quantity.IsPositive) ||
                values.Select(x => x.ResourceId).Distinct().Count() != values.Length)
                throw new ArgumentException("Training cost must be positive and unique.", nameof(cost));
            Rank = rank;
            DurationTicks = durationTicks;
            CombatSkillLevel = combatSkillLevel;
            Cost = Array.AsReadOnly(values.OrderBy(x => x.ResourceId).ToArray());
            HealthBonus = healthBonus;
            DamagePerThousand = damagePerThousand;
            MinimumServiceTicks = minimumServiceTicks;
            RequiredBattleExperience = requiredBattleExperience;
        }

        public MilitaryRank Rank { get; }
        public long DurationTicks { get; }
        public int CombatSkillLevel { get; }
        public IReadOnlyList<ResourceAmount> Cost { get; }
        public int HealthBonus { get; }
        public int DamagePerThousand { get; }
        public long MinimumServiceTicks { get; }
        public int RequiredBattleExperience { get; }
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
            var expected = Enum.GetValues(typeof(MilitaryRank)).Cast<MilitaryRank>().Distinct().OrderBy(x => x).ToArray();
            if (!copy.Select(x => x.Rank).OrderBy(x => x).SequenceEqual(expected))
                throw new ArgumentException("Training catalog must define every approved rank.", nameof(values));
            definitions = copy.ToDictionary(x => x.Rank);
        }

        public MilitaryTrainingLevelDefinition Get(MilitaryRank rank) => definitions.TryGetValue(rank, out var value)
            ? value : throw new KeyNotFoundException("Unknown military rank: " + rank);

        public IReadOnlyList<MilitaryTrainingLevelDefinition> All => definitions.Values.OrderBy(x => x.Rank).ToArray();

        public bool TryGetNext(MilitaryRank? current, out MilitaryTrainingLevelDefinition value)
        {
            var next = current.HasValue ? (int)current.Value + 1 : (int)MilitaryRank.Private;
            if (!Enum.IsDefined(typeof(MilitaryRank), next)) { value = null; return false; }
            value = Get((MilitaryRank)next);
            return true;
        }

        public static MilitaryTrainingCatalog CreateStoneAge() => new MilitaryTrainingCatalog(new[]
        {
            Level(MilitaryRank.Private, 4, 18, 0, 0, 0, 1000, Equipment("stone_spear", 1), Equipment("leather_clothing", 1)),
            Level(MilitaryRank.Sergeant, 8, 28, 2, 0, 5, 1040, Equipment("wooden_shield", 1), Equipment("arrows", 6)),
            Level(MilitaryRank.WarrantOfficer, 12, 38, 4, 0, 10, 1080, Equipment("combat_bow", 1), Equipment("arrows", 12)),
            Level(MilitaryRank.Lieutenant, 18, 48, 7, 0, 15, 1120, Equipment("stone_spear", 1), Equipment("wooden_shield", 1)),
            Level(MilitaryRank.Captain, 24, 58, 10, 4, 20, 1160, Equipment("leather_clothing", 1), Equipment("arrows", 12)),
            Level(MilitaryRank.Major, 32, 68, 14, 6, 25, 1200, Equipment("stone_spear", 1), Equipment("arrows", 12)),
            Level(MilitaryRank.LieutenantColonel, 42, 76, 21, 8, 30, 1250, Equipment("wooden_shield", 1), Equipment("leather_clothing", 1)),
            Level(MilitaryRank.Colonel, 54, 84, 30, 10, 35, 1300, Equipment("combat_bow", 1), Equipment("arrows", 18)),
            Level(MilitaryRank.ColonelGeneral, 72, 92, 45, 12, 40, 1380, Equipment("stone_spear", 1), Equipment("leather_clothing", 1)),
            Level(MilitaryRank.Generalissimo, 96, 100, 60, 16, 50, 1500, Equipment("stone_spear", 1), Equipment("wooden_shield", 1))
        });

        private static MilitaryTrainingLevelDefinition Level(MilitaryRank rank, int gameHours,
            int skill, int serviceDays, int battleExperience, int healthBonus, int damagePerThousand,
            params ResourceAmount[] cost) =>
            new MilitaryTrainingLevelDefinition(rank, TimeSpan.FromHours(gameHours).Ticks, skill,
                cost, healthBonus, damagePerThousand, TimeSpan.FromDays(serviceDays).Ticks, battleExperience);

        private static ResourceAmount Equipment(string id, long units) =>
            new ResourceAmount(new ResourceId(id), new ResourceQuantity(units));
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
        InsufficientResources = 9,
        BarracksFull = 10,
        RankLimitReached = 11,
        ReservedForRuler = 12,
        InsufficientService = 13,
        InsufficientBattleExperience = 14
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
