using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Simulation.AI;
using Game.Simulation.Resources;
using Game.Simulation.Time;
using Game.Simulation.Work;
using NUnit.Framework;

namespace Game.Tests
{
    [Category("P103")]
    public sealed class P103ContextCommandTests
    {
        [Test]
        public void EveryRequiredContextActionHasAValidTargetAndIsVisibleToRules()
        {
            var entity = StableEntityId.NewId();
            var position = new WorldPosition(3, 0, 4);
            var targets = new[]
            {
                new ContextCommandTarget(ContextTargetKind.Character, position, entity),
                new ContextCommandTarget(ContextTargetKind.ResourceSource, position, entity,
                    new ResourceId("berries"), ContextResourceClass.Gatherable),
                new ContextCommandTarget(ContextTargetKind.ResourceSource, position, entity,
                    new ResourceId("stone"), ContextResourceClass.Mineable),
                new ContextCommandTarget(ContextTargetKind.ResourceSource, position, entity,
                    new ResourceId("wood_log"), ContextResourceClass.Choppable),
                new ContextCommandTarget(ContextTargetKind.ResourcePile, position, entity, new ResourceId("wood_log")),
                new ContextCommandTarget(ContextTargetKind.Building, position, entity),
                new ContextCommandTarget(ContextTargetKind.Ground, position),
                new ContextCommandTarget(ContextTargetKind.Ground, position)
            };
            var actions = (ContextActionKind[])Enum.GetValues(typeof(ContextActionKind));
            Assert.That(actions, Has.Length.EqualTo(8));
            for (var i = 0; i < actions.Length; i++)
                Assert.That(ContextActionRules.Evaluate(actions[i], targets[i], Ready()).Enabled,
                    Is.True, actions[i].ToString());
        }

        [TestCase(ContextUnavailableReason.WrongTargetType)]
        [TestCase(ContextUnavailableReason.WrongRole)]
        [TestCase(ContextUnavailableReason.TooYoung)]
        [TestCase(ContextUnavailableReason.InvalidStatus)]
        [TestCase(ContextUnavailableReason.MissingTool)]
        [TestCase(ContextUnavailableReason.MissingTechnology)]
        [TestCase(ContextUnavailableReason.TooFar)]
        [TestCase(ContextUnavailableReason.NoPath)]
        [TestCase(ContextUnavailableReason.NoResource)]
        [TestCase(ContextUnavailableReason.NoCapacity)]
        [TestCase(ContextUnavailableReason.Cooldown)]
        public void DisabledActionReturnsSpecificReason(ContextUnavailableReason expected)
        {
            var target = expected == ContextUnavailableReason.WrongTargetType
                ? new ContextCommandTarget(ContextTargetKind.Ground, default)
                : new ContextCommandTarget(ContextTargetKind.ResourcePile, default, StableEntityId.NewId(),
                    new ResourceId("wood_log"));
            Assert.That(ContextActionRules.Evaluate(ContextActionKind.Haul, target, FactsFor(expected)).Reason,
                Is.EqualTo(expected));
        }

        [Test]
        public void BusyCriticalAndProtectedActionsRemainVisibleButDelayed()
        {
            var target = new ContextCommandTarget(ContextTargetKind.Ground, default);
            var busy = ContextActionRules.Evaluate(ContextActionKind.Move, target,
                new ContextActionFacts(alive: true, busy: true));
            var protectedPhase = ContextActionRules.Evaluate(ContextActionKind.Move, target,
                new ContextActionFacts(alive: true, busy: true, interruptible: false));
            var critical = ContextActionRules.Evaluate(ContextActionKind.Move, target,
                new ContextActionFacts(alive: true, criticalNeed: true));
            Assert.That(busy.Enabled && busy.Delayed && busy.Reason == ContextUnavailableReason.Busy, Is.True);
            Assert.That(protectedPhase.Enabled && protectedPhase.Delayed &&
                protectedPhase.Reason == ContextUnavailableReason.NonInterruptiblePhase, Is.True);
            Assert.That(critical.Enabled && critical.Delayed &&
                critical.Reason == ContextUnavailableReason.CriticalNeed, Is.True);
        }

        [Test]
        public void NormalCommandGoesToFrontShiftCommandGoesToTailAndQueueRoundTripsV21()
        {
            var characters = new CharacterRegistry();
            var character = CreateCharacter("QueueWorker");
            characters.Add(character);
            var work = new WorkManager(characters);
            var first = work.CreateMoveJob(character.Id, new WorldPosition(1, 0, 0), WorkPriority.High,
                queuePlacement: WorkQueuePlacement.Tail);
            var shifted = work.CreateMoveJob(character.Id, new WorldPosition(2, 0, 0), WorkPriority.High,
                queuePlacement: WorkQueuePlacement.Tail);
            var front = work.CreateMoveJob(character.Id, new WorldPosition(3, 0, 0), WorkPriority.Immediate,
                queuePlacement: WorkQueuePlacement.Front);
            Assert.That(work.GetQueue(character.Id).Select(x => x.Id),
                Is.EqualTo(new[] { front.Id, first.Id, shifted.Id }));

            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(), 400, work: work.CaptureState());
            var restoredState = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot)).Work;
            var restored = new WorkManager(characters);
            restored.RestoreState(restoredState);
            Assert.That(restored.GetQueue(character.Id).Select(x => x.Target.Position.X),
                Is.EqualTo(new[] { 3f, 1f, 2f }));
            Assert.That(restoredState.NextQueueOrder, Is.GreaterThan(1));
            Assert.That(restoredState.NextFrontOrder, Is.LessThan(-1));
        }

        [Test]
        public void V20SaveLoadsWithDeterministicLegacyQueueOrder()
        {
            var characterId = StableEntityId.NewId();
            var state = new WorkSaveState(3, null, new[]
            {
                new WorkOrderState(2, WorkJobType.Move, WorkPriority.High,
                    WorkTarget.At(new WorldPosition(2, 0, 0)), null, characterId, null, null, default,
                    false, WorkJobStatus.Assigned, WorkClaimState.None),
                new WorkOrderState(1, WorkJobType.Move, WorkPriority.High,
                    WorkTarget.At(new WorldPosition(1, 0, 0)), null, characterId, null, null, default,
                    false, WorkJobStatus.Assigned, WorkClaimState.None)
            });
            var snapshot = new SaveSnapshot(SaveSnapshot.ConnectedWorldVersion, StableEntityId.NewId(),
                new GameTimeState(), 400, work: state);
            var loaded = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            Assert.That(loaded.Work.Jobs.OrderBy(x => x.Id).Select(x => x.QueueOrder), Is.EqualTo(new long[] { 1, 2 }));
        }

        [Test]
        public void NeedsOnlyModeStartsIdleAndCriticalNeedsBlockManualPreemption()
        {
            var fixture = CreateAiFixture(new Tier1Needs(0d, 1d));
            fixture.Scheduler.SetAutonomousWorkMode(Tier1AiScheduler.AutonomousWorkMode.NeedsOnly);
            fixture.Scheduler.Advance(new GameTimeAdvance(TimeSpan.FromSeconds(2), TimeSpan.Zero));
            Assert.That(fixture.Agent.CurrentAction, Is.EqualTo(UtilityActionKind.Idle));
            Assert.That(fixture.Scheduler.CanInterruptForDirectWork(fixture.Character.Id), Is.True);

            var hungry = CreateAiFixture(new Tier1Needs(0.95d, 1d));
            hungry.Scheduler.SetAutonomousWorkMode(Tier1AiScheduler.AutonomousWorkMode.NeedsOnly);
            hungry.Scheduler.Advance(new GameTimeAdvance(TimeSpan.FromSeconds(2), TimeSpan.Zero));
            Assert.That(hungry.Agent.CurrentAction, Is.EqualTo(UtilityActionKind.Eat));
            Assert.That(hungry.Scheduler.CanInterruptForDirectWork(hungry.Character.Id), Is.False);
        }

        [Test]
        public void BuildOrderCompletesCanonicalBuildingThroughScheduler()
        {
            var fixture = CreateAiFixture(new Tier1Needs(0d, 1d), includeBuilding: true);
            var work = fixture.Work;
            var order = work.CreateDirectJob(WorkJobType.Build, fixture.Character.Id,
                WorkTarget.ForEntity(WorkTargetKind.Building, fixture.Building.Id, new WorldPosition(1, 0, 0)),
                WorkPriority.Immediate, exclusiveTarget: false, queuePlacement: WorkQueuePlacement.Front);
            fixture.Scheduler.Advance(new GameTimeAdvance(TimeSpan.FromSeconds(2), TimeSpan.Zero));
            fixture.Movement.Arrive();
            fixture.Scheduler.Advance(new GameTimeAdvance(TimeSpan.FromMinutes(11), TimeSpan.Zero));
            Assert.That(order.Status, Is.EqualTo(WorkJobStatus.Completed));
            Assert.That(fixture.Building.IsOperational, Is.True);
        }

        private static ContextActionFacts Ready() => new ContextActionFacts(alive: true);

        private static ContextActionFacts FactsFor(ContextUnavailableReason reason) => new ContextActionFacts(
            alive: reason != ContextUnavailableReason.InvalidStatus,
            adult: reason != ContextUnavailableReason.TooYoung,
            correctRole: reason != ContextUnavailableReason.WrongRole,
            validStatus: reason != ContextUnavailableReason.InvalidStatus,
            hasTool: reason != ContextUnavailableReason.MissingTool,
            technologyUnlocked: reason != ContextUnavailableReason.MissingTechnology,
            withinDistance: reason != ContextUnavailableReason.TooFar,
            pathAvailable: reason != ContextUnavailableReason.NoPath,
            resourceAvailable: reason != ContextUnavailableReason.NoResource,
            capacityAvailable: reason != ContextUnavailableReason.NoCapacity,
            cooldownReady: reason != ContextUnavailableReason.Cooldown);

        private static AiFixture CreateAiFixture(Tier1Needs needs, bool includeBuilding = false)
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction)
            });
            var inventories = new ResourceInventoryRegistry();
            var piles = new WorldPileService(resources, inventories);
            var buildings = new BuildingRegistry();
            Building building = null;
            if (includeBuilding)
            {
                building = Building.CreateNew(new BuildingDefinitionId("hut"), new GridCoordinate(1, 0),
                    BuildingOrientation.North);
                buildings.Add(building);
            }
            var characters = new CharacterRegistry();
            var character = CreateCharacter("Worker");
            characters.Add(character);
            var claims = new HaulClaimRegistry();
            var world = new Tier1AiWorld(new HaulWorldQuery(inventories, piles, buildings, claims), claims,
                new ResourceTransferService(inventories), buildings: buildings);
            var work = new WorkManager(characters);
            var scheduler = new Tier1AiScheduler(characters, new Tier1AiAgentRegistry(), world,
                new Tier1AiSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(30),
                    1, 1, 1, 1, 2f, 4, 0.1d, 0d, 0d, 60d), work);
            var movement = new Movement(character.Id);
            var agent = new Tier1AiAgentState(character.Id, default, needs);
            scheduler.Register(character, agent, movement);
            return new AiFixture(character, agent, movement, scheduler, work, building);
        }

        private static Character CreateCharacter(string name) => Character.CreateNew(new CharacterCreationData(
            new CharacterName(name), CharacterSex.Male, 0, null, null, null, null, new ProfessionId("worker"),
            new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
            null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private sealed class AiFixture
        {
            public AiFixture(Character character, Tier1AiAgentState agent, Movement movement,
                Tier1AiScheduler scheduler, WorkManager work, Building building)
            { Character = character; Agent = agent; Movement = movement; Scheduler = scheduler; Work = work; Building = building; }
            public Character Character { get; }
            public Tier1AiAgentState Agent { get; }
            public Movement Movement { get; }
            public Tier1AiScheduler Scheduler { get; }
            public WorkManager Work { get; }
            public Building Building { get; }
        }

        private sealed class Movement : ITier1MovementDriver
        {
            private WorldPosition destination;
            public Movement(StableEntityId characterId) { CharacterId = characterId; }
            public StableEntityId CharacterId { get; }
            public WorldPosition CurrentPosition { get; private set; }
            public MovementState State { get; private set; }
            public bool IsAvailable => true;
            public void MoveTo(WorldPosition target, float stoppingDistance) { destination = target; State = MovementState.Moving; }
            public void Stop() => State = MovementState.Idle;
            public void Arrive() { CurrentPosition = destination; State = MovementState.Arrived; }
        }
    }
}
