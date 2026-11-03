using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.AI;
using Game.Simulation.Buildings;
using Game.Simulation.Production;
using Game.Simulation.Research;
using Game.Simulation.Resources;
using Game.Simulation.Time;
using Game.Presentation.AI;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U16")]
    public sealed class ResearchFrameworkTests
    {
        [Test, Category("DEV01")]
        public void ThreeProjectQueuePreservesOrderDependenciesAndNoRetroactiveWorkAfterLoad()
        {
            var ids = Enumerable.Range(1,4).Select(n => new TechnologyId("queue_"+n)).ToArray();
            var catalog = new TechnologyCatalog(ids.Select((id,n) => new TechnologyDefinition(id,id.ToString(),
                n==0?null:new[]{ids[n-1]},TimeSpan.FromHours(1),null)),new TechnologyUnlockTargets(null,null,null));
            var service = new ResearchService(catalog); var worker = Id(1); service.AssignWorker(worker);
            Assert.That(service.Enqueue(ids[1]),Is.False,"A successor needs its predecessor in the plan");
            Assert.That(service.Enqueue(ids[0]),Is.True); Assert.That(service.Enqueue(ids[1]),Is.True);
            Assert.That(service.Enqueue(ids[2]),Is.True); Assert.That(service.Enqueue(ids[3]),Is.False);
            service.AdvanceWorkerTo(worker,TimeSpan.FromHours(1).Ticks,true);
            Assert.That(service.IsCompleted(ids[0]),Is.True);
            Assert.That(service.ActiveProject.TechnologyId,Is.EqualTo(ids[1]));
            Assert.That(service.ActiveProject.AccumulatedWorkTicks,Is.Zero);
            var restored = new ResearchService(catalog); restored.RestoreState(service.CaptureState());
            Assert.That(restored.QueuedTechnologies,Is.EqualTo(new[]{ids[2]}));
            Assert.That(restored.ComputeStateHash(),Is.EqualTo(service.ComputeStateHash()));
            restored.AdvanceWorkerTo(worker,TimeSpan.FromHours(2).Ticks,true);
            Assert.That(restored.ActiveProject.TechnologyId,Is.EqualTo(ids[2]));
            Assert.That(restored.ActiveProject.AccumulatedWorkTicks,Is.Zero);
            restored.AdvanceWorkerTo(worker,TimeSpan.FromHours(3).Ticks,true);
            Assert.That(restored.ActiveProject,Is.Null); Assert.That(restored.QueuedTechnologies,Is.Empty);
        }

        private sealed class SuppliedSettlement : IResearchSettlementContext
        {
            public long Fact(string key)=>1000;
            public bool CanPay(ResearchTechnologyProfile profile)=>true;
            public bool TryPay(ResearchTechnologyProfile profile)=>true;
        }
        private sealed class Fixture
        {
            public readonly ResourceCatalog Resources;
            public readonly ProductionCatalog ProductionCatalog;
            public readonly TechnologyCatalog Technologies;
            public readonly ResearchService Research;
            public readonly StableEntityId Worker = Id(100);

            public Fixture(IResearchRateProvider rates = null, bool startsWithStoneTools = false)
            {
                Resources = CreateResources();
                ProductionCatalog = ProductionCatalog.CreateStoneAge(Resources);
                Technologies = TechnologyCatalog.CreateStoneAge(ProductionCatalog, BuildingIds, startsWithStoneTools);
                Research = new ResearchService(Technologies, rates);
                Research.SettlementContext=new SuppliedSettlement();
                Research.AssignWorker(Worker);
            }
        }

        private sealed class MemoryStore : Game.Simulation.ISaveStore
        {
            private readonly Dictionary<StableEntityId, SaveSnapshot> values = new Dictionary<StableEntityId, SaveSnapshot>();
            public void Save(StableEntityId slotId, SaveSnapshot snapshot) => values[slotId] = snapshot;
            public SaveSnapshot Load(StableEntityId slotId) => values[slotId];
        }

        private sealed class InstantMovement : ITier1MovementDriver
        {
            public InstantMovement(StableEntityId id) { CharacterId = id; }
            public StableEntityId CharacterId { get; }
            public WorldPosition CurrentPosition { get; private set; }
            public MovementState State { get; private set; }
            public bool IsAvailable => true;
            public void MoveTo(WorldPosition target, float stoppingDistance) { CurrentPosition = target; State = MovementState.Arrived; }
            public void Stop() => State = MovementState.Idle;
        }

        private static readonly BuildingDefinitionId[] BuildingIds =
        {
            new BuildingDefinitionId("primitive_shelter"), new BuildingDefinitionId("storage_basket"),
            new BuildingDefinitionId("primitive_crop_plot"), new BuildingDefinitionId("research_house")
        };

        private sealed class WalkingMovement : ITier1MovementDriver
        {
            public StableEntityId CharacterId { get; set; }
            public WorldPosition CurrentPosition { get; set; }
            public WorldPosition Target { get; private set; }
            public MovementState State { get; set; }
            public bool IsAvailable => true;
            public void MoveTo(WorldPosition target, float stoppingDistance) { Target = target; State = MovementState.Moving; }
            public void Stop() => State = MovementState.Idle;
            public void Arrive() { CurrentPosition = Target; State = MovementState.Arrived; }
        }

        [Test] public void ResearchRequiresCompletedHouseAndPhysicalArrivalAndReleasesOnShiftEnd()
        {
            var fixture = new Fixture();
            fixture.Research.UnassignWorker(fixture.Worker);
            var buildings = new BuildingRegistry();
            var workplace = new IndoorWorkplaceService(buildings, new Game.Domain.Combat.CombatSideId("player"),
                _ => new WorldPosition(20, 0, 20));
            fixture.Research.Workplace = workplace;
            Assert.That(fixture.Research.IsBuildingUnlocked(IndoorWorkplaceService.ResearchHouse), Is.True);
            Assert.That(fixture.Research.TryAssignWorker(fixture.Worker), Is.False);
            Assert.That(fixture.Research.Start(new TechnologyId("controlled_fire")), Is.False);
            var house = Building.Restore(new BuildingState(Id(700), IndoorWorkplaceService.ResearchHouse,
                new GridCoordinate(20,20), (BuildingOrientation)0, ConstructionState.Completed));
            buildings.Add(house);
            Assert.That(fixture.Research.TryAssignWorker(fixture.Worker), Is.True);
            Assert.That(fixture.Research.Start(new TechnologyId("controlled_fire")), Is.True);
            var movement = new WalkingMovement { CharacterId = fixture.Worker };
            Assert.That(workplace.Travel(fixture.Worker, house.Id, movement), Is.False);
            Assert.That(workplace.IsInside(fixture.Worker), Is.False);
            Assert.That(fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.TicksPerHour, true), Is.Zero);
            // A movement driver claiming arrival at the wrong position cannot grant indoor work.
            movement.State = MovementState.Arrived;
            Assert.That(workplace.Travel(fixture.Worker, house.Id, movement), Is.False);
            Assert.That(movement.State, Is.EqualTo(MovementState.Moving), "A projected arrival outside the entrance must retry travel");
            movement.Arrive();
            Assert.That(workplace.Travel(fixture.Worker, house.Id, movement), Is.True);
            Assert.That(fixture.Research.AdvanceWorkerTo(fixture.Worker, 2 * TimeSpan.TicksPerHour, true), Is.GreaterThan(0));
            Assert.That(fixture.Research.ActiveProject.AccumulatedWorkTicks, Is.EqualTo(TimeSpan.TicksPerHour));
            fixture.Research.SynchronizeWorkers(Array.Empty<StableEntityId>(), 2 * TimeSpan.TicksPerHour);
            Assert.That(workplace.IsInside(fixture.Worker), Is.False);
            buildings.Remove(house);
            Assert.That(fixture.Research.TryAssignWorker(fixture.Worker), Is.False);
        }

        [Test] public void WorkplaceResolvesAnotherEntranceAfterConstructionBlocksTheOldRoute()
        {
            var buildings = new BuildingRegistry();
            var house = Building.Restore(new BuildingState(Id(702), IndoorWorkplaceService.ResearchHouse,
                new GridCoordinate(20, 20), (BuildingOrientation)0, ConstructionState.Completed));
            buildings.Add(house);
            var entrance = new WorldPosition(20, 0, 18);
            var workplace = new IndoorWorkplaceService(buildings, new Game.Domain.Combat.CombatSideId("player"), _ => entrance);
            var movement = new WalkingMovement { CharacterId = Id(703) };
            Assert.That(workplace.Travel(movement.CharacterId, house.Id, movement), Is.False);
            entrance = new WorldPosition(24, 0, 20);
            movement.State = MovementState.Failed;
            Assert.That(workplace.Travel(movement.CharacterId, house.Id, movement), Is.False);
            Assert.That(movement.Target, Is.EqualTo(entrance));
            Assert.That(workplace.IsInside(movement.CharacterId), Is.False);
            movement.Arrive();
            Assert.That(workplace.Travel(movement.CharacterId, house.Id, movement), Is.True);
        }

        [Test] public void ResearchHouseIsInitialCostlyBuildingAndItsMaterialsCanBeGatheredImmediately()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<Game.Presentation.Buildings.BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var definition = catalog.ResolveDefinition(IndoorWorkplaceService.ResearchHouse);
            Assert.That(definition.RequiredTechnologies, Is.Empty);
            Assert.That(definition.ConstructionCost.Single(x => x.ResourceId.ToString() == "wood_log").Quantity.Units, Is.EqualTo(80));
            Assert.That(definition.ConstructionCost.Single(x => x.ResourceId.ToString() == "stone").Quantity.Units, Is.EqualTo(50));
            Assert.That(catalog.ResolvePrefab(IndoorWorkplaceService.ResearchHouse), Is.Not.Null);
            var research = new Fixture().Research;
            Assert.That(research.IsProductionRecipeUnlocked(new ProductionRecipeId("gather_stone")), Is.True);
            Assert.That(research.IsProductionRecipeUnlocked(new ProductionRecipeId("chop_wood")), Is.True);
        }

        [Test] public void DefinitionsRejectDuplicateMissingSelfCycleAndInvalidUnlock()
        {
            var targets = new TechnologyUnlockTargets(Array.Empty<BuildingDefinitionId>(),
                Array.Empty<ProductionRecipeId>(), new[] { new CapabilityId("valid") });
            TechnologyDefinition D(string id, string[] prerequisites = null, string capability = "valid") =>
                new TechnologyDefinition(new TechnologyId(id), id,
                    (prerequisites ?? Array.Empty<string>()).Select(x => new TechnologyId(x)), TimeSpan.FromHours(1),
                    new[] { TechnologyUnlock.Capability(new CapabilityId(capability)) });
            Assert.Throws<ArgumentException>(() => new TechnologyCatalog(new[] { D("a"), D("a") }, targets));
            Assert.Throws<ArgumentException>(() => new TechnologyCatalog(new[] { D("a", new[] { "missing" }) }, targets));
            Assert.Throws<ArgumentException>(() => new TechnologyCatalog(new[] { D("a", new[] { "a" }) }, targets));
            Assert.Throws<ArgumentException>(() => new TechnologyCatalog(new[]
                { D("a", new[] { "b" }), D("b", new[] { "a" }) }, targets));
            Assert.Throws<ArgumentException>(() => new TechnologyCatalog(new[] { D("a", capability: "unknown") }, targets));
        }

        [Test] public void StartingKnowledgeIsLimitedAndPrerequisitesControlAvailability()
        {
            var fixture = new Fixture();
            CollectionAssert.AreEquivalent(new[] { "basic_survival", "gathering" },
                fixture.Research.CompletedTechnologies.Select(x => x.ToString()));
            CollectionAssert.Contains(fixture.Research.AvailableTechnologies.Select(x => x.Id.ToString()), "stone_tools");
            CollectionAssert.DoesNotContain(fixture.Research.AvailableTechnologies.Select(x => x.Id.ToString()), "woodworking");
            Assert.That(fixture.Research.IsProductionRecipeUnlocked(new ProductionRecipeId("gather_berries")), Is.True);
            Assert.That(fixture.Research.IsProductionRecipeUnlocked(new ProductionRecipeId("chop_wood")), Is.True);
        }

        [Test] public void StoneAgeCatalogHasFiveSpecializationBranchesAndMilitaryOrganizationUnlock()
        {
            var fixture = new Fixture();
            var ids = fixture.Technologies.All.Select(value => value.Id.ToString()).ToArray();
            foreach (var id in new[] { "seasonal_preparation", "standardization", "militia",
                         "formation_training", "military_organization", "military_logistics", "fortification",
                         "observation", "herbalism", "practical_anatomy", "record_keeping", "natural_philosophy",
                         "kinship", "settlement_council", "exchange_and_measures", "land_administration",
                         "customary_law" })
                CollectionAssert.Contains(ids, id);
            Assert.That(fixture.Technologies.All.Count,Is.EqualTo(51));
            Assert.That(fixture.Technologies.Get(new TechnologyId("stone_age_mastery")).Prerequisites,Is.Empty);
            Assert.That(fixture.Research.IsAvailable(new TechnologyId("stone_age_mastery")),Is.False);
            Assert.That(fixture.Technologies.Get(new TechnologyId("military_organization")).Unlocks
                .Any(value => value.Kind == TechnologyUnlockKind.Capability &&
                    value.ContentId == "military_organization"), Is.True);
            CollectionAssert.AreEquivalent(new[] { "gathering", "stone_tools", "primitive_weapons", "observation", "kinship" },
                fixture.Technologies.All.Where(value => value.Prerequisites.Count == 1 &&
                        value.Prerequisites[0] == new TechnologyId("basic_survival"))
                    .Select(value => value.Id.ToString()));
            Assert.That(fixture.Research.IsAvailable(new TechnologyId("formation_training")), Is.False);
            Complete(fixture.Research, fixture.Worker, "primitive_weapons");
            Complete(fixture.Research, fixture.Worker, "militia");
            Assert.That(fixture.Research.IsAvailable(new TechnologyId("formation_training")), Is.True);
            Assert.That(fixture.Research.IsAvailable(new TechnologyId("military_organization")), Is.False);
        }

        [Test] public void PartialProgressCompletionAndExactOnceCommitAreDeterministic()
        {
            var fixture = new Fixture();
            Assert.That(fixture.Research.Start(new TechnologyId("controlled_fire")), Is.True);
            fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(.75).Ticks, true);
            Assert.That(fixture.Research.ActiveProject.Progress, Is.EqualTo(0.5d).Within(0.000001d));
            fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(2).Ticks, true);
            Assert.That(fixture.Research.IsCompleted(new TechnologyId("controlled_fire")), Is.True);
            Assert.That(fixture.Research.CompletionCount, Is.EqualTo(1));
            fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(3).Ticks, true);
            Assert.That(fixture.Research.CompletionCount, Is.EqualTo(1));
            Assert.That(fixture.Research.LastCompletionTick, Is.EqualTo(TimeSpan.FromDays(1.5).Ticks));
        }

        [Test] public void ProductionAndBuildingDefinitionsRemainPresentButAreGatedUntilUnlock()
        {
            var fixture = new Fixture();
            Assert.That(fixture.ProductionCatalog.Get(new ProductionRecipeId("chop_wood")), Is.Not.Null);
            var inventories = new ResourceInventoryRegistry();
            var buildings = new BuildingRegistry();
            var workerOwner = new InventoryOwner(InventoryOwnerKind.Character, fixture.Worker);
            var workerInventory = new ResourceInventory(workerOwner, fixture.Resources, 100);
            inventories.Add(workerInventory);
            var production = new ProductionService(fixture.ProductionCatalog, fixture.Resources, inventories,
                buildings, new ResourceTransferService(inventories), researchUnlocks: fixture.Research);
            production.SetTarget(new ResourceId("wood_log"), 20);
            production.AddSite(ProductionSite.Restore(new ProductionSiteState(Id(200),
                new ProductionRecipeId("chop_wood"), ProductionSiteKind.NaturalSource, default,
                20, 20, CropPlotPhase.Unplanted, 0, 0)));
            Assert.That(production.TryReserve(fixture.Worker, default, out var basicHarvest), Is.True);
            production.Cancel(basicHarvest.Task.Id);

            var occupancy = new BuildingOccupancyGrid();
            var placement = new BuildingPlacementService(buildings, occupancy, new BuildingGridBounds(-10, -10, 10, 10));
            var storage = new StorageService(inventories, fixture.Resources);
            var construction = new ConstructionFundingService(placement, inventories, fixture.Resources, storage, fixture.Research);
            var shelter = new BuildingDefinition(new BuildingDefinitionId("primitive_shelter"), "Shelter",
                BuildingCategory.Shelter, new BuildingFootprint(1, 1));
            Assert.That(construction.Evaluate(shelter, new GridCoordinate(0, 0), BuildingOrientation.North,
                new[] { workerOwner }).Failure, Is.EqualTo(ConstructionFailureReason.LockedByResearch));

            Complete(fixture.Research, fixture.Worker, "stone_tools");
            Assert.That(production.TryReserve(fixture.Worker, default, out _), Is.True);
            Complete(fixture.Research, fixture.Worker, "shelter_construction");
            Assert.That(construction.Evaluate(shelter, new GridCoordinate(0, 0), BuildingOrientation.North,
                new[] { workerOwner }).Failure, Is.EqualTo(ConstructionFailureReason.None));
        }

        [Test] public void WorkerAssignmentCancellationAndRateHookAreSafe()
        {
            var fast = new Fixture(new ConstantResearchRateProvider(1500));
            Assert.That(fast.Research.Start(new TechnologyId("controlled_fire")), Is.True);
            fast.Research.AdvanceWorkerTo(fast.Worker, TimeSpan.FromDays(.75).Ticks, true);
            Assert.That(fast.Research.ActiveProject.Progress, Is.EqualTo(0.75d).Within(0.000001d));
            Assert.That(fast.Research.Cancel(), Is.True);
            Assert.That(fast.Research.UnassignWorker(fast.Worker), Is.True);
            fast.Research.AdvanceWorkerTo(fast.Worker, TimeSpan.FromDays(2).Ticks, true);
            Assert.That(fast.Research.ActiveProject, Is.Null);
        }

        [Test] public void SaveLoadPreservesProgressAssignmentsUnlocksAndDoesNotDoubleComplete()
        {
            var fixture = new Fixture();
            Complete(fixture.Research, fixture.Worker, "stone_tools");
            Assert.That(fixture.Research.Start(new TechnologyId("woodworking")), Is.True);
            fixture.Research.AdvanceWorkerTo(fixture.Worker,
                fixture.Research.LastProcessedTick + TimeSpan.FromDays(1).Ticks, true);
            var clock = new GameClock(new GameTimeState(fixture.Research.LastProcessedTick, 0, 1, false));
            var coordinator = new Game.Simulation.SaveCoordinator(Id(1), clock, new MemoryStore(),
                researchService: fixture.Research);
            var bytes = new SaveXmlCodec().Serialize(coordinator.Capture());
            var decoded = new SaveXmlCodec().Deserialize(bytes);
            Assert.That(decoded.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentVersion));
            var restored = new Fixture();
            restored.Research.RestoreState(decoded.Research);
            Assert.That(restored.Research.ActiveProject.AccumulatedWorkTicks,
                Is.EqualTo(fixture.Research.ActiveProject.AccumulatedWorkTicks));
            Assert.That(restored.Research.IsCompleted(new TechnologyId("stone_tools")), Is.True);
            Assert.That(restored.Research.AssignedWorkers.Single(), Is.EqualTo(fixture.Worker));
            restored.Research.AdvanceWorkerTo(restored.Worker,
                restored.Research.LastProcessedTick + TimeSpan.FromDays(2).Ticks, true);
            Assert.That(restored.Research.IsCompleted(new TechnologyId("woodworking")), Is.True);
            Assert.That(restored.Research.CompletionCount, Is.EqualTo(1));
        }

        [Test] public void SameInputsAndTierRoutesProduceIdenticalResearchState()
        {
            var direct = Route(new[] { 6 });
            var viaTier2 = Route(new[] { 2, 4, 6 });
            var viaTier1 = Route(new[] { 1, 3, 6 });
            TestContext.WriteLine($"U16 route hash: 0x{direct:X16}");
            Assert.That(viaTier2, Is.EqualTo(direct));
            Assert.That(viaTier1, Is.EqualTo(direct));
        }

        [Test] public void StaleTierCheckpointIsIgnoredWithoutRewindOrDuplicateWork()
        {
            var fixture = new Fixture();
            Assert.That(fixture.Research.Start(new TechnologyId("controlled_fire")), Is.True);
            fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(1).Ticks, true);
            var hash = fixture.Research.ComputeStateHash();
            Assert.That(fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromHours(12).Ticks, true), Is.Zero);
            Assert.That(fixture.Research.ComputeStateHash(), Is.EqualTo(hash));
        }

        [Test] public void Tier1UtilityAiSelectsResearchWithoutPerAgentUpdate()
        {
            var fixture = new Fixture();
            var characters = new CharacterRegistry();
            var character = CreateCharacter(fixture.Worker);
            characters.Add(character);
            var inventories = new ResourceInventoryRegistry();
            var piles = new WorldPileService(fixture.Resources, inventories);
            var buildings = new BuildingRegistry();
            var claims = new HaulClaimRegistry();
            var world = new Tier1AiWorld(new HaulWorldQuery(inventories, piles, buildings, claims), claims,
                new ResourceTransferService(inventories), research: fixture.Research);
            var settings = new Tier1AiSettings(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromHours(1),
                10, 10, 10, 1, 1f, 1, 0d, 0d, 0d, 1d);
            var agents = new Tier1AiAgentRegistry();
            var scheduler = new Tier1AiScheduler(characters, agents, world, settings);
            var state = new Tier1AiAgentState(character.Id, default);
            scheduler.Register(character, state, new InstantMovement(character.Id));
            fixture.Research.Start(new TechnologyId("controlled_fire"));
            scheduler.Advance(new GameTimeAdvance(TimeSpan.FromDays(1), TimeSpan.Zero));
            Assert.That(state.CurrentAction, Is.EqualTo(UtilityActionKind.Research));
            scheduler.Advance(new GameTimeAdvance(TimeSpan.FromDays(1), TimeSpan.Zero));
            Assert.That(fixture.Research.ActiveProject.Progress, Is.GreaterThan(0d));
        }

        [TestCase(10)]
        [TestCase(22)]
        public void RuntimeResearchUsesCanonicalDayOrNightClockAndResumesAfterLoad(int hour)
        {
            var fixture = new Fixture();
            var characters = new CharacterRegistry(); var character = CreateCharacter(fixture.Worker);
            characters.Add(character);
            var inventories = new ResourceInventoryRegistry();
            var piles = new WorldPileService(fixture.Resources, inventories);
            var claims = new HaulClaimRegistry();
            var world = new Tier1AiWorld(new HaulWorldQuery(inventories, piles, new BuildingRegistry(), claims),
                claims, new ResourceTransferService(inventories), research: fixture.Research);
            var settings = new Tier1AiSettings(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), TimeSpan.FromHours(1),
                10, 10, 10, 1, 1f, 1, 0d, 0d, 0d, 1d);
            var scheduler = new Tier1AiScheduler(characters, new Tier1AiAgentRegistry(), world, settings);
            var host = new UnityEngine.GameObject("Research clock regression");
            try
            {
                var runtime = host.AddComponent<Tier1AiRuntimeDriver>();
                var tick = TimeSpan.FromHours(hour).Ticks;
                var clock = new GameClock(new GameTimeState(tick, 0, 1, false));
                runtime.Initialize(clock, scheduler);
                var extra = new Tier1AiScheduler(characters, new Tier1AiAgentRegistry(), world, settings);
                runtime.AddScheduler(extra);
                scheduler.Register(character, new Tier1AiAgentState(character.Id, default), new InstantMovement(character.Id));
                fixture.Research.SynchronizeWorkers(new[] { character.Id }, tick);
                Assert.That(fixture.Research.Start(new TechnologyId("controlled_fire")), Is.True);
                var saved = fixture.Research.CaptureState();
                runtime.AdvanceForDebug(TimeSpan.FromSeconds(2));
                runtime.AdvanceForDebug(TimeSpan.FromSeconds(2));
                Assert.That(scheduler.CalendarTicks, Is.EqualTo(clock.State.CalendarTicks));
                Assert.That(extra.CalendarTicks, Is.EqualTo(clock.State.CalendarTicks));
                Assert.That(fixture.Research.ActiveProject.AccumulatedWorkTicks, Is.GreaterThan(0));
                Assert.That(fixture.Research.ActiveProject.AccumulatedWorkTicks, Is.LessThanOrEqualTo(TimeSpan.FromMinutes(4).Ticks));
                fixture.Research.RestoreState(saved);
                var restored = new GameClock(new GameTimeState(tick, 0, 1, false));
                runtime.RebindClock(restored);
                runtime.AdvanceForDebug(TimeSpan.FromSeconds(2));
                Assert.That(scheduler.CalendarTicks, Is.EqualTo(restored.State.CalendarTicks));
                Assert.That(fixture.Research.ActiveProject.AccumulatedWorkTicks, Is.GreaterThan(0));
                Assert.That(fixture.Research.ActiveProject.AccumulatedWorkTicks, Is.LessThanOrEqualTo(TimeSpan.FromMinutes(2).Ticks));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test] public void CompleteStoneAgeTreeHasNoDeadlockAndReachesCapstone()
        {
            var fixture = new Fixture();
            var completedBefore = fixture.Research.CompletedTechnologies.Count;
            var guard = 0;
            while (!fixture.Research.StoneAgeCompleted && guard++ < 64)
            {
                var next = fixture.Research.AvailableTechnologies.FirstOrDefault();
                Assert.That(next, Is.Not.Null, "Technology graph deadlocked before its capstone.");
                Complete(fixture.Research, fixture.Worker, next.Id.ToString());
            }
            Assert.That(fixture.Research.StoneAgeCompleted, Is.True);
            Assert.That(fixture.Research.CompletedTechnologies.Count, Is.EqualTo(fixture.Technologies.All.Count(x=>!x.Profile.IsFuture)));
            Assert.That(fixture.Research.HasCapability(new CapabilityId("stone_age_completed")), Is.True);
            Assert.That(fixture.Research.CompletionCount, Is.EqualTo(fixture.Technologies.All.Count(x=>!x.Profile.IsFuture) - completedBefore));
        }

        [Test] public void U15ProductionAndU16ResearchAdvanceTogetherForMultipleYears()
        {
            var fixture = new Fixture();
            var inventories = new ResourceInventoryRegistry();
            var buildings = new BuildingRegistry();
            var productionWorker = Id(101);
            var owner = new InventoryOwner(InventoryOwnerKind.Character, productionWorker);
            var productionInventory = new ResourceInventory(owner, fixture.Resources, 10000);
            Assert.That(productionInventory.TryAdd(new ResourceId("berries"), new ResourceQuantity(20)), Is.True);
            Assert.That(productionInventory.TryAdd(new ResourceId("cooked_berries"), new ResourceQuantity(1000)), Is.True);
            inventories.Add(productionInventory);
            var production = new ProductionService(fixture.ProductionCatalog, fixture.Resources, inventories,
                buildings, new ResourceTransferService(inventories), researchUnlocks: fixture.Research);
            // One abstract worker can sustainably feed this fixture population after agriculture unlocks.
            production.Population = 5;
            production.SetTarget(new ResourceId("berries"), 500);
            production.SetTarget(new ResourceId("plant_fiber"), 100);
            production.SetTarget(new ResourceId("wood_log"), 100);
            production.SetTarget(new ResourceId("stone"), 100);
            AddNatural(production, "gather_berries", 300, 1000);
            AddNatural(production, "gather_fiber", 301, 1000);
            AddNatural(production, "chop_wood", 302, 1000);
            AddNatural(production, "gather_stone", 303, 1000);
            var plot = ProductionSite.CreateCropPlot(Id(200), new ProductionRecipeId("cultivate_berries"),
                default, production.Settings.CropGrowthDuration);
            production.AddSite(plot);
            for (var day = 1; day <= 730; day++)
            {
                if (fixture.Research.ActiveProject == null && !fixture.Research.StoneAgeCompleted)
                    fixture.Research.Start(fixture.Research.AvailableTechnologies.First().Id);
                var tick = TimeSpan.FromDays(day).Ticks;
                fixture.Research.AdvanceWorkerTo(fixture.Worker, tick, true);
                // Keep this cross-system test focused on deterministic progression rather than a one-worker
                // settlement's meal throughput; food balance has its own dedicated fixtures.
                productionInventory.TryAdd(new ResourceId("cooked_berries"), new ResourceQuantity(10), true);
                production.AdvanceTo(tick, new[] { productionWorker });
            }
            Assert.That(fixture.Research.StoneAgeCompleted, Is.True);
            Assert.That(production.GetProducedTotal(new ResourceId("berries")), Is.GreaterThan(0));
            Assert.That(production.Food.TotalShortage, Is.EqualTo(0));
            Assert.That(inventories.Get(owner).CaptureState().Amounts.All(x => x.Quantity.Units >= 0), Is.True);
            TestContext.WriteLine($"U16 730-day research hash: 0x{fixture.Research.ComputeStateHash():X16}");
            TestContext.WriteLine($"U15 730-day production hash: 0x{production.ComputeStateHash():X16}; food shortage={production.Food.TotalShortage}");
        }

        [Test] public void ThousandWorkerCentralizedResearchHasBoundedTimeAndAllocation()
        {
            var capability = new CapabilityId("scale_verified");
            var catalog = new TechnologyCatalog(new[]
            {
                new TechnologyDefinition(new TechnologyId("research_scale"), "Research Scale",
                    Array.Empty<TechnologyId>(), TimeSpan.FromDays(10000),
                    new[] { TechnologyUnlock.Capability(capability) })
            }, new TechnologyUnlockTargets(Array.Empty<BuildingDefinitionId>(),
                Array.Empty<ProductionRecipeId>(), new[] { capability }));
            var research = new ResearchService(catalog);
            var workers = Enumerable.Range(1000, 1000).Select(Id).ToArray();
            foreach (var worker in workers) research.AssignWorker(worker);
            Assert.That(research.Start(new TechnologyId("research_scale")), Is.True);

            GC.Collect();
            var allocationBefore = GC.GetAllocatedBytesForCurrentThread();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (var day = 1; day <= 5; day++)
                research.AdvanceWorkersTo(TimeSpan.FromDays(day).Ticks, workers);
            timer.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;

            TestContext.WriteLine($"U16 scale: workers=1000, days=5, elapsedMs={timer.ElapsedMilliseconds}, allocatedBytes={allocated}, hash=0x{research.ComputeStateHash():X16}");
            Assert.That(research.ActiveProject, Is.Not.Null);
            Assert.That(timer.ElapsedMilliseconds, Is.LessThan(5000));
            Assert.That(allocated, Is.LessThan(128L * 1024L * 1024L));
        }

        [Test] public void ApprovedCircularGraphHasSingleLocalPredecessorsAndNoFutureGate()
        {
            var fixture=new Fixture();
            foreach(var definition in fixture.Technologies.All)
            {
                var p=definition.Profile;
                if(p.Branch=="root"||p.Branch=="epoch")continue;
                Assert.That(definition.Prerequisites.Count,Is.EqualTo(1),p.Id);
                var parent=fixture.Technologies.Get(definition.Prerequisites[0]);
                Assert.That(parent.Profile.Branch=="root"||parent.Profile.Branch==p.Branch,Is.True,p.Id);
                if(!p.IsFuture)Assert.That(parent.Profile.IsFuture,Is.False,p.Id);
            }
            Complete(fixture.Research,fixture.Worker,"controlled_fire");
            Assert.That(fixture.Research.IsAvailable(new TechnologyId("cooking")),Is.True);
            Assert.That(fixture.Research.IsCompleted(new TechnologyId("hunting")),Is.False);
            Assert.That(fixture.Research.IsAvailable(new TechnologyId("pottery")),Is.False);
        }

        [Test] public void GroupContributionIsBoundedAndNewWorkersDoNotReceiveRetroactiveWork()
        {
            var id=new TechnologyId("long_project");
            var catalog=new TechnologyCatalog(new[]{new TechnologyDefinition(id,"Long project",null,
                TimeSpan.FromDays(20),null)},new TechnologyUnlockTargets(null,null,null));
            var service=new ResearchService(catalog);
            var ids=Enumerable.Range(1,100).Select(Id).ToArray();
            service.SynchronizeWorkers(ids,TimeSpan.FromDays(10).Ticks);
            Assert.That(service.Start(id),Is.True);
            service.AdvanceWorkersTo(TimeSpan.FromDays(11).Ticks,ids);
            Assert.That(service.ActiveProject.Progress,Is.EqualTo(.14d).Within(.000001));
            var saved=service.CaptureState();
            var restored=new ResearchService(catalog);restored.RestoreState(saved);
            restored.SynchronizeWorkers(new[]{Id(200)},TimeSpan.FromDays(40).Ticks);
            restored.AdvanceWorkerTo(Id(200),TimeSpan.FromDays(40).Ticks,true);
            Assert.That(restored.ActiveProject.Progress,Is.EqualTo(.14d).Within(.000001));
            restored.AdvanceWorkerTo(Id(200),TimeSpan.FromDays(41).Ticks,true);
            Assert.That(restored.ActiveProject.Progress,Is.EqualTo(.19d).Within(.000001));
        }

        [Test] public void MaterialCostIsPaidOnceAndMissingPracticalFactsBlockTheEpoch()
        {
            var f=new Fixture();var inventories=new ResourceInventoryRegistry();
            var owner=new InventoryOwner(InventoryOwnerKind.BuildingStorage,Id(700));
            var inventory=new ResourceInventory(owner,f.Resources,100);inventories.Add(inventory);
            inventory.TryAdd(new ResourceId("wood_log"),new ResourceQuantity(10));
            long population=0;
            f.Research.SettlementContext=new ResearchSettlementContext(inventories,f.Resources,()=>new[]{owner},key=>population);
            Assert.That(f.Research.Start(new TechnologyId("controlled_fire")),Is.True);
            Assert.That(inventory.GetAmount(new ResourceId("wood_log")).Units,Is.EqualTo(7));
            Assert.That(f.Research.Start(new TechnologyId("controlled_fire")),Is.False);
            Assert.That(inventory.GetAmount(new ResourceId("wood_log")).Units,Is.EqualTo(7));
            Assert.That(f.Research.GetBlockers(new TechnologyId("stone_age_mastery")).Any(x=>x.Contains("Живые жители")),Is.True);
        }

        [Test] public void ExistingGroupDoesNotEarnIdleTimeBeforeANewProject()
        {
            var id=new TechnologyId("long_project");
            var catalog=new TechnologyCatalog(new[]{new TechnologyDefinition(id,"Long project",null,
                TimeSpan.FromDays(20),null)},new TechnologyUnlockTargets(null,null,null));
            var service=new ResearchService(catalog);
            var worker=Id(100);
            service.AssignWorker(worker);
            service.SynchronizeWorkers(new[]{worker},TimeSpan.FromDays(10).Ticks);
            Assert.That(service.Start(id),Is.True);
            service.AdvanceWorkerTo(worker,TimeSpan.FromDays(11).Ticks,true);
            Assert.That(service.ActiveProject.Progress,Is.EqualTo(.05d).Within(.000001));
            service.Cancel();
            service.SynchronizeWorkers(new[]{worker},TimeSpan.FromDays(30).Ticks);
            Assert.That(service.Start(id),Is.True);
            service.AdvanceWorkerTo(worker,TimeSpan.FromDays(31).Ticks,true);
            Assert.That(service.ActiveProject.Progress,Is.EqualTo(.05d).Within(.000001));
        }

        [TestCase("peaceful")]
        [TestCase("scholar")]
        [TestCase("military")]
        [TestCase("mixed")]
        public void ApprovedSpecializationsReachEpochWithoutResearchingTheWholeTree(string route)
        {
            var f=new Fixture(startsWithStoneTools:true);
            var baseIds=new[]{"stone_tools","controlled_fire","shelter_construction","woodworking","organized_labor","advanced_stonecraft"};
            var routes=new Dictionary<string,string[]> {
                ["peaceful"]=new[]{"clay_working","hide_working","fishing","cooking","food_preservation","reliable_housing","seasonal_preparation","kinship","settlement_council","spiritual_tradition","shared_storage"},
                ["scholar"]=new[]{"observation","plant_lore","animal_lore","herbalism","wound_care","season_calendar","record_keeping","knowledge_transfer","natural_philosophy","hunting","fishing"},
                ["military"]=new[]{"primitive_weapons","militia","territory_guard","formation_training","throwing_weapons","military_organization","fortification","hunting","cooking","food_preservation"},
                ["mixed"]=new[]{"fishing","cooking","food_preservation","observation","plant_lore","herbalism","wound_care","season_calendar"}
            };
            void Learn(string id)
            {
                var tech=f.Technologies.Get(new TechnologyId(id));
                if(f.Research.IsCompleted(tech.Id))return;
                foreach(var parent in tech.Prerequisites)Learn(parent.ToString());
                Complete(f.Research,f.Worker,id);
            }
            foreach(var id in baseIds.Concat(routes[route]))Learn(id);
            Assert.That(f.Research.IsAvailable(new TechnologyId("stone_age_mastery")),Is.True,
                string.Join("; ",f.Research.GetBlockers(new TechnologyId("stone_age_mastery"))));
            Assert.That(f.Research.CompletedTechnologies.Count,Is.LessThan(25));
            if(route=="peaceful")Assert.That(f.Research.CompletedTechnologies.Any(id=>f.Technologies.Get(id).Profile.Branch=="military"),Is.False);
            if(route=="scholar")
            {
                Assert.That(f.Research.EffectiveWorkerLimit,Is.EqualTo(6));
                Assert.That(f.Research.ScienceBonusPercent,Is.EqualTo(20));
            }
            Learn("stone_age_mastery");
            Assert.That(f.Research.StoneAgeCompleted,Is.True);
            Assert.That(f.Research.AvailableTechnologies,Is.Not.Empty);
        }

        private static ulong Route(int[] cumulativeDays)
        {
            var fixture = new Fixture();
            fixture.Research.Start(new TechnologyId("controlled_fire"));
            foreach (var day in cumulativeDays)
                fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(day).Ticks, true);
            return fixture.Research.ComputeStateHash();
        }

        private static void Complete(ResearchService research, StableEntityId worker, string technologyId)
        {
            var id = new TechnologyId(technologyId);
            if (research.IsCompleted(id)) return;
            Assert.That(research.Start(id), Is.True, "Technology was not available: " + technologyId);
            var target = checked(research.LastProcessedTick + research.Catalog.Get(id).ResearchWorkRequiredTicks);
            research.AdvanceWorkerTo(worker, target, true);
            Assert.That(research.IsCompleted(id), Is.True);
        }

        private static void AddNatural(ProductionService production, string recipe, int id, long amount) =>
            production.AddSite(ProductionSite.Restore(new ProductionSiteState(Id(id), new ProductionRecipeId(recipe),
                ProductionSiteKind.NaturalSource, default, amount, amount, CropPlotPhase.Unplanted, 0, 0)));

        private static ResourceCatalog CreateResources() => new ResourceCatalog(new[]
        {
            new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("hide"), "Hide", ResourceCategory.Other),
            new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food),
            new ResourceDefinition(new ResourceId("cooked_berries"), "Cooked Berries", ResourceCategory.Food)
        });

        private static Character CreateCharacter(StableEntityId id) => Character.Restore(new CharacterState(id,
            new CharacterName("Researcher"), CharacterSex.Female, 0, CharacterLifeState.Alive, null,
            null, null, null, null, new ProfessionId("researcher"),
            new[] { new TraitId("calm"), new TraitId("brave"), new TraitId("kind"), new TraitId("curious") },
            new[] { new SkillState(new SkillId("research"), new SkillLevel(50)) },
            new CharacterAttributes(new CharacterAttributeValue(50), new CharacterAttributeValue(50)),
            BodyHealth.Healthy, new Wealth(0), new Influence(0), null));

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
