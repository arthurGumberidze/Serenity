using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Simulation.AI;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class P109HarvestTests
    {
        [Test]
        public void EveryCatchCreatesASeparateBankPileAndHaulTakesOnlyAvailableStorageSpace()
        {
            var resources = new ResourceCatalog(new[] { "wood_log", "stone", "plant_fiber", "berries", "fish" }
                .Select(x => new ResourceDefinition(new ResourceId(x), x, ResourceCategory.Construction)));
            var inventories = new ResourceInventoryRegistry();
            var worker = StableEntityId.NewId();
            var carrier = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.CarryLoad, worker), resources, 12);
            inventories.Add(carrier);
            var buildings = new BuildingRegistry();
            var storage = Building.CreateNew(new BuildingDefinitionId("storage_basket"), new GridCoordinate(0, 0), BuildingOrientation.North);
            storage.MarkCompleted();
            buildings.Add(storage);
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, storage.Id), resources, 2));
            var piles = new WorldPileService(resources, inventories);
            var production = new ProductionService(ProductionCatalog.CreatePlayablePatch(resources), resources,
                inventories, buildings, new ResourceTransferService(inventories));
            var bank = new GridCoordinate(10, 12);
            production.UseGroundOutputs(piles, point => new GridCoordinate((int)point.X, (int)point.Z));
            var source = ProductionSite.CreateNatural(new ProductionRecipeId("fish_water"), new WorldPosition(10, 12, 12), 48);
            production.AddSite(source);
            for (var catchIndex = 0; catchIndex < 2; catchIndex++)
            {
                Assert.That(production.TryReserveAtSite(worker, source.Id, out var work), Is.True);
                var result = production.AdvanceTask(work.Task.Id, TimeSpan.FromMinutes(20));
                Assert.That(result.Completed, Is.True);
                Assert.That(result.OutputOwner.Kind, Is.EqualTo(InventoryOwnerKind.WorldPile));
                Assert.That(carrier.TotalUnits, Is.Zero);
            }
            Assert.That(piles.All.Count, Is.EqualTo(2));
            Assert.That(piles.All.All(x => x.Coordinate.Equals(bank)), Is.True);
            var fish = new ResourceId("fish");
            Assert.That(piles.All.Sum(x => inventories.Get(x.InventoryOwner).GetAmount(fish).Units), Is.EqualTo(8));
            var query = new HaulWorldQuery(inventories, piles, buildings, new HaulClaimRegistry());
            Assert.That(query.TryClaim(new Tier1AiAgentState(worker, source.Position), 12, out var claim, out _), Is.True);
            Assert.That(claim.Quantity.Units, Is.EqualTo(2));
            Assert.That(new ResourceTransferService(inventories).TransferExact(claim.Source, carrier.Owner,
                fish, claim.Quantity), Is.EqualTo(TransferFailureReason.None));
            Assert.That(piles.All.Sum(x => inventories.Get(x.InventoryOwner).GetAmount(fish).Units), Is.EqualTo(6));
        }

        [Test]
        public void HarvestLeavesGroundPileAndHaulReservesOnlyStorageSpace()
        {
            var resources = new ResourceCatalog(new[] { "wood_log", "stone", "plant_fiber", "berries" }
                .Select(x => new ResourceDefinition(new ResourceId(x), x, ResourceCategory.Construction)));
            var inventories = new ResourceInventoryRegistry();
            var worker = StableEntityId.NewId();
            var carrier = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.CarryLoad, worker), resources, 12);
            inventories.Add(carrier);
            var buildings = new BuildingRegistry();
            var storage = Building.CreateNew(new BuildingDefinitionId("storage_basket"), new GridCoordinate(4, 4), BuildingOrientation.North);
            storage.MarkCompleted();
            buildings.Add(storage);
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage, storage.Id), resources, 5));
            var piles = new WorldPileService(resources, inventories);
            var production = new ProductionService(ProductionCatalog.CreatePlayablePatch(resources), resources,
                inventories, buildings, new ResourceTransferService(inventories));
            production.UseGroundOutputs(piles, _ => new GridCoordinate(10, 10));
            var tree = ProductionSite.CreateNatural(new ProductionRecipeId("chop_wood"), new WorldPosition(10, 0, 10), 24);
            production.AddSite(tree);
            Assert.That(production.TryReserveAtSite(worker, tree.Id, out var work), Is.True);
            var result = production.AdvanceTask(work.Task.Id, TimeSpan.FromHours(1));
            Assert.That(result.Completed, Is.True);
            Assert.That(result.OutputOwner.Kind, Is.EqualTo(InventoryOwnerKind.WorldPile));
            Assert.That(carrier.TotalUnits, Is.Zero);
            var pile = piles.All.Single();
            var wood = new ResourceId("wood_log");
            Assert.That(inventories.Get(pile.InventoryOwner).GetAmount(wood).Units, Is.EqualTo(24));
            var claims = new HaulClaimRegistry();
            var query = new HaulWorldQuery(inventories, piles, buildings, claims);
            var agent = new Tier1AiAgentState(worker, tree.Position);
            Assert.That(query.TryClaim(agent, 12, out var candidate, out _), Is.True);
            Assert.That(candidate.Quantity.Units, Is.EqualTo(5));
            Assert.That(new ResourceTransferService(inventories).TransferExact(candidate.Source, carrier.Owner,
                wood, candidate.Quantity), Is.EqualTo(TransferFailureReason.None));
            Assert.That(inventories.Get(pile.InventoryOwner).GetAmount(wood).Units, Is.EqualTo(19));
            query.DropTransportLoad(worker, tree.Position);
            Assert.That(carrier.TotalUnits, Is.Zero);
            Assert.That(piles.All.Sum(x => inventories.Get(x.InventoryOwner).GetAmount(wood).Units), Is.EqualTo(24));
        }

        [Test]
        public void WholeTreeIsReservedOnceAndCommitsExactQuantityOnce()
        {
            var resources = new ResourceCatalog(new[] { "wood_log", "stone", "plant_fiber", "berries" }
                .Select(x => new ResourceDefinition(new ResourceId(x), x, ResourceCategory.Construction)));
            var catalog = ProductionCatalog.CreatePlayablePatch(resources);
            var inventories = new ResourceInventoryRegistry();
            var first = StableEntityId.Parse("00000000000000000000000000000001");
            var second = StableEntityId.Parse("00000000000000000000000000000002");
            foreach (var id in new[] { first, second })
                inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, id), resources, 128));
            var production = new ProductionService(catalog, resources, inventories,
                new BuildingRegistry(), new ResourceTransferService(inventories));
            var tree = ProductionSite.CreateNatural(new ProductionRecipeId("chop_wood"), default(WorldPosition), 24);
            production.AddSite(tree);
            Assert.That(production.TryReserveAtSite(first, tree.Id, out var job), Is.True);
            Assert.That(production.TryReserveAtSite(second, tree.Id, out _), Is.False);
            Assert.That(production.AdvanceTask(job.Task.Id, TimeSpan.FromMinutes(15)).Completed, Is.False);
            Assert.That(tree.RemainingAmount, Is.EqualTo(24));
            var result = production.AdvanceTask(job.Task.Id, TimeSpan.FromMinutes(15));
            Assert.That(result.Completed, Is.True);
            Assert.That(result.Units, Is.EqualTo(24));
            Assert.That(tree.IsDepleted, Is.True);
            Assert.That(production.AdvanceTask(job.Task.Id, TimeSpan.FromDays(1)).Completed, Is.False);
            Assert.That(production.GetProducedTotal(new ResourceId("wood_log")), Is.EqualTo(24));
            Assert.That(ProductionCatalog.CreateStoneAge(resources).Get(tree.RecipeId).HarvestEntireSource, Is.False,
                "Legacy balance remains a separate catalog.");
        }
    }
}
