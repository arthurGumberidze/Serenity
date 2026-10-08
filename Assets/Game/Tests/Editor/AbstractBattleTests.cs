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
using Game.Domain.Military;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.AI;
using Game.Simulation.Combat;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Military;
using Game.Simulation.Tiers;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("U23")]
    public sealed class AbstractBattleTests
    {
        private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

        [Test]
        public void SharedParametersDrivePhysicalAndAbstractResolution()
        {
            var fixture = UnitFixture.Create(2);
            fixture.ToAggregate();
            var battle = fixture.Battles.StartAbstractBattle(fixture.FirstUnit, fixture.SecondUnit,
                Id(8000), 0, _ => new CombatWeaponId("stone_spear"));

            Assert.That(battle.BattleId, Is.EqualTo(Id(8000)));
            Assert.That(battle.Mode, Is.EqualTo(BattleMode.Abstract));
            Assert.That(battle.SideAUnitId, Is.EqualTo(fixture.FirstUnit));
            Assert.That(battle.SideBUnitId, Is.EqualTo(fixture.SecondUnit));
            Assert.That(battle.BattleHistory.Single().Kind, Is.EqualTo(BattleEventKind.Started));
            var participant = battle.Combatants[0];
            var shared = fixture.Combat.GetStats(participant.CharacterId, participant.WeaponId);
            Assert.That(shared.AttackIntervalTicks,
                Is.EqualTo(fixture.Combat.Weapons.Get(participant.WeaponId).AttackIntervalTicks));
            Assert.That(CombatRules.SelectInjury(CombatRules.CalculateImpact(shared, shared),
                fixture.Combat.Weapons.Get(participant.WeaponId).MinimumInjury).IsValid, Is.True);
        }

        [Test]
        public void AbstractBattleAttacksRespectCooldownApplyCanonicalInjuryMoraleAndOutcome()
        {
            var fixture = UnitFixture.Create(1);
            fixture.ToAggregate();
            fixture.Battles.StartAbstractBattle(fixture.FirstUnit, fixture.SecondUnit, Id(8001), 0,
                _ => new CombatWeaponId("stone_spear"));

            fixture.Battles.AdvanceTo(Minute.Ticks, 0);
            var first = fixture.Combat.Encounters.Single();
            Assert.That(first.AttackHistory.Count, Is.EqualTo(2));
            Assert.That(fixture.Health.Conditions.Count, Is.EqualTo(2));
            Assert.That(first.Combatants.All(x => x.AttackSequence == 1), Is.True);
            fixture.Battles.AdvanceTo(TimeSpan.FromMinutes(4).Ticks, 0);
            Assert.That(fixture.Combat.Encounters.Single().AttackHistory.Count, Is.EqualTo(2));

            RunUnitBattleToCompletion(fixture);
            var finished = fixture.Combat.Encounters.Single();
            Assert.That(finished.Status, Is.EqualTo(CombatEncounterStatus.Finished));
            Assert.That(finished.Outcome, Is.Not.EqualTo(CombatOutcome.None));
            Assert.That(finished.Combatants.Any(x => x.State == CombatantState.Incapacitated ||
                x.State == CombatantState.Dead || x.State == CombatantState.Disengaged), Is.True);
            Assert.That(finished.BattleHistory.Last().Kind, Is.EqualTo(BattleEventKind.Finished));
            Assert.That(fixture.Units.Get(fixture.FirstUnit).ActiveEncounterId, Is.Null);
            Assert.That(fixture.Units.Get(fixture.SecondUnit).ActiveEncounterId, Is.Null);
        }

        [Test]
        public void PhysicalAbstractPhysicalPreservesIdentityHealthMoraleAndMidCooldownExactly()
        {
            var fixture = UnitFixture.Create(2);
            fixture.Battles.StartPhysicalBattle(fixture.FirstUnit, fixture.SecondUnit, Id(8002), 0,
                _ => new CombatWeaponId("stone_spear"));
            fixture.Battles.AdvanceTo(Minute.Ticks, 0);
            var before = fixture.Combat.Encounters.Single();
            var attackCount = before.AttackHistory.Count;
            var conditionIds = fixture.Health.Conditions.Select(x => x.InstanceId).OrderBy(x => x).ToArray();
            var sequences = before.Combatants.ToDictionary(x => x.CharacterId, x => x.AttackSequence);
            var cooldowns = before.Combatants.ToDictionary(x => x.CharacterId, x => x.NextAttackTick);
            var morale = before.Combatants.ToDictionary(x => x.CharacterId, x => x.Morale);

            var toAbstract = fixture.Battles.Transition(before.BattleId, BattleMode.Abstract, Minute.Ticks);
            Assert.That(toAbstract.Status, Is.EqualTo(BattleTransitionStatus.Changed));
            Assert.That(fixture.Units.Get(fixture.FirstUnit).Representation,
                Is.EqualTo(MilitaryUnitRepresentationMode.Aggregate));
            fixture.Battles.AdvanceTo(TimeSpan.FromMinutes(2).Ticks, 0);
            var during = fixture.Combat.Encounters.Single();
            Assert.That(during.AttackHistory.Count, Is.EqualTo(attackCount), "mid-cooldown transition repeated a hit");
            Assert.That(during.Combatants.ToDictionary(x => x.CharacterId, x => x.AttackSequence), Is.EqualTo(sequences));
            Assert.That(during.Combatants.ToDictionary(x => x.CharacterId, x => x.NextAttackTick), Is.EqualTo(cooldowns));
            Assert.That(during.Combatants.ToDictionary(x => x.CharacterId, x => x.Morale), Is.EqualTo(morale));
            Assert.That(fixture.Health.Conditions.Select(x => x.InstanceId).OrderBy(x => x), Is.EqualTo(conditionIds));

            var toPhysical = fixture.Battles.Transition(before.BattleId, BattleMode.Physical,
                TimeSpan.FromMinutes(2).Ticks);
            Assert.That(toPhysical.Status, Is.EqualTo(BattleTransitionStatus.Changed));
            Assert.That(fixture.Combat.Encounters.Single().BattleId, Is.EqualTo(before.BattleId));
            Assert.That(fixture.Combat.Encounters.Single().Combatants.Select(x => x.CharacterId),
                Is.EqualTo(before.Combatants.Select(x => x.CharacterId)));
            Assert.That(fixture.Battles.Transition(before.BattleId, BattleMode.Abstract,
                TimeSpan.FromMinutes(2).Ticks).Changed, Is.True);
            Assert.That(fixture.Battles.Transition(before.BattleId, BattleMode.Physical,
                TimeSpan.FromMinutes(2).Ticks).Changed, Is.True);
        }

        [Test]
        public void DirectlyControlledParticipantBlocksAbstractionUntilReleased()
        {
            var fixture = UnitFixture.Create(1);
            var battle = fixture.Battles.StartPhysicalBattle(fixture.FirstUnit, fixture.SecondUnit, Id(8003), 0);
            fixture.DirectControl.Controlled.Add(battle.Combatants[0].CharacterId);
            Assert.That(fixture.Battles.Transition(battle.BattleId, BattleMode.Abstract, 0).Status,
                Is.EqualTo(BattleTransitionStatus.BlockedByDirectControl));
            Assert.That(fixture.Combat.Encounters.Single().Mode, Is.EqualTo(BattleMode.Physical));
            fixture.DirectControl.Controlled.Clear();
            Assert.That(fixture.Battles.Transition(battle.BattleId, BattleMode.Abstract, 0).Status,
                Is.EqualTo(BattleTransitionStatus.Changed));
        }

        [Test]
        public void RetreatStateSurvivesBothTransitionsAndTerminalBattleRejectsFurtherTransition()
        {
            var fixture = UnitFixture.Create(2);
            var battle = fixture.Battles.StartPhysicalBattle(fixture.FirstUnit, fixture.SecondUnit, Id(8010), 0);
            var retreatingId = battle.Combatants.First(x => x.SideId == battle.SideA).CharacterId;
            Assert.That(fixture.Combat.OrderRetreat(retreatingId, 0), Is.True);
            Assert.That(fixture.Battles.Transition(battle.BattleId, BattleMode.Abstract, 0).Changed, Is.True);
            fixture.Battles.AdvanceTo(Minute.Ticks, 0);
            Assert.That(fixture.Combat.Encounters.Single().Combatants.Single(x => x.CharacterId == retreatingId).State,
                Is.EqualTo(CombatantState.Retreating));
            Assert.That(fixture.Battles.Transition(battle.BattleId, BattleMode.Physical, Minute.Ticks).Changed, Is.True);
            Assert.That(fixture.Combat.Encounters.Single().Combatants.Single(x => x.CharacterId == retreatingId).State,
                Is.EqualTo(CombatantState.Retreating));
            Assert.That(fixture.Combat.CancelEncounter(battle.BattleId), Is.True);
            Assert.That(fixture.Battles.Transition(battle.BattleId, BattleMode.Abstract, Minute.Ticks).Status,
                Is.EqualTo(BattleTransitionStatus.BattleFinished));
        }

        [Test]
        public void PhysicalAbstractAndMixedRoutesAllFinishWithoutIdentityReset()
        {
            var physical = UnitFixture.Create(2);
            var p = physical.Battles.StartPhysicalBattle(physical.FirstUnit, physical.SecondUnit, Id(8011), 0);
            RunUnitBattleToCompletion(physical);

            var abstractOnly = UnitFixture.Create(2);
            abstractOnly.ToAggregate();
            var a = abstractOnly.Battles.StartAbstractBattle(abstractOnly.FirstUnit, abstractOnly.SecondUnit,
                Id(8011), 0);
            RunUnitBattleToCompletion(abstractOnly);

            var physicalAbstract = UnitFixture.Create(2);
            var pa = physicalAbstract.Battles.StartPhysicalBattle(physicalAbstract.FirstUnit,
                physicalAbstract.SecondUnit, Id(8011), 0);
            physicalAbstract.Battles.AdvanceTo(Minute.Ticks, 0);
            Assert.That(physicalAbstract.Battles.Transition(pa.BattleId, BattleMode.Abstract,
                Minute.Ticks).Changed, Is.True);
            RunUnitBattleToCompletion(physicalAbstract);

            var abstractPhysical = UnitFixture.Create(2);
            abstractPhysical.ToAggregate();
            var ap = abstractPhysical.Battles.StartAbstractBattle(abstractPhysical.FirstUnit,
                abstractPhysical.SecondUnit, Id(8011), 0);
            abstractPhysical.Battles.AdvanceTo(TimeSpan.FromMinutes(2).Ticks, 0);
            Assert.That(abstractPhysical.Battles.Transition(ap.BattleId, BattleMode.Physical,
                TimeSpan.FromMinutes(2).Ticks).Changed, Is.True);
            RunUnitBattleToCompletion(abstractPhysical);

            var repeated = UnitFixture.Create(2);
            var pap = repeated.Battles.StartPhysicalBattle(repeated.FirstUnit, repeated.SecondUnit, Id(8011), 0);
            Assert.That(repeated.Battles.Transition(pap.BattleId, BattleMode.Abstract, 0).Changed, Is.True);
            Assert.That(repeated.Battles.Transition(pap.BattleId, BattleMode.Physical, 0).Changed, Is.True);
            RunUnitBattleToCompletion(repeated);

            foreach (var value in new[] { physical, abstractOnly, physicalAbstract, abstractPhysical, repeated })
            {
                var finished = value.Combat.Encounters.Single();
                Assert.That(finished.Status, Is.EqualTo(CombatEncounterStatus.Finished));
                Assert.That(finished.BattleId, Is.EqualTo(Id(8011)));
                Assert.That(finished.Combatants.Select(x => x.CharacterId).Distinct().Count(), Is.EqualTo(4));
            }
            Assert.That(Math.Abs(Casualties(physical.Combat.Encounters.Single()) -
                Casualties(abstractOnly.Combat.Encounters.Single())), Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void AbstractBattleV10RoundTripResumesWithoutRerollAndLegacyV1ThroughV9RemainReadable()
        {
            var source = UnitFixture.Create(2);
            source.ToAggregate();
            source.Battles.StartAbstractBattle(source.FirstUnit, source.SecondUnit, Id(8004), 0,
                _ => new CombatWeaponId("stone_spear"));
            source.Battles.AdvanceTo(TimeSpan.FromMinutes(6).Ticks, 0);
            var codec = new SaveXmlCodec();
            var snapshot = new SaveSnapshot(SaveSnapshot.BattleVersion, Id(8999),
                new GameTimeState(TimeSpan.FromMinutes(6).Ticks, 0, 1, false), 400,
                healthFamily: source.Health.CaptureState(), combat: source.Combat.CaptureState(),
                military: source.Units.CaptureState());
            var decoded = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(decoded.FormatVersion, Is.EqualTo(10));
            Assert.That(decoded.Combat.Encounters.Single().Mode, Is.EqualTo(BattleMode.Abstract));
            Assert.That(decoded.Combat.Encounters.Single().BattleHistory.Count, Is.GreaterThan(0));

            var restored = UnitFixture.Create(2);
            restored.Health.RestoreState(decoded.HealthFamily);
            restored.Combat.RestoreState(decoded.Combat);
            restored.Units.RestoreState(decoded.Military, restored.Combat);
            restored.Battles.AdvanceTo(TimeSpan.FromMinutes(20).Ticks, 0);
            source.Battles.AdvanceTo(TimeSpan.FromMinutes(20).Ticks, 0);
            Assert.That(restored.Combat.ComputeStateHash(), Is.EqualTo(source.Combat.ComputeStateHash()));
            Assert.That(restored.Health.Conditions.Select(x => x.DefinitionId),
                Is.EqualTo(source.Health.Conditions.Select(x => x.DefinitionId)));

            for (var version = 1; version <= 9; version++)
            {
                var legacy = new SaveSnapshot(version, Id(8100 + version), new GameTimeState(), 400);
                Assert.That(codec.Deserialize(codec.Serialize(legacy)).FormatVersion, Is.EqualTo(version));
            }
        }

        [Test]
        public void AbstractCatchUpAndCollectionOrderAreExactlyDeterministic()
        {
            var direct = CombatFixture.Create(6, 6, 2301, BattleMode.Abstract, false);
            var stepped = CombatFixture.Create(6, 6, 2301, BattleMode.Abstract, true);
            direct.Combat.AdvanceTo(TimeSpan.FromMinutes(60).Ticks, 0);
            for (var minute = 1; minute <= 60; minute++)
                stepped.Combat.AdvanceTo(TimeSpan.FromMinutes(minute).Ticks, 0);
            Assert.That(direct.Combat.ComputeStateHash(), Is.EqualTo(stepped.Combat.ComputeStateHash()));
            Assert.That(direct.Health.ComputeStateHash(), Is.EqualTo(stepped.Health.ComputeStateHash()));
            TestContext.WriteLine($"U23 abstract determinism/catch-up hash {direct.Combat.ComputeStateHash():X16}");
        }

        [Test]
        public void TenSimultaneousAbstractBattlesAreIsolatedAndProcessingOrderIndependent()
        {
            var forward = MultiBattleWorld.Create(10, false);
            var reverse = MultiBattleWorld.Create(10, true);
            forward.Combat.AdvanceTo(TimeSpan.FromMinutes(90).Ticks, 0);
            reverse.Combat.AdvanceTo(TimeSpan.FromMinutes(90).Ticks, 0);
            Assert.That(forward.Combat.Encounters.Count, Is.EqualTo(10));
            Assert.That(forward.Combat.Encounters.SelectMany(x => x.Combatants).Select(x => x.CharacterId)
                .Distinct().Count(), Is.EqualTo(20));
            Assert.That(forward.Combat.ComputeStateHash(), Is.EqualTo(reverse.Combat.ComputeStateHash()));
            TestContext.WriteLine($"U23 10-battle hash {forward.Combat.ComputeStateHash():X16}");
        }

        [Test]
        public void OffCameraMonarchAndCaptainDeathUsesSuccessionAndFallbackExactlyOnce()
        {
            var fixture = UnitFixture.Create(2);
            fixture.ToAggregate();
            var battle = fixture.Battles.StartAbstractBattle(fixture.FirstUnit, fixture.SecondUnit, Id(8005), 0,
                _ => new CombatWeaponId("stone_spear"));
            var monarch = fixture.Units.Get(fixture.FirstUnit).CaptainId.Value;
            fixture.Health.ApplyCombatInjury(monarch, new HealthConditionId("serious_wound"), 0, 0, true);
            fixture.Battles.AdvanceTo(Minute.Ticks, 0);
            var unit = fixture.Units.Get(fixture.FirstUnit);
            Assert.That(fixture.Registry.Get(monarch).LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(fixture.Succession.Dynasty.CurrentMonarchId, Is.Not.EqualTo(monarch));
            Assert.That(fixture.Succession.Dynasty.History.Count, Is.EqualTo(1));
            Assert.That(unit.CaptainId, Is.Not.EqualTo(monarch));
            Assert.That(fixture.Combat.Encounters.Single().BattleHistory.Any(x =>
                x.Kind == BattleEventKind.CaptainDeath && x.CharacterId == monarch), Is.True);
            fixture.Battles.AdvanceTo(TimeSpan.FromMinutes(2).Ticks, 0);
            Assert.That(fixture.Succession.Dynasty.History.Count, Is.EqualTo(1));
        }

        [Test]
        public void PhysicalAndAbstractOutcomesConvergeAcrossOneHundredPairedScenarios()
        {
            const int scenarios = 100;
            var winnerMismatch = 0;
            double physicalCasualties = 0, abstractCasualties = 0;
            double physicalDuration = 0, abstractDuration = 0;
            for (var seed = 1; seed <= scenarios; seed++)
            {
                var physical = CombatFixture.Create(3, 3, seed, BattleMode.Physical, seed % 2 == 0);
                var abstractBattle = CombatFixture.Create(3, 3, seed, BattleMode.Abstract, seed % 2 != 0);
                var physicalMinutes = RunCombatToCompletion(physical);
                var abstractMinutes = RunCombatToCompletion(abstractBattle);
                var p = physical.Combat.Encounters.Single();
                var a = abstractBattle.Combat.Encounters.Single();
                if (p.Outcome != a.Outcome) winnerMismatch++;
                physicalCasualties += Casualties(p);
                abstractCasualties += Casualties(a);
                physicalDuration += physicalMinutes;
                abstractDuration += abstractMinutes;
            }
            var winRateDelta = winnerMismatch / (double)scenarios;
            var meanCasualtyDelta = Math.Abs(physicalCasualties - abstractCasualties) / scenarios;
            var meanDurationDelta = Math.Abs(physicalDuration - abstractDuration) / scenarios;
            TestContext.WriteLine($"U23 convergence n={scenarios}: win delta {winRateDelta:P1}, " +
                $"mean casualty delta {meanCasualtyDelta:0.###}, mean duration delta {meanDurationDelta:0.###} minutes");
            Assert.That(winRateDelta, Is.LessThanOrEqualTo(0.15));
            Assert.That(meanCasualtyDelta, Is.LessThanOrEqualTo(1.0));
            Assert.That(meanDurationDelta, Is.LessThanOrEqualTo(10.0));
        }

        [Test]
        public void FiveHundredParticipantAbstractStressIsDeterministicAndBounded()
        {
            var fixture = CombatFixture.Create(250, 250, 99123, BattleMode.Abstract, false);
            fixture.Combat.AdvanceTo(Minute.Ticks, 0); // warm the deterministic path
            var before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            fixture.Combat.AdvanceTo(TimeSpan.FromMinutes(120).Ticks, 0);
            stopwatch.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            var hash = fixture.Combat.ComputeStateHash();
            Assert.That(fixture.Combat.Encounters.Single().Combatants.Count, Is.EqualTo(500));
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(20)));
            Assert.That(allocated, Is.LessThan(256L * 1024L * 1024L));
            TestContext.WriteLine($"U23 250v250 abstract stress: {stopwatch.Elapsed.TotalMilliseconds:0.###} ms, " +
                $"{allocated} managed bytes, hash {hash:X16}");
        }

        private static int Casualties(CombatEncounterState battle) => battle.Combatants.Count(x =>
            x.State == CombatantState.Dead || x.State == CombatantState.Incapacitated);

        private static int RunCombatToCompletion(CombatFixture fixture)
        {
            for (var minute = 1; minute <= 240; minute++)
            {
                fixture.Combat.AdvanceTo(TimeSpan.FromMinutes(minute).Ticks, 0);
                if (fixture.Combat.Encounters.Single().Status != CombatEncounterStatus.Active) return minute;
            }
            Assert.Fail("Battle did not finish within the deterministic test budget.");
            return 240;
        }

        private static void RunUnitBattleToCompletion(UnitFixture fixture)
        {
            for (var minute = 5; minute <= 240; minute++)
            {
                fixture.Battles.AdvanceTo(TimeSpan.FromMinutes(minute).Ticks, 0);
                if (fixture.Combat.Encounters.Single().Status != CombatEncounterStatus.Active) return;
            }
            Assert.Fail("Unit battle did not finish within the deterministic test budget.");
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        private static MilitaryUnitId UnitId(int value) => new MilitaryUnitId(Id(value));

        private static Character MakeCharacter(StableEntityId id, int influence, StableEntityId dynastyId) =>
            Character.Restore(new CharacterState(id,
                new CharacterName("Fighter" + id.ToString().Substring(28)), CharacterSex.Male,
                0, CharacterLifeState.Alive, null, null, null,
                dynastyId, dynastyId, null,
                new[] { new TraitId("brave"), new TraitId("calm"), new TraitId("fit"), new TraitId("steady") },
                new[] { new SkillState(new SkillId("combat"), new SkillLevel(40 + influence % 20)) },
                new CharacterAttributes(new CharacterAttributeValue(50 + influence % 10),
                    new CharacterAttributeValue(50)), BodyHealth.Healthy, new Wealth(0),
                new Influence(influence), null));

        private sealed class UnitFixture
        {
            public CharacterRegistry Registry;
            public SuccessionService Succession;
            public HealthAndFamilyService Health;
            public CombatService Combat;
            public TierManager Tiers;
            public MilitaryUnitService Units;
            public MilitaryUnitRepresentationService Representations;
            public MilitaryUnitId FirstUnit;
            public MilitaryUnitId SecondUnit;
            public BattleResolutionService Battles;
            public FakeDirectControl DirectControl;

            public static UnitFixture Create(int membersPerSide)
            {
                var fixture = new UnitFixture { Registry = new CharacterRegistry(), DirectControl = new FakeDirectControl() };
                var ids = Enumerable.Range(1, membersPerSide * 2).Select(Id).ToArray();
                var dynastyId = Id(9000);
                var characters = ids.Select((id, index) => MakeCharacter(id, 100 - index, dynastyId)).ToArray();
                foreach (var character in characters) fixture.Registry.Add(character);
                var dynasty = DynastyState.Create(dynastyId, "House Battle", characters[0].Id,
                    characters[0].Id, characters.Select(x => x.Id));
                fixture.Succession = new SuccessionService(fixture.Registry, dynasty,
                    new PlayerRealmState(characters[0].Id), new GameRunState());
                fixture.Health = new HealthAndFamilyService(fixture.Registry,
                    HealthConditionCatalog.CreateStoneAge(), 4411, fixture.Succession);
                var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
                var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
                var tier3 = new MemoryAdapter(CharacterSimulationTier.Tier3);
                foreach (var id in ids) tier1.Seed(Runtime(id));
                fixture.Tiers = new TierManager(fixture.Registry, tier1, tier2, tier3);
                foreach (var id in ids) fixture.Tiers.RegisterExisting(id, CharacterSimulationTier.Tier1);
                var positions = new MemoryPositions();
                for (var i = 0; i < ids.Length; i++)
                    positions.Set(ids[i], new WorldPosition(i < membersPerSide ? 0f : 1f, 0f, i * 0.01f));
                fixture.Combat = new CombatService(fixture.Registry, fixture.Health, positions, 4411, fixture.Tiers);
                fixture.Tiers.BindTransitionGuard(fixture.Combat);
                fixture.Units = new MilitaryUnitService(fixture.Registry, fixture.Health);
                fixture.FirstUnit = UnitId(7001);
                fixture.SecondUnit = UnitId(7002);
                fixture.Units.Create(fixture.FirstUnit, new MilitarySideId("player"), ids.Take(membersPerSide), ids[0]);
                fixture.Units.Create(fixture.SecondUnit, new MilitarySideId("enemy"), ids.Skip(membersPerSide),
                    ids[membersPerSide]);
                fixture.Representations = new MilitaryUnitRepresentationService(fixture.Units, fixture.Tiers,
                    fixture.Combat, fixture.Registry, fixture.Health);
                var unitCombat = new MilitaryUnitCombatService(fixture.Units, fixture.Combat);
                fixture.Battles = new BattleResolutionService(fixture.Registry, fixture.Combat, fixture.Units,
                    fixture.Representations, unitCombat, fixture.DirectControl);
                return fixture;
            }

            public void ToAggregate()
            {
                Representations.Transition(FirstUnit, MilitaryUnitRepresentationMode.Aggregate);
                Representations.Transition(SecondUnit, MilitaryUnitRepresentationMode.Aggregate);
            }

            private static CharacterRuntimeState Runtime(StableEntityId id) => new CharacterRuntimeState(id,
                new WorldPosition(0, 0, 0), default, false, "local", 0, 0);
        }

        private sealed class CombatFixture
        {
            public CharacterRegistry Registry;
            public HealthAndFamilyService Health;
            public CombatService Combat;

            public static CombatFixture Create(int sideACount, int sideBCount, long seed, BattleMode mode,
                bool reverseRegistration)
            {
                var fixture = new CombatFixture { Registry = new CharacterRegistry() };
                var ids = Enumerable.Range(1, sideACount + sideBCount).Select(Id).ToArray();
                var dynastyId = Id(9200);
                var characters = ids.Select((id, index) =>
                    MakeCharacter(id, (int)((index * 17L + seed) % 100L), dynastyId)).ToArray();
                foreach (var character in reverseRegistration ? characters.Reverse() : characters)
                    fixture.Registry.Add(character);
                var dynasty = DynastyState.Create(dynastyId, "House Combat", characters[0].Id,
                    characters[0].Id, characters.Select(x => x.Id));
                var succession = new SuccessionService(fixture.Registry, dynasty,
                    new PlayerRealmState(characters[0].Id), new GameRunState());
                fixture.Health = new HealthAndFamilyService(fixture.Registry,
                    HealthConditionCatalog.CreateStoneAge(), seed, succession);
                var positions = new MemoryPositions();
                for (var i = 0; i < ids.Length; i++)
                    positions.Set(ids[i], new WorldPosition(i < sideACount ? 0f : 1f, 0f, i * 0.001f));
                fixture.Combat = new CombatService(fixture.Registry, fixture.Health, positions, seed);
                var sideA = new CombatSideId("a");
                var sideB = new CombatSideId("b");
                foreach (var id in ids.Take(sideACount)) fixture.Combat.RegisterSide(id, sideA);
                foreach (var id in ids.Skip(sideACount)) fixture.Combat.RegisterSide(id, sideB);
                fixture.Combat.CreateEncounter(Id(8000), sideA, sideB, ids.Take(sideACount),
                    ids.Skip(sideACount), 0, _ => new CombatWeaponId("stone_spear"), mode);
                return fixture;
            }
        }

        private sealed class MultiBattleWorld
        {
            public CombatService Combat;

            public static MultiBattleWorld Create(int battleCount, bool reverse)
            {
                var registry = new CharacterRegistry();
                var ids = Enumerable.Range(1, battleCount * 2).Select(Id).ToArray();
                var dynastyId = Id(9300);
                var characters = ids.Select((id, index) => MakeCharacter(id, index, dynastyId)).ToArray();
                foreach (var character in characters) registry.Add(character);
                var dynasty = DynastyState.Create(dynastyId, "House Multiple", characters[0].Id,
                    characters[0].Id, characters.Select(x => x.Id));
                var succession = new SuccessionService(registry, dynasty,
                    new PlayerRealmState(characters[0].Id), new GameRunState());
                var health = new HealthAndFamilyService(registry, HealthConditionCatalog.CreateStoneAge(), 5511,
                    succession);
                var positions = new MemoryPositions();
                foreach (var id in ids) positions.Set(id, default);
                var combat = new CombatService(registry, health, positions, 5511);
                var order = Enumerable.Range(0, battleCount);
                if (reverse) order = order.Reverse();
                foreach (var index in order)
                {
                    var a = ids[index * 2];
                    var b = ids[index * 2 + 1];
                    var sideA = new CombatSideId("a" + index);
                    var sideB = new CombatSideId("b" + index);
                    combat.RegisterSide(a, sideA);
                    combat.RegisterSide(b, sideB);
                    combat.CreateEncounter(Id(8100 + index), sideA, sideB, new[] { a }, new[] { b }, 0,
                        _ => new CombatWeaponId("stone_spear"), BattleMode.Abstract);
                }
                return new MultiBattleWorld { Combat = combat };
            }
        }

        private sealed class FakeDirectControl : IExternalCharacterActivityOwner
        {
            public readonly HashSet<StableEntityId> Controlled = new HashSet<StableEntityId>();
            public bool IsExternallyControlled(StableEntityId characterId) => Controlled.Contains(characterId);
        }

        private sealed class MemoryPositions : ICombatPositionProvider
        {
            private readonly Dictionary<StableEntityId, WorldPosition> values =
                new Dictionary<StableEntityId, WorldPosition>();
            public void Set(StableEntityId id, WorldPosition value) => values[id] = value;
            public bool TryGetPosition(StableEntityId characterId, out WorldPosition position) =>
                values.TryGetValue(characterId, out position);
            public void MoveTo(StableEntityId characterId, WorldPosition target, float stoppingDistance) =>
                values[characterId] = target;
            public void Stop(StableEntityId characterId) { }
        }

        private sealed class MemoryAdapter : ICharacterTierAdapter
        {
            private readonly Dictionary<StableEntityId, CharacterRuntimeState> states =
                new Dictionary<StableEntityId, CharacterRuntimeState>();
            public MemoryAdapter(CharacterSimulationTier tier) => Tier = tier;
            public CharacterSimulationTier Tier { get; }
            public void Seed(CharacterRuntimeState state) => states.Add(state.CharacterId, state);
            public bool IsActive(StableEntityId characterId) => states.ContainsKey(characterId);
            public CharacterRuntimeState Capture(StableEntityId characterId) => states[characterId];
            public void ValidateMaterialization(Character character, CharacterRuntimeState state)
            {
                if (character.Id != state.CharacterId || states.ContainsKey(character.Id))
                    throw new InvalidOperationException();
            }
            public void Materialize(Character character, CharacterRuntimeState state) => states.Add(character.Id, state);
            public void Dematerialize(StableEntityId characterId)
            {
                if (!states.Remove(characterId)) throw new InvalidOperationException();
            }
        }
    }
}
