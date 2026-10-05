using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.ECS.Tier2;
using Game.Simulation.Tiers;
using Game.Simulation.Time;
using NUnit.Framework;

namespace Game.Tests
{
    [TestFixture]
    [Category("U13")]
    public sealed class TierManagerTests
    {
        [Test]
        public void NamedCharacterRoundTripPreservesIdentityFamilyHealthAndRepresentativeState()
        {
            var setup = CreateManager();
            var character = setup.Character;
            var before = character.CaptureState();

            setup.Manager.Transition(character.Id, CharacterSimulationTier.Tier2);
            setup.Manager.Transition(character.Id, CharacterSimulationTier.Tier3);
            setup.Manager.Transition(character.Id, CharacterSimulationTier.Tier1);

            var after = character.CaptureState();
            Assert.That(after.Id, Is.EqualTo(before.Id));
            Assert.That(after.Name, Is.EqualTo(before.Name));
            Assert.That(after.Sex, Is.EqualTo(before.Sex));
            Assert.That(after.Parents, Is.EquivalentTo(before.Parents));
            Assert.That(after.SpouseId, Is.EqualTo(before.SpouseId));
            Assert.That(after.FamilyId, Is.EqualTo(before.FamilyId));
            Assert.That(after.DynastyId, Is.EqualTo(before.DynastyId));
            Assert.That(after.Health, Is.EqualTo(before.Health));
            Assert.That(after.Traits, Is.EquivalentTo(before.Traits));
            Assert.That(after.Skills, Is.EquivalentTo(before.Skills));
            Assert.That(after.ProfessionId, Is.EqualTo(before.ProfessionId));
            Assert.That(after.Relationships, Is.EquivalentTo(before.Relationships));
            Assert.That(setup.Manager.GetRuntimeState(character.Id), Is.EqualTo(setup.InitialState));
        }

        [Test]
        public void EverySupportedTransitionKeepsExactlyOneActiveRepresentation()
        {
            var setup = CreateManager();
            var sequence = new[]
            {
                CharacterSimulationTier.Tier2, CharacterSimulationTier.Tier1,
                CharacterSimulationTier.Tier3, CharacterSimulationTier.Tier2,
                CharacterSimulationTier.Tier3, CharacterSimulationTier.Tier1
            };
            foreach (var tier in sequence)
            {
                setup.Manager.Transition(setup.Character.Id, tier);
                Assert.That(setup.Manager.GetTier(setup.Character.Id), Is.EqualTo(tier));
                Assert.That(setup.Manager.CountActiveRepresentations(setup.Character.Id), Is.EqualTo(1));
            }
        }

        [Test]
        public void OneHundredRoundTripsDoNotLeakRepresentations()
        {
            var setup = CreateManager();
            for (var i = 0; i < 100; i++)
            {
                setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier2);
                setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier3);
                setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier1);
                Assert.That(setup.Manager.CountActiveRepresentations(setup.Character.Id), Is.EqualTo(1));
            }
            Assert.That(setup.Manager.GetRuntimeState(setup.Character.Id), Is.EqualTo(setup.InitialState));
        }

        [Test]
        public void SameTierTransitionIsNoOpAndDoesNotDuplicateRepresentation()
        {
            var setup = CreateManager();
            var result = setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier1);
            Assert.That(result.Changed, Is.False);
            Assert.That(setup.Tier1.MaterializeCount, Is.EqualTo(0));
            Assert.That(setup.Manager.CountActiveRepresentations(setup.Character.Id), Is.EqualTo(1));
        }

        [Test]
        public void FailedTargetMaterializationRollsBackUsableSource()
        {
            var setup = CreateManager();
            setup.Tier2.FailNextMaterialization = true;
            var error = Assert.Throws<TierTransitionException>(() =>
                setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier2));
            Assert.That(error.SourceRestored, Is.True);
            Assert.That(setup.Manager.GetTier(setup.Character.Id), Is.EqualTo(CharacterSimulationTier.Tier1));
            Assert.That(setup.Manager.CountActiveRepresentations(setup.Character.Id), Is.EqualTo(1));
            Assert.That(setup.Manager.GetRuntimeState(setup.Character.Id), Is.EqualTo(setup.InitialState));
        }

        [Test]
        public void Tier3RecordIsLightweightAndDoesNotCopyCanonicalBiography()
        {
            var setup = CreateManager();
            setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier3);
            var record = setup.Tier3Records.Get(setup.Character.Id);
            Assert.That(record.CharacterId, Is.EqualTo(setup.Character.Id));
            Assert.That(record.State, Is.EqualTo(setup.InitialState));
            Assert.That(typeof(Tier3CharacterRecord).GetProperty("Health"), Is.Null);
            Assert.That(typeof(Tier3CharacterRecord).GetProperty("Traits"), Is.Null);
            Assert.That(typeof(Tier3CharacterRecord).GetProperty("FamilyId"), Is.Null);
        }

        [Test]
        public void CanonicalInventoryAndWorkGroupSurviveAllTierChanges()
        {
            var setup = CreateManager();
            var catalog = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction)
            });
            var inventory = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character,
                setup.Character.Id), catalog, 12);
            inventory.TryAdd(new ResourceId("wood_log"), new ResourceQuantity(7));
            var inventories = new ResourceInventoryRegistry();
            inventories.Add(inventory);
            var groups = new WorkGroupRegistry();
            var group = WorkGroup.CreateNew("Builders");
            group.AddMember(setup.Character.Id);
            groups.Add(group);

            setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier2);
            setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier3);
            setup.Manager.Transition(setup.Character.Id, CharacterSimulationTier.Tier1);

            Assert.That(inventories.Get(inventory.Owner), Is.SameAs(inventory));
            Assert.That(inventory.GetAmount(new ResourceId("wood_log")).Units, Is.EqualTo(7));
            Assert.That(groups.TryGetForMember(setup.Character.Id, out var restoredGroup), Is.True);
            Assert.That(restoredGroup, Is.SameAs(group));
        }

        [Test]
        public void RealTier2AdapterUsesU12ProjectionAndExtractionBarrier()
        {
            var character = CreateCharacter();
            var registry = new CharacterRegistry();
            registry.Add(character);
            var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
            var tier3 = new Tier3CharacterAdapter(new Tier3CharacterRegistry());
            var initial = Runtime(character.Id, x: 2f, velocity: 4f);
            tier1.Seed(initial);
            using (var runtime = new Tier2Runtime("U13 adapter test"))
            {
                var manager = new TierManager(registry, tier1, new Tier2CharacterAdapter(runtime), tier3);
                manager.RegisterExisting(character.Id, CharacterSimulationTier.Tier1);
                manager.Transition(character.Id, CharacterSimulationTier.Tier2);
                runtime.Step(new GameTimeAdvance(TimeSpan.FromHours(12), TimeSpan.FromHours(2)));
                var projected = manager.GetRuntimeState(character.Id);
                Assert.That(projected.CharacterId, Is.EqualTo(character.Id));
                Assert.That(projected.Position.X, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(projected.StepCount, Is.EqualTo(10));
                manager.Transition(character.Id, CharacterSimulationTier.Tier3);
                Assert.That(runtime.Materializer.EntityCount, Is.Zero);
                Assert.That(manager.GetRuntimeState(character.Id).Position.X, Is.EqualTo(4f).Within(0.0001f));
            }
        }

        [Test]
        public void DistancePolicyAppliesHysteresisCooldownAndTransitionBudget()
        {
            var registry = new CharacterRegistry();
            var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
            var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
            var tier3 = new MemoryAdapter(CharacterSimulationTier.Tier3);
            var manager = new TierManager(registry, tier1, tier2, tier3);
            for (var i = 1; i <= 3; i++)
            {
                var character = CreateCharacter("Policy" + i);
                registry.Add(character);
                var state = Runtime(character.Id, 100f + i, 0f);
                tier1.Seed(state);
                manager.RegisterExisting(character.Id, CharacterSimulationTier.Tier1);
            }
            var policy = new TierDistancePolicy(manager,
                new TierDistancePolicySettings(10f, 20f, 60f, 80f, 3, 1, 1));
            var focus = new WorldPosition(0, 0, 0);

            policy.Evaluate(focus);
            Assert.That(policy.LastTransitioned, Is.Zero, "minimum residency prevents the first evaluation from thrashing");
            policy.Evaluate(focus);
            Assert.That(policy.LastTransitioned, Is.EqualTo(1));
            Assert.That(manager.ManagedIds.Count(id => manager.GetTier(id) == CharacterSimulationTier.Tier3), Is.EqualTo(1));
            policy.Evaluate(focus);
            Assert.That(policy.LastTransitioned, Is.EqualTo(1), "only one transition is allowed per evaluation");
        }

        private static Setup CreateManager()
        {
            var character = CreateCharacter();
            var registry = new CharacterRegistry();
            registry.Add(character);
            var tier1 = new MemoryAdapter(CharacterSimulationTier.Tier1);
            var tier2 = new MemoryAdapter(CharacterSimulationTier.Tier2);
            var tier3Records = new Tier3CharacterRegistry();
            var state = Runtime(character.Id, 12f, 3f);
            tier1.Seed(state);
            var manager = new TierManager(registry, tier1, tier2, new Tier3CharacterAdapter(tier3Records));
            manager.RegisterExisting(character.Id, CharacterSimulationTier.Tier1);
            return new Setup(character, manager, tier1, tier2, tier3Records, state);
        }

        private static Character CreateCharacter(string name = "Aren")
        {
            var mother = StableEntityId.Parse("00000000000000000000000000000011");
            var spouse = StableEntityId.Parse("00000000000000000000000000000012");
            var family = StableEntityId.Parse("00000000000000000000000000000013");
            var dynasty = StableEntityId.Parse("00000000000000000000000000000014");
            var related = StableEntityId.Parse("00000000000000000000000000000015");
            return Character.CreateNew(new CharacterCreationData(new CharacterName(name, "Stone"), CharacterSex.Male,
                123, new[] { new ParentLink(mother, ParentRole.Mother, ParentageKind.Biological, true) }, spouse,
                family, dynasty, new ProfessionId("builder"),
                new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                new[] { new SkillState(new SkillId("craft"), new SkillLevel(67)) },
                new CharacterAttributes(new CharacterAttributeValue(72), new CharacterAttributeValue(61)),
                new BodyHealth(new HealthValue(91), new HealthValue(82), new HealthValue(73),
                    new HealthValue(64), new HealthValue(55), new HealthValue(46)),
                new Wealth(27), new Influence(35), new[] { new RelationshipState(related, new RelationshipScore(44)) }));
        }

        private static CharacterRuntimeState Runtime(StableEntityId id, float x, float velocity) =>
            new CharacterRuntimeState(id, new WorldPosition(x, 0, 5), new WorldPosition(velocity, 0, 0),
                velocity != 0f, "settlement.alpha", 77, 88, 9, 0.25d, 0.75d);

        private sealed class MemoryAdapter : ICharacterTierAdapter
        {
            private readonly Dictionary<StableEntityId, CharacterRuntimeState> states =
                new Dictionary<StableEntityId, CharacterRuntimeState>();
            public MemoryAdapter(CharacterSimulationTier tier) { Tier = tier; }
            public CharacterSimulationTier Tier { get; }
            public bool FailNextMaterialization { get; set; }
            public int MaterializeCount { get; private set; }
            public void Seed(CharacterRuntimeState state) => states.Add(state.CharacterId, state);
            public bool IsActive(StableEntityId characterId) => states.ContainsKey(characterId);
            public CharacterRuntimeState Capture(StableEntityId characterId) => states[characterId];
            public void ValidateMaterialization(Character character, CharacterRuntimeState state)
            {
                if (character.Id != state.CharacterId) throw new ArgumentException();
                if (states.ContainsKey(character.Id)) throw new InvalidOperationException();
            }
            public void Materialize(Character character, CharacterRuntimeState state)
            {
                MaterializeCount++;
                if (FailNextMaterialization)
                {
                    FailNextMaterialization = false;
                    throw new InvalidOperationException("forced failure");
                }
                states.Add(character.Id, state);
            }
            public void Dematerialize(StableEntityId characterId)
            {
                if (!states.Remove(characterId)) throw new InvalidOperationException();
            }
        }

        private readonly struct Setup
        {
            public Setup(Character character, TierManager manager, MemoryAdapter tier1, MemoryAdapter tier2,
                Tier3CharacterRegistry tier3Records, CharacterRuntimeState initialState)
            {
                Character = character;
                Manager = manager;
                Tier1 = tier1;
                Tier2 = tier2;
                Tier3Records = tier3Records;
                InitialState = initialState;
            }
            public Character Character { get; }
            public TierManager Manager { get; }
            public MemoryAdapter Tier1 { get; }
            public MemoryAdapter Tier2 { get; }
            public Tier3CharacterRegistry Tier3Records { get; }
            public CharacterRuntimeState InitialState { get; }
        }
    }
}
