using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Presentation.AI;
using Game.Presentation.Characters;
using Game.Simulation.AI;
using Game.Simulation.Resources;
using Game.Simulation.Time;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    [Category("U10")]
    public sealed class Tier1UtilityAiTests
    {
        [Test]
        public void UtilitySelectionIsBoundedDeterministicAndIgnoresUnavailableActions()
        {
            Assert.That(new UtilityScore(-5d).Value, Is.Zero);
            Assert.That(new UtilityScore(5d).Value, Is.EqualTo(1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UtilityScore(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UtilityScore(double.PositiveInfinity));
            var candidates = new[]
            {
                new UtilityCandidate(UtilityActionKind.Idle, new UtilityScore(0.1d), true, 2),
                new UtilityCandidate(UtilityActionKind.Haul, new UtilityScore(1d), false, 1),
                new UtilityCandidate(UtilityActionKind.Rest, new UtilityScore(0.7d), true, 0)
            };
            Assert.That(UtilitySelector.Select(candidates).Action, Is.EqualTo(UtilityActionKind.Rest));
            var tie = new[]
            {
                new UtilityCandidate(UtilityActionKind.Haul, new UtilityScore(0.5d), true, 1),
                new UtilityCandidate(UtilityActionKind.Rest, new UtilityScore(0.5d), true, 0)
            };
            Assert.That(UtilitySelector.Select(tie).Action, Is.EqualTo(UtilityActionKind.Rest));
        }

        [Test]
        public void UtilityCommitmentRequiresMarginAndHonorsNonInterruptiblePhase()
        {
            var current = new UtilityCandidate(UtilityActionKind.Haul, new UtilityScore(0.6d), true, 1);
            var weak = new UtilityCandidate(UtilityActionKind.Rest, new UtilityScore(0.7d), true, 0);
            var strong = new UtilityCandidate(UtilityActionKind.Rest, new UtilityScore(0.8d), true, 0);
            Assert.That(UtilitySelector.ShouldSwitch(current, weak, 0.15d, true, true), Is.False);
            Assert.That(UtilitySelector.ShouldSwitch(current, strong, 0.15d, true, true), Is.True);
            Assert.That(UtilitySelector.ShouldSwitch(current, strong, 0.15d, true, false), Is.False);
            Assert.That(UtilitySelector.ShouldSwitch(current, weak, 0.15d, false, false), Is.True);
        }

        [Test]
        public void NeedsUseExplicitSimulationDeltaAndClamp()
        {
            var needs = new Tier1Needs(0.9d, 0.1d);
            needs.Advance(0.2d, 0.2d);
            Assert.That(needs.Hunger, Is.EqualTo(1d));
            Assert.That(needs.Energy, Is.Zero);
            needs.RecoverEnergy(2d);
            Assert.That(needs.Energy, Is.EqualTo(1d));
        }

        [Test]
        public void ClaimRegistryPreventsSourceAndDestinationOverbooking()
        {
            var claims = new HaulClaimRegistry();
            var source = new InventoryOwner(InventoryOwnerKind.WorldPile, StableEntityId.NewId());
            var destination = new InventoryOwner(InventoryOwnerKind.BuildingStorage, StableEntityId.NewId());
            var wood = new ResourceId("wood_log");
            Assert.That(claims.TryClaim(StableEntityId.NewId(), source, destination, wood,
                new ResourceQuantity(4), 5, 6, out var first), Is.True);
            Assert.That(claims.TryClaim(StableEntityId.NewId(), source, destination, wood,
                new ResourceQuantity(2), 5, 6, out _), Is.False);
            Assert.That(claims.Release(first), Is.True);
            Assert.That(claims.Count, Is.Zero);
        }

        [Test]
        public void SchedulerBatchesStaggersAndPauseDoesNotAdvance()
        {
            var fixture = CreateFixture(12, 48, 200, thinkBatchSize: 3);
            fixture.Scheduler.Advance(new GameTimeAdvance(TimeSpan.Zero, TimeSpan.Zero));
            Assert.That(fixture.Scheduler.Metrics.TotalDecisions, Is.Zero);
            fixture.Scheduler.Advance(new GameTimeAdvance(TimeSpan.FromSeconds(2), TimeSpan.Zero));
            Assert.That(fixture.Scheduler.Metrics.LastDecisionBatch, Is.LessThanOrEqualTo(3));
            Assert.That(fixture.Scheduler.Metrics.TotalDecisions, Is.EqualTo(3));
            fixture.Scheduler.Advance(new GameTimeAdvance(TimeSpan.FromMilliseconds(1), TimeSpan.Zero));
            Assert.That(fixture.Scheduler.Metrics.TotalDecisions, Is.EqualTo(6));
            Assert.Throws<InvalidOperationException>(() => fixture.Scheduler.Register(fixture.Characters[0],
                fixture.Agents[0], fixture.Movements[0]));
        }

        [Test]
        public void LowEnergyAgentSelectsRestAndRecoversOnlyFromSimulationDelta()
        {
            var fixture = CreateFixture(1, 8, 0, thinkBatchSize: 1, initialEnergy: 0.1d);
            fixture.AdvanceSeconds(2d);
            Assert.That(fixture.Agents[0].CurrentAction, Is.EqualTo(UtilityActionKind.Rest));
            var before = fixture.Agents[0].Needs.Energy;
            fixture.Scheduler.Advance(new GameTimeAdvance(TimeSpan.Zero, TimeSpan.Zero));
            Assert.That(fixture.Agents[0].Needs.Energy, Is.EqualTo(before));
            fixture.AdvanceSeconds(60d);
            Assert.That(fixture.Agents[0].Needs.Energy, Is.GreaterThan(before));
        }

        [Test]
        public void HaulMovesExactQuantityAndConservesResources()
        {
            var fixture = CreateFixture(1, 8, 40, thinkBatchSize: 1);
            var before = fixture.TotalWood();
            StartAndPickup(fixture, 0);
            Assert.That(fixture.CharacterInventory(0).GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            Assert.That(fixture.SourceInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            CompleteDropoff(fixture, 0);
            Assert.That(fixture.StorageInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            Assert.That(fixture.CharacterInventory(0).GetAmount(fixture.Wood).Units, Is.Zero);
            Assert.That(fixture.TotalWood(), Is.EqualTo(before));
            Assert.That(fixture.Claims.Count, Is.Zero);
        }

        [Test]
        public void CancelBeforePickupLeavesSourceAndReleasesClaim()
        {
            var fixture = CreateFixture(1, 8, 40, thinkBatchSize: 1);
            fixture.AdvanceSeconds(2);
            Assert.That(fixture.Claims.Count, Is.EqualTo(1));
            Assert.That(fixture.Scheduler.Unregister(fixture.Characters[0].Id), Is.True);
            Assert.That(fixture.SourceInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(8));
            Assert.That(fixture.Claims.Count, Is.Zero);
        }

        [Test]
        public void CancelAfterPickupKeepsResourceInCharacterInventory()
        {
            var fixture = CreateFixture(1, 8, 40, thinkBatchSize: 1);
            StartAndPickup(fixture, 0);
            fixture.Scheduler.Unregister(fixture.Characters[0].Id);
            Assert.That(fixture.CharacterInventory(0).GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            Assert.That(fixture.SourceInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            Assert.That(fixture.Claims.Count, Is.Zero);
        }

        [Test]
        public void DestinationCapacityRaceFailsWithoutLossOrOverflow()
        {
            var fixture = CreateFixture(1, 8, 4, thinkBatchSize: 1);
            StartAndPickup(fixture, 0);
            Assert.That(fixture.StorageInventory.TryAdd(fixture.Wood, new ResourceQuantity(4)), Is.True);
            CompleteDropoff(fixture, 0);
            Assert.That(fixture.StorageInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            Assert.That(fixture.CharacterInventory(0).GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            Assert.That(fixture.TotalWood(), Is.EqualTo(12));
            Assert.That(fixture.Claims.Count, Is.Zero);
        }

        [Test]
        public void PathFailureCancelsTaskAndReleasesReservation()
        {
            var fixture = CreateFixture(1, 8, 40, thinkBatchSize: 1);
            fixture.AdvanceSeconds(2d);
            fixture.Movements[0].Fail();
            fixture.AdvanceSeconds(0.01d);
            Assert.That(fixture.Claims.Count, Is.Zero);
            Assert.That(fixture.SourceInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(8));
            Assert.That(fixture.CharacterInventory(0).TotalUnits, Is.Zero);
        }

        [Test]
        public void MissingDestinationAfterPickupKeepsCarriedResource()
        {
            var fixture = CreateFixture(1, 8, 40, thinkBatchSize: 1);
            StartAndPickup(fixture, 0);
            Assert.That(fixture.Inventories.Remove(fixture.StorageInventory.Owner, fixture.StorageInventory), Is.True);
            Assert.That(fixture.Buildings.Remove(fixture.StorageBuilding), Is.True);
            fixture.AdvanceSeconds(0.01d);
            Assert.That(fixture.CharacterInventory(0).GetAmount(fixture.Wood).Units, Is.EqualTo(4));
            Assert.That(fixture.Claims.Count, Is.Zero);
        }

        [Test]
        public void OneHundredAgentsAreProcessedCentrallyAndPerformHauling()
        {
            var fixture = CreateFixture(100, 800, 1000, thinkBatchSize: 8);
            var before = fixture.TotalWood();
            var stopwatch = Stopwatch.StartNew();
            var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 40; i++)
            {
                foreach (var movement in fixture.Movements) movement.Arrive();
                fixture.AdvanceSeconds(i == 0 ? 2d : 0.01d);
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
            stopwatch.Stop();
            Assert.That(fixture.Scheduler.Count, Is.EqualTo(100));
            Assert.That(fixture.Scheduler.Metrics.MaxDecisionBatch, Is.LessThanOrEqualTo(8));
            Assert.That(fixture.Agents.All(x => x.DecisionsMade > 0), Is.True);
            Assert.That(fixture.StorageInventory.GetAmount(fixture.Wood).Units, Is.GreaterThan(0));
            Assert.That(fixture.TotalWood(), Is.EqualTo(before));
            Assert.That(allocated, Is.LessThan(8_000_000));
            TestContext.WriteLine($"U10_PERF agents=100 decisions={fixture.Scheduler.Metrics.TotalDecisions} " +
                $"maxEvaluationsPerAdvance={fixture.Scheduler.Metrics.MaxDecisionBatch} elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.###} " +
                $"allocatedBytes={allocated} perAgentAiUpdates=0");
        }

        [Test]
        public void AiDecisionLogicHasNoPerCharacterMonoBehaviourUpdate()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
            Assert.That(typeof(CharacterPresenter).GetMethod("Update", flags), Is.Null);
            Assert.That(typeof(NavMeshMovementDriver).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
            Assert.That(typeof(Tier1AiRuntimeDriver).GetMethod("Update", flags), Is.Not.Null);
        }

        private static void StartAndPickup(Fixture fixture, int index)
        {
            fixture.AdvanceSeconds(2d);
            fixture.Movements[index].Arrive();
            fixture.AdvanceSeconds(0.01d);
            Assert.That(fixture.Agents[index].Phase, Is.EqualTo(AiActionPhase.Pickup));
            fixture.AdvanceSeconds(0.01d);
            Assert.That(fixture.Agents[index].Phase, Is.EqualTo(AiActionPhase.MoveToDestination));
        }

        private static void CompleteDropoff(Fixture fixture, int index)
        {
            fixture.Movements[index].Arrive();
            fixture.AdvanceSeconds(0.01d);
            Assert.That(fixture.Agents[index].Phase, Is.EqualTo(AiActionPhase.Dropoff));
            fixture.AdvanceSeconds(0.01d);
        }

        private static Fixture CreateFixture(int agentCount, long sourceUnits, long storageCapacity, int thinkBatchSize,
            double initialEnergy = 1d)
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("wood_log"), "Wood Log", ResourceCategory.Construction)
            });
            var inventories = new ResourceInventoryRegistry();
            var piles = new WorldPileService(resources, inventories);
            var pile = piles.Create(new ResourceId("wood_log"), new ResourceQuantity(sourceUnits), new GridCoordinate(0, 0));
            var buildings = new BuildingRegistry();
            var storageBuilding = Building.CreateNew(new BuildingDefinitionId("storage_basket"), new GridCoordinate(10, 0), BuildingOrientation.North);
            storageBuilding.MarkCompleted();
            buildings.Add(storageBuilding);
            var storage = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, storageBuilding.Id),
                resources, storageCapacity);
            inventories.Add(storage);
            var characters = new CharacterRegistry();
            var claims = new HaulClaimRegistry();
            var query = new HaulWorldQuery(inventories, piles, buildings, claims);
            var world = new Tier1AiWorld(query, claims, new ResourceTransferService(inventories));
            var registry = new Tier1AiAgentRegistry();
            var settings = new Tier1AiSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30),
                thinkBatchSize, Math.Max(agentCount, 1), Math.Max(agentCount, 1), 100,
                2.5f, 4, 0.15d, 0d, 0d, 360d);
            var scheduler = new Tier1AiScheduler(characters, registry, world, settings);
            var fixture = new Fixture(resources, inventories, pile, storage, buildings, storageBuilding, characters, claims, scheduler);
            for (var i = 0; i < agentCount; i++)
            {
                var character = CreateCharacter("Agent" + i);
                characters.Add(character);
                inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, character.Id), resources, 4));
                var movement = new FakeMovement(character.Id, new WorldPosition(i * 0.01f, 0f, 0f));
                var agent = new Tier1AiAgentState(character.Id, movement.CurrentPosition, new Tier1Needs(0d, initialEnergy));
                scheduler.Register(character, agent, movement);
                fixture.Characters.Add(character);
                fixture.Agents.Add(agent);
                fixture.Movements.Add(movement);
            }
            return fixture;
        }

        private static Character CreateCharacter(string name) => Character.CreateNew(new CharacterCreationData(
            new CharacterName(name), CharacterSex.Male, 0, null, null, null, null, null,
            new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
            null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private sealed class Fixture
        {
            public Fixture(ResourceCatalog resources, ResourceInventoryRegistry inventories, WorldResourcePile pile,
                ResourceInventory storage, BuildingRegistry buildings, Building storageBuilding,
                CharacterRegistry characterRegistry, HaulClaimRegistry claims, Tier1AiScheduler scheduler)
            {
                Resources = resources; Inventories = inventories; Pile = pile; StorageInventory = storage;
                Buildings = buildings; StorageBuilding = storageBuilding;
                CharacterRegistry = characterRegistry; Claims = claims; Scheduler = scheduler;
            }
            public ResourceCatalog Resources { get; }
            public ResourceInventoryRegistry Inventories { get; }
            public WorldResourcePile Pile { get; }
            public ResourceInventory SourceInventory => Inventories.Get(Pile.InventoryOwner);
            public ResourceInventory StorageInventory { get; }
            public BuildingRegistry Buildings { get; }
            public Building StorageBuilding { get; }
            public CharacterRegistry CharacterRegistry { get; }
            public HaulClaimRegistry Claims { get; }
            public Tier1AiScheduler Scheduler { get; }
            public ResourceId Wood => new ResourceId("wood_log");
            public List<Character> Characters { get; } = new List<Character>();
            public List<Tier1AiAgentState> Agents { get; } = new List<Tier1AiAgentState>();
            public List<FakeMovement> Movements { get; } = new List<FakeMovement>();
            public void AdvanceSeconds(double seconds) => Scheduler.Advance(new GameTimeAdvance(TimeSpan.FromSeconds(seconds), TimeSpan.Zero));
            public ResourceInventory CharacterInventory(int index) => Inventories.Get(
                new InventoryOwner(InventoryOwnerKind.Character, Characters[index].Id));
            public long TotalWood() => Inventories.All.Sum(x => x.GetAmount(Wood).Units);
        }

        private sealed class FakeMovement : ITier1MovementDriver
        {
            private WorldPosition target;
            public FakeMovement(StableEntityId characterId, WorldPosition start)
            { CharacterId = characterId; CurrentPosition = start; State = MovementState.Idle; }
            public StableEntityId CharacterId { get; }
            public WorldPosition CurrentPosition { get; private set; }
            public MovementState State { get; private set; }
            public bool IsAvailable { get; set; } = true;
            public void MoveTo(WorldPosition destination, float stoppingDistance) { target = destination; State = MovementState.Moving; }
            public void Stop() => State = MovementState.Idle;
            public void Arrive()
            {
                if (State != MovementState.Moving) return;
                CurrentPosition = target;
                State = MovementState.Arrived;
            }
            public void Fail() => State = MovementState.Failed;
        }
    }
}
