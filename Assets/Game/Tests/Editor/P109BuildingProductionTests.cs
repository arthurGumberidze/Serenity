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
            Assert.That(decoded.FormatVersion, Is.EqualTo(SaveSnapshot.BuildingProductionVersion));
            Assert.That(site.OwnerBuildingId, Is.EqualTo(fixture.Building.Id));
            Assert.That(site.QueuedBatches, Is.EqualTo(2));
            Assert.That(site.Paused, Is.True);
            Assert.That(restored.Production.ActiveTasks.Single().AccumulatedWorkTicks,
                Is.EqualTo(TimeSpan.FromMinutes(7).Ticks));
            Assert.That(restored.Inventories.Get(restored.Production.InputOwner(restored.Building.Id))
                .GetAmount(restored.Berries).Units, Is.EqualTo(6));
        }
    }
}
