using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Simulation.Production;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("U15")]
    public sealed class ProductionAndFarmingTests
    {
        private sealed class Fixture
        {
            public readonly ResourceId Berries = new ResourceId("berries");
            public readonly ResourceId Wood = new ResourceId("wood_log");
            public readonly ResourceId Stone = new ResourceId("stone");
            public readonly ResourceId Fiber = new ResourceId("plant_fiber");
            public readonly ResourceCatalog Resources;
            public readonly ProductionCatalog Recipes;
            public readonly ResourceInventoryRegistry Inventories = new ResourceInventoryRegistry();
            public readonly BuildingRegistry Buildings = new BuildingRegistry();
            public readonly Game.Simulation.Resources.ResourceTransferService Transfers;
            public readonly ProductionService Production;
            public readonly InventoryOwner StorageOwner;
            public readonly ResourceInventory Storage;
            private int nextId = 100;

            public Fixture(long storageCapacity = 10000, IEnumerable<ResourceStockTarget> targets = null,
                TimeSpan? foodRate = null)
            {
                Resources = new ResourceCatalog(new[]
                {
                    new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                    new ResourceDefinition(Stone, "Stone", ResourceCategory.Construction),
                    new ResourceDefinition(Fiber, "Fiber", ResourceCategory.Construction),
                    new ResourceDefinition(new ResourceId("hide"), "Hide", ResourceCategory.Other),
                    new ResourceDefinition(Berries, "Berries", ResourceCategory.Food)
                });
                Recipes = ProductionCatalog.CreateStoneAge(Resources);
                Transfers = new Game.Simulation.Resources.ResourceTransferService(Inventories);
                var storageId = Id(2);
                var building = Building.Restore(new BuildingState(storageId, new BuildingDefinitionId("storage_basket"),
                    new GridCoordinate(5, 5), BuildingOrientation.North, ConstructionState.Completed));
                Buildings.Add(building);
                StorageOwner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, storageId);
                Storage = new ResourceInventory(StorageOwner, Resources, storageCapacity);
                Inventories.Add(Storage);
                var configuredTargets = targets ?? new[]
                {
                    new ResourceStockTarget(Berries, 40), new ResourceStockTarget(Fiber, 20),
                    new ResourceStockTarget(Stone, 20), new ResourceStockTarget(Wood, 30)
                };
                var settings = new ProductionSettings(TimeSpan.FromDays(1), TimeSpan.FromDays(2),
                    foodRate ?? TimeSpan.FromDays(15), configuredTargets);
                Production = new ProductionService(Recipes, Resources, Inventories, Buildings, Transfers, settings);
            }

            public StableEntityId AddWorker(int? id = null, long capacity = 8)
            {
                var worker = Id(id ?? nextId++);
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, worker),
                    Resources, capacity));
                return worker;
            }

            public ProductionSite AddNatural(string recipeId, int id, long amount, float x = 0f)
            {
                var site = ProductionSite.Restore(new ProductionSiteState(Id(id), new ProductionRecipeId(recipeId),
                    ProductionSiteKind.NaturalSource, new WorldPosition(x, 0f, 0f), amount, amount,
                    CropPlotPhase.Unplanted, 0, 0));
                Production.AddSite(site);
                return site;
            }

            public ProductionSite AddPlot(int id)
            {
                var site = ProductionSite.CreateCropPlot(Id(id), new ProductionRecipeId("cultivate_berries"),
                    new WorldPosition(0f, 0f, 0f), Production.Settings.CropGrowthDuration);
                Production.AddSite(site);
                return site;
            }

            public ResourceInventory CharacterInventory(StableEntityId id) =>
                Inventories.Get(new InventoryOwner(InventoryOwnerKind.Character, id));

            public static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        }

        [Test] public void ProductionRecipeValidationAndDeclaredOutputAreDataDriven()
        {
            var fixture = new Fixture();
            var recipe = fixture.Recipes.Get(new ProductionRecipeId("chop_wood"));
            Assert.That(recipe.WorkRequiredTicks, Is.EqualTo(TimeSpan.FromHours(8).Ticks));
            Assert.That(recipe.Outputs.Single().ResourceId, Is.EqualTo(fixture.Wood));
            Assert.That(recipe.Outputs.Single().Quantity.Units, Is.EqualTo(4));
            Assert.Throws<ArgumentException>(() => new ProductionRecipe(new ProductionRecipeId("invalid"), "Invalid",
                null, Array.Empty<ResourceAmount>(), TimeSpan.FromHours(1), ProductionSiteKind.NaturalSource));
        }

        [Test] public void FiniteSourceDecreasesAndCannotOverHarvest()
        {
            var fixture = new Fixture(targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 100) });
            var worker = fixture.AddWorker();
            var source = fixture.AddNatural("chop_wood", 10, 1);
            Assert.That(fixture.Production.TryReserve(worker, default, out var job), Is.True);
            var result = fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(8));
            Assert.That(result.Completed, Is.True);
            Assert.That(source.RemainingAmount, Is.Zero);
            Assert.That(source.IsDepleted, Is.True);
            Assert.That(fixture.Production.HasEligibleWork(worker, default), Is.False);
            Assert.That(fixture.CharacterInventory(worker).GetAmount(fixture.Wood).Units, Is.EqualTo(4));
        }

        [Test] public void PartialWorkCompletesOneBatchExactlyOnceAndCancellationReleasesSource()
        {
            var fixture = new Fixture(targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 100) });
            var first = fixture.AddWorker(101);
            var second = fixture.AddWorker(102);
            var source = fixture.AddNatural("chop_wood", 11, 2);
            Assert.That(fixture.Production.TryReserve(first, default, out var task), Is.True);
            Assert.That(fixture.Production.AdvanceTask(task.Task.Id, TimeSpan.FromHours(3)).Completed, Is.False);
            Assert.That(task.Task.AccumulatedWorkTicks, Is.EqualTo(TimeSpan.FromHours(3).Ticks));
            Assert.That(fixture.Production.Cancel(task.Task.Id), Is.True);
            Assert.That(fixture.Production.TryReserve(second, default, out var replacement), Is.True);
            var complete = fixture.Production.AdvanceTask(replacement.Task.Id, TimeSpan.FromHours(8));
            Assert.That(complete.Completed, Is.True);
            Assert.That(fixture.Production.AdvanceTask(replacement.Task.Id, TimeSpan.FromHours(8)).Completed, Is.False);
            Assert.That(source.RemainingAmount, Is.EqualTo(1));
            Assert.That(fixture.Production.GetProducedTotal(fixture.Wood), Is.EqualTo(4));
        }

        [Test] public void OutputHasOneCanonicalOwnerAndFullStorageLeavesItWithWorker()
        {
            var fixture = new Fixture(storageCapacity: 1,
                targets: new[] { new ResourceStockTarget(new ResourceId("stone"), 100) });
            fixture.Storage.TryAdd(fixture.Wood, new ResourceQuantity(1));
            var worker = fixture.AddWorker();
            fixture.AddNatural("gather_stone", 12, 1);
            Assert.That(fixture.Production.TryReserve(worker, default, out var job), Is.True);
            var result = fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(10));
            Assert.That(result.OutputOwner.Kind, Is.EqualTo(InventoryOwnerKind.Character));
            Assert.That(fixture.Production.TryFindStorage(fixture.Stone, 4, default, out _, out _), Is.False);
            Assert.That(fixture.CharacterInventory(worker).GetAmount(fixture.Stone).Units, Is.EqualTo(4));
            Assert.That(fixture.Storage.GetAmount(fixture.Stone).Units, Is.Zero);
            Assert.That(fixture.Production.GetCanonicalTotal(fixture.Stone), Is.EqualTo(4));
        }

        [Test] public void ReservationPreventsTwoWorkersClaimingLastBatch()
        {
            var fixture = new Fixture(targets: new[] { new ResourceStockTarget(new ResourceId("plant_fiber"), 100) });
            var first = fixture.AddWorker(101);
            var second = fixture.AddWorker(102);
            fixture.AddNatural("gather_fiber", 13, 1);
            Assert.That(fixture.Production.TryReserve(first, default, out _), Is.True);
            Assert.That(fixture.Production.TryReserve(second, default, out _), Is.False);
        }

        [Test] public void FoodConsumptionNeverGoesNegativeAndReportsShortage()
        {
            var fixture = new Fixture(foodRate: TimeSpan.FromDays(1));
            fixture.Storage.TryAdd(fixture.Berries, new ResourceQuantity(3));
            fixture.Production.Food.Advance(TimeSpan.FromDays(5), 1);
            Assert.That(fixture.Storage.GetAmount(fixture.Berries).Units, Is.Zero);
            Assert.That(fixture.Production.Food.TotalConsumed, Is.EqualTo(3));
            Assert.That(fixture.Production.Food.TotalShortage, Is.EqualTo(2));
            Assert.That(fixture.Production.Food.IsShortage, Is.True);
        }

        [Test] public void TargetPolicyCreatesWorkBelowTargetAndStopsAtTarget()
        {
            var fixture = new Fixture(targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 4) });
            var worker = fixture.AddWorker();
            fixture.AddNatural("chop_wood", 14, 5);
            Assert.That(fixture.Production.HasEligibleWork(worker, default), Is.True);
            fixture.Production.TryReserve(worker, default, out var job);
            fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(8));
            Assert.That(fixture.Production.GetCanonicalTotal(fixture.Wood), Is.EqualTo(4));
            Assert.That(fixture.Production.HasEligibleWork(worker, default), Is.False);
        }

        [Test] public void SaveLoadPreservesSourcesProgressOwnershipPolicyAndDoesNotRepeatBatch()
        {
            var fixture = new Fixture(targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 100) });
            var worker = fixture.AddWorker(101);
            var source = fixture.AddNatural("chop_wood", 15, 3);
            fixture.Production.TryReserve(worker, default, out var job);
            fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(3));
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Fixture.Id(99),
                new GameTimeState(TimeSpan.FromDays(7).Ticks, TimeSpan.FromDays(7).Ticks, 1, false), 400,
                fixture.Production.CaptureState());
            var restoredSnapshot = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));

            var restored = new Fixture(targets: new[] { new ResourceStockTarget(new ResourceId("wood_log"), 1) });
            restored.Production.RestoreState(restoredSnapshot.Production);
            var restoredSource = restored.Production.Sites.Single(x => x.Id == source.Id);
            var restoredTask = restored.Production.ActiveTasks.Single();
            Assert.That(restoredTask.Id, Is.EqualTo(job.Task.Id));
            Assert.That(restoredTask.AccumulatedWorkTicks, Is.EqualTo(TimeSpan.FromHours(3).Ticks));
            Assert.That(restoredSource.RemainingAmount, Is.EqualTo(3));
            Assert.That(restored.Production.GetTarget(fixture.Wood), Is.EqualTo(100));
            var result = restored.Production.AdvanceTask(restoredTask.Id, TimeSpan.FromHours(5));
            Assert.That(result.Completed, Is.True);
            Assert.That(restored.Production.AdvanceTask(restoredTask.Id, TimeSpan.FromHours(8)).Completed, Is.False);
            Assert.That(restoredSource.RemainingAmount, Is.EqualTo(2));
            Assert.That(restored.Production.GetCanonicalTotal(fixture.Wood), Is.EqualTo(4));
            Assert.That(restoredSnapshot.Production.Inventories.Any(x => x.Owner.Id == worker), Is.True);
        }

        [Test] public void DeterminismAndCollectionOrderIndependenceProduceSameHash()
        {
            var first = DeterministicScenario(false);
            var second = DeterministicScenario(true);
            Assert.That(first.Hash, Is.EqualTo(second.Hash));
            Assert.That(first.Berries, Is.EqualTo(second.Berries));
            Assert.That(first.Wood, Is.EqualTo(second.Wood));
        }

        [Test] public void TierRouteChunkingUsesOneAuthoritativeEconomicResult()
        {
            var tier3Only = RouteScenario(new[] { 30 });
            var tier3Tier2Tier3 = RouteScenario(new[] { 10, 20, 30 });
            var tier3Tier1Tier3 = RouteScenario(new[] { 7, 23, 30 });
            Assert.That(tier3Tier2Tier3, Is.EqualTo(tier3Only));
            Assert.That(tier3Tier1Tier3, Is.EqualTo(tier3Only));
        }

        [Test] public void PrimitiveCropPlotPlantsGrowsAndHarvestsExactlyOneBatch()
        {
            var fixture = new Fixture(targets: new[] { new ResourceStockTarget(new ResourceId("berries"), 100) });
            var worker = fixture.AddWorker();
            var plot = fixture.AddPlot(20);
            fixture.Production.TryReserve(worker, default, out var plant);
            var planted = fixture.Production.AdvanceTask(plant.Task.Id, TimeSpan.FromHours(8));
            Assert.That(planted.Completed, Is.True);
            Assert.That(planted.ProducedOutput, Is.False);
            Assert.That(plot.CropPhase, Is.EqualTo(CropPlotPhase.Growing));
            plot.AdvanceGrowth(TimeSpan.FromDays(2).Ticks);
            Assert.That(plot.CropPhase, Is.EqualTo(CropPlotPhase.Ready));
            fixture.Production.TryReserve(worker, default, out var harvest);
            var harvested = fixture.Production.AdvanceTask(harvest.Task.Id, TimeSpan.FromHours(8));
            Assert.That(harvested.Completed, Is.True);
            Assert.That(plot.CropPhase, Is.EqualTo(CropPlotPhase.Unplanted));
            Assert.That(fixture.CharacterInventory(worker).GetAmount(fixture.Berries).Units, Is.EqualTo(6));
            Assert.That(fixture.Production.AdvanceTask(harvest.Task.Id, TimeSpan.FromHours(8)).Completed, Is.False);
        }

        [Test] public void FiveYearSettlementRemainsFoodPositiveWithoutNegativeResources()
        {
            var fixture = new Fixture(storageCapacity: 1000,
                targets: new[] { new ResourceStockTarget(new ResourceId("berries"), 80) });
            var workers = Enumerable.Range(100, 20).Select(id => fixture.AddWorker(id)).ToArray();
            fixture.AddPlot(21);
            fixture.Storage.TryAdd(fixture.Berries, new ResourceQuantity(20));
            fixture.Production.Population = 20;
            fixture.Production.AdvanceTo(TimeSpan.FromDays(365 * 5).Ticks, workers);
            Assert.That(fixture.Production.Food.TotalShortage, Is.Zero);
            Assert.That(fixture.Production.Food.TotalConsumed, Is.GreaterThan(2000));
            Assert.That(fixture.Production.GetProducedTotal(fixture.Berries), Is.GreaterThan(2000));
            Assert.That(fixture.Production.GetCanonicalTotal(fixture.Berries), Is.GreaterThan(0));
            Assert.That(fixture.Inventories.All.All(inventory => inventory.CaptureState().Amounts.All(x => x.Quantity.Units >= 0)), Is.True);
            TestContext.WriteLine($"U15 five-year hash={fixture.Production.ComputeStateHash():X16} produced={fixture.Production.GetProducedTotal(fixture.Berries)} consumed={fixture.Production.Food.TotalConsumed}");
        }

        [Test] public void ThousandWorkerThirtyDayProductionGateRecordsMeasurementAndHash()
        {
            var fixture = new Fixture(storageCapacity: 1000000, targets: new[]
            {
                new ResourceStockTarget(new ResourceId("berries"), 200),
                new ResourceStockTarget(new ResourceId("wood_log"), 200),
                new ResourceStockTarget(new ResourceId("stone"), 200),
                new ResourceStockTarget(new ResourceId("plant_fiber"), 200)
            });
            var workers = Enumerable.Range(1000, 1000).Select(id => fixture.AddWorker(id)).ToArray();
            fixture.AddNatural("gather_berries", 30, 1000, -3);
            fixture.AddNatural("gather_fiber", 31, 1000, -1);
            fixture.AddNatural("chop_wood", 32, 1000, 1);
            fixture.AddNatural("gather_stone", 33, 1000, 3);
            fixture.Production.Population = 10000;
            GC.Collect();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            fixture.Production.AdvanceTo(TimeSpan.FromDays(30).Ticks, workers.Reverse());
            stopwatch.Stop();
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.That(fixture.Production.LastCalendarTick, Is.EqualTo(TimeSpan.FromDays(30).Ticks));
            Assert.That(fixture.Production.ComputeStateHash(), Is.Not.Zero);
            TestContext.WriteLine($"U15 performance: workers=1000 population=10000 days=30 elapsedMs={stopwatch.Elapsed.TotalMilliseconds:0.000} allocatedBytes={allocatedBytes} hash={fixture.Production.ComputeStateHash():X16}");
        }

        private static (ulong Hash, long Berries, long Wood) DeterministicScenario(bool reverse)
        {
            var fixture = new Fixture();
            var workers = new[] { fixture.AddWorker(101), fixture.AddWorker(102), fixture.AddWorker(103) };
            fixture.AddNatural("gather_berries", 40, 100, -1);
            fixture.AddNatural("chop_wood", 41, 100, 1);
            fixture.AddPlot(42);
            fixture.Production.Population = 3;
            fixture.Production.AdvanceTo(TimeSpan.FromDays(30).Ticks, reverse ? workers.Reverse() : workers);
            return (fixture.Production.ComputeStateHash(), fixture.Production.GetCanonicalTotal(fixture.Berries),
                fixture.Production.GetCanonicalTotal(fixture.Wood));
        }

        private static ulong RouteScenario(int[] cumulativeDays)
        {
            var fixture = new Fixture();
            var workers = new[] { fixture.AddWorker(101), fixture.AddWorker(102), fixture.AddWorker(103) };
            fixture.AddNatural("gather_berries", 50, 100, -1);
            fixture.AddNatural("chop_wood", 51, 100, 1);
            fixture.AddPlot(52);
            fixture.Production.Population = 3;
            foreach (var day in cumulativeDays)
                fixture.Production.AdvanceTo(TimeSpan.FromDays(day).Ticks, workers);
            return fixture.Production.ComputeStateHash();
        }
    }
}
