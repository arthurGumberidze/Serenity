using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Presentation.AI;
using Game.Presentation.Characters;
using Game.Presentation.Work;
using Game.Simulation.AI;
using Game.Simulation.Resources;
using Game.Simulation.Time;
using Game.Simulation.Tiers;
using Game.Simulation.Work;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    [Category("U11")]
    public sealed class WorkGroupAndJobTests
    {
        [Test]
        public void GroupCreateRejectsDuplicateAndRemovesWithoutTouchingCharacter()
        {
            var registry = new CharacterRegistry();
            var first = CreateCharacter("Aren");
            var second = CreateCharacter("Mira");
            registry.Add(first);
            registry.Add(second);
            var manager = new WorkManager(registry);
            var group = manager.CreateGroup("Haulers", new[] { second.Id, first.Id });

            Assert.That(group.Members.Select(x => x.ToString()), Is.Ordered);
            Assert.Throws<InvalidOperationException>(() => group.AddMember(first.Id));
            Assert.That(manager.RemoveMember(group.Id, first.Id), Is.True);
            Assert.That(group.Contains(first.Id), Is.False);
            Assert.That(registry.Get(first.Id), Is.SameAs(first));
        }

        [Test]
        public void MembershipIsUniqueAcrossCanonicalGroupsAndRoundTripsDetachedState()
        {
            var registry = new CharacterRegistry();
            var character = CreateCharacter("Worker");
            registry.Add(character);
            var manager = new WorkManager(registry);
            var first = manager.CreateGroup("First", new[] { character.Id });
            var second = manager.CreateGroup("Second", Array.Empty<StableEntityId>());
            Assert.Throws<InvalidOperationException>(() => manager.AddMember(second.Id, character.Id));

            var restored = WorkGroup.Restore(first.CaptureState());
            Assert.That(restored.Id, Is.EqualTo(first.Id));
            Assert.That(restored.DisplayName, Is.EqualTo("First"));
            Assert.That(restored.Members.Single(), Is.EqualTo(character.Id));
        }

        [Test]
        public void JobLifecycleIsQueryableAcrossEveryTerminalState()
        {
            var registry = new CharacterRegistry();
            var character = CreateCharacter("Worker");
            registry.Add(character);
            var manager = new WorkManager(registry);
            var queued = manager.CreateMoveJob(null, new WorldPosition(1f, 0f, 1f), WorkPriority.Low);
            Assert.That(manager.Capture(WorkJobStatus.Queued), Does.Contain(queued));
            Assert.That(manager.Assign(queued.Id, character.Id), Is.True);
            Assert.That(manager.Start(queued.Id), Is.True);
            Assert.That(manager.Complete(queued.Id), Is.True);
            Assert.That(manager.Capture(WorkJobStatus.Completed), Does.Contain(queued));

            var cancelled = manager.CreateMoveJob(character.Id, new WorldPosition(2f, 0f, 2f), WorkPriority.Normal);
            Assert.That(manager.Cancel(cancelled.Id), Is.True);
            Assert.That(manager.Capture(WorkJobStatus.Cancelled), Does.Contain(cancelled));

            var failed = manager.CreateMoveJob(character.Id, new WorldPosition(3f, 0f, 3f), WorkPriority.Normal);
            Assert.That(manager.Fail(failed.Id), Is.True);
            Assert.That(manager.Capture(WorkJobStatus.Failed), Does.Contain(failed));
        }

        [Test]
        public void QueuedJobsAssignByPriorityThenStableJobId()
        {
            var registry = new CharacterRegistry();
            var first = CreateCharacter("First");
            var second = CreateCharacter("Second");
            registry.Add(first);
            registry.Add(second);
            var manager = new WorkManager(registry);
            var low = manager.CreateMoveJob(null, new WorldPosition(1f, 0f, 0f), WorkPriority.Low);
            var urgentFirst = manager.CreateMoveJob(null, new WorldPosition(2f, 0f, 0f), WorkPriority.Urgent);
            var urgentSecond = manager.CreateMoveJob(null, new WorldPosition(3f, 0f, 0f), WorkPriority.Urgent);

            Assert.That(manager.TryAssignNext(first.Id, out var selected), Is.True);
            Assert.That(selected, Is.SameAs(urgentFirst));
            Assert.That(manager.TryAssignNext(second.Id, out selected), Is.True);
            Assert.That(selected, Is.SameAs(urgentSecond));
            Assert.That(low.Status, Is.EqualTo(WorkJobStatus.Queued));
        }

        [Test]
        public void ExclusiveTargetAndCharacterOwnershipRejectConflictingJobs()
        {
            var registry = new CharacterRegistry();
            var character = CreateCharacter("Worker");
            registry.Add(character);
            var manager = new WorkManager(registry);
            var target = new WorldPosition(5f, 0f, 5f);
            var first = manager.CreateMoveJob(character.Id, target, WorkPriority.Normal, exclusiveTarget: true);
            Assert.Throws<InvalidOperationException>(() => manager.CreateMoveJob(character.Id,
                new WorldPosition(6f, 0f, 6f), WorkPriority.High));
            Assert.Throws<InvalidOperationException>(() => manager.CreateMoveJob(null, target,
                WorkPriority.High, exclusiveTarget: true));
            manager.Cancel(first.Id);
            Assert.DoesNotThrow(() => manager.CreateMoveJob(character.Id, target,
                WorkPriority.High, exclusiveTarget: true));
        }

        [Test]
        public void GroupDistributionIsDeterministicAndNeverDuplicatesAssignees()
        {
            var registry = new CharacterRegistry();
            var members = Enumerable.Range(0, 12).Select(x => CreateCharacter("Member" + x)).ToArray();
            foreach (var member in members) registry.Add(member);
            var manager = new WorkManager(registry);
            var group = manager.CreateGroup("Movers", members.Select(x => x.Id));
            var jobs = manager.CreateGroupMoveJobs(group.Id, new WorldPosition(10f, 0f, 10f), WorkPriority.High);

            Assert.That(jobs.Count, Is.EqualTo(members.Length));
            Assert.That(jobs.Select(x => x.AssigneeId.Value).Distinct().Count(), Is.EqualTo(members.Length));
            Assert.That(jobs.Select(x => x.GroupId.Value).Distinct().Single(), Is.EqualTo(group.Id));
        }

        [Test]
        public void ManualMoveCommandCreatesDeterministicFormationAndRejectsUnavailableTier()
        {
            var registry = new CharacterRegistry();
            var first = CreateCharacter("Aren");
            var second = CreateCharacter("Mira");
            var remote = CreateCharacter("Remote");
            registry.Add(first);
            registry.Add(second);
            registry.Add(remote);
            var work = new WorkManager(registry);
            var tiers = new FakeTierLookup();
            tiers.Set(first.Id, CharacterSimulationTier.Tier1);
            tiers.Set(second.Id, CharacterSimulationTier.Tier1);
            tiers.Set(remote.Id, CharacterSimulationTier.Tier3);
            var commands = new ManualMoveCommandService(work, tiers);

            var result = commands.Issue(new[] { remote.Id, second.Id, first.Id },
                new WorldPosition(10f, 0f, 12f), WorkPriority.High);

            Assert.That(result.CreatedCount, Is.EqualTo(2));
            Assert.That(result.UnavailableCount, Is.EqualTo(1));
            Assert.That(result.Entries.Single(x => x.CharacterId == remote.Id).Status,
                Is.EqualTo(ManualMoveCommandStatus.TierUnavailable));
            Assert.That(work.Jobs.Any(x => x.AssigneeId == remote.Id), Is.False);
            Assert.That(result.Jobs.Select(x => x.Target.Position).Distinct().Count(), Is.EqualTo(2));
            Assert.That(result.Jobs.All(x => Math.Abs(x.Target.Position.Z - 12f) < 0.001f), Is.True);
            Assert.That(result.Jobs.Select(x => x.Target.Position.X).OrderBy(x => x),
                Is.EqualTo(new[] { 9.25f, 10.75f }));
        }

        [Test]
        public void OneHundredTwentyEightNpcGroupOperationsStayCentralized()
        {
            var registry = new CharacterRegistry();
            var members = Enumerable.Range(0, 128).Select(x => CreateCharacter("Npc" + x)).ToArray();
            foreach (var member in members) registry.Add(member);
            var manager = new WorkManager(registry);
            var groups = new List<WorkGroup>();
            for (var i = 0; i < 8; i++)
                groups.Add(manager.CreateGroup("Group " + i, members.Skip(i * 16).Take(16).Select(x => x.Id)));
            foreach (var group in groups)
                manager.CreateGroupMoveJobs(group.Id, new WorldPosition(group.Id.GetHashCode() % 10, 0f, 4f),
                    WorkPriority.Normal);

            Assert.That(manager.Groups.Count, Is.EqualTo(8));
            Assert.That(manager.JobCount, Is.EqualTo(128));
            Assert.That(manager.Jobs.Select(x => x.AssigneeId.Value).Distinct().Count(), Is.EqualTo(128));
            Assert.That(typeof(WorkManager).IsSubclassOf(typeof(MonoBehaviour)), Is.False);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
            Assert.That(typeof(WorkDebugOverlay).GetMethod("Update", flags), Is.Null);
            Assert.That(typeof(CharacterPresenter).GetMethod("Update", flags), Is.Null);
            Assert.That(typeof(Tier1AiRuntimeDriver).GetMethod("Update", flags), Is.Not.Null);
        }

        [Test]
        public void ManualMovePreemptsInterruptibleAutonomousHaulAndReleasesItsClaim()
        {
            var fixture = CreateFixture(1, 20, 40, 1d);
            fixture.AdvanceSeconds(2d);
            Assert.That(fixture.Agents[0].CurrentAction, Is.EqualTo(UtilityActionKind.Haul));
            Assert.That(fixture.Claims.Count, Is.EqualTo(1));

            var job = fixture.Work.CreateMoveJob(fixture.Characters[0].Id,
                new WorldPosition(4f, 0f, 4f), WorkPriority.High);
            fixture.AdvanceSeconds(2d);

            Assert.That(fixture.Agents[0].CurrentAction, Is.EqualTo(UtilityActionKind.ManualMove));
            Assert.That(job.Status, Is.EqualTo(WorkJobStatus.Active));
            Assert.That(fixture.Claims.Count, Is.Zero);
        }

        [Test]
        public void CriticalEnergyPreemptsManualOrderUntilRestCompletes()
        {
            var fixture = CreateFixture(1, 20, 40, 0.05d);
            var job = fixture.Work.CreateMoveJob(fixture.Characters[0].Id,
                new WorldPosition(4f, 0f, 4f), WorkPriority.Urgent);
            fixture.AdvanceSeconds(2d);

            Assert.That(fixture.Agents[0].CurrentAction, Is.EqualTo(UtilityActionKind.Rest));
            Assert.That(job.Status, Is.EqualTo(WorkJobStatus.Assigned));
            fixture.AdvanceSeconds(15d);
            Assert.That(fixture.Agents[0].Needs.Energy, Is.GreaterThan(Tier1AiScheduler.CriticalRestEnergy));
            fixture.AdvanceSeconds(2d);
            Assert.That(fixture.Agents[0].CurrentAction, Is.EqualTo(UtilityActionKind.ManualMove));
        }

        [Test]
        public void ManualHaulCancelAndFailureReleaseClaimsWithoutDuplicatingResources()
        {
            var fixture = CreateFixture(1, 20, 40, 1d);
            var job = fixture.Work.CreateHaulJob(fixture.Characters[0].Id, fixture.Pile.InventoryOwner,
                fixture.StorageInventory.Owner, fixture.Wood, new ResourceQuantity(2), WorkPriority.High);
            var initial = fixture.TotalWood();
            fixture.AdvanceSeconds(2d);
            Assert.That(fixture.Claims.Count, Is.EqualTo(1));
            Assert.That(job.ClaimState, Is.EqualTo(WorkClaimState.Reserved));
            var cancelledClaim = fixture.Claims.CaptureActive().Single();
            fixture.Work.Cancel(job.Id);
            fixture.AdvanceSeconds(2d);
            Assert.That(fixture.Claims.IsActive(cancelledClaim), Is.False);
            Assert.That(job.ClaimState, Is.EqualTo(WorkClaimState.Released));
            Assert.That(fixture.TotalWood(), Is.EqualTo(initial));
            Assert.That(fixture.SourceInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(20));

            var failed = fixture.Work.CreateHaulJob(fixture.Characters[0].Id, fixture.Pile.InventoryOwner,
                fixture.StorageInventory.Owner, fixture.Wood, new ResourceQuantity(2), WorkPriority.High);
            fixture.AdvanceSeconds(2d);
            var failedClaim = fixture.Claims.CaptureActive().Single(x => x.Quantity.Units == 2);
            fixture.Movements[0].Fail();
            fixture.AdvanceSeconds(0.1d);
            Assert.That(failed.Status, Is.EqualTo(WorkJobStatus.Failed));
            Assert.That(fixture.Claims.IsActive(failedClaim), Is.False);
            Assert.That(fixture.TotalWood(), Is.EqualTo(initial));
        }

        [Test]
        public void ManualHaulUsesCanonicalTransferThenReturnsToAutonomousUtility()
        {
            var fixture = CreateFixture(1, 20, 40, 1d);
            var job = fixture.Work.CreateHaulJob(fixture.Characters[0].Id, fixture.Pile.InventoryOwner,
                fixture.StorageInventory.Owner, fixture.Wood, new ResourceQuantity(2), WorkPriority.High);
            var initial = fixture.TotalWood();
            fixture.AdvanceSeconds(2d);
            fixture.Movements[0].Arrive();
            fixture.AdvanceSeconds(0.1d);
            fixture.AdvanceSeconds(0.1d);
            Assert.That(job.ClaimState, Is.EqualTo(WorkClaimState.Carrying));
            fixture.Movements[0].Arrive();
            fixture.AdvanceSeconds(0.1d);
            fixture.AdvanceSeconds(0.1d);

            Assert.That(job.Status, Is.EqualTo(WorkJobStatus.Completed));
            Assert.That(fixture.StorageInventory.GetAmount(fixture.Wood).Units, Is.EqualTo(2));
            Assert.That(fixture.TotalWood(), Is.EqualTo(initial));
            fixture.AdvanceSeconds(2d);
            Assert.That(fixture.Agents[0].CurrentAction, Is.EqualTo(UtilityActionKind.Haul));
        }

        private static Fixture CreateFixture(int agentCount, long sourceUnits, long storageCapacity, double energy)
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("wood_log"), "Wood Log", ResourceCategory.Construction)
            });
            var inventories = new ResourceInventoryRegistry();
            var piles = new WorldPileService(resources, inventories);
            var pile = piles.Create(new ResourceId("wood_log"), new ResourceQuantity(sourceUnits), new GridCoordinate(0, 0));
            var buildings = new BuildingRegistry();
            var storageBuilding = Building.CreateNew(new BuildingDefinitionId("storage_basket"),
                new GridCoordinate(10, 0), BuildingOrientation.North);
            storageBuilding.MarkCompleted();
            buildings.Add(storageBuilding);
            var storage = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage,
                storageBuilding.Id), resources, storageCapacity);
            inventories.Add(storage);
            var characters = new CharacterRegistry();
            var work = new WorkManager(characters);
            var claims = new HaulClaimRegistry();
            var world = new Tier1AiWorld(new HaulWorldQuery(inventories, piles, buildings, claims), claims,
                new ResourceTransferService(inventories));
            var registry = new Tier1AiAgentRegistry();
            var settings = new Tier1AiSettings(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30), Math.Max(agentCount, 1), Math.Max(agentCount, 1),
                Math.Max(agentCount, 1), 100, 2.5f, 4, 0.15d, 0d, 0d, 360d);
            var scheduler = new Tier1AiScheduler(characters, registry, world, settings, work);
            var fixture = new Fixture(resources, inventories, pile, storage, characters, claims, work, scheduler);
            for (var i = 0; i < agentCount; i++)
            {
                var character = CreateCharacter("Agent" + i);
                characters.Add(character);
                inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character,
                    character.Id), resources, 4));
                var movement = new FakeMovement(character.Id, new WorldPosition(i, 0f, 0f));
                var agent = new Tier1AiAgentState(character.Id, movement.CurrentPosition, new Tier1Needs(0d, energy));
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
                ResourceInventory storage, CharacterRegistry characters, HaulClaimRegistry claims,
                WorkManager work, Tier1AiScheduler scheduler)
            {
                Resources = resources;
                Inventories = inventories;
                Pile = pile;
                StorageInventory = storage;
                CharacterRegistry = characters;
                Claims = claims;
                Work = work;
                Scheduler = scheduler;
            }
            public ResourceCatalog Resources { get; }
            public ResourceInventoryRegistry Inventories { get; }
            public WorldResourcePile Pile { get; }
            public ResourceInventory SourceInventory => Inventories.Get(Pile.InventoryOwner);
            public ResourceInventory StorageInventory { get; }
            public CharacterRegistry CharacterRegistry { get; }
            public HaulClaimRegistry Claims { get; }
            public WorkManager Work { get; }
            public Tier1AiScheduler Scheduler { get; }
            public ResourceId Wood => new ResourceId("wood_log");
            public List<Character> Characters { get; } = new List<Character>();
            public List<Tier1AiAgentState> Agents { get; } = new List<Tier1AiAgentState>();
            public List<FakeMovement> Movements { get; } = new List<FakeMovement>();
            public void AdvanceSeconds(double seconds) => Scheduler.Advance(
                new GameTimeAdvance(TimeSpan.FromSeconds(seconds), TimeSpan.Zero));
            public long TotalWood() => Inventories.All.Sum(x => x.GetAmount(Wood).Units);
        }

        private sealed class FakeMovement : ITier1MovementDriver
        {
            private WorldPosition target;
            public FakeMovement(StableEntityId characterId, WorldPosition start)
            {
                CharacterId = characterId;
                CurrentPosition = start;
                State = MovementState.Idle;
            }
            public StableEntityId CharacterId { get; }
            public WorldPosition CurrentPosition { get; private set; }
            public MovementState State { get; private set; }
            public bool IsAvailable { get; set; } = true;
            public void MoveTo(WorldPosition destination, float stoppingDistance)
            {
                target = destination;
                State = MovementState.Moving;
            }
            public void Stop() => State = MovementState.Idle;
            public void Arrive()
            {
                CurrentPosition = target;
                State = MovementState.Arrived;
            }
            public void Fail() => State = MovementState.Failed;
        }

        private sealed class FakeTierLookup : ICharacterTierLookup
        {
            private readonly Dictionary<StableEntityId, CharacterSimulationTier> tiers =
                new Dictionary<StableEntityId, CharacterSimulationTier>();

            public void Set(StableEntityId id, CharacterSimulationTier tier) => tiers[id] = tier;
            public bool TryGetTier(StableEntityId characterId, out CharacterSimulationTier tier) =>
                tiers.TryGetValue(characterId, out tier);
        }
    }
}
