using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Research;
using Game.Simulation.Tiers;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U18")]
    public sealed class HealthAndFamilyTests
    {
        private static readonly long AdultTick = Character.BiologicalTicksPerYear * 20L;

        [Test, Category("U30")]
        public void HistoricalConditionsDoNotRemainInActiveIndexAndRestoreRebuildsIt()
        {
            var fixture = Fixture.Create(77);
            for (var i = 0; i < 1000; i++)
            {
                fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("minor_cut"), i);
                Assert.That(fixture.Health.RecoverAll(fixture.Father.Id), Is.EqualTo(1));
            }
            fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("sprain"), 1001);
            Assert.That(fixture.Health.RetainedConditionCount, Is.EqualTo(1001));
            Assert.That(fixture.Health.ActiveConditionCount, Is.EqualTo(1));
            var saved = fixture.Health.CaptureState();
            fixture.Health.RecoverAll(fixture.Father.Id);
            fixture.Health.RestoreState(saved);
            Assert.That(fixture.Health.ActiveConditionCount, Is.EqualTo(1));
            Assert.That(fixture.Health.HasActiveCondition(fixture.Father.Id, new HealthConditionId("sprain")), Is.True);
            Assert.That(fixture.Health.HasActiveCondition(fixture.Father.Id, new HealthConditionId("minor_cut")), Is.False);
            Assert.That(fixture.Health.RetainedConditionCount, Is.EqualTo(1001));
            fixture.Health.AdvanceTo(TimeSpan.FromDays(10).Ticks, AdultTick + TimeSpan.FromDays(4000).Ticks, false);
            Assert.That(fixture.Health.HasActiveCondition(fixture.Father.Id, new HealthConditionId("sprain")), Is.False);
            Assert.That(fixture.Health.ActiveConditionCount, Is.EqualTo(0));
            Assert.That(fixture.Health.Conditions.Count, Is.EqualTo(1001));
        }

        [Test]
        public void HealthDefinitionsValidateAndStoneAgeCatalogIsSmallDataDrivenSet()
        {
            var catalog = HealthConditionCatalog.CreateStoneAge();
            Assert.That(catalog.All.Count, Is.InRange(6, 12));
            Assert.That(catalog.Get(new HealthConditionId("minor_cut")).Type, Is.EqualTo(HealthConditionType.Injury));
            Assert.That(catalog.Get(new HealthConditionId("infection")).Type, Is.EqualTo(HealthConditionType.Disease));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthEffect(-1, 1000, 1000, 1000, false, true, 0));
            Assert.Throws<ArgumentException>(() => new HealthEffect(1, 1000, 0, 1000, true, false, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthConditionDefinition(
                new HealthConditionId("bad"), "Bad", HealthConditionType.Disease, HealthSeverity.Minor,
                TimeSpan.Zero, HealthEffect.Healthy));
        }

        [Test]
        public void TemporaryConditionProgressesAndRecoversDeterministically()
        {
            var fixture = Fixture.Create(77);
            var condition = fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("minor_cut"), 0);
            var result = fixture.Health.AdvanceTo(TimeSpan.FromDays(3).Ticks, AdultTick + TimeSpan.FromDays(1200).Ticks);
            var resolved = fixture.Health.Conditions.Single(x => x.InstanceId == condition.InstanceId);
            Assert.That(result.ConditionTransitions, Is.EqualTo(3));
            Assert.That(resolved.State, Is.EqualTo(HealthConditionState.Recovered));
            Assert.That(fixture.Father.LifeState, Is.EqualTo(CharacterLifeState.Alive));
        }

        [Test]
        public void PermanentOutcomePersistsAsCapabilityPenalty()
        {
            var catalog = new HealthConditionCatalog(new[]
            {
                new HealthConditionDefinition(new HealthConditionId("test_injury"), "Test injury",
                    HealthConditionType.Injury, HealthSeverity.Serious, TimeSpan.FromDays(1),
                    new HealthEffect(500, 600, 700, 400, false, false, 0), permanentOutcomePermille: 1000)
            });
            var fixture = Fixture.Create(12, catalog);
            fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("test_injury"), 0);
            fixture.Health.AdvanceTo(TimeSpan.FromDays(1).Ticks, AdultTick + TimeSpan.FromDays(400).Ticks);
            Assert.That(fixture.Health.Conditions.Single().State, Is.EqualTo(HealthConditionState.Permanent));
            Assert.That(fixture.Health.GetCapabilities(fixture.Father.Id).WorkPerThousand, Is.EqualTo(500));
            Assert.That(fixture.Health.GetCapabilities(fixture.Father.Id).MovementPerThousand, Is.EqualTo(600));
        }

        [Test]
        public void ModifiersAffectWorkResearchCombatAndIncapacitation()
        {
            var fixture = Fixture.Create(99);
            fixture.Health.AdvanceTo(0, AdultTick);
            var healthyRate = new CharacterResearchRateProvider(fixture.Registry, fixture.Health)
                .GetRatePerThousand(fixture.Father.Id);
            fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("serious_wound"), 0);
            var capability = fixture.Health.GetCapabilities(fixture.Father.Id);
            var injuredRate = new CharacterResearchRateProvider(fixture.Registry, fixture.Health)
                .GetRatePerThousand(fixture.Father.Id);
            Assert.That(capability.IsIncapacitated, Is.True);
            Assert.That(capability.CanPerformOrdinaryWork, Is.False);
            Assert.That(capability.CanResearch, Is.False);
            Assert.That(capability.CombatPerThousand, Is.EqualTo(100));
            Assert.That(injuredRate, Is.Zero);
            Assert.That(healthyRate, Is.GreaterThan(0));
        }

        [Test]
        public void FatalConditionUsesSuccessionBoundaryExactlyOnce()
        {
            var fatalCatalog = new HealthConditionCatalog(new[]
            {
                new HealthConditionDefinition(new HealthConditionId("fatal_test"), "Fatal test",
                    HealthConditionType.Disease, HealthSeverity.Critical, TimeSpan.FromDays(1),
                    new HealthEffect(0, 0, 0, 0, true, false, 1000))
            });
            var fixture = Fixture.Create(5, fatalCatalog);
            fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("fatal_test"), 0);
            var first = fixture.Health.AdvanceTo(TimeSpan.FromDays(1).Ticks, AdultTick + TimeSpan.FromDays(400).Ticks);
            var second = fixture.Health.AdvanceTo(TimeSpan.FromDays(2).Ticks, AdultTick + TimeSpan.FromDays(800).Ticks);
            Assert.That(first.Deaths, Is.EqualTo(1));
            Assert.That(second.Deaths, Is.Zero);
            Assert.That(fixture.Father.LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(fixture.Dynasty.CurrentMonarchId, Is.EqualTo(fixture.Son.Id));
            Assert.That(fixture.Dynasty.History.Count, Is.EqualTo(1));
            Assert.That(fixture.Health.Conditions.Single().DeathProcessed, Is.True);
        }

        [Test]
        public void FamilyEventChangesExistingRelationshipsOnceAndBoundsScore()
        {
            var fixture = Fixture.Create(41);
            var eventId = new CharacterEventId("marriage_bond_strengthened");
            Assert.That(fixture.Health.ApplyRelationshipEvent(eventId, fixture.Mother.Id, fixture.Father.Id, 10), Is.True);
            Assert.That(fixture.Health.ApplyRelationshipEvent(eventId, fixture.Mother.Id, fixture.Father.Id, 10), Is.False);
            Assert.That(fixture.Mother.TryGetRelationship(fixture.Father.Id, out var relation), Is.True);
            Assert.That(relation.Affinity.Value, Is.EqualTo(10));
            Assert.That(fixture.Health.Events.Count, Is.EqualTo(1));
            Assert.Throws<KeyNotFoundException>(() => fixture.Health.ApplyRelationshipEvent(eventId,
                fixture.Mother.Id, Id(999), 11));
        }

        [Test]
        public void PregnancyValidatesParentsAndRejectsSecondConcurrentPregnancy()
        {
            var fixture = Fixture.Create(101);
            fixture.Health.AdvanceTo(0, AdultTick);
            var pregnancy = fixture.Health.StartPregnancy(fixture.Mother.Id, fixture.Father.Id, 0, AdultTick);
            Assert.That(pregnancy.DueBiologicalTick - pregnancy.ConceptionBiologicalTick,
                Is.EqualTo(TimeSpan.FromDays(750).Ticks));
            Assert.That(pregnancy.DueCalendarTick - pregnancy.ConceptionCalendarTick,
                Is.EqualTo(TimeSpan.FromDays(1.875).Ticks));
            Assert.Throws<InvalidOperationException>(() => fixture.Health.StartPregnancy(fixture.Mother.Id,
                fixture.Father.Id, 1, AdultTick + 1));
            var deadFixture = Fixture.Create(102);
            deadFixture.Mother.MarkDead(AdultTick);
            deadFixture.Health.AdvanceTo(0, AdultTick);
            Assert.Throws<InvalidOperationException>(() => deadFixture.Health.StartPregnancy(
                deadFixture.Mother.Id, deadFixture.Father.Id, 0, AdultTick));
        }

        [Test]
        public void BirthCreatesCanonicalChildParentsDynastyAndAgeGates()
        {
            var fixture = Fixture.Create(2026);
            fixture.Health.AdvanceTo(0, AdultTick);
            var pregnancy = fixture.Health.StartPregnancy(fixture.Mother.Id, fixture.Father.Id, 0, AdultTick);
            var advance = fixture.Health.AdvanceTo(pregnancy.DueCalendarTick, pregnancy.DueBiologicalTick);
            var processed = fixture.Health.Pregnancies.Single();
            var child = fixture.Registry.Get(processed.ChildId.Value);
            Assert.That(advance.Births, Is.EqualTo(1));
            Assert.That(child.BiologicalBirthTick, Is.EqualTo(pregnancy.DueBiologicalTick));
            Assert.That(child.Parents.Select(x => x.ParentId), Is.EquivalentTo(new[] { fixture.Mother.Id, fixture.Father.Id }));
            Assert.That(fixture.Service.Family.GetChildren(fixture.Father.Id).Select(x => x.Id), Does.Contain(child.Id));
            Assert.That(fixture.Dynasty.Contains(child.Id), Is.True);
            Assert.That(child.DynastyId, Is.EqualTo(fixture.Dynasty.Id));
            var capability = fixture.Health.GetCapabilities(child.Id);
            Assert.That(capability.IsAdult, Is.False);
            Assert.That(capability.CanPerformOrdinaryWork, Is.False);
            Assert.That(capability.CanResearch, Is.False);
            Assert.That(child.AgeInCompletedYears(pregnancy.DueBiologicalTick), Is.Zero);
        }

        [Test]
        public void SaveLoadPreservesPregnancyConditionEventAndDeterministicChildIdentity()
        {
            var source = Fixture.Create(303);
            source.Health.AdvanceTo(0, AdultTick);
            source.Health.ApplyCondition(source.Father.Id, new HealthConditionId("sprain"), 0);
            source.Health.ApplyRelationshipEvent(new CharacterEventId("family_support"),
                source.Mother.Id, source.Father.Id, 7);
            var pregnancy = source.Health.StartPregnancy(source.Mother.Id, source.Father.Id, 0, AdultTick);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(900),
                new GameTimeState(0, AdultTick, 1, false), 400, null, null,
                source.Service.CaptureState(), source.Health.CaptureState());
            var codec = new SaveXmlCodec();
            var restoredSnapshot = codec.Deserialize(codec.Serialize(snapshot));
            var target = Fixture.Create(303);
            target.Service.RestoreState(restoredSnapshot.DynastyWorld);
            target.Health.RestoreState(restoredSnapshot.HealthFamily);
            Assert.That(target.Health.ApplyRelationshipEvent(new CharacterEventId("family_support"),
                target.Mother.Id, target.Father.Id, 7), Is.False);

            source.Health.AdvanceTo(pregnancy.DueCalendarTick, pregnancy.DueBiologicalTick);
            target.Health.AdvanceTo(pregnancy.DueCalendarTick, pregnancy.DueBiologicalTick);
            Assert.That(target.Health.Pregnancies.Single().ChildId, Is.EqualTo(source.Health.Pregnancies.Single().ChildId));
            Assert.That(target.Health.ComputeStateHash(), Is.EqualTo(source.Health.ComputeStateHash()));
            Assert.That(target.Registry.Count, Is.EqualTo(source.Registry.Count));
        }

        [Test]
        public void LegacySnapshotVersionsOneThroughFourRemainReadableWithNoHealthPayload()
        {
            var codec = new SaveXmlCodec();
            for (var version = 1; version <= 4; version++)
            {
                var roundTrip = codec.Deserialize(codec.Serialize(new SaveSnapshot(version, Id(901),
                    new GameTimeState(1, 2, 1, false), 400)));
                Assert.That(roundTrip.HealthFamily, Is.Null);
            }
        }

        [Test]
        public void CollectionOrderAndTierRoutesProduceIdenticalHealthBirthAndDynastyState()
        {
            var direct = RunRoute(null, false);
            var tier2 = RunRoute(CharacterSimulationTier.Tier2, true);
            var tier1 = RunRoute(CharacterSimulationTier.Tier1, true);
            Assert.That(tier2.Hash, Is.EqualTo(direct.Hash));
            Assert.That(tier1.Hash, Is.EqualTo(direct.Hash));
            Assert.That(tier2.ChildId, Is.EqualTo(direct.ChildId));
            Assert.That(tier1.ChildId, Is.EqualTo(direct.ChildId));
            Assert.That(tier2.DynastyMembers, Is.EqualTo(direct.DynastyMembers));
            TestContext.WriteLine("U18_ROUTE_HASH=" + direct.Hash);
        }

        [Test]
        public void LongRunTwentyPersonSimulationHasNoDuplicateChildrenBrokenParentsOrInvalidConditions()
        {
            var registry = new CharacterRegistry();
            var people = new List<Character>();
            for (var i = 1; i <= 20; i++)
            {
                var person = Person(i, "Person" + i, i % 2 == 0 ? CharacterSex.Female : CharacterSex.Male, null);
                registry.Add(person); people.Add(person);
            }
            for (var i = 0; i < 10; i += 2) registry.LinkSpouses(people[i].Id, people[i + 1].Id);
            var dynastyId = Id(500);
            people[0].SetFamily(dynastyId, dynastyId);
            var dynasty = DynastyState.Create(dynastyId, "House Long", people[0].Id, people[0].Id, new[] { people[0].Id });
            var succession = new SuccessionService(registry, dynasty, new PlayerRealmState(people[0].Id), new GameRunState());
            var health = new HealthAndFamilyService(registry, HealthConditionCatalog.CreateStoneAge(), 444, succession);
            health.AdvanceTo(0, AdultTick);
            for (var i = 0; i < 10; i += 2)
            {
                var father = people[i].Sex == CharacterSex.Male ? people[i] : people[i + 1];
                var mother = people[i].Sex == CharacterSex.Female ? people[i] : people[i + 1];
                health.StartPregnancy(mother.Id, father.Id, 0, AdultTick);
                health.ApplyCondition(father.Id, new HealthConditionId("minor_cut"), 0);
            }
            var finalBio = AdultTick + Character.BiologicalTicksPerYear * 3L;
            var finalCalendar = (finalBio - AdultTick) / 400L;
            health.AdvanceTo(finalCalendar, finalBio);
            var childIds = health.Pregnancies.Select(x => x.ChildId.Value).ToArray();
            Assert.That(childIds.Distinct().Count(), Is.EqualTo(childIds.Length));
            Assert.That(registry.Count, Is.EqualTo(25));
            Assert.That(childIds.All(id => registry.Get(id).Parents.Count == 2), Is.True);
            Assert.That(health.Conditions.All(x => x.ProgressionSteps >= 0), Is.True);
            var hash = health.ComputeStateHash();
            Assert.That(hash, Is.Not.Zero);
            TestContext.WriteLine("U18_LONG_RUN_HASH=" + hash);
        }

        [Test]
        public void InvalidTargetsAndDuplicateConditionsFailCleanly()
        {
            var fixture = Fixture.Create(55);
            fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("minor_cut"), 0);
            Assert.Throws<InvalidOperationException>(() => fixture.Health.ApplyCondition(fixture.Father.Id,
                new HealthConditionId("minor_cut"), 0));
            Assert.Throws<KeyNotFoundException>(() => fixture.Health.ApplyCondition(Id(999),
                new HealthConditionId("minor_cut"), 0));
            Assert.That(StableEntityId.FromDeterministicHash(0, 0).IsValid, Is.True);
        }

        private static RouteResult RunRoute(CharacterSimulationTier? intermediate, bool reverseInsertion)
        {
            var fixture = Fixture.Create(777, reverseInsertion: reverseInsertion);
            var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
            var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
            var tier3 = new MemoryAdapter(CharacterSimulationTier.Tier3);
            foreach (var character in fixture.Registry.GetAll()) tier3.Seed(Runtime(character.Id));
            var manager = new TierManager(fixture.Registry, tier1, tier2, tier3);
            foreach (var character in fixture.Registry.GetAll()) manager.RegisterExisting(character.Id, CharacterSimulationTier.Tier3);
            fixture.Health.AdvanceTo(0, AdultTick);
            fixture.Health.ApplyCondition(fixture.Father.Id, new HealthConditionId("sprain"), 0);
            var pregnancy = fixture.Health.StartPregnancy(fixture.Mother.Id, fixture.Father.Id, 0, AdultTick);
            if (intermediate.HasValue)
            {
                manager.Transition(fixture.Father.Id, intermediate.Value);
                manager.Transition(fixture.Father.Id, CharacterSimulationTier.Tier3);
                manager.Transition(fixture.Mother.Id, intermediate.Value);
                manager.Transition(fixture.Mother.Id, CharacterSimulationTier.Tier3);
            }
            fixture.Health.AdvanceTo(pregnancy.DueCalendarTick, pregnancy.DueBiologicalTick);
            return new RouteResult(fixture.Health.ComputeStateHash(), fixture.Health.Pregnancies.Single().ChildId.Value,
                fixture.Dynasty.MemberIds.Select(x => x.ToString()).ToArray());
        }

        private readonly struct RouteResult
        {
            public RouteResult(ulong hash, StableEntityId childId, string[] dynastyMembers)
            { Hash = hash; ChildId = childId; DynastyMembers = dynastyMembers; }
            public ulong Hash { get; }
            public StableEntityId ChildId { get; }
            public string[] DynastyMembers { get; }
        }

        private sealed class Fixture
        {
            public CharacterRegistry Registry;
            public Character Father;
            public Character Mother;
            public Character Son;
            public DynastyState Dynasty;
            public SuccessionService Service;
            public HealthAndFamilyService Health;

            public static Fixture Create(long seed, HealthConditionCatalog catalog = null, bool reverseInsertion = false)
            {
                var fixture = new Fixture { Registry = new CharacterRegistry() };
                var dynastyId = Id(100);
                fixture.Father = Person(1, "Father", CharacterSex.Male, dynastyId);
                fixture.Mother = Person(2, "Mother", CharacterSex.Female, dynastyId);
                fixture.Son = Person(3, "Son", CharacterSex.Male, dynastyId,
                    new ParentLink(fixture.Mother.Id, ParentRole.Mother, ParentageKind.Biological, true),
                    new ParentLink(fixture.Father.Id, ParentRole.Father, ParentageKind.Biological, true));
                var ordered = reverseInsertion
                    ? new[] { fixture.Son, fixture.Mother, fixture.Father }
                    : new[] { fixture.Father, fixture.Mother, fixture.Son };
                foreach (var person in ordered) fixture.Registry.Add(person);
                fixture.Registry.LinkSpouses(fixture.Mother.Id, fixture.Father.Id);
                fixture.Dynasty = DynastyState.Create(dynastyId, "House Test", fixture.Father.Id,
                    fixture.Father.Id, new[] { fixture.Father.Id, fixture.Mother.Id, fixture.Son.Id });
                fixture.Service = new SuccessionService(fixture.Registry, fixture.Dynasty,
                    new PlayerRealmState(fixture.Father.Id), new GameRunState());
                fixture.Health = new HealthAndFamilyService(fixture.Registry,
                    catalog ?? HealthConditionCatalog.CreateStoneAge(), seed, fixture.Service);
                return fixture;
            }
        }

        private static Character Person(int id, string name, CharacterSex sex, StableEntityId? dynastyId,
            params ParentLink[] parents) => Character.Restore(new CharacterState(Id(id), new CharacterName(name), sex, 0,
            CharacterLifeState.Alive, null, parents, null, dynastyId, dynastyId, null,
            new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") }, null,
            new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static CharacterRuntimeState Runtime(StableEntityId id) => new CharacterRuntimeState(id, default,
            default, false, "test", lastSimulationCalendarTick: 0, lastSimulationBiologicalTick: AdultTick);
        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        private sealed class MemoryAdapter : ICharacterTierAdapter
        {
            private readonly Dictionary<StableEntityId, CharacterRuntimeState> states =
                new Dictionary<StableEntityId, CharacterRuntimeState>();
            public MemoryAdapter(CharacterSimulationTier tier) { Tier = tier; }
            public CharacterSimulationTier Tier { get; }
            public bool IsActive(StableEntityId characterId) => states.ContainsKey(characterId);
            public void ValidateMaterialization(Character character, CharacterRuntimeState state) { }
            public void Materialize(Character character, CharacterRuntimeState state) => states.Add(character.Id, state);
            public CharacterRuntimeState Capture(StableEntityId characterId) => states[characterId];
            public void Dematerialize(StableEntityId characterId) => states.Remove(characterId);
            public void Seed(CharacterRuntimeState state) => states.Add(state.CharacterId, state);
        }
    }
}
