using System;
using System.IO;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Infrastructure.Reliability;
using Game.Simulation.Tiers;
using NUnit.Framework;

namespace Game.Tests
{
    [TestFixture]
    [Category("U30")]
    public sealed class U30SoakTests
    {
        [Test]
        public void RequestedSeedReachesBootstrapOnlyWhenSoakIsEnabled()
        {
            Assert.That(U30SoakConfig.ResolveWorldSeed(new[] { "-u30Soak", "-u30Seed", "3030" }, 99), Is.EqualTo(3030));
            Assert.That(U30SoakConfig.ResolveWorldSeed(new[] { "-u30Seed", "3030" }, 99), Is.EqualTo(99));
        }

        [Test]
        public void CpuPreservesSubsecondUsageAndReportsUnavailableWindow()
        {
            Assert.That(U30Evidence.CpuPercent(TimeSpan.TicksPerSecond / 4, 1), Is.EqualTo(25));
            Assert.That(U30Evidence.CpuPercent(1, 0), Is.EqualTo(-1));
        }

        [Test]
        public void LivenessDetectsEightRepeatedRejectionsButNotIntentionalHolding()
        {
            var rejected = Enumerable.Range(1, 8).Select(i => new AiDecision(i, i,
                AiDecisionKind.Recover, AiStrategicGoal.Recover, 0, "Travel rejected: NoDirectRoute")).ToArray();
            Assert.That(U30Evidence.HasRepeatedTravelRejection(rejected), Is.True);
            Assert.That(U30Evidence.HasRepeatedTravelRejection(rejected.Take(7)), Is.False);
            Assert.That(U30Evidence.HasRepeatedTravelRejection(rejected.Take(7).Append(new AiDecision(8, 8,
                AiDecisionKind.AwaitBattle, AiStrategicGoal.AttackSettlement, 0, "Holding"))), Is.False);
        }

        [Test]
        public void JsonRoundTripIsBitExactAndHashCoversRuntimeNotOnlyIdAndTier()
        {
            var state = new CharacterRuntimeState(StableEntityId.Parse("00000000000000000000000000003030"),
                new WorldPosition(1.2345678f, 2, 3), default, true, "remote", 7, 8, 9,
                0.12345678901234567d, 0.99999999999999989d, 10, 11, 12, 13, 14);
            var record = U30TierRuntimeRecord.Capture(CharacterSimulationTier.Tier2, state);
            var decoded = UnityEngine.JsonUtility.FromJson<U30TierRuntimeRecord>(UnityEngine.JsonUtility.ToJson(record));
            Assert.That(BitConverter.DoubleToInt64Bits(decoded.ToState().Energy), Is.EqualTo(BitConverter.DoubleToInt64Bits(state.Energy)));
            Assert.That(BitConverter.DoubleToInt64Bits(decoded.ToState().Hunger), Is.EqualTo(BitConverter.DoubleToInt64Bits(state.Hunger)));
            var original = U30Evidence.CanonicalHash(new byte[] { 1 }, new[] { record });
            Assert.That(U30Evidence.CanonicalHash(new byte[] { 1 }, new[] { decoded }), Is.EqualTo(original));
            decoded.deterministic_accumulator++;
            Assert.That(U30Evidence.CanonicalHash(new byte[] { 1 }, new[] { decoded }), Is.Not.EqualTo(original));
        }
        [Test]
        public void ConfigParsesSegmentAndRejectsRelativeEvidencePaths()
        {
            var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "serenity-u30-config"));
            var config = U30SoakConfig.Parse(new[]
            {
                "Serenity.exe", "-u30Soak", "-u30Seed", "31", "-u30SegmentIndex", "2",
                "-u30SegmentSeconds", "12.5", "-u30CheckpointIntervalSeconds", "3",
                "-u30OutputDirectory", root, "-u30CheckpointRoot", Path.Combine(root, "checkpoints")
            });
            Assert.That(config.seed, Is.EqualTo(31));
            Assert.That(config.TargetSimulationSteps, Is.EqualTo(50));
            Assert.Throws<InvalidOperationException>(() => U30SoakConfig.Parse(new[]
            {
                "Serenity.exe", "-u30Soak", "-u30Seed", "1", "-u30SegmentIndex", "1",
                "-u30SegmentSeconds", "5", "-u30CheckpointIntervalSeconds", "1",
                "-u30OutputDirectory", "relative", "-u30CheckpointRoot", Path.Combine(root, "checkpoints")
            }));
        }

        [Test]
        public void AggregateValidatorFailsClosedOnShortOrIncidentRun()
        {
            var valid = ValidAggregate();
            Assert.That(U30SoakResultValidator.Validate(valid, true), Is.Empty);
            valid.actual_wall_clock_seconds = 28799;
            valid.critical_errors = 1;
            valid.passed = true;
            Assert.That(U30SoakResultValidator.Validate(valid, true), Does.Contain("short_actual_duration"));
            Assert.That(U30SoakResultValidator.Validate(valid, true), Does.Contain("critical_errors"));
            Assert.That(U30SoakResultValidator.Validate(valid, true), Does.Contain("passed_flag_contradicts_evidence"));
        }

        [Test]
        public void AggregateValidatorRejectsUnproductiveWaitingAndUnavailableMemory()
        {
            var result = ValidAggregate();
            result.productive_simulation_seconds = 28799;
            result.memory_metrics_verified = false;
            Assert.That(U30SoakResultValidator.Validate(result, true), Does.Contain("short_productive_duration"));
            Assert.That(U30SoakResultValidator.Validate(result, true), Does.Contain("memory_metrics_unverified"));
        }

        [Test]
        public void TierRuntimeRecordRoundTripsEveryContinuationField()
        {
            var id = StableEntityId.Parse("00000000000000000000000000003030");
            var state = new CharacterRuntimeState(id, new WorldPosition(1, 2, 3), new WorldPosition(4, 5, 6),
                true, "remote", 7, 8, 9, 0.25, 0.75, 10, 11, 12, 13, 14);
            var record = U30TierRuntimeRecord.Capture(CharacterSimulationTier.Tier2, state);
            Assert.That(record.tier, Is.EqualTo((int)CharacterSimulationTier.Tier2));
            Assert.That(record.ToState(), Is.EqualTo(state));
        }

        [Test]
        public void OfficialGateRejectsInvocationOnlyCoverage()
        {
            var result = ValidAggregate();
            result.coverage.production_output_units = 0;
            Assert.That(result.coverage.CoversRequiredSystems, Is.True);
            Assert.That(U30SoakResultValidator.Validate(result, true), Does.Contain("incomplete_observed_workloads"));
            Assert.That(U30SoakResultValidator.Validate(result, true), Does.Contain("passed_flag_contradicts_evidence"));
        }

        [Test]
        public void TierManagerRestoresExactRepresentationStateAtBarrier()
        {
            var registry = new CharacterRegistry();
            var character = Character.Restore(TestCharacterState(
                StableEntityId.Parse("00000000000000000000000000003031")));
            registry.Add(character);
            var first = new FakeAdapter(CharacterSimulationTier.Tier1);
            var second = new FakeAdapter(CharacterSimulationTier.Tier2);
            var third = new FakeAdapter(CharacterSimulationTier.Tier3);
            var manager = new TierManager(registry, first, second, third);
            var initial = new CharacterRuntimeState(character.Id, new WorldPosition(1, 0, 1), default, false);
            first.Materialize(character, initial);
            manager.RegisterExisting(character.Id, CharacterSimulationTier.Tier1);
            var restored = new CharacterRuntimeState(character.Id, new WorldPosition(9, 0, 8),
                new WorldPosition(2, 0, 1), true, "checkpoint", 100, 200, 3, 0.4, 0.6, 300, 400, 5, 6, 7);
            manager.RestoreRepresentationState(character.Id, CharacterSimulationTier.Tier3, restored);
            Assert.That(manager.GetTier(character.Id), Is.EqualTo(CharacterSimulationTier.Tier3));
            Assert.That(manager.GetRuntimeState(character.Id), Is.EqualTo(restored));
            Assert.That(manager.CountActiveRepresentations(character.Id), Is.EqualTo(1));
        }

        [Test]
        public void RequiredCoverageRequiresEverySubsystemAndEitherBattleMode()
        {
            var coverage = new U30Coverage
            {
                production_steps = 1, research_steps = 1, dynasty_checks = 1, health_steps = 1,
                weather_days = 1, military_checks = 1, abstract_battle_steps = 1, fire_steps = 1,
                capture_attempts = 1, global_travel_steps = 1, strategic_ai_steps = 1, event_steps = 1,
                physical_battle_steps = 1, production_output_units = 1, research_active_steps = 1,
                burning_steps = 1, active_travel_steps = 1, successful_captures = 1,
                tier1_steps = 1, tier2_steps = 1, tier3_steps = 1
            };
            Assert.That(coverage.CoversRequiredSystems, Is.True);
            coverage.event_steps = 0;
            Assert.That(coverage.CoversRequiredSystems, Is.False);
        }

        private static U30AggregateResult ValidAggregate() => new U30AggregateResult
        {
            passed = true, seed = 1, requested_wall_clock_seconds = 28800, actual_wall_clock_seconds = 28800,
            started_utc = "2026-10-12T00:00:00.0000000Z", completed_utc = "2026-10-12T08:00:00.0000000Z",
            checkpoint_resume_verified = true, final_state_hash = "ABC", summary = "pass",
            segments_completed = 2, productive_simulation_seconds = 28800, performance_samples = 10,
            memory_metrics_verified = true,
            coverage = new U30Coverage
            {
                production_steps = 1, research_steps = 1, dynasty_checks = 1, health_steps = 1,
                weather_days = 1, military_checks = 1, abstract_battle_steps = 1, fire_steps = 1,
                capture_attempts = 1, global_travel_steps = 1, strategic_ai_steps = 1, event_steps = 1,
                physical_battle_steps = 1, production_output_units = 1, research_active_steps = 1,
                burning_steps = 1, active_travel_steps = 1, successful_captures = 1,
                tier1_steps = 1, tier2_steps = 1, tier3_steps = 1
            },
            incident_journal_path = "incidents.jsonl", incident_journal_sha256 = "AA",
            segment_manifest_path = "segments.jsonl", segment_manifest_sha256 = "BB"
        };

        private static CharacterState TestCharacterState(StableEntityId id) => new CharacterState(id,
            new CharacterName("Soak"), CharacterSex.Male, 0, CharacterLifeState.Alive, null,
            null, null, null, null, null,
            new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
            null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null);

        private sealed class FakeAdapter : ICharacterTierAdapter
        {
            private readonly System.Collections.Generic.Dictionary<StableEntityId, CharacterRuntimeState> values =
                new System.Collections.Generic.Dictionary<StableEntityId, CharacterRuntimeState>();
            public FakeAdapter(CharacterSimulationTier tier) => Tier = tier;
            public CharacterSimulationTier Tier { get; }
            public bool IsActive(StableEntityId characterId) => values.ContainsKey(characterId);
            public void ValidateMaterialization(Character character, CharacterRuntimeState state)
            {
                if (character.Id != state.CharacterId) throw new InvalidOperationException();
            }
            public void Materialize(Character character, CharacterRuntimeState state) => values.Add(character.Id, state);
            public CharacterRuntimeState Capture(StableEntityId characterId) => values[characterId];
            public void Dematerialize(StableEntityId characterId) => values.Remove(characterId);
        }
    }
}
