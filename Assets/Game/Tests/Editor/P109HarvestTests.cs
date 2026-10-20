using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class P109HarvestTests
    {
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
