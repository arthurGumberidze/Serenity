using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Simulation.Tiers;
using NUnit.Framework;

namespace Game.Tests
{
    [TestFixture]
    [Category("U14")]
    public sealed class OffCameraSimulationTests
    {
        private const long Seed = 0x123456789L;

        [Test]
        public void SameStateSeedAndIntervalProduceExactSameStateAndHash()
        {
            var initial = Runtime(Id(1));
            var target = AtDays(10);
            var a = Sim(Seed);
            var b = Sim(Seed);
            var resultA = a.AdvanceState(initial, target);
            var resultB = b.AdvanceState(initial, target);

            Assert.That(resultB, Is.EqualTo(resultA));
            Assert.That(b.ComputeStateHash(new[] { resultB }), Is.EqualTo(a.ComputeStateHash(new[] { resultA })));
            Assert.That(resultA.OffCameraStepCount, Is.EqualTo(10UL));
            Assert.That(resultA.LastSimulationCalendarTick, Is.EqualTo(target.CalendarTicks));
            Assert.That(resultA.LastSimulationBiologicalTick, Is.EqualTo(target.BiologicalTicks));
        }

        [Test]
        public void RepeatingSameReplayOneHundredTimesProducesOneHash()
        {
            var initial = Runtime(Id(2));
            var expected = Sim(Seed).ComputeStateHash(new[] { Sim(Seed).AdvanceState(initial, AtDays(40)) });
            for (var i = 0; i < 100; i++)
            {
                var simulation = Sim(Seed);
                var state = simulation.AdvanceState(initial, AtDays(40));
                Assert.That(simulation.ComputeStateHash(new[] { state }), Is.EqualTo(expected), "Replay " + i);
            }
        }

        [Test]
        public void CollectionOrderDoesNotChangePerCharacterStateOrBatchHash()
        {
            var ascending = Enumerable.Range(1, 100).Select(i => new Tier3CharacterRecord(Runtime(Id(i)))).ToArray();
            var reversed = ascending.Reverse().Select(x => new Tier3CharacterRecord(x.State)).ToArray();
            var a = Sim(Seed).Advance(ascending, AtDays(30));
            var b = Sim(Seed).Advance(reversed, AtDays(30));

            Assert.That(b.StateHash, Is.EqualTo(a.StateHash));
            Assert.That(b.ProcessedSteps, Is.EqualTo(a.ProcessedSteps));
            var byId = ascending.ToDictionary(x => x.CharacterId, x => x.State);
            foreach (var record in reversed) Assert.That(record.State, Is.EqualTo(byId[record.CharacterId]));
        }

        [Test]
        public void ContinuousAndSaveBreakRestoreProduceSameResult()
        {
            var initial = Runtime(Id(3));
            var continuous = Sim(Seed).AdvanceState(initial, AtDays(10));
            var firstSession = Sim(Seed).AdvanceState(initial, AtDays(5));
            var restored = new CharacterRuntimeState(firstSession.CharacterId, firstSession.Position,
                firstSession.UnitsPerGameDay, firstSession.IsMoving, firstSession.LocationKey,
                firstSession.ProcessedCalendarTicks, firstSession.ProcessedBiologicalTicks, firstSession.StepCount,
                firstSession.Hunger, firstSession.Energy, firstSession.LastSimulationCalendarTick,
                firstSession.LastSimulationBiologicalTick, firstSession.OffCameraStepCount,
                firstSession.DeterministicAccumulator, firstSession.AbstractActivityProgress);
            var split = Sim(Seed).AdvanceState(restored, AtDays(10));
            Assert.That(split, Is.EqualTo(continuous));
        }

        [Test]
        public void OneDayAtOnceEqualsTwentyFourHourlyCatchUps()
        {
            var initial = Runtime(Id(4));
            var once = Sim(Seed).AdvanceState(initial, AtHours(24));
            var split = initial;
            var simulation = Sim(Seed);
            for (var hour = 1; hour <= 24; hour++) split = simulation.AdvanceState(split, AtHours(hour));
            Assert.That(split, Is.EqualTo(once));
        }

        [Test]
        public void KeyedRandomIsStableAndIndependentForNeighboringCharacters()
        {
            var id = Id(5);
            var expected = DeterministicKeyedRandom.Sample64(Seed, id, 42, DeterministicKeyedRandom.AbstractActivityStream);
            _ = DeterministicKeyedRandom.Sample64(Seed, Id(6), 42, DeterministicKeyedRandom.AbstractActivityStream);
            Assert.That(DeterministicKeyedRandom.Sample64(Seed, id, 42,
                DeterministicKeyedRandom.AbstractActivityStream), Is.EqualTo(expected));
            Assert.That(DeterministicKeyedRandom.Sample64(Seed + 1, id, 42,
                DeterministicKeyedRandom.AbstractActivityStream), Is.Not.EqualTo(expected));
        }

        [Test]
        public void DifferentWorldSeedChangesOnlyDeterministicRemoteActivityFields()
        {
            var initial = Runtime(Id(7));
            var a = Sim(Seed).AdvanceState(initial, AtDays(20));
            var b = Sim(Seed + 1).AdvanceState(initial, AtDays(20));
            Assert.That(b.CharacterId, Is.EqualTo(a.CharacterId));
            Assert.That(b.Position, Is.EqualTo(a.Position));
            Assert.That(b.Hunger, Is.EqualTo(a.Hunger));
            Assert.That(b.Energy, Is.EqualTo(a.Energy));
            Assert.That(b.LastSimulationCalendarTick, Is.EqualTo(a.LastSimulationCalendarTick));
            Assert.That(b.DeterministicAccumulator, Is.Not.EqualTo(a.DeterministicAccumulator));
        }

        [Test]
        public void PausedOrRepeatedTargetProducesNoProgress()
        {
            var simulation = Sim(Seed);
            var state = simulation.AdvanceState(Runtime(Id(8)), AtHours(12));
            Assert.That(simulation.AdvanceState(state, AtHours(12)), Is.EqualTo(state));
        }

        [Test]
        public void AutomaticTier3BatchRunsAtMostOncePerFixedDayWhileTransitionCatchUpStaysExact()
        {
            var character = CreateCharacter();
            var records = new Tier3CharacterRegistry();
            var time = new TimeBox { Value = AtHours(1) };
            var adapter = new Tier3CharacterAdapter(records, Sim(Seed), () => time.Value);
            adapter.Materialize(character, Runtime(character.Id));

            adapter.AdvanceAllToCurrent();
            time.Value = AtHours(2);
            adapter.AdvanceAllToCurrent();
            time.Value = AtHours(23);
            adapter.AdvanceAllToCurrent();
            Assert.That(adapter.AutomaticBatchCount, Is.EqualTo(1));

            time.Value = AtHours(24);
            adapter.AdvanceAllToCurrent();
            Assert.That(adapter.AutomaticBatchCount, Is.EqualTo(2));
            time.Value = AtHours(25);
            Assert.That(adapter.Capture(character.Id).LastSimulationCalendarTick,
                Is.EqualTo(AtHours(25).CalendarTicks), "Transition capture must still commit the exact sub-day target.");
        }

        [Test]
        public void OneYearJumpIsBoundedAndUsesDailySteps()
        {
            var watch = Stopwatch.StartNew();
            var state = Sim(Seed).AdvanceState(Runtime(Id(9)), AtDays(365));
            watch.Stop();
            Assert.That(state.OffCameraStepCount, Is.EqualTo(365UL));
            Assert.That(state.LastSimulationCalendarTick, Is.EqualTo(TimeSpan.FromDays(365).Ticks));
            Assert.That(state.AbstractActivityProgress, Is.InRange(365L, 1460L));
            TestContext.WriteLine($"U14 one-character one-year catch-up: {watch.Elapsed.TotalMilliseconds:0.###} ms");
        }

        [Test]
        public void ThousandAndTenThousandCharacterBatchesAreDeterministicAndLightweight()
        {
            var thousand = Records(1000);
            var simulation = Sim(Seed);
            _ = simulation.Advance(new[] { new Tier3CharacterRecord(Runtime(Id(20001))) }, AtDays(1));
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            var result = simulation.Advance(thousand, AtDays(365));
            watch.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(result.ProcessedEntities, Is.EqualTo(1000));
            Assert.That(result.ProcessedSteps, Is.EqualTo(365000UL));
            Assert.That(thousand.Select(x => x.CharacterId).Distinct().Count(), Is.EqualTo(1000));
            var replay = Sim(Seed).Advance(Records(1000), AtDays(365));
            Assert.That(replay.StateHash, Is.EqualTo(result.StateHash));
            TestContext.WriteLine($"U14 1,000 x 365 days: {watch.Elapsed.TotalMilliseconds:0.###} ms, " +
                $"managed allocation {allocated} bytes, hash {result.StateHash:X16}");

            var tenThousandWatch = Stopwatch.StartNew();
            var tenThousand = Sim(Seed).Advance(Records(10000), AtDays(30));
            tenThousandWatch.Stop();
            Assert.That(tenThousand.ProcessedEntities, Is.EqualTo(10000));
            Assert.That(tenThousand.ProcessedSteps, Is.EqualTo(300000UL));
            TestContext.WriteLine($"U14 10,000 x 30 days: {tenThousandWatch.Elapsed.TotalMilliseconds:0.###} ms, " +
                $"hash {tenThousand.StateHash:X16}");
        }

        [Test]
        public void TierRoutesConvergeWithoutPlayerInterventionAndDoNotDoubleAdvance()
        {
            var character = CreateCharacter();
            var registry = new CharacterRegistry();
            registry.Add(character);
            var allTier3 = RunRoute(registry, character, Route.AllTier3);
            var viaTier2 = RunRoute(registry, character, Route.ViaTier2);
            var viaTier1 = RunRoute(registry, character, Route.ViaTier1);

            AssertU14StateEqual(allTier3, viaTier2);
            AssertU14StateEqual(allTier3, viaTier1);
            Assert.That(allTier3.OffCameraStepCount, Is.EqualTo(10UL));
        }

        [Test]
        public void CatchUpPreservesCanonicalCharacterAndBiologicalAgeUsesExistingClock()
        {
            var character = CreateCharacter();
            var before = character.CaptureState();
            var state = Sim(Seed).AdvanceState(Runtime(character.Id),
                new SimulationTimePoint(TimeSpan.FromDays(10).Ticks, TimeSpan.FromDays(365 * 20L).Ticks));
            var after = character.CaptureState();
            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.Parents, Is.EquivalentTo(before.Parents));
            Assert.That(after.SpouseId, Is.EqualTo(before.SpouseId));
            Assert.That(after.FamilyId, Is.EqualTo(before.FamilyId));
            Assert.That(after.DynastyId, Is.EqualTo(before.DynastyId));
            Assert.That(after.Health, Is.EqualTo(before.Health));
            Assert.That(character.AgeInCompletedYears(state.LastSimulationBiologicalTick), Is.EqualTo(19));
        }

        [Test]
        public void BackwardsTimeAndDuplicateIdentityAreRejectedBeforeAmbiguousCommit()
        {
            var simulation = Sim(Seed);
            var advanced = simulation.AdvanceState(Runtime(Id(10)), AtDays(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => simulation.AdvanceState(advanced, AtDays(1)));
            var duplicate = new[] { new Tier3CharacterRecord(Runtime(Id(11))),
                new Tier3CharacterRecord(Runtime(Id(11))) };
            Assert.Throws<InvalidOperationException>(() => simulation.Advance(duplicate, AtDays(1)));
        }

        private static CharacterRuntimeState RunRoute(CharacterRegistry registry, Character character, Route route)
        {
            var time = new TimeBox();
            var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
            var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
            var tier3Records = new Tier3CharacterRegistry();
            var simulation = Sim(Seed);
            var tier3 = new Tier3CharacterAdapter(tier3Records, simulation, () => time.Value);
            var manager = new TierManager(registry, tier1, tier2, tier3);
            var initial = Runtime(character.Id);
            tier3.Materialize(character, initial);
            manager.RegisterExisting(character.Id, CharacterSimulationTier.Tier3);

            time.Value = AtDays(3);
            tier3.AdvanceAllToCurrent();
            if (route == Route.ViaTier2) manager.Transition(character.Id, CharacterSimulationTier.Tier2);
            if (route == Route.ViaTier1) manager.Transition(character.Id, CharacterSimulationTier.Tier1);
            time.Value = AtDays(7);
            if (route != Route.AllTier3) manager.Transition(character.Id, CharacterSimulationTier.Tier3);
            tier3.AdvanceAllToCurrent();
            time.Value = AtDays(10);
            return manager.GetRuntimeState(character.Id);
        }

        private static void AssertU14StateEqual(CharacterRuntimeState expected, CharacterRuntimeState actual)
        {
            Assert.That(actual.LastSimulationCalendarTick, Is.EqualTo(expected.LastSimulationCalendarTick));
            Assert.That(actual.LastSimulationBiologicalTick, Is.EqualTo(expected.LastSimulationBiologicalTick));
            Assert.That(actual.OffCameraStepCount, Is.EqualTo(expected.OffCameraStepCount));
            Assert.That(actual.DeterministicAccumulator, Is.EqualTo(expected.DeterministicAccumulator));
            Assert.That(actual.AbstractActivityProgress, Is.EqualTo(expected.AbstractActivityProgress));
        }

        private static OffCameraSimulationService Sim(long seed) =>
            new OffCameraSimulationService(OffCameraSimulationSettings.ForWorld(seed));
        private static SimulationTimePoint AtDays(long days) => new SimulationTimePoint(
            TimeSpan.FromDays(days).Ticks, TimeSpan.FromDays(days * 400d).Ticks);
        private static SimulationTimePoint AtHours(int hours) => new SimulationTimePoint(
            TimeSpan.FromHours(hours).Ticks, TimeSpan.FromHours(hours * 400d).Ticks);
        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        private static CharacterRuntimeState Runtime(StableEntityId id) => new CharacterRuntimeState(id,
            new WorldPosition(3f, 0f, 4f), default, false, "settlement.alpha", 77, 88, 9, 0.25d, 0.75d);
        private static Tier3CharacterRecord[] Records(int count) => Enumerable.Range(1, count)
            .Select(i => new Tier3CharacterRecord(Runtime(Id(i)))).ToArray();

        private static Character CreateCharacter()
        {
            var mother = Id(30001);
            var spouse = Id(30002);
            var family = Id(30003);
            var dynasty = Id(30004);
            return Character.CreateNew(new CharacterCreationData(new CharacterName("U14", "Traveler"),
                CharacterSex.Female, TimeSpan.FromDays(365).Ticks,
                new[] { new ParentLink(mother, ParentRole.Mother, ParentageKind.Biological, true) }, spouse,
                family, dynasty, new ProfessionId("builder"),
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(10), new Influence(5), null));
        }

        private enum Route { AllTier3, ViaTier2, ViaTier1 }
        private sealed class TimeBox { public SimulationTimePoint Value; }
        private sealed class MemoryAdapter : ICharacterTierAdapter
        {
            private readonly Dictionary<StableEntityId, CharacterRuntimeState> states =
                new Dictionary<StableEntityId, CharacterRuntimeState>();
            public MemoryAdapter(CharacterSimulationTier tier) { Tier = tier; }
            public CharacterSimulationTier Tier { get; }
            public bool IsActive(StableEntityId id) => states.ContainsKey(id);
            public CharacterRuntimeState Capture(StableEntityId id) => states[id];
            public void ValidateMaterialization(Character character, CharacterRuntimeState state)
            {
                if (character.Id != state.CharacterId || IsActive(character.Id)) throw new InvalidOperationException();
            }
            public void Materialize(Character character, CharacterRuntimeState state)
            {
                ValidateMaterialization(character, state);
                states.Add(character.Id, state);
            }
            public void Dematerialize(StableEntityId id) { states.Remove(id); }
        }
    }
}
