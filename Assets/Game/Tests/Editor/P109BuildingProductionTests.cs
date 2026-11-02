using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Domain.Work;
using Game.Infrastructure;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Simulation.Work;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P10.9")]
    public sealed class P109BuildingProductionTests
    {
        private sealed class Fixture
        {
            public readonly ResourceId Wood = new ResourceId("wood_log");
            public readonly ResourceId Berries = new ResourceId("berries");
            public readonly ResourceId Cooked = new ResourceId("cooked_berries");
            public readonly ResourceCatalog Resources;
            public readonly ResourceInventoryRegistry Inventories = new ResourceInventoryRegistry();
            public readonly BuildingRegistry Buildings = new BuildingRegistry();
            public readonly ProductionService Production;
            public readonly Building Building;
            public readonly BuildingDefinition Definition;

            public Fixture(long outputCapacity = 12)
            {
                Resources = new ResourceCatalog(new[]
                {
                    new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                    new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
                    new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
                    new ResourceDefinition(Berries, "Berries", ResourceCategory.Food),
                    new ResourceDefinition(Cooked, "Cooked berries", ResourceCategory.Food)
                });
                var recipes = ProductionCatalog.CreateStoneAge(Resources);
                var buildingId = Id(10);
                Building = Building.Restore(new BuildingState(buildingId, new BuildingDefinitionId("campfire"),
                    new GridCoordinate(2, 3), BuildingOrientation.North, ConstructionState.Completed));
                Buildings.Add(Building);
                Definition = new BuildingDefinition(new BuildingDefinitionId("campfire"), "Campfire",
                    BuildingCategory.Food, BuildingEra.StoneAge, string.Empty, string.Empty, null,
                    new ConstructionSpec(null, TimeSpan.FromMinutes(1)),
                    new PlacementSpec(new BuildingFootprint(2, 2)), workplace: new WorkplaceSpec(1),
                    production: new ProductionSpec(new[] { new ProductionRecipeId("cook_berries") }, 24,
                        outputCapacity));
                Production = new ProductionService(recipes, Resources, Inventories, Buildings,
                    new ResourceTransferService(Inventories), new ProductionSettings(TimeSpan.FromDays(1),
                        TimeSpan.FromDays(2), TimeSpan.FromDays(15), Array.Empty<ResourceStockTarget>()));
                Production.RegisterBuildingWorkplace(buildingId, Definition, new WorldPosition(2, 0, 3));
                Site.SetContinuous(false); // These cases exercise the explicit finite-queue mode.
            }

            public StableEntityId AddWorker(int value)
            {
                var id = Id(value);
                Inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, id),
                    Resources, 8));
                return id;
            }

            public ResourceInventory Input => Inventories.Get(Production.InputOwner(Building.Id));
            public ResourceInventory Output => Inventories.Get(Production.OutputOwner(Building.Id));
            public ProductionSite Site => Production.Sites.Single();
            public static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        }

        [Test]
        public void QueueRequiresDeliveredRawFoodAndFuelThenProducesIntoOutputBuffer()
        {
            var fixture = new Fixture();
            var worker = fixture.AddWorker(20);
            Assert.That(fixture.Production.QueueBatches(fixture.Building.Id,
                new ProductionRecipeId("cook_berries"), 2), Is.True);
            Assert.That(fixture.Production.GetWaitingReason(fixture.Site),
                Is.EqualTo(ProductionWaitingReason.MissingInputs));
            Assert.That(fixture.Production.TryReserveAtSite(worker, fixture.Site.Id, out _), Is.False);

            fixture.Input.TryAdd(fixture.Berries, new ResourceQuantity(6));
            fixture.Input.TryAdd(fixture.Wood, new ResourceQuantity(2));
            Assert.That(fixture.Production.TryReserveAtSite(worker, fixture.Site.Id, out var job), Is.True);
            var completion = fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromMinutes(20));

            Assert.That(completion.Completed, Is.True);
            Assert.That(completion.OutputOwner, Is.EqualTo(fixture.Production.OutputOwner(fixture.Building.Id)));
            Assert.That(fixture.Output.GetAmount(fixture.Cooked).Units, Is.EqualTo(3));
            Assert.That(fixture.Input.GetAmount(fixture.Berries).Units, Is.EqualTo(3));
            Assert.That(fixture.Input.GetAmount(fixture.Wood).Units, Is.EqualTo(1));
            Assert.That(fixture.Site.QueuedBatches, Is.EqualTo(1));
        }

        [Test]
        public void QueueCreatesPhysicalRawAndFuelDeliveryJobsWithoutImmediateDeduction()
        {
            var fixture = new Fixture();
            var sourceOwner = new InventoryOwner(InventoryOwnerKind.WorldPile, Fixture.Id(30));
            var source = new ResourceInventory(sourceOwner, fixture.Resources, 20);
            source.TryAdd(fixture.Berries, new ResourceQuantity(9));
            source.TryAdd(fixture.Wood, new ResourceQuantity(3));
            fixture.Inventories.Add(source);
            var work = new WorkManager(new CharacterRegistry());

            Assert.That(fixture.Production.QueueBatchesWithDeliveries(fixture.Building.Id,
                new ProductionRecipeId("cook_berries"), 2, work), Is.EqualTo(2));
            Assert.That(work.Jobs.Select(x => x.ResourceId.Value),
                Is.EquivalentTo(new[] { fixture.Berries, fixture.Wood }));
            Assert.That(work.Jobs.All(x => x.Destination.Value.Kind == WorkTargetKind.ProductionInput), Is.True);
            Assert.That(work.Jobs.Sum(x => x.Quantity.Units), Is.EqualTo(8));
            Assert.That(source.TotalUnits, Is.EqualTo(12), "Queueing reserves work; hauling performs the transfer.");

            Assert.That(fixture.Production.QueueBatchesWithDeliveries(fixture.Building.Id,
                new ProductionRecipeId("cook_berries"), 1, work), Is.EqualTo(2));
            Assert.That(work.Jobs.Sum(x => x.Quantity.Units), Is.EqualTo(12));
        }

        [Test]
        public void AutomaticStockGoalStopsContinuousCookingAfterAWholeBatch()
        {
            var fixture = new Fixture(outputCapacity: 100);
            var worker = fixture.AddWorker(20);
            fixture.Site.SetContinuous(true);
            fixture.Input.TryAdd(fixture.Berries, new ResourceQuantity(18));
            fixture.Input.TryAdd(fixture.Wood, new ResourceQuantity(6));
            fixture.Production.SetTarget(fixture.Cooked, 4);
            fixture.Production.SetStockPolicy(new StockPlanningPolicy(resources: new[] { new StockResourcePolicy(fixture.Cooked, true) }));
            Assert.That(fixture.Production.TryReserveAtSite(worker, fixture.Site.Id, out var job), Is.True);
            fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromDays(1));
            Assert.That(fixture.Output.GetAmount(fixture.Cooked).Units, Is.EqualTo(6), "Finish two whole three-portion batches, then stop.");
            Assert.That(fixture.Input.GetAmount(fixture.Berries).Units, Is.EqualTo(12));
            Assert.That(fixture.Production.ActiveTasks, Is.Empty);
            fixture.Output.TryRemove(fixture.Cooked, new ResourceQuantity(3));
            Assert.That(fixture.Production.TryReserve(worker, default, out _), Is.True, "A later shortage should restart cooking.");
        }

        [Test]
        public void ContinuousDeliveriesPreferStableStorageOverRacingGroundPiles()
        {
            var fixture = new Fixture();
            fixture.Site.SetContinuous(true);
            var pileOwner = new InventoryOwner(InventoryOwnerKind.WorldPile, Fixture.Id(31));
            var pile = new ResourceInventory(pileOwner, fixture.Resources, 20);
            pile.TryAdd(fixture.Berries, new ResourceQuantity(9));
            pile.TryAdd(fixture.Wood, new ResourceQuantity(3));
            fixture.Inventories.Add(pile);
            var storageOwner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, Fixture.Id(32));
            var storage = new ResourceInventory(storageOwner, fixture.Resources, 20);
            storage.TryAdd(fixture.Berries, new ResourceQuantity(9));
            storage.TryAdd(fixture.Wood, new ResourceQuantity(3));
            fixture.Inventories.Add(storage);
            var work = new WorkManager(new CharacterRegistry());

            Assert.That(fixture.Production.MaintainContinuousDeliveries(work), Is.EqualTo(2));
            Assert.That(work.Jobs.All(value => value.Target.Kind == WorkTargetKind.BuildingStorage &&
                value.Target.EntityId == storageOwner.Id), Is.True);
        }

        [Test]
        public void FullOutputPausesCommittedWorkWithoutConsumingOrDuplicatingBatch()
        {
            var fixture = new Fixture(3);
            var worker = fixture.AddWorker(21);
            fixture.Production.QueueBatches(fixture.Building.Id, new ProductionRecipeId("cook_berries"), 1);
            fixture.Input.TryAdd(fixture.Berries, new ResourceQuantity(3));
            fixture.Input.TryAdd(fixture.Wood, new ResourceQuantity(1));
            Assert.That(fixture.Production.TryReserveAtSite(worker, fixture.Site.Id, out var job), Is.True);
            fixture.Output.TryAdd(fixture.Wood, new ResourceQuantity(3));

            Assert.That(fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromMinutes(20)).Completed, Is.False);
            Assert.That(fixture.Input.GetAmount(fixture.Berries).Units, Is.EqualTo(3));
            Assert.That(fixture.Site.QueuedBatches, Is.EqualTo(1));
            fixture.Output.TryRemove(fixture.Wood, new ResourceQuantity(3));
            Assert.That(fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.Zero).Completed, Is.True);
            Assert.That(fixture.Output.GetAmount(fixture.Cooked).Units, Is.EqualTo(3));
            Assert.That(fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromMinutes(20)).Completed, Is.False);
            Assert.That(fixture.Output.GetAmount(fixture.Cooked).Units, Is.EqualTo(3));
        }

        [Test]
        public void WorkplaceSlotPauseAndVersion28RoundTripAreCanonical()
        {
            var fixture = new Fixture();
            var first = fixture.AddWorker(22);
            var second = fixture.AddWorker(23);
            fixture.Production.QueueBatches(fixture.Building.Id, new ProductionRecipeId("cook_berries"), 2);
            fixture.Input.TryAdd(fixture.Berries, new ResourceQuantity(6));
            fixture.Input.TryAdd(fixture.Wood, new ResourceQuantity(2));
            Assert.That(fixture.Production.TryReserveAtSite(first, fixture.Site.Id, out var job), Is.True);
            Assert.That(fixture.Production.TryReserveAtSite(second, fixture.Site.Id, out _), Is.False);
            fixture.Production.AdvanceTask(job.Task.Id, TimeSpan.FromMinutes(7));
            fixture.Production.SetWorkplacePaused(fixture.Building.Id, fixture.Site.RecipeId, true);

            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Fixture.Id(99), new GameTimeState(), 400,
                fixture.Production.CaptureState());
            var decoded = new SaveXmlCodec().Deserialize(new SaveXmlCodec().Serialize(snapshot));
            var restored = new Fixture();
            restored.Production.RestoreState(decoded.Production);

            var site = restored.Production.Sites.Single();
            Assert.That(decoded.FormatVersion, Is.EqualTo(SaveSnapshot.CurrentVersion));
            Assert.That(site.OwnerBuildingId, Is.EqualTo(fixture.Building.Id));
            Assert.That(site.QueuedBatches, Is.EqualTo(2));
            Assert.That(site.Paused, Is.True);
            Assert.That(restored.Production.ActiveTasks.Single().AccumulatedWorkTicks,
                Is.EqualTo(TimeSpan.FromMinutes(7).Ticks));
            Assert.That(restored.Inventories.Get(restored.Production.InputOwner(restored.Building.Id))
                .GetAmount(restored.Berries).Units, Is.EqualTo(6));
        }

        [Test]
        public void ButcherStationConservesOneDressedCarcassIntoMeatAndBone()
        {
            var carcass = new ResourceId("dressed_carcass");
            var meat = new ResourceId("meat");
            var hide = new ResourceId("hide");
            var bone = new ResourceId("bone");
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(carcass, "Carcass", ResourceCategory.Other),
                new ResourceDefinition(meat, "Meat", ResourceCategory.Food),
                new ResourceDefinition(hide, "Hide", ResourceCategory.Other),
                new ResourceDefinition(bone, "Bone", ResourceCategory.Other),
                new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food)
            });
            var inventories = new ResourceInventoryRegistry();
            var buildings = new BuildingRegistry();
            var building = Building.Restore(new BuildingState(Fixture.Id(70),
                new BuildingDefinitionId("butcher_station"), new GridCoordinate(0, 0),
                BuildingOrientation.North, ConstructionState.Completed));
            buildings.Add(building);
            var definition = new BuildingDefinition(new BuildingDefinitionId("butcher_station"), "Butcher",
                BuildingCategory.Food, BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(null, TimeSpan.FromMinutes(1)),
                new PlacementSpec(new BuildingFootprint(3, 3)), workplace: new WorkplaceSpec(1),
                production: new ProductionSpec(new[] { new ProductionRecipeId("butcher_carcass") }, 12, 24));
            var production = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, buildings, new ResourceTransferService(inventories));
            production.RegisterBuildingWorkplace(building.Id, definition, default);
            production.Sites.Single().SetContinuous(false);
            var worker = Fixture.Id(71);
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, worker),
                resources, 10));
            inventories.Get(production.InputOwner(building.Id)).TryAdd(carcass, new ResourceQuantity(1));
            Assert.That(production.QueueBatches(building.Id, new ProductionRecipeId("butcher_carcass"), 1), Is.True);
            Assert.That(production.TryReserveAtBuilding(worker, building.Id, out var job), Is.True);
            Assert.That(production.AdvanceTask(job.Task.Id, TimeSpan.FromMinutes(45)).Completed, Is.True);
            var output = inventories.Get(production.OutputOwner(building.Id));
            Assert.That(output.GetAmount(meat).Units, Is.EqualTo(5));
            Assert.That(output.GetAmount(hide).Units, Is.Zero,"Hide is removed by the earlier dressing stage.");
            Assert.That(output.GetAmount(bone).Units, Is.EqualTo(3));
            Assert.That(inventories.Get(production.InputOwner(building.Id)).GetAmount(carcass).Units, Is.Zero);
        }

        [Test]
        public void StageDPreservationWorkplacesConsumeInputsAndDriedFoodCanBeEaten()
        {
            var wood = new ResourceId("wood_log");
            var stone = new ResourceId("stone");
            var fiber = new ResourceId("plant_fiber");
            var berries = new ResourceId("berries");
            var meat = new ResourceId("meat");
            var hide = new ResourceId("raw_hide");
            var driedMeat = new ResourceId("dried_meat");
            var curedHide = new ResourceId("cured_hide");
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(wood, "Wood", ResourceCategory.Construction),
                new ResourceDefinition(stone, "Stone", ResourceCategory.Construction),
                new ResourceDefinition(fiber, "Fiber", ResourceCategory.Construction),
                new ResourceDefinition(berries, "Berries", ResourceCategory.Food),
                new ResourceDefinition(meat, "Meat", ResourceCategory.Food),
                new ResourceDefinition(hide, "Hide", ResourceCategory.Other),
                new ResourceDefinition(driedMeat, "Dried Meat", ResourceCategory.Food),
                new ResourceDefinition(curedHide, "Cured Hide", ResourceCategory.Other)
            });
            var inventories = new ResourceInventoryRegistry();
            var buildings = new BuildingRegistry();
            var production = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, buildings, new ResourceTransferService(inventories));
            var drying = AddWorkplace(buildings, production, "drying_rack", "dry_meat", 80);
            var hides = AddWorkplace(buildings, production, "hide_workshop", "cure_hide", 81);
            var dryWorker = AddWorker(inventories, resources, 82);
            var hideWorker = AddWorker(inventories, resources, 83);

            inventories.Get(production.InputOwner(drying.Id)).TryAdd(meat, new ResourceQuantity(3));
            inventories.Get(production.InputOwner(hides.Id)).TryAdd(hide, new ResourceQuantity(2));
            inventories.Get(production.InputOwner(hides.Id)).TryAdd(fiber, new ResourceQuantity(1));
            Assert.That(production.QueueBatches(drying.Id, new ProductionRecipeId("dry_meat"), 1), Is.True);
            Assert.That(production.QueueBatches(hides.Id, new ProductionRecipeId("cure_hide"), 1), Is.True);
            Assert.That(production.TryReserveAtBuilding(dryWorker, drying.Id, out var dryingJob), Is.True);
            Assert.That(production.TryReserveAtBuilding(hideWorker, hides.Id, out var hideJob), Is.True);
            Assert.That(production.AdvanceTask(dryingJob.Task.Id, TimeSpan.FromHours(2)).Completed, Is.True);
            Assert.That(production.AdvanceTask(hideJob.Task.Id, TimeSpan.FromHours(3)).Completed, Is.True);

            Assert.That(inventories.Get(production.OutputOwner(drying.Id)).GetAmount(driedMeat).Units, Is.EqualTo(2));
            Assert.That(inventories.Get(production.OutputOwner(hides.Id)).GetAmount(curedHide).Units, Is.EqualTo(1));
            var foodStorage = new InventoryOwner(InventoryOwnerKind.BuildingStorage, Fixture.Id(84));
            inventories.Add(new ResourceInventory(foodStorage, resources, 120, new[] { ResourceCategory.Food }));
            Assert.That(new ResourceTransferService(inventories).TransferExact(production.OutputOwner(drying.Id),
                foodStorage, driedMeat, new ResourceQuantity(2)), Is.EqualTo(TransferFailureReason.None));
            Assert.That(production.Food.TryConsumeMeal(), Is.True);
            Assert.That(inventories.Get(foodStorage).GetAmount(driedMeat).Units, Is.EqualTo(1));
        }

        [Test]
        public void FishingRequiresAnOperationalFishingCamp()
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
                new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food),
                new ResourceDefinition(new ResourceId("fish"), "Fish", ResourceCategory.Food)
            });
            var inventories = new ResourceInventoryRegistry(); var buildings = new BuildingRegistry();
            var production = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, buildings, new ResourceTransferService(inventories));
            var recipe = new ProductionRecipeId("fish_water");
            Assert.That(production.IsRecipeAvailable(recipe), Is.False);
            var camp = Building.Restore(new BuildingState(Fixture.Id(90), new BuildingDefinitionId("fishing_camp"),
                default, BuildingOrientation.North, ConstructionState.Completed));
            buildings.Add(camp);
            Assert.That(production.IsRecipeAvailable(recipe), Is.True);
            camp.ApplyDamage(Fixture.Id(91), camp.MaxIntegrity, BuildingDamageType.Generic, 1);
            Assert.That(production.IsRecipeAvailable(recipe), Is.False);
        }

        private static Building AddWorkplace(BuildingRegistry buildings, ProductionService production,
            string buildingId, string recipeId, int identity)
        {
            var building = Building.Restore(new BuildingState(Fixture.Id(identity),
                new BuildingDefinitionId(buildingId), new GridCoordinate(identity, 0), BuildingOrientation.North,
                ConstructionState.Completed));
            buildings.Add(building);
            var definition = new BuildingDefinition(new BuildingDefinitionId(buildingId), buildingId,
                BuildingCategory.Production, BuildingEra.StoneAge, string.Empty, string.Empty, null,
                new ConstructionSpec(null, TimeSpan.FromMinutes(1)), new PlacementSpec(new BuildingFootprint(3, 3)),
                workplace: new WorkplaceSpec(1), production: new ProductionSpec(
                    new[] { new ProductionRecipeId(recipeId) }, 18, 18));
            production.RegisterBuildingWorkplace(building.Id, definition, default);
            production.Sites.Single(x=>x.OwnerBuildingId==building.Id).SetContinuous(false);
            return building;
        }

        private static StableEntityId AddWorker(ResourceInventoryRegistry inventories, ResourceCatalog resources,
            int identity)
        {
            var id = Fixture.Id(identity);
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, id), resources, 8));
            return id;
        }
    }
}
