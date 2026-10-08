using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Control;
using Game.Domain.Dynasties;
using Game.Domain.Health;
using Game.Domain.Military;
using Game.Domain.Time;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Combat;
using Game.Simulation.Dynasties;
using Game.Simulation.Health;
using Game.Simulation.Military;
using Game.Simulation.Tiers;
using Game.Simulation.World;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [TestFixture]
    [Category("U25")]
    public sealed class GlobalMapTests
    {
        [Test]
        public void StarterModelHasStableLocationsSettlementsAndValidRoutes()
        {
            var state = StarterGlobalMap.Create();
            Assert.That(state.Locations.Count, Is.EqualTo(6));
            Assert.That(state.Settlements.Count, Is.EqualTo(2));
            Assert.That(state.Locations.Select(x => x.Id), Is.Unique);
            Assert.That(state.Routes.All(route => state.Locations.Any(x => x.Id == route.From) &&
                state.Locations.Any(x => x.Id == route.To)), Is.True);
            Assert.That(state.ActiveLocationId, Is.EqualTo(new WorldLocationId("starter_settlement")));
        }

        [Test]
        public void DuplicateLocationRouteAndInvalidSettlementAreRejected()
        {
            var state = StarterGlobalMap.Create();
            Assert.Throws<ArgumentException>(() => new GlobalMapSaveState(state.GraphId, state.Mode,
                state.ActiveLocationId, state.Locations.Concat(new[] { state.Locations[0] }), state.Routes,
                state.Settlements, state.Travels, 0));
            Assert.Throws<ArgumentException>(() => new GlobalMapSaveState(state.GraphId, state.Mode,
                state.ActiveLocationId, state.Locations, state.Routes.Concat(new[] {
                    new WorldRouteState(state.Routes[0].To, state.Routes[0].From, TimeSpan.TicksPerHour) }),
                state.Settlements, state.Travels, 0));
            Assert.Throws<ArgumentException>(() => new SettlementState(new SettlementId(Id(99)),
                new WorldLocationId("bad id"), new CombatSideId("player"), null, null));
        }

        [Test]
        public void ValidTravelOrderHasDeterministicTicksAndArrivesExactlyOnce()
        {
            var fixture = Fixture.Create(2);
            var destination = new WorldLocationId("river_ford");
            var issued = fixture.Map.IssueTravelOrder(fixture.FirstUnit, destination, 10);
            Assert.That(issued.Status, Is.EqualTo(TravelOrderStatus.Issued));
            Assert.That(issued.Travel.ArrivalTick, Is.EqualTo(10 + 8 * TimeSpan.TicksPerHour));
            fixture.Map.AdvanceTo(issued.Travel.ArrivalTick - 1);
            Assert.That(fixture.Units.Get(fixture.FirstUnit).AbstractLocationId, Is.EqualTo("starter_settlement"));
            fixture.Map.AdvanceTo(issued.Travel.ArrivalTick);
            Assert.That(fixture.Units.Get(fixture.FirstUnit).AbstractLocationId, Is.EqualTo("river_ford"));
            var hash = fixture.Map.ComputeStateHash();
            fixture.Map.AdvanceTo(issued.Travel.ArrivalTick);
            Assert.That(fixture.Map.ComputeStateHash(), Is.EqualTo(hash));
            Assert.That(fixture.Map.Travels, Is.Empty);
        }

        [Test]
        public void InvalidDestinationRouteDetailedAndDuplicateOrdersDoNotMutateTravel()
        {
            var fixture = Fixture.Create(2, aggregate: false);
            Assert.That(fixture.Map.IssueTravelOrder(fixture.FirstUnit,
                new WorldLocationId("river_ford"), 0).Status, Is.EqualTo(TravelOrderStatus.UnitNotAggregate));
            fixture.Representations.Transition(fixture.FirstUnit, MilitaryUnitRepresentationMode.Aggregate);
            Assert.That(fixture.Map.IssueTravelOrder(fixture.FirstUnit,
                new WorldLocationId("enemy_settlement"), 0).Status, Is.EqualTo(TravelOrderStatus.NoDirectRoute));
            Assert.That(fixture.Map.IssueTravelOrder(fixture.FirstUnit,
                new WorldLocationId("river_ford"), 0).Issued, Is.True);
            Assert.That(fixture.Map.IssueTravelOrder(fixture.FirstUnit,
                new WorldLocationId("north_forest"), 0).Status, Is.EqualTo(TravelOrderStatus.AlreadyTravelling));
        }

        [Test]
        public void DirectCatchUpEqualsSteppedTravelAndCollectionOrder()
        {
            var direct = Fixture.Create(20);
            var stepped = Fixture.Create(20, reverseCharacters: true);
            foreach (var unit in direct.Units.Units) direct.Map.IssueTravelOrder(unit.Id,
                new WorldLocationId("river_ford"), 0);
            foreach (var unit in stepped.Units.Units.Reverse()) stepped.Map.IssueTravelOrder(unit.Id,
                new WorldLocationId("river_ford"), 0);
            var arrival = 8 * TimeSpan.TicksPerHour;
            direct.Map.AdvanceTo(arrival);
            for (var hour = 1; hour <= 8; hour++) stepped.Map.AdvanceTo(hour * TimeSpan.TicksPerHour);
            Assert.That(stepped.Map.ComputeStateHash(), Is.EqualTo(direct.Map.ComputeStateHash()));
            Assert.That(stepped.Units.ComputeStateHash(), Is.EqualTo(direct.Units.ComputeStateHash()));
            TestContext.WriteLine($"U25 travel catch-up/order hash {direct.Map.ComputeStateHash():X16}");
        }

        [Test]
        public void HostileArrivalStartsOneCanonicalAbstractBattleAndCatchUpMatchesStepped()
        {
            var direct = Fixture.CreateHostilePair();
            var stepped = Fixture.CreateHostilePair();
            var player = direct.Map.IssueTravelOrder(direct.FirstUnit, new WorldLocationId("river_ford"), 0).Travel;
            var enemy = direct.Map.IssueTravelOrder(direct.SecondUnit, new WorldLocationId("river_ford"), 0).Travel;
            stepped.Map.IssueTravelOrder(stepped.FirstUnit, new WorldLocationId("river_ford"), 0);
            stepped.Map.IssueTravelOrder(stepped.SecondUnit, new WorldLocationId("river_ford"), 0);
            var target = Math.Max(player.ArrivalTick, enemy.ArrivalTick) + 6 * TimeSpan.TicksPerHour;
            var started = direct.Map.AdvanceTo(target);
            direct.Battles.AdvanceTo(target, target);
            for (var hour = 1; hour <= target / TimeSpan.TicksPerHour; hour++)
            {
                var tick = hour * TimeSpan.TicksPerHour;
                stepped.Map.AdvanceTo(tick);
                stepped.Battles.AdvanceTo(tick, tick);
            }
            Assert.That(started.Count, Is.EqualTo(1));
            var battle = direct.Battles.Battles.Single();
            Assert.That(battle.BattleId, Is.EqualTo(started[0]));
            Assert.That(battle.Mode, Is.EqualTo(BattleMode.Abstract));
            Assert.That(battle.SideAUnitId.HasValue && battle.SideBUnitId.HasValue, Is.True);
            Assert.That(stepped.Combat.ComputeStateHash(), Is.EqualTo(direct.Combat.ComputeStateHash()));
            Assert.That(stepped.Health.ComputeStateHash(), Is.EqualTo(direct.Health.ComputeStateHash()));
            Assert.That(stepped.Units.ComputeStateHash(), Is.EqualTo(direct.Units.ComputeStateHash()));
        }

        [Test]
        public void GlobalV12RoundTripPreservesModeMidTravelAndLegacyV1ThroughV11()
        {
            var fixture = Fixture.Create(2);
            fixture.Map.IssueTravelOrder(fixture.FirstUnit, new WorldLocationId("river_ford"), 100);
            fixture.Map.AdvanceTo(100 + TimeSpan.TicksPerHour);
            var mapState = fixture.Map.CaptureState();
            var codec = new SaveXmlCodec();
            var snapshot = new SaveSnapshot(SaveSnapshot.GlobalMapVersion, Id(900),
                new GameTimeState(mapState.LastProcessedTick, 0, 1, false), 400,
                military: fixture.Units.CaptureState(), globalMap: mapState);
            var restored = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(restored.FormatVersion, Is.EqualTo(12));
            Assert.That(restored.GlobalMap.Travels.Single().Destination,
                Is.EqualTo(new WorldLocationId("river_ford")));
            Assert.That(restored.GlobalMap.Travels.Single().DepartureTick, Is.EqualTo(100));
            for (var version = 1; version <= 11; version++)
            {
                var legacy = codec.Deserialize(codec.Serialize(new SaveSnapshot(version, Id(1000 + version),
                    new GameTimeState(), 400)));
                Assert.That(legacy.FormatVersion, Is.EqualTo(version));
                Assert.That(legacy.GlobalMap, Is.Null);
            }
        }

        [Test]
        public void RestoredMidTravelContinuesWithoutTeleportOrDuplicateArrival()
        {
            var source = Fixture.Create(2);
            var order = source.Map.IssueTravelOrder(source.FirstUnit, new WorldLocationId("river_ford"), 0).Travel;
            source.Map.AdvanceTo(TimeSpan.TicksPerHour);
            var savedMap = source.Map.CaptureState();
            var savedUnits = source.Units.CaptureState();
            var restored = Fixture.Create(2);
            restored.Units.RestoreState(savedUnits, restored.Combat);
            restored.Map.RestoreState(savedMap);
            Assert.That(restored.Units.Get(restored.FirstUnit).AbstractLocationId, Is.EqualTo("starter_settlement"));
            source.Map.AdvanceTo(order.ArrivalTick + TimeSpan.TicksPerHour);
            restored.Map.AdvanceTo(order.ArrivalTick + TimeSpan.TicksPerHour);
            Assert.That(restored.Map.ComputeStateHash(), Is.EqualTo(source.Map.ComputeStateHash()));
            Assert.That(restored.Units.ComputeStateHash(), Is.EqualTo(source.Units.ComputeStateHash()));
        }

        [Test]
        public void LocalGlobalLocalPreservesCharactersUnitsSettlementsAndTpsExits()
        {
            var fixture = Fixture.Create(4, aggregate: false);
            fixture.Control.ModeValue = PlayerControlMode.ThirdPerson;
            fixture.Control.Controlled = fixture.CharacterIds[0];
            var characterRefs = fixture.CharacterIds.Select(fixture.Registry.Get).ToArray();
            var unitIds = fixture.Units.Units.Select(x => x.Id).ToArray();
            var settlementIds = fixture.Map.Settlements.Select(x => x.Id).ToArray();
            var toGlobal = fixture.Transitions.EnterGlobal(0);
            Assert.That(toGlobal.Changed, Is.True);
            Assert.That(fixture.Control.Mode, Is.EqualTo(PlayerControlMode.Rts));
            Assert.That(fixture.CharacterIds.All(id => fixture.Tiers.GetTier(id) == CharacterSimulationTier.Tier3), Is.True);
            var toLocal = fixture.Transitions.EnterLocation(new WorldLocationId("starter_settlement"), 0);
            Assert.That(toLocal.Changed, Is.True);
            Assert.That(fixture.Map.Mode, Is.EqualTo(GameWorldMode.Local));
            Assert.That(fixture.Units.Units.Select(x => x.Id), Is.EqualTo(unitIds));
            Assert.That(fixture.Map.Settlements.Select(x => x.Id), Is.EqualTo(settlementIds));
            for (var i = 0; i < characterRefs.Length; i++)
                Assert.That(fixture.Registry.Get(fixture.CharacterIds[i]), Is.SameAs(characterRefs[i]));
        }

        [Test]
        public void PhysicalBattleBecomesAbstractBeforeGlobalAndReturnsWithSameBattleId()
        {
            var fixture = Fixture.CreateHostilePair(aggregate: false);
            fixture.Units.SetAbstractLocation(fixture.SecondUnit, "starter_settlement");
            var battle = fixture.Battles.StartPhysicalBattle(fixture.FirstUnit, fixture.SecondUnit, Id(8000), 0);
            Assert.That(fixture.Transitions.EnterGlobal(0).Changed, Is.True);
            Assert.That(fixture.Battles.Battles.Single().Mode, Is.EqualTo(BattleMode.Abstract));
            Assert.That(fixture.Battles.Battles.Single().BattleId, Is.EqualTo(battle.BattleId));
            Assert.That(fixture.Transitions.EnterBattle(battle.BattleId, 0).Changed, Is.True);
            Assert.That(fixture.Battles.Battles.Single().Mode, Is.EqualTo(BattleMode.Physical));
            Assert.That(fixture.Battles.Battles.Single().BattleId, Is.EqualTo(battle.BattleId));
        }

        [Test]
        public void FailedTransitionRollsBackUnitTierTpsAndMode()
        {
            var fixture = Fixture.Create(2, aggregate: false, failTier3For: Id(2));
            fixture.Control.ModeValue = PlayerControlMode.ThirdPerson;
            fixture.Control.Controlled = fixture.CharacterIds[0];
            var result = fixture.Transitions.EnterGlobal(0);
            Assert.That(result.Status, Is.EqualTo(GameModeTransitionStatus.FailedAndRolledBack));
            Assert.That(fixture.Map.Mode, Is.EqualTo(GameWorldMode.Local));
            Assert.That(fixture.Units.Get(fixture.FirstUnit).Representation,
                Is.EqualTo(MilitaryUnitRepresentationMode.Detailed));
            Assert.That(fixture.CharacterIds.All(id => fixture.Tiers.GetTier(id) == CharacterSimulationTier.Tier1), Is.True);
            Assert.That(fixture.Control.Mode, Is.EqualTo(PlayerControlMode.ThirdPerson));
        }

        [Test]
        public void HundredUnitsFiftyLocationsProgressWithoutCharacterGameObjects()
        {
            var fixture = Fixture.Create(200);
            var locations = Enumerable.Range(0, 50).Select(i => new WorldLocationState(
                new WorldLocationId("node_" + i), "Node " + i, i < 2 ? WorldLocationType.Settlement :
                    WorldLocationType.Wilderness, i, i % 7)).ToArray();
            var routes = Enumerable.Range(1, 49).Select(i => new WorldRouteState(locations[0].Id,
                locations[i].Id, TimeSpan.TicksPerHour + i)).ToArray();
            var settlements = new[] { new SettlementState(new SettlementId(Id(7000)), locations[0].Id,
                new CombatSideId("player"), null, fixture.CharacterIds) };
            foreach (var unit in fixture.Units.Units) fixture.Units.SetAbstractLocation(unit.Id, locations[0].Id.Value);
            var service = new GlobalMapService(new GlobalMapSaveState("stress_graph", GameWorldMode.Global,
                locations[0].Id, locations, routes, settlements, null, 0), fixture.Units);
            foreach (var unit in fixture.Units.Units.Reverse())
                service.IssueTravelOrder(unit.Id, locations[1 + Math.Abs(unit.Id.GetHashCode()) % 49].Id, 0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var watch = Stopwatch.StartNew();
            service.AdvanceTo(2 * TimeSpan.TicksPerHour);
            watch.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(service.Travels, Is.Empty);
            Assert.That(fixture.Units.Units.Count, Is.EqualTo(100));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)));
            Assert.That(allocated, Is.LessThan(32L * 1024L * 1024L));
            TestContext.WriteLine($"U25 100-unit/50-location stress: {watch.Elapsed.TotalMilliseconds:0.###} ms, " +
                $"{allocated} measured current-thread managed bytes, hash {service.ComputeStateHash():X16}");
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
            public BattleResolutionService Battles;
            public GlobalMapService Map;
            public GameModeTransitionService Transitions;
            public FakeControl Control;
            public StableEntityId[] CharacterIds;
            public MilitaryUnitId FirstUnit;
            public MilitaryUnitId SecondUnit;

            public static Fixture Create(int characterCount, bool aggregate = true, bool reverseCharacters = false,
                StableEntityId? failTier3For = null)
            {
                var fixture = new Fixture { Registry = new CharacterRegistry(), Control = new FakeControl() };
                fixture.CharacterIds = Enumerable.Range(1, characterCount).Select(Id).ToArray();
                var dynastyId = Id(9000);
                var characters = fixture.CharacterIds.Select((id, i) => MakeCharacter(id, dynastyId, i)).ToArray();
                foreach (var character in reverseCharacters ? characters.Reverse() : characters) fixture.Registry.Add(character);
                var dynasty = DynastyState.Create(dynastyId, "House Global", characters[0].Id,
                    characters[0].Id, characters.Select(x => x.Id));
                var succession = new SuccessionService(fixture.Registry, dynasty,
                    new PlayerRealmState(characters[0].Id), new GameRunState());
                fixture.Health = new HealthAndFamilyService(fixture.Registry,
                    HealthConditionCatalog.CreateStoneAge(), 2525, succession);
                var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
                var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
                var tier3 = new MemoryAdapter(CharacterSimulationTier.Tier3, failTier3For);
                foreach (var id in fixture.CharacterIds) tier1.Seed(Runtime(id));
                fixture.Tiers = new TierManager(fixture.Registry, tier1, tier2, tier3);
                foreach (var id in fixture.CharacterIds) fixture.Tiers.RegisterExisting(id, CharacterSimulationTier.Tier1);
                var positions = new MemoryPositions();
                foreach (var id in fixture.CharacterIds) positions.Set(id, default);
                fixture.Combat = new CombatService(fixture.Registry, fixture.Health, positions, 2525, fixture.Tiers);
                fixture.Tiers.BindTransitionGuard(fixture.Combat);
                fixture.Units = new MilitaryUnitService(fixture.Registry, fixture.Health);
                var groups = fixture.CharacterIds.Select((id, index) => new { id, index })
                    .GroupBy(x => x.index / 2).Select(x => x.Select(v => v.id).ToArray()).ToArray();
                for (var i = 0; i < groups.Length; i++)
                {
                    var unit = fixture.Units.Create(UnitId(500 + i), new MilitarySideId("player"), groups[i],
                        groups[i][0], MilitaryUnitRepresentationMode.Detailed, "starter_settlement");
                    if (i == 0) fixture.FirstUnit = unit.Id;
                }
                fixture.Representations = new MilitaryUnitRepresentationService(fixture.Units, fixture.Tiers,
                    fixture.Combat, fixture.Registry, fixture.Health);
                if (aggregate)
                    foreach (var unit in fixture.Units.Units)
                        fixture.Representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
                var physical = new MilitaryUnitCombatService(fixture.Units, fixture.Combat);
                fixture.Battles = new BattleResolutionService(fixture.Registry, fixture.Combat, fixture.Units,
                    fixture.Representations, physical);
                var initial = StarterGlobalMap.Create();
                var starter = initial.Settlements.Single(x => x.LocationId == new WorldLocationId("starter_settlement"));
                initial = new GlobalMapSaveState(initial.GraphId, initial.Mode, initial.ActiveLocationId,
                    initial.Locations, initial.Routes, new[] {
                        new SettlementState(starter.Id, starter.LocationId, starter.OwnerSideId, null, fixture.CharacterIds),
                        initial.Settlements.Single(x => x.LocationId == new WorldLocationId("enemy_settlement")) },
                    null, 0);
                fixture.Map = new GlobalMapService(initial, fixture.Units, fixture.Battles);
                fixture.Transitions = new GameModeTransitionService(fixture.Map, fixture.Tiers, fixture.Units,
                    fixture.Representations, fixture.Battles, fixture.Control);
                return fixture;
            }

            public static Fixture CreateHostilePair(bool aggregate = true)
            {
                var fixture = Create(4, aggregate: false);
                var secondMembers = fixture.Units.Units.Last().ActiveMemberIds;
                var originalSecond = fixture.Units.Units.Last();
                foreach (var id in secondMembers) fixture.Units.RemoveMember(originalSecond.Id, id);
                fixture.SecondUnit = UnitId(599);
                fixture.Units.Create(fixture.SecondUnit, new MilitarySideId("enemy"), secondMembers,
                    secondMembers[0], MilitaryUnitRepresentationMode.Detailed, "enemy_settlement");
                if (aggregate)
                    foreach (var unit in fixture.Units.Units.Where(x => x.Status != MilitaryUnitStatus.Disbanded))
                        fixture.Representations.Transition(unit.Id, MilitaryUnitRepresentationMode.Aggregate);
                return fixture;
            }

            private static Character MakeCharacter(StableEntityId id, StableEntityId dynastyId, int index) =>
                Character.Restore(new CharacterState(id, new CharacterName("Global" + index), CharacterSex.Male,
                    0, CharacterLifeState.Alive, null, null, null, dynastyId, dynastyId, null,
                    new[] { new TraitId("brave"), new TraitId("calm"), new TraitId("fit"), new TraitId("steady") },
                    new[] { new SkillState(new SkillId("combat"), new SkillLevel(40)) },
                    new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                    BodyHealth.Healthy, new Wealth(0), new Influence(index), null));

            private static CharacterRuntimeState Runtime(StableEntityId id) => new CharacterRuntimeState(id,
                default, default, false, "starter_settlement", 0, 0);
        }

        private sealed class FakeControl : IGameModePlayerControl
        {
            public PlayerControlMode ModeValue = PlayerControlMode.Rts;
            public StableEntityId? Controlled;
            public PlayerControlMode Mode => ModeValue;
            public StableEntityId? ControlledCharacterId => Controlled;
            public bool Exit() { ModeValue = PlayerControlMode.Rts; Controlled = null; return true; }
            public bool TryEnter(StableEntityId characterId)
            { ModeValue = PlayerControlMode.ThirdPerson; Controlled = characterId; return true; }
        }

        private sealed class MemoryPositions : ICombatPositionProvider
        {
            private readonly Dictionary<StableEntityId, WorldPosition> values =
                new Dictionary<StableEntityId, WorldPosition>();
            public void Set(StableEntityId id, WorldPosition position) => values[id] = position;
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
            private readonly StableEntityId? failFor;
            public MemoryAdapter(CharacterSimulationTier tier, StableEntityId? fail = null)
            { Tier = tier; failFor = fail; }
            public CharacterSimulationTier Tier { get; }
            public void Seed(CharacterRuntimeState state) => states.Add(state.CharacterId, state);
            public bool IsActive(StableEntityId characterId) => states.ContainsKey(characterId);
            public CharacterRuntimeState Capture(StableEntityId characterId) => states[characterId];
            public void ValidateMaterialization(Character character, CharacterRuntimeState state)
            {
                if (character.Id != state.CharacterId || states.ContainsKey(character.Id)) throw new InvalidOperationException();
                if (failFor == character.Id) throw new InvalidOperationException("Injected tier failure.");
            }
            public void Materialize(Character character, CharacterRuntimeState state) => states.Add(character.Id, state);
            public void Dematerialize(StableEntityId characterId)
            { if (!states.Remove(characterId)) throw new InvalidOperationException(); }
        }
    }
}
