using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Health;
using Game.Domain.Military;

namespace Game.Domain.Combat
{
    public readonly struct CombatSideId : IEquatable<CombatSideId>, IComparable<CombatSideId>
    {
        public CombatSideId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value != value.Trim() ||
                !value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_'))
                throw new ArgumentException("Combat side ID must be canonical lowercase text.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public int CompareTo(CombatSideId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(CombatSideId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CombatSideId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CombatSideId left, CombatSideId right) => left.Equals(right);
        public static bool operator !=(CombatSideId left, CombatSideId right) => !left.Equals(right);
    }

    public readonly struct CombatWeaponId : IEquatable<CombatWeaponId>, IComparable<CombatWeaponId>
    {
        public CombatWeaponId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value != value.Trim() ||
                !value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_'))
                throw new ArgumentException("Weapon ID must be canonical lowercase text.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public int CompareTo(CombatWeaponId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(CombatWeaponId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CombatWeaponId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public sealed class CombatWeaponProfile
    {
        public CombatWeaponProfile(CombatWeaponId id, string displayName, int attackBonus, int defenseBonus,
            int rangeMillimetres, long attackIntervalTicks, HealthConditionId minimumInjury)
        {
            if (!id.IsValid) throw new ArgumentException("Weapon ID required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80 || displayName != displayName.Trim())
                throw new ArgumentException("Weapon display name must be canonical.", nameof(displayName));
            if (attackBonus < 0 || defenseBonus < 0) throw new ArgumentOutOfRangeException(nameof(attackBonus));
            if (rangeMillimetres < 500 || rangeMillimetres > 10000) throw new ArgumentOutOfRangeException(nameof(rangeMillimetres));
            if (attackIntervalTicks <= 0) throw new ArgumentOutOfRangeException(nameof(attackIntervalTicks));
            if (!minimumInjury.IsValid) throw new ArgumentException("Minimum injury required.", nameof(minimumInjury));
            Id = id;
            DisplayName = displayName;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            RangeMillimetres = rangeMillimetres;
            AttackIntervalTicks = attackIntervalTicks;
            MinimumInjury = minimumInjury;
        }

        public CombatWeaponId Id { get; }
        public string DisplayName { get; }
        public int AttackBonus { get; }
        public int DefenseBonus { get; }
        public int RangeMillimetres { get; }
        public float RangeMetres => RangeMillimetres / 1000f;
        public long AttackIntervalTicks { get; }
        public HealthConditionId MinimumInjury { get; }
    }

    public sealed class CombatWeaponCatalog
    {
        private readonly Dictionary<CombatWeaponId, CombatWeaponProfile> values;

        public CombatWeaponCatalog(IEnumerable<CombatWeaponProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));
            values = new Dictionary<CombatWeaponId, CombatWeaponProfile>();
            foreach (var profile in profiles)
            {
                if (profile == null) throw new ArgumentException("Weapon profiles cannot contain null.", nameof(profiles));
                if (!values.TryAdd(profile.Id, profile)) throw new ArgumentException("Duplicate weapon ID: " + profile.Id);
            }
            if (values.Count == 0) throw new ArgumentException("At least one weapon profile is required.", nameof(profiles));
        }

        public IReadOnlyList<CombatWeaponProfile> All => values.Values.OrderBy(x => x.Id).ToArray();
        public CombatWeaponProfile Get(CombatWeaponId id) => values.TryGetValue(id, out var value)
            ? value : throw new KeyNotFoundException("Unknown combat weapon: " + id);

        public static CombatWeaponCatalog CreateStoneAge() => new CombatWeaponCatalog(new[]
        {
            new CombatWeaponProfile(new CombatWeaponId("unarmed"), "Unarmed", 40, 0, 1200,
                TimeSpan.FromMinutes(4).Ticks, new HealthConditionId("minor_cut")),
            new CombatWeaponProfile(new CombatWeaponId("stone_axe"), "Stone axe", 260, 20, 1500,
                TimeSpan.FromMinutes(5).Ticks, new HealthConditionId("sprain")),
            new CombatWeaponProfile(new CombatWeaponId("stone_spear"), "Stone spear", 220, 35, 2200,
                TimeSpan.FromMinutes(4).Ticks, new HealthConditionId("sprain"))
        });
    }

    public enum CombatEncounterStatus { Active = 1, Finished = 2, Cancelled = 3 }
    public enum CombatOutcome { None = 0, SideAVictory = 1, SideBVictory = 2, MutualDisengagement = 3, Cancelled = 4 }
    public enum CombatantState { Fighting = 1, Approaching = 2, Attacking = 3, Breaking = 4, Retreating = 5, Disengaged = 6, Incapacitated = 7, Dead = 8 }
    public enum BattleMode { Physical = 1, Abstract = 2 }
    public enum BattleEventKind { Started = 1, ModeChanged = 2, SideBroken = 3, CaptainDeath = 4, Finished = 5 }

    public sealed class BattleEventState
    {
        public BattleEventState(ulong sequence, long calendarTick, BattleEventKind kind,
            StableEntityId? characterId = null, CombatSideId? sideId = null, BattleMode? mode = null)
        {
            if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!Enum.IsDefined(typeof(BattleEventKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (characterId.HasValue && !characterId.Value.IsValid)
                throw new ArgumentException("Optional battle-event character ID must be valid.", nameof(characterId));
            if (sideId.HasValue && !sideId.Value.IsValid)
                throw new ArgumentException("Optional battle-event side ID must be valid.", nameof(sideId));
            if (mode.HasValue && !Enum.IsDefined(typeof(BattleMode), mode.Value))
                throw new ArgumentOutOfRangeException(nameof(mode));
            Sequence = sequence;
            CalendarTick = calendarTick;
            Kind = kind;
            CharacterId = characterId;
            SideId = sideId;
            Mode = mode;
        }

        public ulong Sequence { get; }
        public long CalendarTick { get; }
        public BattleEventKind Kind { get; }
        public StableEntityId? CharacterId { get; }
        public CombatSideId? SideId { get; }
        public BattleMode? Mode { get; }
    }

    public sealed class CombatantCombatState
    {
        public CombatantCombatState(StableEntityId characterId, CombatSideId sideId, CombatWeaponId weaponId,
            CombatantState state, StableEntityId? targetId, StableEntityId? assignedTargetId,
            long nextAttackTick, ulong attackSequence, int morale, WorldPosition retreatPoint,
            string recentInjury, bool casualtyPenaltyApplied)
        {
            if (!characterId.IsValid) throw new ArgumentException("Combatant character ID required.", nameof(characterId));
            if (!sideId.IsValid) throw new ArgumentException("Combat side ID required.", nameof(sideId));
            if (!weaponId.IsValid) throw new ArgumentException("Weapon ID required.", nameof(weaponId));
            if (!Enum.IsDefined(typeof(CombatantState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            ValidateOptional(targetId, nameof(targetId));
            ValidateOptional(assignedTargetId, nameof(assignedTargetId));
            if (targetId == characterId || assignedTargetId == characterId)
                throw new ArgumentException("A combatant cannot target itself.");
            if (nextAttackTick < 0) throw new ArgumentOutOfRangeException(nameof(nextAttackTick));
            if (morale < 0 || morale > 1000) throw new ArgumentOutOfRangeException(nameof(morale));
            if (recentInjury != null && recentInjury.Length > 64) throw new ArgumentException("Recent injury text is too long.", nameof(recentInjury));
            CharacterId = characterId;
            SideId = sideId;
            WeaponId = weaponId;
            State = state;
            TargetId = targetId;
            AssignedTargetId = assignedTargetId;
            NextAttackTick = nextAttackTick;
            AttackSequence = attackSequence;
            Morale = morale;
            RetreatPoint = retreatPoint;
            RecentInjury = recentInjury ?? string.Empty;
            CasualtyPenaltyApplied = casualtyPenaltyApplied;
        }

        public StableEntityId CharacterId { get; }
        public CombatSideId SideId { get; }
        public CombatWeaponId WeaponId { get; }
        public CombatantState State { get; }
        public StableEntityId? TargetId { get; }
        public StableEntityId? AssignedTargetId { get; }
        public long NextAttackTick { get; }
        public ulong AttackSequence { get; }
        public int Morale { get; }
        public WorldPosition RetreatPoint { get; }
        public string RecentInjury { get; }
        public bool CasualtyPenaltyApplied { get; }

        public CombatantCombatState With(CombatantState? state = null, StableEntityId? targetId = null,
            bool clearTarget = false, StableEntityId? assignedTargetId = null, bool clearAssignedTarget = false,
            long? nextAttackTick = null, ulong? attackSequence = null, int? morale = null,
            string recentInjury = null, bool? casualtyPenaltyApplied = null) =>
            new CombatantCombatState(CharacterId, SideId, WeaponId, state ?? State,
                clearTarget ? (StableEntityId?)null : targetId ?? TargetId,
                clearAssignedTarget ? (StableEntityId?)null : assignedTargetId ?? AssignedTargetId,
                nextAttackTick ?? NextAttackTick, attackSequence ?? AttackSequence,
                morale ?? Morale, RetreatPoint, recentInjury ?? RecentInjury,
                casualtyPenaltyApplied ?? CasualtyPenaltyApplied);

        private static void ValidateOptional(StableEntityId? id, string name)
        {
            if (id.HasValue && !id.Value.IsValid) throw new ArgumentException("Optional ID must be valid.", name);
        }
    }

    public sealed class CombatAttackRecordState
    {
        public CombatAttackRecordState(StableEntityId attackerId, StableEntityId defenderId, ulong attackSequence,
            long calendarTick, HealthConditionId injuryId, bool applied, bool fatal)
        {
            if (!attackerId.IsValid || !defenderId.IsValid || attackerId == defenderId)
                throw new ArgumentException("Attack identities must be valid and distinct.");
            if (attackSequence == 0) throw new ArgumentOutOfRangeException(nameof(attackSequence));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!injuryId.IsValid) throw new ArgumentException("Attack injury ID required.", nameof(injuryId));
            AttackerId = attackerId;
            DefenderId = defenderId;
            AttackSequence = attackSequence;
            CalendarTick = calendarTick;
            InjuryId = injuryId;
            Applied = applied;
            Fatal = fatal;
        }

        public StableEntityId AttackerId { get; }
        public StableEntityId DefenderId { get; }
        public ulong AttackSequence { get; }
        public long CalendarTick { get; }
        public HealthConditionId InjuryId { get; }
        public bool Applied { get; }
        public bool Fatal { get; }
    }

    public sealed class CombatEncounterState
    {
        public CombatEncounterState(StableEntityId encounterId, CombatSideId sideA, CombatSideId sideB,
            long startCalendarTick, long lastProcessedCalendarTick, CombatEncounterStatus status,
            CombatOutcome outcome, IEnumerable<CombatantCombatState> combatants,
            IEnumerable<CombatAttackRecordState> attackHistory, BattleMode mode = BattleMode.Physical,
            MilitaryUnitId? sideAUnitId = null, MilitaryUnitId? sideBUnitId = null,
            IEnumerable<BattleEventState> battleHistory = null)
        {
            if (!encounterId.IsValid) throw new ArgumentException("Encounter ID required.", nameof(encounterId));
            if (!sideA.IsValid || !sideB.IsValid || sideA == sideB) throw new ArgumentException("Two distinct combat sides are required.");
            if (startCalendarTick < 0 || lastProcessedCalendarTick < startCalendarTick)
                throw new ArgumentOutOfRangeException(nameof(startCalendarTick));
            if (!Enum.IsDefined(typeof(CombatEncounterStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if (!Enum.IsDefined(typeof(CombatOutcome), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
            if (!Enum.IsDefined(typeof(BattleMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if ((status == CombatEncounterStatus.Active) != (outcome == CombatOutcome.None))
                throw new ArgumentException("Only an active encounter may have no outcome.");
            if (sideAUnitId.HasValue && !sideAUnitId.Value.IsValid)
                throw new ArgumentException("Side A unit ID must be valid.", nameof(sideAUnitId));
            if (sideBUnitId.HasValue && !sideBUnitId.Value.IsValid)
                throw new ArgumentException("Side B unit ID must be valid.", nameof(sideBUnitId));
            if (sideAUnitId.HasValue && sideBUnitId.HasValue && sideAUnitId.Value == sideBUnitId.Value)
                throw new ArgumentException("Battle sides require distinct military units.");
            EncounterId = encounterId;
            SideA = sideA;
            SideB = sideB;
            StartCalendarTick = startCalendarTick;
            LastProcessedCalendarTick = lastProcessedCalendarTick;
            Status = status;
            Outcome = outcome;
            Mode = mode;
            SideAUnitId = sideAUnitId;
            SideBUnitId = sideBUnitId;
            Combatants = SnapshotCombatants(combatants, sideA, sideB);
            AttackHistory = new ReadOnlyCollection<CombatAttackRecordState>((attackHistory ?? Array.Empty<CombatAttackRecordState>()).ToArray());
            if (AttackHistory.Any(x => x == null)) throw new ArgumentException("Attack history cannot contain null.");
            var events = (battleHistory ?? Array.Empty<BattleEventState>()).OrderBy(x => x.Sequence).ToArray();
            if (events.Any(x => x == null) || events.Select(x => x.Sequence).Distinct().Count() != events.Length)
                throw new ArgumentException("Battle history entries must be non-null with unique sequences.");
            BattleHistory = new ReadOnlyCollection<BattleEventState>(events);
        }

        public StableEntityId BattleId => EncounterId;
        public StableEntityId EncounterId { get; }
        public CombatSideId SideA { get; }
        public CombatSideId SideB { get; }
        public long StartCalendarTick { get; }
        public long LastProcessedCalendarTick { get; }
        public CombatEncounterStatus Status { get; }
        public CombatOutcome Outcome { get; }
        public BattleMode Mode { get; }
        public MilitaryUnitId? SideAUnitId { get; }
        public MilitaryUnitId? SideBUnitId { get; }
        public IReadOnlyList<CombatantCombatState> Combatants { get; }
        public IReadOnlyList<CombatAttackRecordState> AttackHistory { get; }
        public IReadOnlyList<BattleEventState> BattleHistory { get; }

        private static IReadOnlyList<CombatantCombatState> SnapshotCombatants(IEnumerable<CombatantCombatState> values,
            CombatSideId sideA, CombatSideId sideB)
        {
            var array = (values ?? throw new ArgumentNullException(nameof(values))).OrderBy(x => x.CharacterId).ToArray();
            if (array.Length < 2 || array.Any(x => x == null)) throw new ArgumentException("Encounter requires combatants.");
            var ids = new HashSet<StableEntityId>();
            foreach (var value in array)
            {
                if (!ids.Add(value.CharacterId)) throw new ArgumentException("Duplicate combat participant.");
                if (value.SideId != sideA && value.SideId != sideB) throw new ArgumentException("Combatant side is not part of the encounter.");
            }
            if (!array.Any(x => x.SideId == sideA) || !array.Any(x => x.SideId == sideB))
                throw new ArgumentException("Both combat sides require participants.");
            return new ReadOnlyCollection<CombatantCombatState>(array);
        }
    }

    public sealed class CombatSaveState
    {
        public CombatSaveState(long worldSeed, long lastCalendarTick, IEnumerable<CombatEncounterState> encounters)
        {
            if (lastCalendarTick < 0) throw new ArgumentOutOfRangeException(nameof(lastCalendarTick));
            WorldSeed = worldSeed;
            LastCalendarTick = lastCalendarTick;
            var array = (encounters ?? Array.Empty<CombatEncounterState>()).OrderBy(x => x.EncounterId).ToArray();
            if (array.Any(x => x == null)) throw new ArgumentException("Combat saves cannot contain null encounters.");
            var ids = new HashSet<StableEntityId>();
            if (array.Any(x => !ids.Add(x.EncounterId))) throw new ArgumentException("Encounter IDs must be unique.");
            Encounters = new ReadOnlyCollection<CombatEncounterState>(array);
        }

        public long WorldSeed { get; }
        public long LastCalendarTick { get; }
        public IReadOnlyList<CombatEncounterState> Encounters { get; }
    }
}
