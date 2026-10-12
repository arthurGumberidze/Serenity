using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.Combat;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("U20")]
    public sealed class TacticalCombatTests
    {
        private static readonly CombatSideId Player = new CombatSideId("player");
        private static readonly CombatSideId Enemy = new CombatSideId("enemy");

        [Test]
        public void EncounterCreationPreservesSidesParticipantsAndStableIdentity()
        {
            var fixture = Fixture.Create(2, 2);
            var state = fixture.Start();
            Assert.That(state.Status, Is.EqualTo(CombatEncounterStatus.Active));
            Assert.That(state.SideA, Is.EqualTo(Player));
            Assert.That(state.SideB, Is.EqualTo(Enemy));
            Assert.That(state.Combatants.Count, Is.EqualTo(4));
            Assert.That(state.Combatants.Select(x => x.CharacterId), Is.Unique);
            Assert.That(fixture.Combat.IsPinnedToTier1(fixture.SideA[0]), Is.True);
        }

        [Test]
        public void DuplicateParticipantAndSideConflictAreRejected()
        {
            var fixture = Fixture.Create(2, 1);
            Assert.Throws<ArgumentException>(() => fixture.Combat.CreateEncounter(Id(800), Player, Enemy,
                new[] { fixture.SideA[0], fixture.SideA[0] }, fixture.SideB, 0));
            Assert.Throws<InvalidOperationException>(() => fixture.Combat.RegisterSide(fixture.SideA[0], Enemy));
        }

        [Test]
        public void DeadIncapacitatedAndChildParticipantsAreRejected()
        {
            var dead = Fixture.Create(1, 1);
            dead.Registry.Get(dead.SideA[0]).MarkDead(0);
            Assert.Throws<InvalidOperationException>(() => dead.Start());

            var injured = Fixture.Create(1, 1);
            injured.Health.ApplyCombatInjury(injured.SideA[0], new HealthConditionId("serious_wound"), 0, 0);
            Assert.Throws<InvalidOperationException>(() => injured.Start());

            var childFixture = Fixture.Create(1, 1);
            var parent = childFixture.Registry.Get(childFixture.SideA[0]);
            var child = MakeCharacter(700, null, new[]
            {
                new ParentLink(parent.Id, ParentRole.Father, ParentageKind.Biological, true)
            });
            childFixture.Registry.Add(child);
            childFixture.Positions.Set(child.Id, new WorldPosition(0, 0, 0));
            childFixture.Combat.RegisterSide(child.Id, Player);
            Assert.That(childFixture.Combat.IsEligible(child.Id), Is.False);
        }

        [Test]
        public void TargetingUsesAssignedThenNearestAndStableIdTieBreak()
        {
            var fixture = Fixture.Create(1, 3);
            var state = fixture.Start();
            var attacker = state.Combatants.Single(x => x.CharacterId == fixture.SideA[0]);
            fixture.Positions.Set(fixture.SideA[0], new WorldPosition(0, 0, 0));
            fixture.Positions.Set(fixture.SideB[0], new WorldPosition(2, 0, 0));
            fixture.Positions.Set(fixture.SideB[1], new WorldPosition(-2, 0, 0));
            fixture.Positions.Set(fixture.SideB[2], new WorldPosition(5, 0, 0));
            var targeting = new CombatTargetingService(fixture.Registry, fixture.Health, fixture.Positions);
            var tied = new[] { state.Combatants.Single(x => x.CharacterId == fixture.SideB[1]),
                state.Combatants.Single(x => x.CharacterId == fixture.SideB[0]), attacker };
            Assert.That(targeting.Select(attacker, tied), Is.EqualTo(fixture.SideB.Min()));
            var assigned = attacker.With(assignedTargetId: fixture.SideB[2]);
            Assert.That(targeting.Select(assigned, state.Combatants), Is.EqualTo(fixture.SideB[2]));
        }

        [Test]
        public void TargetingRejectsDeadAndIncapacitatedTargets()
        {
            var fixture = Fixture.Create(1, 2);
            var state = fixture.Start();
            fixture.Health.ApplyCombatInjury(fixture.SideB[0], new HealthConditionId("serious_wound"), 0, 0);
            fixture.Succession.ProcessDeath(fixture.SideB[1], 0, 0);
            var targeting = new CombatTargetingService(fixture.Registry, fixture.Health, fixture.Positions);
            var attacker = state.Combatants.Single(x => x.CharacterId == fixture.SideA[0]);
            Assert.That(targeting.Select(attacker, state.Combatants), Is.Null);
        }

        [Test]
        public void AttackRequiresRangeAndCooldownAndIncrementsSequenceExactlyOnce()
        {
            var fixture = Fixture.Create(1, 1, initialDistance: 10f);
            fixture.Start(_ => new CombatWeaponId("unarmed"));
            fixture.Positions.MovesInstantly = false;
            fixture.Combat.AdvanceTo(1, 0);
            Assert.That(fixture.Combat.Encounters.Single().AttackHistory, Is.Empty);
            fixture.Positions.MovesInstantly = true;
            fixture.Positions.Set(fixture.SideA[0], new WorldPosition(0, 0, 0));
            fixture.Positions.Set(fixture.SideB[0], new WorldPosition(1, 0, 0));
            fixture.Combat.AdvanceTo(TimeSpan.FromHours(1).Ticks, 0);
            var firstCount = fixture.Combat.Encounters.Single().AttackHistory.Count;
            var firstSequences = fixture.Combat.Encounters.Single().Combatants.Sum(x => (long)x.AttackSequence);
            Assert.That(firstCount, Is.EqualTo(2));
            Assert.That(firstSequences, Is.EqualTo(2));
            fixture.Combat.AdvanceTo(TimeSpan.FromHours(1).Ticks, 0);
            Assert.That(fixture.Combat.Encounters.Single().AttackHistory.Count, Is.EqualTo(firstCount));
        }

        [Test]
        public void CombatInjuryEscalatesThroughU18AndIncapacitatedCombatantStopsAttacking()
        {
            var fixture = Fixture.Create(1, 1);
            fixture.Start(id => id == fixture.SideA[0] ? new CombatWeaponId("stone_axe") : new CombatWeaponId("unarmed"));
            for (var i = 1; i <= 4 && fixture.Combat.Encounters.Single().Status == CombatEncounterStatus.Active; i++)
                fixture.Combat.AdvanceTo(TimeSpan.FromHours(i).Ticks, 0);
            var target = fixture.Combat.Encounters.Single().Combatants.Single(x => x.CharacterId == fixture.SideB[0]);
            Assert.That(fixture.Health.Conditions.Any(x => x.TargetId == target.CharacterId), Is.True);
            Assert.That(target.State, Is.EqualTo(CombatantState.Incapacitated).Or.EqualTo(CombatantState.Dead));
            var sequence = target.AttackSequence;
            fixture.Combat.AdvanceTo(TimeSpan.FromHours(6).Ticks, 0);
            Assert.That(fixture.Combat.Encounters.Single().Combatants.Single(x => x.CharacterId == target.CharacterId)
                .AttackSequence, Is.EqualTo(sequence));
        }

        [Test]
        public void FatalCombatInjuryUsesU17SuccessionExactlyOnce()
        {
            var registry = new CharacterRegistry();
            var dynastyId = Id(950);
            var monarch = MakeCharacter(951, dynastyId);
            var heir = MakeCharacter(952, dynastyId);
            registry.Add(monarch); registry.Add(heir);
            var dynasty = DynastyState.Create(dynastyId, "Combat House", monarch.Id, monarch.Id,
                new[] { monarch.Id, heir.Id });
            var succession = new SuccessionService(registry, dynasty, new PlayerRealmState(monarch.Id), new GameRunState());
            var health = new HealthAndFamilyService(registry, HealthConditionCatalog.CreateStoneAge(), 55, succession);
            var result = health.ApplyCombatInjury(monarch.Id, new HealthConditionId("serious_wound"), 5, 7, true);
            var repeated = health.ApplyCombatInjury(monarch.Id, new HealthConditionId("serious_wound"), 5, 7, true);
            Assert.That(result.Fatal, Is.True);
            Assert.That(repeated.Applied, Is.False);
            Assert.That(monarch.LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(dynasty.CurrentMonarchId, Is.EqualTo(heir.Id));
            Assert.That(dynasty.History.Count, Is.EqualTo(1));
        }

        [Test]
        public void AllyCasualtiesBoundMoraleAndCauseBreakRetreatAndDisengage()
        {
            var fixture = Fixture.Create(4, 1);
            fixture.Start();
            for (var i = 0; i < 3; i++)
                fixture.Health.ApplyCombatInjury(fixture.SideA[i], new HealthConditionId("serious_wound"), 0, 0);
            fixture.Combat.AdvanceTo(1, 0);
            var survivorId = fixture.SideA[3];
            var survivor = fixture.Combat.Encounters.Single().Combatants.Single(x => x.CharacterId == survivorId);
            Assert.That(survivor.Morale, Is.InRange(0, CombatService.BreakThreshold - 1));
            Assert.That(survivor.State, Is.EqualTo(CombatantState.Retreating));
            fixture.Combat.AdvanceTo(2, 0);
            fixture.Combat.AdvanceTo(CombatService.RetreatDuration.Ticks + 3, 0);
            var final = fixture.Combat.Encounters.Single();
            Assert.That(final.Combatants.Single(x => x.CharacterId == survivorId).State, Is.EqualTo(CombatantState.Disengaged));
            Assert.That(final.Outcome, Is.EqualTo(CombatOutcome.SideBVictory));
        }

        [Test]
        public void ExplicitRetreatStopsAttacksAndEndsEncounterExactlyOnce()
        {
            var fixture = Fixture.Create(1, 1);
            fixture.Start();
            Assert.That(fixture.Combat.OrderRetreat(fixture.SideA[0], 0), Is.True);
            fixture.Combat.AdvanceTo(1, 0);
            var attacks = fixture.Combat.Encounters.Single().AttackHistory.Count;
            fixture.Combat.AdvanceTo(CombatService.RetreatDuration.Ticks + 1, 0);
            var finished = fixture.Combat.Encounters.Single();
            Assert.That(finished.Status, Is.EqualTo(CombatEncounterStatus.Finished));
            Assert.That(finished.Outcome, Is.EqualTo(CombatOutcome.SideBVictory));
            Assert.That(finished.AttackHistory.Count, Is.EqualTo(attacks));
            fixture.Combat.AdvanceTo(CombatService.RetreatDuration.Ticks + 2, 0);
            Assert.That(fixture.Combat.Encounters.Single().Outcome, Is.EqualTo(CombatOutcome.SideBVictory));
        }

        [Test]
        public void StrongerSideCompletesBattleWithValidOutcome()
        {
            var fixture = Fixture.Create(3, 3);
            fixture.Start(id => fixture.SideA.Contains(id) ? new CombatWeaponId("stone_axe") : new CombatWeaponId("unarmed"));
            RunToCompletion(fixture);
            var encounter = fixture.Combat.Encounters.Single();
            Assert.That(encounter.Status, Is.EqualTo(CombatEncounterStatus.Finished));
            Assert.That(encounter.Outcome, Is.EqualTo(CombatOutcome.SideAVictory));
            Assert.That(encounter.AttackHistory.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ActiveCombatPinsTier1AndReleasesPinAfterFinish()
        {
            var fixture = Fixture.Create(1, 1);
            fixture.Start();
            Assert.That(fixture.Combat.CanTransition(fixture.SideA[0], CharacterSimulationTier.Tier2), Is.False);
            fixture.Combat.CancelEncounter(fixture.EncounterId);
            Assert.That(fixture.Combat.CanTransition(fixture.SideA[0], CharacterSimulationTier.Tier2), Is.True);
        }

        [Test]
        public void ActiveCombatRoundTripsV7WithoutDuplicateAttackOrTargetReroll()
        {
            var source = Fixture.Create(2, 2);
            source.Start();
            source.Combat.AssignTarget(source.SideA[0], source.SideB[1]);
            source.Combat.AdvanceTo(TimeSpan.FromHours(1).Ticks, 0);
            var snapshot = new SaveSnapshot(SaveSnapshot.CombatVersion, Id(990),
                new GameTimeState(TimeSpan.FromHours(1).Ticks, 0, 1, false), 400,
                healthFamily: source.Health.CaptureState(), combat: source.Combat.CaptureState());
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(decoded.FormatVersion, Is.EqualTo(7));

            var restored = Fixture.Create(2, 2);
            restored.Health.RestoreState(decoded.HealthFamily);
            restored.Combat.RestoreState(decoded.Combat);
            var before = restored.Combat.Encounters.Single();
            var target = before.Combatants.Single(x => x.CharacterId == restored.SideA[0]).AssignedTargetId;
            var history = before.AttackHistory.Count;
            restored.Combat.AdvanceTo(TimeSpan.FromHours(1).Ticks, 0);
            var sameTick = restored.Combat.Encounters.Single();
            Assert.That(sameTick.AttackHistory.Count, Is.EqualTo(history));
            Assert.That(sameTick.Combatants.Single(x => x.CharacterId == restored.SideA[0]).AssignedTargetId,
                Is.EqualTo(target));
            Assert.That(restored.Health.Conditions.Count, Is.EqualTo(source.Health.Conditions.Count));
        }

        [Test]
        public void FatalityAndFinishedOutcomeRoundTripWithoutDuplicateResolution()
        {
            var source = Fixture.Create(1, 1);
            source.Start();
            var fatal = source.Health.ApplyCombatInjury(source.SideB[0],
                new HealthConditionId("serious_wound"), 1, 1, true);
            source.Combat.AdvanceTo(1, 1);
            var finished = source.Combat.Encounters.Single();
            Assert.That(fatal.Fatal, Is.True);
            Assert.That(finished.Status, Is.EqualTo(CombatEncounterStatus.Finished));
            Assert.That(finished.Outcome, Is.EqualTo(CombatOutcome.SideAVictory));

            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(991),
                new GameTimeState(1, 1, 1, false), 400,
                dynastyWorld: source.Succession.CaptureState(),
                healthFamily: source.Health.CaptureState(),
                combat: source.Combat.CaptureState());
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));
            var restored = Fixture.Create(1, 1);
            restored.Succession.RestoreState(decoded.DynastyWorld);
            restored.Health.RestoreState(decoded.HealthFamily);
            restored.Combat.RestoreState(decoded.Combat);
            var before = restored.Combat.Encounters.Single();
            var attackCount = before.AttackHistory.Count;
            var dynastyHistoryCount = restored.Succession.Dynasty.History.Count;

            restored.Combat.AdvanceTo(2, 1);
            var repeated = restored.Health.ApplyCombatInjury(restored.SideB[0],
                new HealthConditionId("serious_wound"), 2, 1, true);
            var after = restored.Combat.Encounters.Single();
            Assert.That(repeated.Applied, Is.False);
            Assert.That(restored.Registry.Get(restored.SideB[0]).LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(after.Status, Is.EqualTo(CombatEncounterStatus.Finished));
            Assert.That(after.Outcome, Is.EqualTo(CombatOutcome.SideAVictory));
            Assert.That(after.AttackHistory.Count, Is.EqualTo(attackCount));
            Assert.That(restored.Succession.Dynasty.History.Count, Is.EqualTo(dynastyHistoryCount));
        }

        [Test]
        public void LegacySnapshotsV1ThroughV6StillLoad()
        {
            var codec = new SaveXmlCodec();
            for (var version = 1; version <= 6; version++)
            {
                var snapshot = new SaveSnapshot(version, Id(1000 + version), new GameTimeState(), 400);
                var decoded = codec.Deserialize(codec.Serialize(snapshot));
                Assert.That(decoded.FormatVersion, Is.EqualTo(version));
                Assert.That(decoded.Combat, Is.Null);
            }
        }

        [Test]
        [Category("U31")]
        public void LogicalOutcomeIsDeterministicAndCollectionOrderIndependent()
        {
            var first = Fixture.Create(5, 5);
            var second = Fixture.Create(5, 5, reverseRegistration: true);
            first.Start(id => first.SideA.Contains(id) ? new CombatWeaponId("stone_axe") : new CombatWeaponId("stone_spear"));
            second.Start(id => second.SideA.Contains(id) ? new CombatWeaponId("stone_axe") : new CombatWeaponId("stone_spear"), reverseInputs: true);
            RunToCompletion(first);
            RunToCompletion(second);
            Assert.That(second.Combat.ComputeStateHash(), Is.EqualTo(first.Combat.ComputeStateHash()));
            Assert.That(second.Health.ComputeStateHash(), Is.EqualTo(first.Health.ComputeStateHash()));
        }

        [Test]
        public void OneHundredCombatantStressCompletesWithoutRunawayFailure()
        {
            var fixture = Fixture.Create(50, 50, initialDistance: 1f);
            fixture.Start(id => fixture.SideA.Contains(id) ? new CombatWeaponId("stone_axe") : new CombatWeaponId("stone_spear"));
            var stopwatch = Stopwatch.StartNew();
            var beforeAllocated = GC.GetAllocatedBytesForCurrentThread();
            RunToCompletion(fixture, 30);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - beforeAllocated;
            stopwatch.Stop();
            var encounter = fixture.Combat.Encounters.Single();
            Assert.That(encounter.Combatants.Count, Is.EqualTo(100));
            Assert.That(encounter.Status, Is.EqualTo(CombatEncounterStatus.Finished));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
            TestContext.WriteLine($"U20 50v50 logical stress: {stopwatch.Elapsed.TotalMilliseconds:0.###} ms, " +
                                  $"allocatedBytes={allocated}, attacks={encounter.AttackHistory.Count}, " +
                                  $"hash={fixture.Combat.ComputeStateHash():X16}");
        }

        private static void RunToCompletion(Fixture fixture, int maximumSteps = 20)
        {
            for (var step = 1; step <= maximumSteps &&
                 fixture.Combat.Encounters.Single().Status == CombatEncounterStatus.Active; step++)
                fixture.Combat.AdvanceTo(TimeSpan.FromHours(step).Ticks, 0);
        }

        private static Character MakeCharacter(int value, StableEntityId? dynastyId = null,
            IEnumerable<ParentLink> parents = null, int strength = 50) => Character.Restore(new CharacterState(
            Id(value), new CharacterName("Fighter" + value), CharacterSex.Male, 0,
            CharacterLifeState.Alive, null, parents, null, dynastyId, dynastyId, null,
            new[] { new TraitId("brave"), new TraitId("calm"), new TraitId("fit"), new TraitId("steady") },
            new[] { new SkillState(new SkillId("combat"), new SkillLevel(40)) },
            new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(strength)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        private sealed class MemoryPositions : ICombatPositionProvider
        {
            private readonly Dictionary<StableEntityId, WorldPosition> values = new Dictionary<StableEntityId, WorldPosition>();
            public bool MovesInstantly { get; set; } = true;
            public void Set(StableEntityId id, WorldPosition value) => values[id] = value;
            public bool TryGetPosition(StableEntityId characterId, out WorldPosition position) => values.TryGetValue(characterId, out position);
            public void MoveTo(StableEntityId characterId, WorldPosition target, float stoppingDistance)
            {
                if (MovesInstantly) values[characterId] = target;
            }
            public void Stop(StableEntityId characterId) { }
        }

        private sealed class Fixture
        {
            public CharacterRegistry Registry;
            public HealthAndFamilyService Health;
            public SuccessionService Succession;
            public MemoryPositions Positions;
            public CombatService Combat;
            public StableEntityId[] SideA;
            public StableEntityId[] SideB;
            public StableEntityId EncounterId = Id(799);

            public static Fixture Create(int sideACount, int sideBCount, bool reverseRegistration = false,
                float initialDistance = 1f)
            {
                var fixture = new Fixture
                {
                    Registry = new CharacterRegistry(),
                    Positions = new MemoryPositions(),
                    SideA = Enumerable.Range(1, sideACount).Select(Id).ToArray(),
                    SideB = Enumerable.Range(101, sideBCount).Select(Id).ToArray()
                };
                var guardian = MakeCharacter(9000, Id(9001));
                var characters = fixture.SideA.Concat(fixture.SideB).Select(x => MakeCharacter(int.Parse(x.ToString().Substring(24),
                    System.Globalization.NumberStyles.HexNumber))).Concat(new[] { guardian }).ToArray();
                foreach (var character in reverseRegistration ? characters.Reverse() : characters) fixture.Registry.Add(character);
                var dynasty = DynastyState.Create(Id(9001), "Fixture House", guardian.Id, guardian.Id, new[] { guardian.Id });
                fixture.Succession = new SuccessionService(fixture.Registry, dynasty,
                    new PlayerRealmState(guardian.Id), new GameRunState());
                fixture.Health = new HealthAndFamilyService(fixture.Registry, HealthConditionCatalog.CreateStoneAge(), 2020,
                    fixture.Succession);
                fixture.Combat = new CombatService(fixture.Registry, fixture.Health, fixture.Positions, 2020);
                for (var i = 0; i < fixture.SideA.Length; i++)
                {
                    fixture.Positions.Set(fixture.SideA[i], new WorldPosition(0f, 0f, i * 0.05f));
                    fixture.Combat.RegisterSide(fixture.SideA[i], Player);
                }
                for (var i = 0; i < fixture.SideB.Length; i++)
                {
                    fixture.Positions.Set(fixture.SideB[i], new WorldPosition(initialDistance, 0f, i * 0.05f));
                    fixture.Combat.RegisterSide(fixture.SideB[i], Enemy);
                }
                return fixture;
            }

            public CombatEncounterState Start(Func<StableEntityId, CombatWeaponId> weapons = null,
                bool reverseInputs = false) => Combat.CreateEncounter(EncounterId, Player, Enemy,
                reverseInputs ? SideA.Reverse() : SideA, reverseInputs ? SideB.Reverse() : SideB, 0, weapons);
        }
    }
}
