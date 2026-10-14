using System;
using System.Linq;
using System.Reflection;
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
using Game.Simulation.Work;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    [Category("P83")]
    public sealed class P83WorkGroupTests
    {
        [Test]
        public void NineRoleGroupsAndNearestSuitableAssignmentAreDeterministic()
        {
            var fixture = new Fixture();
            var far = fixture.Add("Far", new WorldPosition(12, 0, 0), 90);
            var near = fixture.Add("Near", new WorldPosition(2, 0, 0), 20);
            var groups = fixture.Commands.EnsureRoleGroups();
            Assert.That(groups.Select(x => x.Role).Distinct().Count(), Is.EqualTo(9));
            var gatherers = groups.Single(x => x.Role == WorkGroupRole.Gatherers);
            fixture.Work.AddMember(gatherers.Id, far.Id);
            fixture.Work.AddMember(gatherers.Id, near.Id);

            var targetId = StableEntityId.NewId();
            var order = fixture.Commands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, targetId, new WorldPosition(0, 0, 0)),
                WorkPriority.High);

            Assert.That(order.AssigneeId, Is.EqualTo(near.Id), "Distance is the primary deterministic key.");
            Assert.That(fixture.Commands.GetCandidates(WorkJobType.Gather, default).Select(x => x.CharacterId).First(),
                Is.EqualTo(near.Id));
        }

        [Test]
        public void ImmediatePriorityPreemptsOnlySafeWorkAndReleasesOldAssignment()
        {
            var fixture = new Fixture();
            var worker = fixture.Add("Worker", default, 50);
            var gatherers = fixture.Commands.EnsureRoleGroups().Single(x => x.Role == WorkGroupRole.Gatherers);
            fixture.Work.AddMember(gatherers.Id, worker.Id);
            var old = fixture.Work.CreateMoveJob(worker.Id, new WorldPosition(5, 0, 0), WorkPriority.Normal,
                gatherers.Id);

            var immediate = fixture.Commands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, StableEntityId.NewId(), new WorldPosition(1, 0, 0)),
                WorkPriority.Immediate);

            Assert.That(immediate.AssigneeId, Is.EqualTo(worker.Id));
            Assert.That(old.Status, Is.EqualTo(WorkJobStatus.Queued));
            Assert.That(old.AssigneeId.HasValue, Is.False);
        }

        [Test]
        public void ImmediatePriorityDoesNotOverrideCriticalRestProtection()
        {
            var fixture = new Fixture();
            var exhausted = fixture.Add("Exhausted", default, 50, 0.05);
            var gatherers = fixture.Commands.EnsureRoleGroups().Single(x => x.Role == WorkGroupRole.Gatherers);
            fixture.Work.AddMember(gatherers.Id, exhausted.Id);

            var order = fixture.Commands.Issue(WorkJobType.Gather,
                WorkTarget.ForEntity(WorkTargetKind.ProductionSite, StableEntityId.NewId(), default),
                WorkPriority.Immediate);

            Assert.That(order.Status, Is.EqualTo(WorkJobStatus.Queued));
            Assert.That(order.AssigneeId.HasValue, Is.False);
        }

        [Test]
        public void ExclusiveTargetConflictCancelAndReprioritizeAreSafe()
        {
            var fixture = new Fixture();
            var worker = fixture.Add("Worker", default, 50);
            var gatherers = fixture.Commands.EnsureRoleGroups().Single(x => x.Role == WorkGroupRole.Gatherers);
            fixture.Work.AddMember(gatherers.Id, worker.Id);
            var target = WorkTarget.ForEntity(WorkTargetKind.ProductionSite, StableEntityId.NewId(), default);
            var first = fixture.Commands.Issue(WorkJobType.Gather, target, WorkPriority.Normal);
            Assert.Throws<InvalidOperationException>(() => fixture.Commands.Issue(WorkJobType.Gather, target, WorkPriority.High));
            fixture.Work.SetPriority(first.Id, WorkPriority.Immediate);
            Assert.That(first.Priority, Is.EqualTo(WorkPriority.Immediate));
            Assert.That(fixture.Work.Cancel(first.Id), Is.True);
            Assert.DoesNotThrow(() => fixture.Commands.Issue(WorkJobType.Gather, target, WorkPriority.High));
        }

        [Test]
        public void WorkStateRoundTripsThroughStrictV18Codec()
        {
            var fixture = new Fixture();
            var worker = fixture.Add("Worker", default, 50);
            var builders = fixture.Commands.EnsureRoleGroups().Single(x => x.Role == WorkGroupRole.Builders);
            fixture.Work.AddMember(builders.Id, worker.Id);
            var order = fixture.Commands.Issue(WorkJobType.Build,
                WorkTarget.ForEntity(WorkTargetKind.Building, StableEntityId.NewId(), new WorldPosition(3, 0, 4)),
                WorkPriority.High);
            fixture.Work.SetClaimState(order.Id, WorkClaimState.Reserved);
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(100, 200, 1, false), 365, work: fixture.Work.CaptureState());

            var restored = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));

            Assert.That(restored.FormatVersion, Is.EqualTo(SaveSnapshot.AppearanceEditorVersion));
            Assert.That(restored.Work.Groups.Count, Is.EqualTo(9));
            var saved = restored.Work.Jobs.Single();
            Assert.That(saved.Id, Is.EqualTo(order.Id.Value));
            Assert.That(saved.Type, Is.EqualTo(WorkJobType.Build));
            Assert.That(saved.AssigneeId, Is.EqualTo(worker.Id));
            Assert.That(saved.ClaimState, Is.EqualTo(WorkClaimState.Reserved));
        }

        [Test]
        public void PlannerAndWorkManagerHaveNoPerNpcUpdateLoop()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
            Assert.That(typeof(DirectWorkCommandService).GetMethod("Update", flags), Is.Null);
            Assert.That(typeof(WorkManager).GetMethod("Update", flags), Is.Null);
            Assert.That(typeof(DirectWorkCommandService).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
        }

        private sealed class Fixture
        {
            private readonly CharacterRegistry characters = new CharacterRegistry();
            private readonly Tier1AiAgentRegistry agents = new Tier1AiAgentRegistry();
            private readonly Tier1AiScheduler scheduler;
            public Fixture()
            {
                var resources = new ResourceCatalog(new[]
                {
                    new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction)
                });
                var inventories = new ResourceInventoryRegistry();
                var piles = new WorldPileService(resources, inventories);
                var buildings = new BuildingRegistry();
                var claims = new HaulClaimRegistry();
                var world = new Tier1AiWorld(new HaulWorldQuery(inventories, piles, buildings, claims), claims,
                    new ResourceTransferService(inventories));
                Work = new WorkManager(characters);
                scheduler = new Tier1AiScheduler(characters, agents, world,
                    new Tier1AiSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30),
                        32, 32, 32, 64, 2f, 4, 0.1, 0, 0, 60), Work);
                Commands = new DirectWorkCommandService(characters, agents, scheduler, Work);
            }
            public WorkManager Work { get; }
            public DirectWorkCommandService Commands { get; }

            public Character Add(string name, WorldPosition position, int strength, double energy = 1d)
            {
                var character = Character.CreateNew(new CharacterCreationData(new CharacterName(name), CharacterSex.Male,
                    0, null, null, null, null, new ProfessionId("worker"),
                    new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
                    null, new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(strength)),
                    BodyHealth.Healthy, new Wealth(0), new Influence(0), null));
                characters.Add(character);
                var movement = new Movement(character.Id, position);
                scheduler.Register(character, new Tier1AiAgentState(character.Id, position, new Tier1Needs(0d, energy)), movement);
                return character;
            }
        }

        private sealed class Movement : ITier1MovementDriver
        {
            public Movement(StableEntityId id, WorldPosition position) { CharacterId = id; CurrentPosition = position; }
            public StableEntityId CharacterId { get; }
            public WorldPosition CurrentPosition { get; private set; }
            public MovementState State { get; private set; }
            public bool IsAvailable => true;
            public void MoveTo(WorldPosition destination, float stoppingDistance) { CurrentPosition = destination; State = MovementState.Arrived; }
            public void Stop() { State = MovementState.Idle; }
        }
    }
}
