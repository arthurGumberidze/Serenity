using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Time;
using Game.Infrastructure;
using Game.Presentation.Resources;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI08R5")]
    public sealed class StockPlanningTests
    {
        private ResourceCatalog resources;
        private ProductionCatalog recipes;
        private static ResourceId Id(string id) => new ResourceId(id);
        [SetUp] public void Setup()
        {
            resources = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>("Assets/Game/Art/Config/ResourceCatalog.asset").CreateCatalog();
            recipes = ProductionCatalog.CreatePlayablePatch(resources);
        }
        private StockPlanningPolicy Auto(int days = 3, int reserve = 0, bool update = false) =>
            new StockPlanningPolicy(days, reserve, update, 0, resources.All.Where(x => x.Id.ToString() != "water")
                .Select(x => new StockResourcePolicy(x.Id, true)));
        private StockPlan Plan(StockPlanningFacts facts, StockPlanningPolicy policy = null, Func<ProductionRecipeId, bool> available = null) =>
            StockPlanner.Calculate(recipes, available ?? (_ => true), policy ?? Auto(), facts, _ => 0);
        [Test] public void MonthlyMenuIsThirtyDaysThreePortionsAndCyclesUnlockedDishes()
        {
            var plan = Plan(new StockPlanningFacts { Residents = 48 });
            Assert.That(plan.CanApply, Is.True); Assert.That(plan.Portions, Is.EqualTo(4320));
            Assert.That(plan.Menu.Take(2), Is.EqualTo(new[] { "cooked_fish", "cooked_meat" }));
            Assert.That(plan.Menu.Count(x => x == "cooked_fish"), Is.EqualTo(8));
            Assert.That(plan.Menu.Count(x => x == "dried_meat"), Is.EqualTo(7));
            Assert.That(plan.Monthly[Id("cooked_fish")], Is.EqualTo(1152));
            Assert.That(plan.Monthly[Id("meat")], Is.EqualTo(2664));
            Assert.That(plan.Monthly[Id("wood_log")], Is.EqualTo(1488));
        }
        [Test] public void LockedRecipesAreNotMealsAndNoOpenMealBlocksApply()
        {
            var facts = new StockPlanningFacts { Residents = 10 };
            var plan = Plan(facts, available: id => id.ToString() == "cook_fish");
            Assert.That(plan.Menu.Distinct(), Is.EqualTo(new[] { "cooked_fish" }));
            Assert.That(plan.Monthly[Id("cooked_fish")], Is.EqualTo(900));
            Assert.That(Plan(facts, available: _ => false).CanApply, Is.False);
        }
        [Test] public void ReadyMealsReduceIngredientsAndFuelButNotMonthlyGrossDemand()
        {
            var facts = new StockPlanningFacts { Residents = 10 };
            facts.Available[Id("cooked_fish")] = 30;
            var plan = Plan(facts);
            Assert.That(plan.Targets[Id("cooked_fish")], Is.EqualTo(30));
            Assert.That(StockPlanner.Get(plan.Targets, Id("fish")), Is.Zero);
            Assert.That(plan.Targets[Id("wood_log")], Is.EqualTo(25));
            Assert.That(plan.Monthly[Id("cooked_fish")], Is.EqualTo(240));
        }
        [Test] public void SharedFuelBatchesAndReserveAreNotCountedTwice()
        {
            var plan = Plan(new StockPlanningFacts { Residents = 1 }, Auto(reserve: 25));
            Assert.That(plan.Targets[Id("cooked_fish")], Is.EqualTo(4));
            Assert.That(plan.Targets[Id("wood_log")], Is.EqualTo(6));
            Assert.That(plan.Targets.ContainsKey(Id("water")), Is.False);
        }
        [Test] public void FieldCarcassesSubstituteForOrdinaryCarcassesWithoutNewHuntingQuota()
        {
            var facts = new StockPlanningFacts { Residents = 10 };
            facts.Available[Id("field_dressed_carcass")] = 10;
            var plan = Plan(facts);
            Assert.That(plan.Targets[Id("meat")], Is.EqualTo(30));
            Assert.That(StockPlanner.Get(plan.Targets, Id("carcass")), Is.Zero);
            Assert.That(StockPlanner.Get(plan.Targets, Id("field_dressed_carcass")), Is.Zero);
        }
        [Test] public void RolesAreOneTimeNeedsFeedIsDailyAndMonthDoesNotMultiplyEquipment()
        {
            var facts = new StockPlanningFacts { Residents = 10 };
            facts.OneTimeNeeds[Id("combat_bow")] = 2;
            facts.OneTimeNeeds[Id("stone_spear")] = 3;
            facts.DailyNeeds[Id("animal_feed")] = 2;
            var plan = Plan(facts);
            Assert.That(plan.Monthly[Id("combat_bow")], Is.EqualTo(2));
            Assert.That(plan.Monthly[Id("stone_spear")], Is.EqualTo(3));
            Assert.That(plan.Monthly[Id("animal_feed")], Is.EqualTo(60));
            Assert.That(plan.Targets[Id("animal_feed")], Is.EqualTo(6));
        }
        [Test] public void ManualGoalsSurviveApplyAutomaticRefreshAndXmlRoundTrip()
        {
            var inventories = new ResourceInventoryRegistry();
            var service = new ProductionService(recipes, resources, inventories, new BuildingRegistry(), new ResourceTransferService(inventories));
            var residents = 10;
            service.StockFacts = () => new StockPlanningFacts { Residents = residents };
            service.SetTarget(Id("wood_log"), 777);
            var policy = Auto(update: true).WithResource(new StockResourcePolicy(Id("wood_log"), false, 4, 10));
            Assert.That(service.ApplyStockPlan(policy), Is.True);
            Assert.That(service.GetTarget(Id("wood_log")), Is.EqualTo(777));
            residents = 20; service.RefreshAutomaticStocks(TimeSpan.TicksPerHour);
            Assert.That(service.GetTarget(Id("cooked_fish")), Is.EqualTo(60));
            Assert.That(service.GetTarget(Id("wood_log")), Is.EqualTo(777));
            service.SetTarget(Id("cooked_fish"), 123);
            residents = 30; service.RefreshAutomaticStocks(TimeSpan.TicksPerDay);
            Assert.That(service.GetTarget(Id("cooked_fish")), Is.EqualTo(123));
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, StableEntityId.NewId(),
                new GameTimeState(0, 0, 1, false), 400, service.CaptureState());
            var codec = new SaveXmlCodec(); var restored = codec.Deserialize(codec.Serialize(snapshot));
            service.RestoreState(restored.Production);
            Assert.That(service.StockPolicy.For(Id("wood_log")).Priority, Is.EqualTo(4));
            Assert.That(service.StockPolicy.For(Id("wood_log")).ReservePercent, Is.EqualTo(10));
            Assert.That(service.StockPolicy.Menu.Count, Is.EqualTo(30));
            Assert.That(service.StockPolicy.AutoUpdate, Is.True);
            Assert.That(service.GetTarget(Id("cooked_fish")), Is.EqualTo(123));
        }
        [Test] public void MonthRolloverRotatesOperationalMenuAndLockedOverrideIsReplaced()
        {
            var menu = Enumerable.Repeat("cooked_meat", 30).ToArray(); menu[0] = "cooked_fish";
            var policy = new StockPlanningPolicy(1, 0, true, 10, Auto().Resources, menu);
            var plan = Plan(new StockPlanningFacts { Residents = 4, Day = 11 }, policy);
            Assert.That(plan.Targets[Id("cooked_meat")], Is.EqualTo(12));
            Assert.That(StockPlanner.Get(plan.Targets, Id("cooked_fish")), Is.Zero);
            var locked = Plan(new StockPlanningFacts { Residents = 4 }, policy, id => id.ToString() == "cook_fish");
            Assert.That(locked.Menu.All(x => x == "cooked_fish"), Is.True);
        }
        [TestCase(4, "gather_stone")]
        [TestCase(0, "chop_wood")]
        public void ResourcePriorityControlsAutomaticWorkBeforeDistance(int priority, string expected)
        {
            var inventories = new ResourceInventoryRegistry();
            var worker = StableEntityId.NewId();
            inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, worker), resources, 1000));
            var service = new ProductionService(recipes, resources, inventories, new BuildingRegistry(), new ResourceTransferService(inventories));
            foreach (var entry in new[] { ("chop_wood", 1f), ("gather_stone", 100f) })
                service.AddSite(ProductionSite.Restore(new ProductionSiteState(StableEntityId.NewId(), new ProductionRecipeId(entry.Item1),
                    ProductionSiteKind.NaturalSource, new Game.Domain.AI.WorldPosition(entry.Item2, 0, 0), 10, 10, CropPlotPhase.Unplanted, 0, 0)));
            service.SetStockPolicy(new StockPlanningPolicy(resources: new[] { new StockResourcePolicy(Id("stone"), false, priority) }));
            Assert.That(service.TryReserve(worker, default, out var job), Is.True);
            Assert.That(job.Task.RecipeId.ToString(), Is.EqualTo(expected));
        }
    }
}
