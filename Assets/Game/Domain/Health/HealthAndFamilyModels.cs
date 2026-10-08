using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Game.Domain.Health
{
    public readonly struct HealthConditionId : IEquatable<HealthConditionId>, IComparable<HealthConditionId>
    {
        public HealthConditionId(string value)
        {
            if (!IsCanonical(value)) throw new ArgumentException("Condition ID must be canonical lowercase text.", nameof(value));
            Value = value;
        }
        public string Value { get; }
        public bool IsValid => IsCanonical(Value);
        public int CompareTo(HealthConditionId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(HealthConditionId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is HealthConditionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        private static bool IsCanonical(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
            value == value.Trim() && value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_');
    }

    public enum HealthConditionType { Injury = 1, Disease = 2, TemporaryCondition = 3, PermanentCondition = 4 }
    public enum HealthSeverity { Minor = 1, Moderate = 2, Serious = 3, Critical = 4 }
    public enum HealthConditionState { Active = 1, Improving = 2, Worsening = 3, Critical = 4, Recovered = 5, Permanent = 6, Fatal = 7 }

    public readonly struct HealthEffect
    {
        public HealthEffect(int workPerThousand, int movementPerThousand, int researchPerThousand,
            int combatPerThousand, bool incapacitated, bool fertilityEligible, int mortalityPermille)
        {
            ValidateModifier(workPerThousand, nameof(workPerThousand));
            ValidateModifier(movementPerThousand, nameof(movementPerThousand));
            ValidateModifier(researchPerThousand, nameof(researchPerThousand));
            ValidateModifier(combatPerThousand, nameof(combatPerThousand));
            if (mortalityPermille < 0 || mortalityPermille > 1000) throw new ArgumentOutOfRangeException(nameof(mortalityPermille));
            if (incapacitated && (workPerThousand != 0 || researchPerThousand != 0))
                throw new ArgumentException("Incapacitation must block work and research.");
            WorkPerThousand = workPerThousand;
            MovementPerThousand = movementPerThousand;
            ResearchPerThousand = researchPerThousand;
            CombatPerThousand = combatPerThousand;
            Incapacitated = incapacitated;
            FertilityEligible = fertilityEligible;
            MortalityPermille = mortalityPermille;
        }
        public int WorkPerThousand { get; }
        public int MovementPerThousand { get; }
        public int ResearchPerThousand { get; }
        public int CombatPerThousand { get; }
        public bool Incapacitated { get; }
        public bool FertilityEligible { get; }
        public int MortalityPermille { get; }
        private static void ValidateModifier(int value, string name)
        {
            if (value < 0 || value > 2000) throw new ArgumentOutOfRangeException(name);
        }
        public static HealthEffect Healthy => new HealthEffect(1000, 1000, 1000, 1000, false, true, 0);
    }

    public sealed class HealthConditionDefinition
    {
        public HealthConditionDefinition(HealthConditionId id, string displayName, HealthConditionType type,
            HealthSeverity severity, TimeSpan duration, HealthEffect effect, int worseningPermille = 0,
            int permanentOutcomePermille = 0)
        {
            if (!id.IsValid) throw new ArgumentException("Condition ID required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80 || displayName != displayName.Trim())
                throw new ArgumentException("Display name must be canonical.", nameof(displayName));
            if (!Enum.IsDefined(typeof(HealthConditionType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            if (!Enum.IsDefined(typeof(HealthSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            if (duration <= TimeSpan.Zero && type != HealthConditionType.PermanentCondition)
                throw new ArgumentOutOfRangeException(nameof(duration));
            if (worseningPermille < 0 || worseningPermille > 1000) throw new ArgumentOutOfRangeException(nameof(worseningPermille));
            if (permanentOutcomePermille < 0 || permanentOutcomePermille > 1000)
                throw new ArgumentOutOfRangeException(nameof(permanentOutcomePermille));
            Id = id; DisplayName = displayName; Type = type; InitialSeverity = severity; Duration = duration;
            Effect = effect; WorseningPermille = worseningPermille; PermanentOutcomePermille = permanentOutcomePermille;
        }
        public HealthConditionId Id { get; }
        public string DisplayName { get; }
        public HealthConditionType Type { get; }
        public HealthSeverity InitialSeverity { get; }
        public TimeSpan Duration { get; }
        public HealthEffect Effect { get; }
        public int WorseningPermille { get; }
        public int PermanentOutcomePermille { get; }
    }

    public sealed class HealthConditionCatalog
    {
        private readonly Dictionary<HealthConditionId, HealthConditionDefinition> definitions;
        public HealthConditionCatalog(IEnumerable<HealthConditionDefinition> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            definitions = new Dictionary<HealthConditionId, HealthConditionDefinition>();
            foreach (var value in values)
            {
                if (value == null) throw new ArgumentException("Condition definitions cannot contain null.", nameof(values));
                if (!definitions.TryAdd(value.Id, value)) throw new ArgumentException("Duplicate condition ID: " + value.Id, nameof(values));
            }
            if (definitions.Count == 0) throw new ArgumentException("At least one condition definition is required.", nameof(values));
        }
        public IReadOnlyList<HealthConditionDefinition> All => definitions.Values.OrderBy(x => x.Id).ToArray();
        public HealthConditionDefinition Get(HealthConditionId id) => definitions.TryGetValue(id, out var value)
            ? value : throw new KeyNotFoundException("Unknown health condition: " + id);

        public static HealthConditionCatalog CreateStoneAge() => new HealthConditionCatalog(new[]
        {
            new HealthConditionDefinition(new HealthConditionId("minor_cut"), "Minor cut", HealthConditionType.Injury,
                HealthSeverity.Minor, TimeSpan.FromDays(3), new HealthEffect(900, 950, 950, 900, false, true, 0), 20),
            new HealthConditionDefinition(new HealthConditionId("sprain"), "Sprain", HealthConditionType.Injury,
                HealthSeverity.Moderate, TimeSpan.FromDays(8), new HealthEffect(750, 600, 900, 650, false, true, 0), 30),
            new HealthConditionDefinition(new HealthConditionId("broken_bone"), "Broken bone", HealthConditionType.Injury,
                HealthSeverity.Serious, TimeSpan.FromDays(30), new HealthEffect(450, 300, 700, 300, false, false, 0), 50, 250),
            new HealthConditionDefinition(new HealthConditionId("serious_wound"), "Serious wound", HealthConditionType.Injury,
                HealthSeverity.Critical, TimeSpan.FromDays(12), new HealthEffect(0, 250, 0, 100, true, false, 240), 220, 350),
            new HealthConditionDefinition(new HealthConditionId("mild_fever"), "Mild fever", HealthConditionType.Disease,
                HealthSeverity.Minor, TimeSpan.FromDays(5), new HealthEffect(800, 850, 750, 700, false, true, 0), 45),
            new HealthConditionDefinition(new HealthConditionId("infection"), "Infection", HealthConditionType.Disease,
                HealthSeverity.Serious, TimeSpan.FromDays(10), new HealthEffect(400, 500, 450, 350, false, false, 90), 180, 100),
            new HealthConditionDefinition(new HealthConditionId("severe_fever"), "Severe fever", HealthConditionType.Disease,
                HealthSeverity.Critical, TimeSpan.FromDays(9), new HealthEffect(0, 300, 0, 150, true, false, 180), 180, 80),
            new HealthConditionDefinition(new HealthConditionId("weakened"), "Weakened", HealthConditionType.TemporaryCondition,
                HealthSeverity.Moderate, TimeSpan.FromDays(14), new HealthEffect(650, 750, 650, 600, false, false, 0)),
            new HealthConditionDefinition(new HealthConditionId("lasting_limp"), "Lasting limp", HealthConditionType.PermanentCondition,
                HealthSeverity.Moderate, TimeSpan.Zero, new HealthEffect(700, 600, 900, 650, false, true, 0)),
            new HealthConditionDefinition(new HealthConditionId("cold_stress"), "Cold stress", HealthConditionType.TemporaryCondition,
                HealthSeverity.Moderate, TimeSpan.FromDays(3), new HealthEffect(700, 800, 800, 650, false, false, 0), 25),
            new HealthConditionDefinition(new HealthConditionId("heat_stress"), "Heat stress", HealthConditionType.TemporaryCondition,
                HealthSeverity.Moderate, TimeSpan.FromDays(2), new HealthEffect(750, 850, 800, 700, false, false, 0), 20)
        });
    }

    public sealed class HealthConditionInstanceState
    {
        public HealthConditionInstanceState(StableEntityId instanceId, HealthConditionId definitionId,
            StableEntityId targetId, long startCalendarTick, long nextProgressCalendarTick, long resolveCalendarTick,
            HealthSeverity severity, HealthConditionState state, int progressionSteps, bool deathProcessed)
        {
            if (!instanceId.IsValid || !targetId.IsValid) throw new ArgumentException("Condition identities must be valid.");
            if (!definitionId.IsValid) throw new ArgumentException("Condition definition ID required.", nameof(definitionId));
            if (startCalendarTick < 0 || nextProgressCalendarTick < startCalendarTick || resolveCalendarTick < startCalendarTick)
                throw new ArgumentOutOfRangeException(nameof(startCalendarTick));
            if (!Enum.IsDefined(typeof(HealthSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));
            if (!Enum.IsDefined(typeof(HealthConditionState), state)) throw new ArgumentOutOfRangeException(nameof(state));
            if (progressionSteps < 0) throw new ArgumentOutOfRangeException(nameof(progressionSteps));
            InstanceId = instanceId; DefinitionId = definitionId; TargetId = targetId; StartCalendarTick = startCalendarTick;
            NextProgressCalendarTick = nextProgressCalendarTick; ResolveCalendarTick = resolveCalendarTick;
            Severity = severity; State = state; ProgressionSteps = progressionSteps; DeathProcessed = deathProcessed;
        }
        public StableEntityId InstanceId { get; }
        public HealthConditionId DefinitionId { get; }
        public StableEntityId TargetId { get; }
        public long StartCalendarTick { get; }
        public long NextProgressCalendarTick { get; }
        public long ResolveCalendarTick { get; }
        public HealthSeverity Severity { get; }
        public HealthConditionState State { get; }
        public int ProgressionSteps { get; }
        public bool DeathProcessed { get; }
    }

    public readonly struct CharacterCapabilities
    {
        public CharacterCapabilities(bool isAdult, bool incapacitated, int work, int movement, int research,
            int combat, bool fertilityEligible)
        {
            IsAdult = isAdult; IsIncapacitated = incapacitated; WorkPerThousand = work; MovementPerThousand = movement;
            ResearchPerThousand = research; CombatPerThousand = combat; FertilityEligible = fertilityEligible;
        }
        public bool IsAdult { get; }
        public bool IsIncapacitated { get; }
        public int WorkPerThousand { get; }
        public int MovementPerThousand { get; }
        public int ResearchPerThousand { get; }
        public int CombatPerThousand { get; }
        public bool FertilityEligible { get; }
        public bool CanPerformOrdinaryWork => IsAdult && !IsIncapacitated && WorkPerThousand > 0;
        public bool CanResearch => IsAdult && !IsIncapacitated && ResearchPerThousand > 0;
        public bool CanFight => IsAdult && !IsIncapacitated && CombatPerThousand > 0;
    }

    public interface ICharacterCapabilityProvider
    {
        CharacterCapabilities GetCapabilities(StableEntityId characterId);
    }

    public readonly struct CharacterEventId : IEquatable<CharacterEventId>, IComparable<CharacterEventId>
    {
        public CharacterEventId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64 || value != value.Trim() ||
                !value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_'))
                throw new ArgumentException("Event ID must be canonical lowercase text.", nameof(value));
            Value = value;
        }
        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);
        public int CompareTo(CharacterEventId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(CharacterEventId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CharacterEventId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }

    public enum CharacterEventCategory { Family = 1, Health = 2, Relationship = 3, Life = 4 }
    public sealed class CharacterEventDefinition
    {
        public CharacterEventDefinition(CharacterEventId id, CharacterEventCategory category, int relationshipDelta)
        {
            if (!id.IsValid) throw new ArgumentException("Event ID required.", nameof(id));
            if (!Enum.IsDefined(typeof(CharacterEventCategory), category)) throw new ArgumentOutOfRangeException(nameof(category));
            if (relationshipDelta < -100 || relationshipDelta > 100) throw new ArgumentOutOfRangeException(nameof(relationshipDelta));
            Id = id; Category = category; RelationshipDelta = relationshipDelta;
        }
        public CharacterEventId Id { get; }
        public CharacterEventCategory Category { get; }
        public int RelationshipDelta { get; }
    }

    public sealed class CharacterEventInstanceState
    {
        public CharacterEventInstanceState(StableEntityId instanceId, CharacterEventId definitionId,
            StableEntityId primaryId, StableEntityId secondaryId, long calendarTick, int appliedDelta, bool applied)
        {
            if (!instanceId.IsValid || !primaryId.IsValid || !secondaryId.IsValid || primaryId == secondaryId)
                throw new ArgumentException("Event identities must be valid and distinct.");
            if (!definitionId.IsValid) throw new ArgumentException("Event definition ID required.", nameof(definitionId));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (appliedDelta < -100 || appliedDelta > 100) throw new ArgumentOutOfRangeException(nameof(appliedDelta));
            InstanceId = instanceId; DefinitionId = definitionId; PrimaryId = primaryId; SecondaryId = secondaryId;
            CalendarTick = calendarTick; AppliedDelta = appliedDelta; Applied = applied;
        }
        public StableEntityId InstanceId { get; }
        public CharacterEventId DefinitionId { get; }
        public StableEntityId PrimaryId { get; }
        public StableEntityId SecondaryId { get; }
        public long CalendarTick { get; }
        public int AppliedDelta { get; }
        public bool Applied { get; }
    }

    public enum PregnancyStatus { Active = 1, BirthProcessed = 2, Cancelled = 3 }
    public sealed class PregnancyState
    {
        public PregnancyState(StableEntityId pregnancyId, StableEntityId motherId, StableEntityId fatherId,
            long conceptionCalendarTick, long conceptionBiologicalTick, long dueCalendarTick, long dueBiologicalTick,
            PregnancyStatus status, StableEntityId? childId)
        {
            if (!pregnancyId.IsValid || !motherId.IsValid || !fatherId.IsValid || motherId == fatherId)
                throw new ArgumentException("Pregnancy identities must be valid and distinct.");
            if (conceptionCalendarTick < 0 || conceptionBiologicalTick < 0 || dueCalendarTick <= conceptionCalendarTick ||
                dueBiologicalTick <= conceptionBiologicalTick) throw new ArgumentOutOfRangeException(nameof(conceptionCalendarTick));
            if (!Enum.IsDefined(typeof(PregnancyStatus), status)) throw new ArgumentOutOfRangeException(nameof(status));
            if ((status == PregnancyStatus.BirthProcessed) != childId.HasValue)
                throw new ArgumentException("Processed birth must have exactly one child identity.", nameof(childId));
            PregnancyId = pregnancyId; MotherId = motherId; FatherId = fatherId;
            ConceptionCalendarTick = conceptionCalendarTick; ConceptionBiologicalTick = conceptionBiologicalTick;
            DueCalendarTick = dueCalendarTick; DueBiologicalTick = dueBiologicalTick; Status = status; ChildId = childId;
        }
        public StableEntityId PregnancyId { get; }
        public StableEntityId MotherId { get; }
        public StableEntityId FatherId { get; }
        public long ConceptionCalendarTick { get; }
        public long ConceptionBiologicalTick { get; }
        public long DueCalendarTick { get; }
        public long DueBiologicalTick { get; }
        public PregnancyStatus Status { get; }
        public StableEntityId? ChildId { get; }
    }

    public sealed class HealthFamilySaveState
    {
        public HealthFamilySaveState(long worldSeed, long lastCalendarTick, long lastBiologicalTick,
            IEnumerable<HealthConditionInstanceState> conditions, IEnumerable<CharacterEventInstanceState> events,
            IEnumerable<PregnancyState> pregnancies)
        {
            if (lastCalendarTick < 0 || lastBiologicalTick < 0) throw new ArgumentOutOfRangeException(nameof(lastCalendarTick));
            WorldSeed = worldSeed; LastCalendarTick = lastCalendarTick; LastBiologicalTick = lastBiologicalTick;
            Conditions = Snapshot(conditions, x => x.InstanceId);
            Events = Snapshot(events, x => x.InstanceId);
            Pregnancies = Snapshot(pregnancies, x => x.PregnancyId);
        }
        public long WorldSeed { get; }
        public long LastCalendarTick { get; }
        public long LastBiologicalTick { get; }
        public IReadOnlyList<HealthConditionInstanceState> Conditions { get; }
        public IReadOnlyList<CharacterEventInstanceState> Events { get; }
        public IReadOnlyList<PregnancyState> Pregnancies { get; }

        private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T> values, Func<T, StableEntityId> key) where T : class
        {
            var array = (values ?? Array.Empty<T>()).OrderBy(key).ToArray();
            if (array.Any(x => x == null)) throw new ArgumentException("Save collections cannot contain null.");
            var ids = new HashSet<StableEntityId>();
            foreach (var value in array) if (!ids.Add(key(value))) throw new ArgumentException("Save IDs must be unique.");
            return new ReadOnlyCollection<T>(array);
        }
    }
}
