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
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U16")]
    public sealed class ResearchFrameworkTests
    {
        private sealed class Fixture
        {
            public readonly ResourceCatalog Resources;
            public readonly ProductionCatalog ProductionCatalog;
            public readonly TechnologyCatalog Technologies;
            public readonly ResearchService Research;
            public readonly StableEntityId Worker = Id(100);

            public Fixture(IResearchRateProvider rates = null)
            {
                Resources = CreateResources();
                ProductionCatalog = ProductionCatalog.CreateStoneAge(Resources);
                Technologies = TechnologyCatalog.CreateStoneAge(ProductionCatalog, BuildingIds);
                Research = new ResearchService(Technologies, rates);
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
            new BuildingDefinitionId("primitive_crop_plot")
        };

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
            Assert.That(fixture.Research.IsProductionRecipeUnlocked(new ProductionRecipeId("chop_wood")), Is.False);
        }

        [Test] public void PartialProgressCompletionAndExactOnceCommitAreDeterministic()
        {
            var fixture = new Fixture();
            Assert.That(fixture.Research.Start(new TechnologyId("controlled_fire")), Is.True);
            fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(1).Ticks, true);
            Assert.That(fixture.Research.ActiveProject.Progress, Is.EqualTo(0.5d).Within(0.000001d));
            fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(2).Ticks, true);
            Assert.That(fixture.Research.IsCompleted(new TechnologyId("controlled_fire")), Is.True);
            Assert.That(fixture.Research.CompletionCount, Is.EqualTo(1));
            fixture.Research.AdvanceWorkerTo(fixture.Worker, TimeSpan.FromDays(3).Ticks, true);
            Assert.That(fixture.Research.CompletionCount, Is.EqualTo(1));
            Assert.That(fixture.Research.LastCompletionTick, Is.EqualTo(TimeSpan.FromDays(2).Ticks));
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
            Assert.That(production.TryReserve(fixture.Worker, default, out _), Is.False);

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
            fast.Research.AdvanceWorkerTo(fast.Worker, TimeSpan.FromDays(1).Ticks, true);
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
            Assert.That(fixture.Research.CompletedTechnologies.Count, Is.EqualTo(fixture.Technologies.All.Count));
            Assert.That(fixture.Research.HasCapability(new CapabilityId("stone_age_completed")), Is.True);
            Assert.That(fixture.Research.CompletionCount, Is.EqualTo(fixture.Technologies.All.Count - completedBefore));
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
            inventories.Add(productionInventory);
            var production = new ProductionService(fixture.ProductionCatalog, fixture.Resources, inventories,
                buildings, new ResourceTransferService(inventories), researchUnlocks: fixture.Research);
            production.Population = 20;
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
            var cropOperated = false;
            for (var day = 1; day <= 730; day++)
            {
                if (fixture.Research.ActiveProject == null && !fixture.Research.StoneAgeCompleted)
                    fixture.Research.Start(fixture.Research.AvailableTechnologies.First().Id);
                var tick = TimeSpan.FromDays(day).Ticks;
                fixture.Research.AdvanceWorkerTo(fixture.Worker, tick, true);
                production.AdvanceTo(tick, new[] { productionWorker });
                cropOperated |= plot.CropPhase != CropPlotPhase.Unplanted;
            }
            Assert.That(fixture.Research.StoneAgeCompleted, Is.True);
            Assert.That(cropOperated, Is.True);
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
            new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food)
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
