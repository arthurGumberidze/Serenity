using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Dynasties;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation;
using Game.Simulation.Dynasties;
using Game.Simulation.Time;
using Game.Simulation.Tiers;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U17")]
    public sealed class DynastyAndSuccessionTests
    {
        [Test]
        public void DynastyStateValidatesFounderMonarchAndUniqueMembers()
        {
            var state = DynastyState.Create(Id(100), "House Test", Id(1), Id(1), new[] { Id(1), Id(2) });
            Assert.That(state.FounderId, Is.EqualTo(Id(1)));
            Assert.That(state.CurrentMonarchId, Is.EqualTo(Id(1)));
            Assert.That(state.Status, Is.EqualTo(DynastyStatus.Active));
            Assert.Throws<ArgumentException>(() => DynastyState.Create(Id(100), "House Test", Id(1), Id(1),
                new[] { Id(1), Id(1) }));
            Assert.Throws<ArgumentException>(() => DynastyState.Create(Id(100), "House Test", Id(9), Id(1),
                new[] { Id(1), Id(2) }));
        }

        [Test]
        public void FamilyQueryDerivesParentsChildrenSiblingsAndDescendants()
        {
            var fixture = Fixture.Basic();
            var query = fixture.Service.Family;
            Assert.That(query.GetParents(Id(3)).Select(x => x.Id), Does.Contain(Id(1)));
            Assert.That(query.GetChildren(Id(1)).Select(x => x.Id), Is.EqualTo(new[] { Id(3), Id(4) }));
            Assert.That(query.GetSiblings(Id(3)).Select(x => x.Id), Is.EqualTo(new[] { Id(4) }));
            Assert.That(query.IsDescendant(Id(5), Id(1)), Is.True);
            Assert.That(query.GetDescendants(Id(3)).Select(x => x.Id), Is.EqualTo(new[] { Id(5) }));
        }

        [Test]
        public void EligibilityRejectsDeadMissingFemaleAndNonMembers()
        {
            var fixture = Fixture.Basic();
            fixture.Registry.Get(Id(3)).MarkDead(10);
            var query = fixture.Service.Family;
            Assert.That(query.CheckEligibility(fixture.Dynasty, Id(4), Id(1)).IsEligible, Is.True);
            Assert.That(query.CheckEligibility(fixture.Dynasty, Id(3), Id(1)).Reason,
                Is.EqualTo(SuccessionIneligibilityReason.Deceased));
            Assert.That(query.CheckEligibility(fixture.Dynasty, Id(99), Id(1)).Reason,
                Is.EqualTo(SuccessionIneligibilityReason.MissingCharacter));
            Assert.That(query.CheckEligibility(fixture.Dynasty, Id(2), Id(1)).Reason,
                Is.EqualTo(SuccessionIneligibilityReason.NotDynastyMember));
            var femaleRegistry = new CharacterRegistry();
            femaleRegistry.Add(Person(1, "Monarch", CharacterSex.Male, 0, Id(100)));
            femaleRegistry.Add(Person(6, "Daughter", CharacterSex.Female, 10, Id(100), Parent(Id(1))));
            var femaleDynasty = DynastyState.Create(Id(100), "House Test", Id(1), Id(1), new[] { Id(1), Id(6) });
            var femaleQuery = Service(femaleRegistry, femaleDynasty).Family;
            Assert.That(femaleQuery.CheckEligibility(femaleDynasty, Id(6), Id(1)).Reason,
                Is.EqualTo(SuccessionIneligibilityReason.FemaleUnderAgnaticRule));
        }

        [Test]
        public void PrimogenitureUsesOldestBranchBirthTickAndStableIdTieBreak()
        {
            var fixture = Fixture.Basic();
            fixture.Registry.Get(Id(3)).MarkDead(10);
            var ordered = fixture.Service.Family.GetOrderedSuccessionCandidates(fixture.Dynasty, Id(1));
            Assert.That(ordered.Select(x => x.Id), Is.EqualTo(new[] { Id(5), Id(4) }),
                "A dead elder son is skipped, but his living male descendant retains branch priority.");

            var tie = Fixture.WithChildren((Id(8), 20L), (Id(7), 20L));
            Assert.That(tie.Service.Family.GetOrderedSuccessionCandidates(tie.Dynasty, Id(1)).Select(x => x.Id),
                Is.EqualTo(new[] { Id(7), Id(8) }));
        }

        [Test]
        public void ChildlessMonarchFallsBackToSiblingBranch()
        {
            var registry = new CharacterRegistry();
            var dynastyId = Id(100);
            registry.Add(Person(10, "Parent", CharacterSex.Male, 0, dynastyId));
            registry.Add(Person(1, "Monarch", CharacterSex.Male, 10, dynastyId, Parent(Id(10))));
            registry.Add(Person(2, "Sibling", CharacterSex.Male, 20, dynastyId, Parent(Id(10))));
            registry.Add(Person(3, "Nephew", CharacterSex.Male, 30, dynastyId, Parent(Id(2))));
            var dynasty = DynastyState.Create(dynastyId, "House Test", Id(10), Id(1),
                new[] { Id(10), Id(1), Id(2), Id(3) });
            var service = Service(registry, dynasty);
            registry.Get(Id(2)).MarkDead(40);
            Assert.That(service.Family.GetOrderedSuccessionCandidates(dynasty, Id(1)).Select(x => x.Id).First(),
                Is.EqualTo(Id(3)));
        }

        [Test]
        public void MonarchDeathTransfersRulerAndRecordsExactlyOnce()
        {
            var fixture = Fixture.Basic();
            var first = fixture.Service.ProcessDeath(Id(1), 100, 200);
            var second = fixture.Service.ProcessDeath(Id(1), 100, 200);
            Assert.That(first.Applied, Is.True);
            Assert.That(first.NewMonarchId, Is.EqualTo(Id(3)));
            Assert.That(second.Applied, Is.False);
            Assert.That(fixture.Dynasty.History.Count, Is.EqualTo(1));
            Assert.That(fixture.Player.CurrentRulerId, Is.EqualTo(Id(3)));
            Assert.That(fixture.Dynasty.History[0].SequenceNumber, Is.EqualTo(1));
            Assert.That(fixture.Dynasty.History[0].CalendarTick, Is.EqualTo(200));
        }

        [Test]
        public void NoValidHeirProducesPermanentDynastyExtinctionGameOver()
        {
            var fixture = Fixture.Basic();
            foreach (var id in new[] { Id(3), Id(4), Id(5) }) fixture.Registry.Get(id).MarkDead(90);
            var result = fixture.Service.ProcessDeath(Id(1), 100, 200);
            Assert.That(result.Outcome, Is.EqualTo(SuccessionOutcome.DynastyExtinct));
            Assert.That(fixture.Dynasty.CurrentMonarchId, Is.Null);
            Assert.That(fixture.Dynasty.Status, Is.EqualTo(DynastyStatus.Extinct));
            Assert.That(fixture.Player.CurrentRulerId, Is.Null);
            Assert.That(fixture.Run.Status, Is.EqualTo(GameRunStatus.GameOver));
            Assert.That(fixture.Run.GameOverReason, Is.EqualTo(GameOverReason.DynastyExtinction));
            Assert.That(fixture.Dynasty.History.Single().Reason, Is.EqualTo(SuccessionReason.DynastyExtinction));
            Assert.That(fixture.Service.ProcessDeath(Id(1), 100, 200).Applied, Is.False);
            Assert.That(fixture.Dynasty.History.Count, Is.EqualTo(1));
        }

        [Test]
        public void CandidateOrderIgnoresRegistryInsertionOrder()
        {
            var forward = Fixture.Basic(new[] { 1, 2, 3, 4, 5 });
            var shuffled = Fixture.Basic(new[] { 5, 2, 4, 1, 3 });
            var first = forward.Service.Family.GetOrderedSuccessionCandidates(forward.Dynasty, Id(1)).Select(x => x.Id);
            var second = shuffled.Service.Family.GetOrderedSuccessionCandidates(shuffled.Dynasty, Id(1)).Select(x => x.Id);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SaveLoadPreservesFamilySuccessionHistoryExtinctionAndExactlyOnceMarkers()
        {
            var fixture = Fixture.Basic();
            fixture.Service.ProcessDeath(Id(1), 100, 200);
            var codec = new SaveXmlCodec();
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(200),
                new GameTimeState(200, 100, 2, false), 100, null, null, fixture.Service.CaptureState());
            var restoredSnapshot = codec.Deserialize(codec.Serialize(snapshot));
            var restored = Fixture.Basic();
            restored.Service.RestoreState(restoredSnapshot.DynastyWorld);

            Assert.That(restored.Dynasty.CurrentMonarchId, Is.EqualTo(Id(3)));
            Assert.That(restored.Player.CurrentRulerId, Is.EqualTo(Id(3)));
            Assert.That(restored.Registry.Get(Id(1)).LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(restored.Registry.Get(Id(3)).Parents.Select(x => x.ParentId), Does.Contain(Id(1)));
            Assert.That(restored.Dynasty.History, Is.EqualTo(fixture.Dynasty.History));
            Assert.That(restored.Service.Family.GetOrderedSuccessionCandidates(restored.Dynasty, Id(3))
                .Select(x => x.Id), Is.EqualTo(new[] { Id(5), Id(4) }));
            Assert.That(restored.Service.ProcessDeath(Id(1), 100, 200).Applied, Is.False);
            Assert.That(restored.Dynasty.History.Count, Is.EqualTo(1));

            restored.Registry.Get(Id(3)).MarkDead(300);
            restored.Registry.Get(Id(4)).MarkDead(300);
            restored.Registry.Get(Id(5)).MarkDead(300);
            restored.Service.ProcessDeath(Id(3), 300, 400);
            var extinctBytes = codec.Serialize(new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(200),
                new GameTimeState(400, 300, 2, false), 100, null, null, restored.Service.CaptureState()));
            var extinct = codec.Deserialize(extinctBytes).DynastyWorld;
            Assert.That(extinct.Dynasty.Status, Is.EqualTo(DynastyStatus.Extinct));
            Assert.That(extinct.RunStatus, Is.EqualTo(GameRunStatus.GameOver));
        }

        [Test]
        public void SaveCoordinatorCapturesAndRestoresDynastyWorld()
        {
            var source = Fixture.Basic();
            source.Service.ProcessDeath(Id(1), 100, 200);
            var store = new MemoryStore();
            var slot = Id(250);
            var coordinator = new SaveCoordinator(Id(200), new GameClock(new GameTimeState(200, 100, 1, false)),
                store, successionService: source.Service);
            coordinator.Save(slot);

            var target = Fixture.Basic();
            var restoredCoordinator = new SaveCoordinator(Id(201), new GameClock(new GameTimeState()), store,
                successionService: target.Service);
            restoredCoordinator.Load(slot);
            Assert.That(target.Dynasty.CurrentMonarchId, Is.EqualTo(Id(3)));
            Assert.That(target.Registry.Get(Id(1)).LifeState, Is.EqualTo(CharacterLifeState.Dead));
            Assert.That(target.Player.CurrentRulerId, Is.EqualTo(Id(3)));
            Assert.That(target.Dynasty.History.Count, Is.EqualTo(1));
        }

        [Test]
        public void LegacySnapshotVersionsOneTwoAndThreeRemainReadable()
        {
            var codec = new SaveXmlCodec();
            for (var version = 1; version <= 3; version++)
            {
                var snapshot = new SaveSnapshot(version, Id(200), new GameTimeState(7, 9, 1, false), 100);
                var restored = codec.Deserialize(codec.Serialize(snapshot));
                Assert.That(restored.FormatVersion, Is.EqualTo(version));
                Assert.That(restored.DynastyWorld, Is.Null);
            }
        }

        [Test]
        public void CorruptFamilyAndDynastyReferencesFailCleanly()
        {
            var registry = new CharacterRegistry();
            var dynastyId = Id(100);
            registry.Add(Person(1, "Monarch", CharacterSex.Male, 0, dynastyId, Parent(Id(99))));
            var dynasty = DynastyState.Create(dynastyId, "House Test", Id(1), Id(1), new[] { Id(1) });
            Assert.Throws<InvalidOperationException>(() => Service(registry, dynasty));

            var selfSpouse = Person(2, "Self", CharacterSex.Male, 0, dynastyId).CaptureState();
            Assert.Throws<ArgumentException>(() => Character.Restore(new CharacterState(selfSpouse.Id, selfSpouse.Name,
                selfSpouse.Sex, selfSpouse.BiologicalBirthTick, selfSpouse.LifeState, selfSpouse.BiologicalDeathTick,
                selfSpouse.Parents, selfSpouse.Id, selfSpouse.FamilyId, selfSpouse.DynastyId,
                selfSpouse.ProfessionId, selfSpouse.Traits, selfSpouse.Skills, selfSpouse.Attributes,
                selfSpouse.Health, selfSpouse.Wealth, selfSpouse.Influence, selfSpouse.Relationships)));
        }

        [Test]
        public void TierRoutesProduceIdenticalHeirAndHistory()
        {
            var tier3 = RunTierRoute(null);
            var viaTier2 = RunTierRoute(CharacterSimulationTier.Tier2);
            var viaTier1 = RunTierRoute(CharacterSimulationTier.Tier1);
            Assert.That(viaTier2.CurrentMonarchId, Is.EqualTo(tier3.CurrentMonarchId));
            Assert.That(viaTier1.CurrentMonarchId, Is.EqualTo(tier3.CurrentMonarchId));
            Assert.That(viaTier2.History, Is.EqualTo(tier3.History));
            Assert.That(viaTier1.History, Is.EqualTo(tier3.History));
        }

        private static DynastyStateSnapshot RunTierRoute(CharacterSimulationTier? intermediate)
        {
            var fixture = Fixture.Basic();
            var t1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
            var t2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
            var t3 = new MemoryAdapter(CharacterSimulationTier.Tier3);
            foreach (var character in fixture.Registry.GetAll())
            {
                t3.Seed(Runtime(character.Id));
            }
            var manager = new TierManager(fixture.Registry, t1, t2, t3);
            foreach (var character in fixture.Registry.GetAll()) manager.RegisterExisting(character.Id, CharacterSimulationTier.Tier3);
            if (intermediate.HasValue)
            {
                foreach (var character in fixture.Registry.GetAll()) manager.Transition(character.Id, intermediate.Value);
                foreach (var character in fixture.Registry.GetAll()) manager.Transition(character.Id, CharacterSimulationTier.Tier3);
            }
            fixture.Service.ProcessDeath(Id(1), 100, 200);
            return fixture.Dynasty.CaptureState();
        }

        private static SuccessionService Service(CharacterRegistry registry, DynastyState dynasty)
        {
            var player = new PlayerRealmState(dynasty.CurrentMonarchId);
            return new SuccessionService(registry, dynasty, player, new GameRunState());
        }

        private static Character Person(int id, string name, CharacterSex sex, long birth,
            StableEntityId? dynastyId, params ParentLink[] parents) => Character.Restore(new CharacterState(
                Id(id), new CharacterName(name), sex, birth, CharacterLifeState.Alive, null, parents, null,
                dynastyId, dynastyId, null, Traits(), null,
                new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static ParentLink Parent(StableEntityId id) =>
            new ParentLink(id, ParentRole.Father, ParentageKind.Biological, true);
        private static TraitId[] Traits() => new[]
            { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") };
        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        private static CharacterRuntimeState Runtime(StableEntityId id) => new CharacterRuntimeState(id,
            new WorldPosition(0, 0, 0), default, false, "test", 0, 0, 0, 0.2d, 0.8d);

        private sealed class MemoryAdapter : ICharacterTierAdapter
        {
            private readonly Dictionary<StableEntityId, CharacterRuntimeState> states =
                new Dictionary<StableEntityId, CharacterRuntimeState>();
            public MemoryAdapter(CharacterSimulationTier tier) => Tier = tier;
            public CharacterSimulationTier Tier { get; }
            public void Seed(CharacterRuntimeState state) => states.Add(state.CharacterId, state);
            public bool IsActive(StableEntityId id) => states.ContainsKey(id);
            public CharacterRuntimeState Capture(StableEntityId id) => states[id];
            public void ValidateMaterialization(Character character, CharacterRuntimeState state)
            {
                if (character.Id != state.CharacterId || states.ContainsKey(character.Id)) throw new InvalidOperationException();
            }
            public void Materialize(Character character, CharacterRuntimeState state) => states.Add(character.Id, state);
            public void Dematerialize(StableEntityId id) => states.Remove(id);
        }

        private sealed class MemoryStore : ISaveStore
        {
            private readonly Dictionary<StableEntityId, SaveSnapshot> values =
                new Dictionary<StableEntityId, SaveSnapshot>();
            public void Save(StableEntityId slotId, SaveSnapshot snapshot) => values[slotId] = snapshot;
            public SaveSnapshot Load(StableEntityId slotId) => values[slotId];
        }

        private sealed class Fixture
        {
            public CharacterRegistry Registry { get; }
            public DynastyState Dynasty { get; }
            public PlayerRealmState Player { get; }
            public GameRunState Run { get; }
            public SuccessionService Service { get; }

            private Fixture(CharacterRegistry registry, DynastyState dynasty)
            {
                Registry = registry;
                Dynasty = dynasty;
                Player = new PlayerRealmState(dynasty.CurrentMonarchId);
                Run = new GameRunState();
                Service = new SuccessionService(registry, dynasty, Player, Run);
            }

            public static Fixture Basic(int[] insertionOrder = null)
            {
                var dynastyId = Id(100);
                var people = new Dictionary<int, Character>
                {
                    [1] = Person(1, "Monarch", CharacterSex.Male, 0, dynastyId),
                    [2] = Person(2, "Spouse", CharacterSex.Female, 0, null),
                    [3] = Person(3, "Elder", CharacterSex.Male, 10, dynastyId, Parent(Id(1))),
                    [4] = Person(4, "Younger", CharacterSex.Male, 20, dynastyId, Parent(Id(1))),
                    [5] = Person(5, "Grandson", CharacterSex.Male, 30, dynastyId, Parent(Id(3)))
                };
                var registry = new CharacterRegistry();
                foreach (var id in insertionOrder ?? new[] { 1, 2, 3, 4, 5 }) registry.Add(people[id]);
                var dynasty = DynastyState.Create(dynastyId, "House Test", Id(1), Id(1),
                    new[] { Id(1), Id(3), Id(4), Id(5) });
                return new Fixture(registry, dynasty);
            }

            public static Fixture WithChildren(params (StableEntityId Id, long Birth)[] children)
            {
                var dynastyId = Id(100);
                var registry = new CharacterRegistry();
                registry.Add(Person(1, "Monarch", CharacterSex.Male, 0, dynastyId));
                foreach (var child in children)
                    registry.Add(Character.Restore(new CharacterState(child.Id, new CharacterName("Child" + child.Id),
                        CharacterSex.Male, child.Birth, CharacterLifeState.Alive, null, new[] { Parent(Id(1)) }, null,
                        dynastyId, dynastyId, null, Traits(), null,
                        new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
                        BodyHealth.Healthy, new Wealth(0), new Influence(0), null)));
                var dynasty = DynastyState.Create(dynastyId, "House Test", Id(1), Id(1),
                    new[] { Id(1) }.Concat(children.Select(x => x.Id)));
                return new Fixture(registry, dynasty);
            }
        }
    }

}
