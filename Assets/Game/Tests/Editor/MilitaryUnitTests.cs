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
using Game.Simulation.Combat;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Military;
using Game.Simulation.Tiers;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U21")]
    public sealed class MilitaryUnitTests
    {
        [Test]
        public void ValidCreationKeepsStableIdentityOwnerAndCanonicalRoster()
        {
            var fixture = Fixture.Create(3);
            var id = UnitId(500);
            var unit = fixture.Units.Create(id, new MilitarySideId("player"),
                fixture.MemberIds.Reverse(), fixture.MemberIds[0]);

            Assert.That(unit.Id, Is.EqualTo(id));
            Assert.That(unit.OwnerSideId, Is.EqualTo(new MilitarySideId("player")));
            Assert.That(unit.CaptainId, Is.EqualTo(fixture.MemberIds[0]));
            Assert.That(unit.ActiveMemberIds, Is.Ordered);
            Assert.That(fixture.Registry.Get(unit.ActiveMemberIds[0]), Is.Not.Null);
            Assert.Throws<InvalidOperationException>(() => fixture.Units.Create(id,
                new MilitarySideId("player"), fixture.MemberIds, fixture.MemberIds[0]));
        }

        [Test]
        public void InvalidRosterCaptainAndDuplicateMembershipAreRejected()
        {
            var fixture = Fixture.Create(4);
            Assert.Throws<ArgumentException>(() => fixture.Units.Create(UnitId(501),
                new MilitarySideId("player"), new[] { fixture.MemberIds[0], fixture.MemberIds[0] }, fixture.MemberIds[0]));
            Assert.Throws<ArgumentException>(() => fixture.Units.Create(UnitId(502),
                new MilitarySideId("player"), fixture.MemberIds.Take(2), fixture.MemberIds[3]));
            fixture.Units.Create(UnitId(503), new MilitarySideId("player"), fixture.MemberIds.Take(2), fixture.MemberIds[0]);
            Assert.Throws<InvalidOperationException>(() => fixture.Units.Create(UnitId(504),
                new MilitarySideId("player"), fixture.MemberIds.Skip(1).Take(2), fixture.MemberIds[1]));
        }

        [Test]
        public void CaptainMustBeLivingAdultCapableMemberAndFallbackIsDeterministic()
        {
            var fixture = Fixture.Create(4, influences: new[] { 10, 90, 90, 20 });
            var child = fixture.AddCharacter(90, hasParent: true);
            Assert.Throws<ArgumentException>(() => fixture.Units.Create(UnitId(505), new MilitarySideId("player"),
                new[] { child.Id }, child.Id));
            var unit = fixture.Units.Create(UnitId(506), new MilitarySideId("player"), fixture.MemberIds,
                fixture.MemberIds[0]);
            fixture.Registry.Get(fixture.MemberIds[0]).MarkDead(1);
            var fallback = fixture.Units.RefreshCaptain(unit.Id);
            var expected = fixture.MemberIds.Skip(1).Take(2).OrderBy(x => x.ToString(), StringComparer.Ordinal).First();
            Assert.That(fallback, Is.EqualTo(expected));
            Assert.That(fixture.Units.AssignCaptain(unit.Id, fixture.MemberIds[0]),
                Is.EqualTo(CommanderAssignmentStatus.Ineligible));
            Assert.That(fixture.Units.AssignCaptain(unit.Id, child.Id), Is.EqualTo(CommanderAssignmentStatus.NotMember));
            foreach (var id in fixture.MemberIds.Skip(1)) fixture.Registry.Get(id).MarkDead(2);
            Assert.That(fixture.Units.RefreshCaptain(unit.Id), Is.Null);
            Assert.That(fixture.Units.Get(unit.Id).Status, Is.EqualTo(MilitaryUnitStatus.Leaderless));
        }

        [Test]
        public void AggregateOrdersRemainLogicalIntentWithoutChangingMembership()
        {
            var fixture = Fixture.Create(2);
            var unit = fixture.Units.Create(UnitId(519), new MilitarySideId("player"), fixture.MemberIds,
                fixture.MemberIds[0]);
            fixture.Representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            fixture.Units.SetOrder(unit.Id, MilitaryUnitOrderKind.Move, new WorldPosition(12f, 0f, -4f), true);
            var move = fixture.Units.Get(unit.Id);
            Assert.That(move.Order.Kind, Is.EqualTo(MilitaryUnitOrderKind.Move));
            Assert.That(move.Order.Target, Is.EqualTo(new WorldPosition(12f, 0f, -4f)));
            Assert.That(move.ActiveMemberIds, Is.EqualTo(unit.ActiveMemberIds));
            fixture.Units.SetOrder(unit.Id, MilitaryUnitOrderKind.Hold);
            Assert.That(fixture.Units.Get(unit.Id).Order.Kind, Is.EqualTo(MilitaryUnitOrderKind.Hold));
        }

        [Test]
        public void RemovedAndDeadMembersRemainHistoricalWhileActiveQueriesReflectCharacters()
        {
            var fixture = Fixture.Create(5);
            var first = fixture.Units.Create(UnitId(507), new MilitarySideId("player"), fixture.MemberIds.Take(3),
                fixture.MemberIds[0]);
            var second = fixture.Units.Create(UnitId(508), new MilitarySideId("player"), fixture.MemberIds.Skip(3),
                fixture.MemberIds[3]);
            fixture.Units.TransferMember(first.Id, second.Id, fixture.MemberIds[2]);
            fixture.Registry.Get(fixture.MemberIds[1]).MarkDead(1);
            var source = fixture.Units.Get(first.Id);
            var summary = fixture.Units.GetRosterSummary(first.Id);
            Assert.That(source.Roster.Count, Is.EqualTo(3));
            Assert.That(source.Roster.Single(x => x.CharacterId == fixture.MemberIds[2]).Status,
                Is.EqualTo(MilitaryUnitMemberStatus.Removed));
            Assert.That(summary.Alive, Is.EqualTo(1));
            Assert.That(fixture.Units.Get(second.Id).ActiveMemberIds, Does.Contain(fixture.MemberIds[2]));
        }

        [Test]
        public void DetailedAggregateRoundTripKeepsIdsRosterCaptainHealthAndDoesNotRegenerateCharacters()
        {
            var fixture = Fixture.Create(4);
            var unit = fixture.Units.Create(UnitId(509), new MilitarySideId("player"), fixture.MemberIds,
                fixture.MemberIds[0]);
            var references = fixture.MemberIds.Select(fixture.Registry.Get).ToArray();
            fixture.Health.ApplyCombatInjury(fixture.MemberIds[2], new HealthConditionId("sprain"), 0, 0);
            var healthHash = fixture.Health.ComputeStateHash();

            fixture.Representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            Assert.That(fixture.MemberIds.All(x => fixture.Tiers.GetTier(x) == CharacterSimulationTier.Tier3), Is.True);
            fixture.Representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Detailed);

            var after = fixture.Units.Get(unit.Id);
            Assert.That(after.ActiveMemberIds, Is.EqualTo(unit.ActiveMemberIds));
            Assert.That(after.CaptainId, Is.EqualTo(unit.CaptainId));
            Assert.That(after.Representation, Is.EqualTo(MilitaryUnitRepresentationMode.Detailed));
            Assert.That(fixture.Health.ComputeStateHash(), Is.EqualTo(healthHash));
            for (var i = 0; i < references.Length; i++) Assert.That(fixture.Registry.Get(fixture.MemberIds[i]), Is.SameAs(references[i]));
        }

        [Test]
        public void ActualTierRouteKeepsMembershipCaptainHealthAndDynastyIdentity()
        {
            var fixture = Fixture.Create(2);
            var dynastyId = fixture.Registry.Get(fixture.MemberIds[0]).DynastyId;
            var unit = fixture.Units.Create(UnitId(510), new MilitarySideId("player"), fixture.MemberIds,
                fixture.MemberIds[0]);
            fixture.Tiers.Transition(fixture.MemberIds[0], CharacterSimulationTier.Tier2);
            fixture.Tiers.Transition(fixture.MemberIds[0], CharacterSimulationTier.Tier3);
            fixture.Tiers.Transition(fixture.MemberIds[0], CharacterSimulationTier.Tier1);
            var after = fixture.Units.Get(unit.Id);
            Assert.That(after.ActiveMemberIds, Is.EqualTo(unit.ActiveMemberIds));
            Assert.That(after.CaptainId, Is.EqualTo(unit.CaptainId));
            Assert.That(fixture.Registry.Get(fixture.MemberIds[0]).DynastyId, Is.EqualTo(dynastyId));
            Assert.That(fixture.Registry.Get(fixture.MemberIds[0]).Health, Is.EqualTo(BodyHealth.Healthy));
        }

        [Test]
        public void DetailedUnitsFeedOnlyEligibleMembersToU20AndCombatBlocksAggregation()
        {
            var fixture = Fixture.Create(6);
            var a = fixture.Units.Create(UnitId(511), new MilitarySideId("player"), fixture.MemberIds.Take(3),
                fixture.MemberIds[0]);
            var b = fixture.Units.Create(UnitId(512), new MilitarySideId("enemy"), fixture.MemberIds.Skip(3),
                fixture.MemberIds[3]);
            fixture.Health.ApplyCombatInjury(fixture.MemberIds[2], new HealthConditionId("serious_wound"), 0, 0);
            var encounter = fixture.UnitCombat.Start(a.Id, b.Id, Id(800), 0);
            Assert.That(encounter.Combatants.Any(x => x.CharacterId == fixture.MemberIds[2]), Is.False);
            Assert.Throws<InvalidOperationException>(() => fixture.Representations.Transition(a.Id,
                MilitaryUnitRepresentationMode.Aggregate));
            fixture.Combat.CancelEncounter(encounter.EncounterId);
            fixture.UnitCombat.ReconcileAll();
            Assert.DoesNotThrow(() => fixture.Representations.Transition(a.Id,
                MilitaryUnitRepresentationMode.Aggregate));
        }

        [Test]
        public void CombatCasualtyAndCaptainDeathBackPropagateWithoutCopiedHealth()
        {
            var fixture = Fixture.Create(4, influences: new[] { 100, 50, 40, 30 });
            var a = fixture.Units.Create(UnitId(513), new MilitarySideId("player"), fixture.MemberIds.Take(2),
                fixture.MemberIds[0]);
            var b = fixture.Units.Create(UnitId(514), new MilitarySideId("enemy"), fixture.MemberIds.Skip(2),
                fixture.MemberIds[2]);
            var encounter = fixture.UnitCombat.Start(a.Id, b.Id, Id(801), 0);
            fixture.Health.ApplyCombatInjury(fixture.MemberIds[0], new HealthConditionId("serious_wound"), 1, 1, true);
            fixture.Combat.AdvanceTo(1, 1);
            fixture.UnitCombat.ReconcileAll();
            var after = fixture.Units.Get(a.Id);
            Assert.That(after.Roster.Count, Is.EqualTo(2));
            Assert.That(fixture.Registry.Get(fixture.MemberIds[0]).LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(after.CaptainId, Is.EqualTo(fixture.MemberIds[1]));
            Assert.That(after.ActiveEncounterId, Is.EqualTo(encounter.EncounterId));
            fixture.Combat.CancelEncounter(encounter.EncounterId);
            fixture.UnitCombat.ReconcileAll();
            Assert.That(fixture.Units.Get(a.Id).ActiveEncounterId, Is.Null);
        }

        [Test]
        public void V8SaveLoadPreservesUnitIdentityRosterCaptainRepresentationOrderAndEncounterLink()
        {
            var fixture = Fixture.Create(4);
            var a = fixture.Units.Create(UnitId(515), new MilitarySideId("player"), fixture.MemberIds.Take(2),
                fixture.MemberIds[0]);
            var b = fixture.Units.Create(UnitId(516), new MilitarySideId("enemy"), fixture.MemberIds.Skip(2),
                fixture.MemberIds[2]);
            var encounter = fixture.UnitCombat.Start(a.Id, b.Id, Id(802), 0);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(990), new GameTimeState(), 400,
                healthFamily: fixture.Health.CaptureState(), combat: fixture.Combat.CaptureState(),
                military: fixture.Units.CaptureState());
            var codec = new SaveXmlCodec();
            var decoded = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(decoded.FormatVersion, Is.EqualTo(8));

            var restored = Fixture.Create(4);
            restored.Health.RestoreState(decoded.HealthFamily);
            restored.Combat.RestoreState(decoded.Combat);
            restored.Units.RestoreState(decoded.Military, restored.Combat);
            var state = restored.Units.Get(a.Id);
            Assert.That(state.Id, Is.EqualTo(a.Id));
            Assert.That(state.CaptainId, Is.EqualTo(a.CaptainId));
            Assert.That(state.ActiveMemberIds, Is.EqualTo(a.ActiveMemberIds));
            Assert.That(state.Representation, Is.EqualTo(MilitaryUnitRepresentationMode.Detailed));
            Assert.That(state.Order.Kind, Is.EqualTo(MilitaryUnitOrderKind.Engage));
            Assert.That(state.ActiveEncounterId, Is.EqualTo(encounter.EncounterId));
        }

        [Test]
        public void LegacySnapshotsV1ThroughV7LoadWithoutMilitaryState()
        {
            var codec = new SaveXmlCodec();
            for (var version = 1; version <= 7; version++)
            {
                var decoded = codec.Deserialize(codec.Serialize(new SaveSnapshot(version, Id(1100 + version),
                    new GameTimeState(), 400)));
                Assert.That(decoded.FormatVersion, Is.EqualTo(version));
                Assert.That(decoded.Military, Is.Null);
            }
        }

        [Test]
        public void InvalidSavedMembershipAndMissingEncounterFailCleanly()
        {
            var fixture = Fixture.Create(2);
            var unit = fixture.Units.Create(UnitId(520), new MilitarySideId("player"), fixture.MemberIds,
                fixture.MemberIds[0]);
            var duplicate = new MilitaryUnitState(UnitId(521), unit.OwnerSideId, fixture.MemberIds[0], unit.Roster,
                MilitaryUnitStatus.Ready, MilitaryUnitRepresentationMode.Detailed, "local", 1000, 1000,
                MilitaryUnitOrderState.Idle(), null, 1);
            Assert.Throws<ArgumentException>(() => fixture.Units.RestoreState(
                new MilitaryUnitSaveState(new[] { unit, duplicate }), fixture.Combat));

            var missingEncounter = new MilitaryUnitState(UnitId(522), unit.OwnerSideId, fixture.MemberIds[0],
                unit.Roster, MilitaryUnitStatus.Engaged, MilitaryUnitRepresentationMode.Detailed, "local", 1000,
                1000, new MilitaryUnitOrderState(MilitaryUnitOrderKind.Engage, default, false, UnitId(523), 2),
                Id(999), 2);
            Assert.Throws<ArgumentException>(() => fixture.Units.RestoreState(
                new MilitaryUnitSaveState(new[] { missingEncounter }), fixture.Combat));
            Assert.Throws<ArgumentException>(() => new MilitaryUnitState(UnitId(524), unit.OwnerSideId,
                fixture.MemberIds[0], new MilitaryUnitMemberState[] { null }, MilitaryUnitStatus.Ready,
                MilitaryUnitRepresentationMode.Detailed, "local", 1000, 1000,
                MilitaryUnitOrderState.Idle(), null, 1));
        }

        [Test]
        public void CollectionOrderDoesNotChangeFallbackSerializationOrStateHash()
        {
            var first = Fixture.Create(5, influences: new[] { 10, 90, 90, 20, 30 });
            var second = Fixture.Create(5, influences: new[] { 10, 90, 90, 20, 30 }, reverseRegistration: true);
            var firstUnit = first.Units.Create(UnitId(517), new MilitarySideId("player"), first.MemberIds,
                first.MemberIds[0]);
            var secondUnit = second.Units.Create(UnitId(517), new MilitarySideId("player"), second.MemberIds.Reverse(),
                second.MemberIds[0]);
            first.Registry.Get(first.MemberIds[0]).MarkDead(1);
            second.Registry.Get(second.MemberIds[0]).MarkDead(1);
            Assert.That(second.Units.RefreshCaptain(secondUnit.Id), Is.EqualTo(first.Units.RefreshCaptain(firstUnit.Id)));
            Assert.That(second.Units.ComputeStateHash(), Is.EqualTo(first.Units.ComputeStateHash()));
            var codec = new SaveXmlCodec();
            var firstBytes = codec.Serialize(new SaveSnapshot(8, Id(1200), new GameTimeState(), 400,
                military: first.Units.CaptureState()));
            var secondBytes = codec.Serialize(new SaveSnapshot(8, Id(1200), new GameTimeState(), 400,
                military: second.Units.CaptureState()));
            Assert.That(secondBytes, Is.EqualTo(firstBytes));
        }

        [Test]
        public void TenUnitHundredMemberTransitionsAreDeterministicWithoutRunawayAllocation()
        {
            var fixture = Fixture.Create(100);
            for (var unitIndex = 0; unitIndex < 10; unitIndex++)
            {
                var members = fixture.MemberIds.Skip(unitIndex * 10).Take(10).Reverse().ToArray();
                fixture.Units.Create(UnitId(600 + unitIndex), new MilitarySideId("player"), members, members[0]);
            }
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            foreach (var unit in fixture.Units.Units)
                fixture.Representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
            foreach (var unit in fixture.Units.Units.Reverse())
                fixture.Representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Detailed);
            watch.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(fixture.Units.Units.Sum(x => x.ActiveMemberIds.Count), Is.EqualTo(100));
            Assert.That(fixture.Units.Units.SelectMany(x => x.ActiveMemberIds).Distinct().Count(), Is.EqualTo(100));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
            TestContext.WriteLine($"U21 10-unit/100-member route: {watch.Elapsed.TotalMilliseconds:0.###} ms, " +
                                  $"allocatedBytes={allocated}, hash={fixture.Units.ComputeStateHash():X16}");
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        private static MilitaryUnitId UnitId(int value) => new MilitaryUnitId(Id(value));

        private sealed class Fixture
        {
            public CharacterRegistry Registry;
            public HealthAndFamilyService Health;
            public CombatService Combat;
            public TierManager Tiers;
            public MilitaryUnitService Units;
            public MilitaryUnitRepresentationService Representations;
            public MilitaryUnitCombatService UnitCombat;
            public StableEntityId[] MemberIds;
            private MemoryAdapter tier1;

            public static Fixture Create(int count, int[] influences = null, bool reverseRegistration = false)
            {
                var fixture = new Fixture { Registry = new CharacterRegistry(), MemberIds = Enumerable.Range(1, count).Select(Id).ToArray() };
                var dynastyId = Id(9000);
                var values = fixture.MemberIds.Select((id, index) => MakeCharacter(id, influences == null ? index : influences[index], dynastyId)).ToArray();
                foreach (var character in reverseRegistration ? values.Reverse() : values) fixture.Registry.Add(character);
                var dynasty = DynastyState.Create(dynastyId, "House Fixture", values[0].Id, values[0].Id,
                    values.Select(x => x.Id));
                var succession = new SuccessionService(fixture.Registry, dynasty,
                    new PlayerRealmState(values[0].Id), new GameRunState());
                fixture.Health = new HealthAndFamilyService(fixture.Registry,
                    HealthConditionCatalog.CreateStoneAge(), 2021, succession);
                fixture.tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
                var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
                var tier3 = new MemoryAdapter(CharacterSimulationTier.Tier3);
                foreach (var id in fixture.MemberIds) fixture.tier1.Seed(Runtime(id));
                fixture.Tiers = new TierManager(fixture.Registry, fixture.tier1, tier2, tier3);
                foreach (var id in fixture.MemberIds) fixture.Tiers.RegisterExisting(id, CharacterSimulationTier.Tier1);
                var positions = new MemoryPositions();
                for (var i = 0; i < fixture.MemberIds.Length; i++)
                    positions.Set(fixture.MemberIds[i], new WorldPosition(i < count / 2 ? 0f : 1f, 0f, i * 0.02f));
                fixture.Combat = new CombatService(fixture.Registry, fixture.Health, positions, 2021, fixture.Tiers);
                fixture.Tiers.BindTransitionGuard(fixture.Combat);
                fixture.Units = new MilitaryUnitService(fixture.Registry, fixture.Health);
                fixture.Representations = new MilitaryUnitRepresentationService(fixture.Units, fixture.Tiers,
                    fixture.Combat, fixture.Registry, fixture.Health);
                fixture.UnitCombat = new MilitaryUnitCombatService(fixture.Units, fixture.Combat);
                return fixture;
            }

            public Character AddCharacter(int value, bool hasParent)
            {
                var character = MakeCharacter(Id(value), 0, Id(9000), hasParent ? Id(9999) : (StableEntityId?)null);
                Registry.Add(character);
                tier1.Seed(Runtime(character.Id));
                Tiers.RegisterExisting(character.Id, CharacterSimulationTier.Tier1);
                return character;
            }

            private static Character MakeCharacter(StableEntityId id, int influence, StableEntityId dynastyId,
                StableEntityId? parentId = null) => Character.Restore(new CharacterState(id,
                new CharacterName("Unit" + id.ToString().Substring(28)), CharacterSex.Male, 0,
                CharacterLifeState.Alive, null,
                parentId.HasValue ? new[] { new ParentLink(parentId.Value, ParentRole.Father,
                    ParentageKind.Biological, true) } : null,
                null, dynastyId, dynastyId, null,
                new[] { new TraitId("brave"), new TraitId("calm"), new TraitId("fit"), new TraitId("steady") },
                new[] { new SkillState(new SkillId("combat"), new SkillLevel(40)) },
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(influence), null));

            private static CharacterRuntimeState Runtime(StableEntityId id) => new CharacterRuntimeState(id,
                new WorldPosition(0, 0, 0), default, false, "local", 0, 0);
        }

        private sealed class MemoryPositions : ICombatPositionProvider
        {
            private readonly Dictionary<StableEntityId, WorldPosition> values = new Dictionary<StableEntityId, WorldPosition>();
            public void Set(StableEntityId id, WorldPosition position) => values[id] = position;
            public bool TryGetPosition(StableEntityId characterId, out WorldPosition position) => values.TryGetValue(characterId, out position);
            public void MoveTo(StableEntityId characterId, WorldPosition target, float stoppingDistance) => values[characterId] = target;
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
                if (character.Id != state.CharacterId || states.ContainsKey(character.Id)) throw new InvalidOperationException();
            }
            public void Materialize(Character character, CharacterRuntimeState state) => states.Add(character.Id, state);
            public void Dematerialize(StableEntityId characterId)
            {
                if (!states.Remove(characterId)) throw new InvalidOperationException();
            }
        }
    }
}
