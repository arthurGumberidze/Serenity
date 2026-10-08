using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Simulation.Dynasties;
using Game.Simulation.Tiers;

namespace Game.Simulation.Health
{
    public readonly struct HealthAdvanceResult
    {
        public HealthAdvanceResult(int conditionTransitions, int deaths, int births, ulong stateHash)
        {
            ConditionTransitions = conditionTransitions; Deaths = deaths; Births = births; StateHash = stateHash;
        }
        public int ConditionTransitions { get; }
        public int Deaths { get; }
        public int Births { get; }
        public ulong StateHash { get; }
    }

    /// <summary>
    /// Canonical engine-free authority for U18 conditions, narrow character events and pregnancy.
    /// It advances in deterministic batches and never owns a GameObject/ECS identity.
    /// </summary>
    public sealed class HealthAndFamilyService : ICharacterCapabilityProvider
    {
        public const int AdultAgeYears = 16;
        public const int MaximumMaternalAgeYears = 50;
        public const uint ConditionProgressionStream = 0x55313801u;
        public const uint ConditionResolutionStream = 0x55313802u;
        public const uint BirthIdentityStream = 0x55313803u;
        public const uint BirthSexStream = 0x55313804u;
        // 750 biological days / 400 biological multiplier = 1.875 calendar days = 45 real minutes at x1.
        private static readonly TimeSpan DefaultPregnancyDuration = TimeSpan.FromDays(750);
        private static readonly TimeSpan ProgressionStep = TimeSpan.FromDays(1);

        private readonly CharacterRegistry characters;
        private readonly HealthConditionCatalog catalog;
        private readonly long worldSeed;
        private readonly long pregnancyDurationBiologicalTicks;
        private readonly int biologicalTicksPerCalendarTick;
        private readonly Dictionary<StableEntityId, HealthConditionInstanceState> conditions =
            new Dictionary<StableEntityId, HealthConditionInstanceState>();
        private readonly Dictionary<StableEntityId, CharacterEventInstanceState> events =
            new Dictionary<StableEntityId, CharacterEventInstanceState>();
        private readonly Dictionary<StableEntityId, PregnancyState> pregnancies =
            new Dictionary<StableEntityId, PregnancyState>();
        private readonly Dictionary<CharacterEventId, CharacterEventDefinition> eventDefinitions;
        private SuccessionService succession;

        public HealthAndFamilyService(CharacterRegistry characterRegistry, HealthConditionCatalog conditionCatalog,
            long simulationWorldSeed, SuccessionService successionService = null,
            TimeSpan? pregnancyDuration = null, int biologicalMultiplier = 400)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            catalog = conditionCatalog ?? throw new ArgumentNullException(nameof(conditionCatalog));
            if (biologicalMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(biologicalMultiplier));
            var duration = pregnancyDuration ?? DefaultPregnancyDuration;
            if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pregnancyDuration));
            worldSeed = simulationWorldSeed;
            pregnancyDurationBiologicalTicks = duration.Ticks;
            biologicalTicksPerCalendarTick = biologicalMultiplier;
            succession = successionService;
            eventDefinitions = CreateEventDefinitions().ToDictionary(x => x.Id);
        }

        public event Action<Character> ChildBorn;
        public HealthConditionCatalog Catalog => catalog;
        public long WorldSeed => worldSeed;
        public long LastCalendarTick { get; private set; }
        public long LastBiologicalTick { get; private set; }
        public IReadOnlyList<HealthConditionInstanceState> Conditions => conditions.Values.OrderBy(x => x.InstanceId).ToArray();
        public IReadOnlyList<CharacterEventInstanceState> Events => events.Values.OrderBy(x => x.InstanceId).ToArray();
        public IReadOnlyList<PregnancyState> Pregnancies => pregnancies.Values.OrderBy(x => x.PregnancyId).ToArray();
        public IReadOnlyList<CharacterEventDefinition> EventDefinitions => eventDefinitions.Values.OrderBy(x => x.Id).ToArray();

        public void BindSuccession(SuccessionService service)
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (succession != null && !ReferenceEquals(succession, service))
                throw new InvalidOperationException("Health service is already bound to another succession authority.");
            succession = service;
        }

        public HealthConditionInstanceState ApplyCondition(StableEntityId targetId, HealthConditionId definitionId,
            long startCalendarTick)
        {
            if (startCalendarTick < LastCalendarTick) throw new ArgumentOutOfRangeException(nameof(startCalendarTick));
            var target = characters.Get(targetId);
            if (target.LifeState != CharacterLifeState.Alive) throw new InvalidOperationException("Cannot add a condition to a dead character.");
            var definition = catalog.Get(definitionId);
            if (conditions.Values.Any(x => x.TargetId == targetId && x.DefinitionId.Equals(definitionId) && IsActive(x.State)))
                throw new InvalidOperationException("The same active condition already exists on this character.");
            var id = DerivedId("condition:" + definitionId, targetId, default, startCalendarTick);
            if (conditions.ContainsKey(id)) throw new InvalidOperationException("Deterministic condition ID collision.");
            var permanent = definition.Type == HealthConditionType.PermanentCondition;
            var resolve = permanent ? startCalendarTick : checked(startCalendarTick + definition.Duration.Ticks);
            var state = new HealthConditionInstanceState(id, definitionId, targetId, startCalendarTick,
                checked(startCalendarTick + ProgressionStep.Ticks), resolve, definition.InitialSeverity,
                permanent ? HealthConditionState.Permanent : HealthConditionState.Active, 0, false);
            conditions.Add(id, state);
            return state;
        }

        /// <summary>
        /// U20 combat entry point. Repeated injuries escalate through the existing U18 definitions;
        /// the health/succession authorities remain the only writers of incapacity and death.
        /// </summary>
        public CombatInjuryApplication ApplyCombatInjury(StableEntityId targetId,
            HealthConditionId requestedDefinitionId, long calendarTick, long biologicalTick,
            bool forceFatal = false)
        {
            if (calendarTick < LastCalendarTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (biologicalTick < LastBiologicalTick) throw new ArgumentOutOfRangeException(nameof(biologicalTick));
            var target = characters.Get(targetId);
            catalog.Get(requestedDefinitionId);
            if (target.LifeState != CharacterLifeState.Alive)
                return new CombatInjuryApplication(requestedDefinitionId, false, true, true);
            if (forceFatal)
            {
                ProcessHealthDeath(targetId, biologicalTick, calendarTick);
                return new CombatInjuryApplication(requestedDefinitionId, true, true, true);
            }

            var escalation = new[]
            {
                new HealthConditionId("minor_cut"),
                new HealthConditionId("sprain"),
                new HealthConditionId("broken_bone"),
                new HealthConditionId("serious_wound")
            };
            var requestedIndex = Array.FindIndex(escalation, x => x.Equals(requestedDefinitionId));
            if (requestedIndex < 0) throw new ArgumentException("Combat injuries must use an existing U18 injury definition.", nameof(requestedDefinitionId));
            var active = new HashSet<HealthConditionId>(conditions.Values.Where(x => x.TargetId == targetId && IsActive(x.State))
                .Select(x => x.DefinitionId));
            for (var i = requestedIndex; i < escalation.Length; i++)
            {
                if (active.Contains(escalation[i])) continue;
                ApplyCondition(targetId, escalation[i], calendarTick);
                var capability = GetCapabilitiesAt(targetId, biologicalTick);
                return new CombatInjuryApplication(escalation[i], true, capability.IsIncapacitated, false);
            }

            // A further damaging blow after the full injury ladder is fatal. This is a combat rule,
            // but death still crosses the existing U17 succession boundary exactly once.
            ProcessHealthDeath(targetId, biologicalTick, calendarTick);
            return new CombatInjuryApplication(new HealthConditionId("serious_wound"), true, true, true);
        }

        public int RecoverAll(StableEntityId targetId)
        {
            characters.Get(targetId);
            var recovered = 0;
            foreach (var value in conditions.Values.Where(x => x.TargetId == targetId && IsTemporaryActive(x.State)).ToArray())
            {
                conditions[value.InstanceId] = CopyCondition(value, state: HealthConditionState.Recovered);
                recovered++;
            }
            return recovered;
        }

        public bool ApplyRelationshipEvent(CharacterEventId definitionId, StableEntityId primaryId,
            StableEntityId secondaryId, long calendarTick)
        {
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!eventDefinitions.TryGetValue(definitionId, out var definition))
                throw new KeyNotFoundException("Unknown character event: " + definitionId);
            var primary = characters.Get(primaryId);
            var secondary = characters.Get(secondaryId);
            if (primaryId == secondaryId) throw new ArgumentException("An event requires two different characters.");
            if (!AreFamilyRelated(primary, secondary))
                throw new InvalidOperationException("U18 family events require a spouse or parent-child relationship.");
            var instanceId = DerivedId("event:" + definitionId, primaryId, secondaryId, calendarTick);
            if (events.ContainsKey(instanceId)) return false;
            var current = primary.TryGetRelationship(secondaryId, out var relation) ? relation.Affinity.Value : 0;
            var updated = Math.Max(RelationshipScore.Minimum,
                Math.Min(RelationshipScore.Maximum, current + definition.RelationshipDelta));
            var appliedDelta = updated - current;
            primary.SetRelationship(new RelationshipState(secondaryId, new RelationshipScore(updated)));
            var reverseCurrent = secondary.TryGetRelationship(primaryId, out var reverse) ? reverse.Affinity.Value : 0;
            var reverseUpdated = Math.Max(RelationshipScore.Minimum,
                Math.Min(RelationshipScore.Maximum, reverseCurrent + definition.RelationshipDelta));
            secondary.SetRelationship(new RelationshipState(primaryId, new RelationshipScore(reverseUpdated)));
            events.Add(instanceId, new CharacterEventInstanceState(instanceId, definitionId, primaryId,
                secondaryId, calendarTick, appliedDelta, true));
            return true;
        }

        public PregnancyState StartPregnancy(StableEntityId motherId, StableEntityId fatherId,
            long conceptionCalendarTick, long conceptionBiologicalTick)
        {
            if (conceptionCalendarTick < LastCalendarTick || conceptionBiologicalTick < LastBiologicalTick)
                throw new ArgumentOutOfRangeException(nameof(conceptionCalendarTick));
            var mother = characters.Get(motherId);
            var father = characters.Get(fatherId);
            if (motherId == fatherId) throw new ArgumentException("Pregnancy parents must be different characters.");
            if (mother.Sex != CharacterSex.Female || father.Sex != CharacterSex.Male)
                throw new InvalidOperationException("The MVP pregnancy roles require a female mother and male father.");
            if (mother.LifeState != CharacterLifeState.Alive || father.LifeState != CharacterLifeState.Alive)
                throw new InvalidOperationException("Pregnancy parents must be alive.");
            if (mother.SpouseId != fatherId || father.SpouseId != motherId)
                throw new InvalidOperationException("The MVP pregnancy flow requires mutual spouses.");
            if (pregnancies.Values.Any(x => x.MotherId == motherId && x.Status == PregnancyStatus.Active))
                throw new InvalidOperationException("The mother already has an active pregnancy.");
            var motherAge = mother.AgeInCompletedYears(conceptionBiologicalTick);
            var fatherAge = father.AgeInCompletedYears(conceptionBiologicalTick);
            if (motherAge < AdultAgeYears || motherAge > MaximumMaternalAgeYears || fatherAge < AdultAgeYears)
                throw new InvalidOperationException("Pregnancy parents must satisfy the configured adult age range.");
            if (!GetCapabilitiesAt(motherId, conceptionBiologicalTick).FertilityEligible ||
                !GetCapabilitiesAt(fatherId, conceptionBiologicalTick).FertilityEligible)
                throw new InvalidOperationException("A severe health condition prevents pregnancy.");
            var dueBiological = checked(conceptionBiologicalTick + pregnancyDurationBiologicalTicks);
            var dueCalendar = checked(conceptionCalendarTick +
                Math.Max(1L, pregnancyDurationBiologicalTicks / biologicalTicksPerCalendarTick));
            var id = DerivedId("pregnancy", motherId, fatherId, conceptionCalendarTick ^ conceptionBiologicalTick);
            if (pregnancies.ContainsKey(id)) throw new InvalidOperationException("Deterministic pregnancy ID collision.");
            var pregnancy = new PregnancyState(id, motherId, fatherId, conceptionCalendarTick,
                conceptionBiologicalTick, dueCalendar, dueBiological, PregnancyStatus.Active, null);
            pregnancies.Add(id, pregnancy);
            return pregnancy;
        }

        public HealthAdvanceResult AdvanceTo(long calendarTick, long biologicalTick)
        {
            if (calendarTick < LastCalendarTick || biologicalTick < LastBiologicalTick)
                throw new ArgumentOutOfRangeException(nameof(calendarTick), "Health/family time cannot move backwards.");
            var transitions = 0;
            var deaths = 0;
            foreach (var original in conditions.Values.OrderBy(x => x.InstanceId).ToArray())
            {
                var current = original;
                var definition = catalog.Get(current.DefinitionId);
                while (IsTemporaryActive(current.State) && current.NextProgressCalendarTick <= calendarTick)
                {
                    var stepTick = current.NextProgressCalendarTick;
                    var step = unchecked((ulong)(stepTick / ProgressionStep.Ticks));
                    var severity = current.Severity;
                    var state = current.State;
                    var progression = DeterministicKeyedRandom.Sample64(worldSeed, current.TargetId, step,
                        ConditionProgressionStream ^ TextHash(current.DefinitionId.ToString()));
                    if (progression % 1000UL < (ulong)definition.WorseningPermille)
                    {
                        severity = (HealthSeverity)Math.Min((int)HealthSeverity.Critical, (int)severity + 1);
                        state = severity == HealthSeverity.Critical ? HealthConditionState.Critical : HealthConditionState.Worsening;
                    }
                    else if (state == HealthConditionState.Active || state == HealthConditionState.Worsening)
                    {
                        state = HealthConditionState.Improving;
                    }
                    var nextTick = checked(stepTick + ProgressionStep.Ticks);
                    current = CopyCondition(current, nextTick, severity, state, current.ProgressionSteps + 1);
                    transitions++;
                    if (stepTick < current.ResolveCalendarTick) continue;
                    var resolution = DeterministicKeyedRandom.Sample64(worldSeed, current.TargetId, step,
                        ConditionResolutionStream ^ TextHash(current.DefinitionId.ToString()));
                    var mortality = EffectiveMortality(definition, severity);
                    if (severity == HealthSeverity.Critical && resolution % 1000UL < (ulong)mortality)
                    {
                        var deathBiologicalTick = MapBiologicalTick(stepTick, calendarTick, biologicalTick);
                        ProcessHealthDeath(current.TargetId, deathBiologicalTick, stepTick);
                        current = CopyCondition(current, state: HealthConditionState.Fatal, deathProcessed: true);
                        deaths++;
                    }
                    else if (resolution / 1000UL % 1000UL < (ulong)definition.PermanentOutcomePermille)
                    {
                        current = CopyCondition(current, state: HealthConditionState.Permanent);
                    }
                    else
                    {
                        current = CopyCondition(current, state: HealthConditionState.Recovered);
                    }
                }
                conditions[current.InstanceId] = current;
            }

            var births = 0;
            foreach (var pregnancy in pregnancies.Values.Where(x => x.Status == PregnancyStatus.Active &&
                         x.DueBiologicalTick <= biologicalTick).OrderBy(x => x.DueBiologicalTick)
                         .ThenBy(x => x.PregnancyId).ToArray())
            {
                var mother = characters.Get(pregnancy.MotherId);
                if (mother.LifeState != CharacterLifeState.Alive)
                {
                    pregnancies[pregnancy.PregnancyId] = new PregnancyState(pregnancy.PregnancyId,
                        pregnancy.MotherId, pregnancy.FatherId, pregnancy.ConceptionCalendarTick,
                        pregnancy.ConceptionBiologicalTick, pregnancy.DueCalendarTick, pregnancy.DueBiologicalTick,
                        PregnancyStatus.Cancelled, null);
                    continue;
                }
                ProcessBirth(pregnancy);
                births++;
            }
            LastCalendarTick = calendarTick;
            LastBiologicalTick = biologicalTick;
            return new HealthAdvanceResult(transitions, deaths, births, ComputeStateHash());
        }

        public CharacterCapabilities GetCapabilities(StableEntityId characterId) =>
            GetCapabilitiesAt(characterId, LastBiologicalTick);

        public CharacterCapabilities GetCapabilitiesAt(StableEntityId characterId, long biologicalTick)
        {
            var character = characters.Get(characterId);
            if (biologicalTick < character.BiologicalBirthTick) throw new ArgumentOutOfRangeException(nameof(biologicalTick));
            var bootstrapAdult = biologicalTick == 0 && character.BiologicalBirthTick == 0 && character.Parents.Count == 0;
            var adult = bootstrapAdult || character.AgeInCompletedYears(biologicalTick) >= AdultAgeYears;
            var work = adult ? 1000 : 0;
            var movement = 1000;
            var research = adult ? 1000 : 0;
            var combat = adult ? 1000 : 0;
            var incapacitated = false;
            var fertility = adult;
            foreach (var condition in conditions.Values.Where(x => x.TargetId == characterId && IsActive(x.State)))
            {
                var effect = catalog.Get(condition.DefinitionId).Effect;
                work = Multiply(work, effect.WorkPerThousand);
                movement = Multiply(movement, effect.MovementPerThousand);
                research = Multiply(research, effect.ResearchPerThousand);
                combat = Multiply(combat, effect.CombatPerThousand);
                incapacitated |= effect.Incapacitated;
                fertility &= effect.FertilityEligible;
            }
            if (character.LifeState != CharacterLifeState.Alive)
            {
                work = movement = research = combat = 0;
                incapacitated = true;
                fertility = false;
            }
            return new CharacterCapabilities(adult, incapacitated, work, movement, research, combat, fertility);
        }

        public HealthFamilySaveState CaptureState() => new HealthFamilySaveState(worldSeed, LastCalendarTick,
            LastBiologicalTick, Conditions, Events, Pregnancies);

        public void RestoreState(HealthFamilySaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.WorldSeed != worldSeed) throw new InvalidOperationException("Health save world seed does not match this session.");
            var restoredConditions = new Dictionary<StableEntityId, HealthConditionInstanceState>();
            foreach (var condition in state.Conditions)
            {
                characters.Get(condition.TargetId);
                catalog.Get(condition.DefinitionId);
                if (!restoredConditions.TryAdd(condition.InstanceId, condition)) throw new ArgumentException("Duplicate condition ID.");
            }
            var restoredEvents = new Dictionary<StableEntityId, CharacterEventInstanceState>();
            foreach (var value in state.Events)
            {
                characters.Get(value.PrimaryId); characters.Get(value.SecondaryId);
                if (!eventDefinitions.ContainsKey(value.DefinitionId)) throw new ArgumentException("Unknown saved event definition.");
                if (!restoredEvents.TryAdd(value.InstanceId, value)) throw new ArgumentException("Duplicate event ID.");
            }
            var restoredPregnancies = new Dictionary<StableEntityId, PregnancyState>();
            var activeMothers = new HashSet<StableEntityId>();
            foreach (var pregnancy in state.Pregnancies)
            {
                characters.Get(pregnancy.MotherId); characters.Get(pregnancy.FatherId);
                if (pregnancy.Status == PregnancyStatus.Active && !activeMothers.Add(pregnancy.MotherId))
                    throw new ArgumentException("A mother cannot have two active pregnancies.");
                if (pregnancy.ChildId.HasValue) characters.Get(pregnancy.ChildId.Value);
                if (!restoredPregnancies.TryAdd(pregnancy.PregnancyId, pregnancy)) throw new ArgumentException("Duplicate pregnancy ID.");
            }
            conditions.Clear(); foreach (var pair in restoredConditions) conditions.Add(pair.Key, pair.Value);
            events.Clear(); foreach (var pair in restoredEvents) events.Add(pair.Key, pair.Value);
            pregnancies.Clear(); foreach (var pair in restoredPregnancies) pregnancies.Add(pair.Key, pair.Value);
            LastCalendarTick = state.LastCalendarTick;
            LastBiologicalTick = state.LastBiologicalTick;
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            Add(ref hash, unchecked((ulong)worldSeed)); Add(ref hash, unchecked((ulong)LastCalendarTick));
            Add(ref hash, unchecked((ulong)LastBiologicalTick));
            foreach (var condition in Conditions)
            {
                AddId(ref hash, condition.InstanceId); AddId(ref hash, condition.TargetId);
                AddText(ref hash, condition.DefinitionId.ToString()); Add(ref hash, unchecked((ulong)condition.StartCalendarTick));
                Add(ref hash, unchecked((ulong)condition.NextProgressCalendarTick)); Add(ref hash, unchecked((ulong)condition.ResolveCalendarTick));
                Add(ref hash, (ulong)condition.Severity); Add(ref hash, (ulong)condition.State); Add(ref hash, (ulong)condition.ProgressionSteps);
            }
            foreach (var value in Events)
            {
                AddId(ref hash, value.InstanceId); AddText(ref hash, value.DefinitionId.ToString());
                AddId(ref hash, value.PrimaryId); AddId(ref hash, value.SecondaryId); Add(ref hash, unchecked((ulong)value.CalendarTick));
                Add(ref hash, unchecked((ulong)value.AppliedDelta));
            }
            foreach (var pregnancy in Pregnancies)
            {
                AddId(ref hash, pregnancy.PregnancyId); AddId(ref hash, pregnancy.MotherId); AddId(ref hash, pregnancy.FatherId);
                Add(ref hash, unchecked((ulong)pregnancy.ConceptionCalendarTick)); Add(ref hash, unchecked((ulong)pregnancy.DueBiologicalTick));
                Add(ref hash, (ulong)pregnancy.Status); if (pregnancy.ChildId.HasValue) AddId(ref hash, pregnancy.ChildId.Value);
            }
            return hash;
        }

        private void ProcessHealthDeath(StableEntityId targetId, long biologicalTick, long calendarTick)
        {
            if (succession == null) throw new InvalidOperationException("Health death requires the U17 SuccessionService boundary.");
            succession.ProcessDeath(targetId, biologicalTick, calendarTick);
        }

        private void ProcessBirth(PregnancyState pregnancy)
        {
            var mother = characters.Get(pregnancy.MotherId);
            var father = characters.Get(pregnancy.FatherId);
            var childId = DerivedId("child", pregnancy.MotherId, pregnancy.FatherId,
                pregnancy.ConceptionBiologicalTick ^ pregnancy.DueBiologicalTick);
            if (characters.TryGet(childId, out _)) throw new InvalidOperationException("Deterministic child ID collision.");
            var sexSample = DeterministicKeyedRandom.Sample64(worldSeed, pregnancy.MotherId,
                unchecked((ulong)pregnancy.DueBiologicalTick), BirthSexStream ^ TextHash(pregnancy.FatherId.ToString()));
            var sex = (sexSample & 1UL) == 0UL ? CharacterSex.Male : CharacterSex.Female;
            var names = sex == CharacterSex.Male
                ? new[] { "Aren", "Borin", "Cale", "Doran", "Eran", "Fenn" }
                : new[] { "Alya", "Brina", "Mira", "Nera", "Rina", "Sela" };
            var familyName = !string.IsNullOrEmpty(mother.Name.FamilyName) ? mother.Name.FamilyName : father.Name.FamilyName;
            var name = new CharacterName(names[(int)(sexSample % (ulong)names.Length)], familyName);
            StableEntityId? dynastyId = mother.DynastyId ?? father.DynastyId;
            if (succession != null && dynastyId != succession.Dynasty.Id) dynastyId = null;
            var attributes = new CharacterAttributes(
                new CharacterAttributeValue((mother.Attributes.Intelligence.Value + father.Attributes.Intelligence.Value) / 2),
                new CharacterAttributeValue((mother.Attributes.PhysicalStrength.Value + father.Attributes.PhysicalStrength.Value) / 2));
            var data = new CharacterCreationData(name, sex, pregnancy.DueBiologicalTick,
                new[]
                {
                    new ParentLink(mother.Id, ParentRole.Mother, ParentageKind.Biological, true),
                    new ParentLink(father.Id, ParentRole.Father, ParentageKind.Biological, true)
                }, null, mother.FamilyId ?? father.FamilyId, dynastyId, null,
                new[] { new TraitId("calm"), new TraitId("curious"), new TraitId("kind"), new TraitId("resilient") },
                null, attributes, BodyHealth.Healthy, new Wealth(0), new Influence(0), null);
            var child = Character.CreateWithId(data, childId);
            characters.Add(child);
            if (succession != null && dynastyId == succession.Dynasty.Id) succession.Dynasty.AddMember(childId);
            pregnancies[pregnancy.PregnancyId] = new PregnancyState(pregnancy.PregnancyId,
                pregnancy.MotherId, pregnancy.FatherId, pregnancy.ConceptionCalendarTick,
                pregnancy.ConceptionBiologicalTick, pregnancy.DueCalendarTick, pregnancy.DueBiologicalTick,
                PregnancyStatus.BirthProcessed, childId);
            ChildBorn?.Invoke(child);
        }

        private long MapBiologicalTick(long eventCalendarTick, long targetCalendarTick, long targetBiologicalTick)
        {
            if (targetCalendarTick == LastCalendarTick) return targetBiologicalTick;
            var calendarDelta = eventCalendarTick - LastCalendarTick;
            var biologicalDelta = targetBiologicalTick - LastBiologicalTick;
            var mappedDelta = (long)((decimal)calendarDelta * biologicalDelta /
                                     (targetCalendarTick - LastCalendarTick));
            return checked(LastBiologicalTick + mappedDelta);
        }

        private StableEntityId DerivedId(string purpose, StableEntityId first, StableEntityId second, long tick)
        {
            var firstHash = 14695981039346656037UL;
            Add(ref firstHash, unchecked((ulong)worldSeed)); AddText(ref firstHash, purpose); AddId(ref firstHash, first);
            if (second.IsValid) AddId(ref firstHash, second); Add(ref firstHash, unchecked((ulong)tick));
            var secondHash = Mix(firstHash ^ 0x9e3779b97f4a7c15UL ^ TextHash(purpose));
            return StableEntityId.FromDeterministicHash(Mix(firstHash), secondHash);
        }

        private static bool AreFamilyRelated(Character first, Character second) => first.SpouseId == second.Id ||
            second.SpouseId == first.Id || first.HasParent(second.Id) || second.HasParent(first.Id);
        private static bool IsActive(HealthConditionState state) => state == HealthConditionState.Active ||
            state == HealthConditionState.Improving || state == HealthConditionState.Worsening ||
            state == HealthConditionState.Critical || state == HealthConditionState.Permanent;
        private static bool IsTemporaryActive(HealthConditionState state) => state == HealthConditionState.Active ||
            state == HealthConditionState.Improving || state == HealthConditionState.Worsening ||
            state == HealthConditionState.Critical;
        private static int EffectiveMortality(HealthConditionDefinition definition, HealthSeverity severity) =>
            Math.Min(1000, definition.Effect.MortalityPermille + Math.Max(0, (int)severity - (int)definition.InitialSeverity) * 75);
        private static int Multiply(int left, int right) => left * right / 1000;
        private static IEnumerable<CharacterEventDefinition> CreateEventDefinitions()
        {
            yield return new CharacterEventDefinition(new CharacterEventId("marriage_bond_strengthened"), CharacterEventCategory.Family, 10);
            yield return new CharacterEventDefinition(new CharacterEventId("marriage_bond_weakened"), CharacterEventCategory.Relationship, -10);
            yield return new CharacterEventDefinition(new CharacterEventId("family_dispute"), CharacterEventCategory.Family, -15);
            yield return new CharacterEventDefinition(new CharacterEventId("family_support"), CharacterEventCategory.Family, 12);
        }

        private static HealthConditionInstanceState CopyCondition(HealthConditionInstanceState source,
            long? nextProgress = null, HealthSeverity? severity = null, HealthConditionState? state = null,
            int? progressionSteps = null, bool? deathProcessed = null) =>
            new HealthConditionInstanceState(source.InstanceId, source.DefinitionId, source.TargetId,
                source.StartCalendarTick, nextProgress ?? source.NextProgressCalendarTick, source.ResolveCalendarTick,
                severity ?? source.Severity, state ?? source.State, progressionSteps ?? source.ProgressionSteps,
                deathProcessed ?? source.DeathProcessed);

        private static uint TextHash(string text)
        {
            var hash = 2166136261u;
            for (var i = 0; i < text.Length; i++) { hash ^= text[i]; hash *= 16777619u; }
            return hash;
        }
        private static ulong Mix(ulong value)
        {
            value += 0x9e3779b97f4a7c15UL; value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL; return value ^ (value >> 31);
        }
        private static void Add(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8) { hash ^= (byte)(value >> shift); hash *= 1099511628211UL; }
        }
        private static void AddId(ref ulong hash, StableEntityId id)
        {
            foreach (var value in id.ToGuid().ToByteArray()) { hash ^= value; hash *= 1099511628211UL; }
        }
        private static void AddText(ref ulong hash, string text)
        {
            for (var i = 0; i < text.Length; i++) Add(ref hash, text[i]);
        }
    }
}
