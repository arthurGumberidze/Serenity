using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Work;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Simulation.AI;
using Game.Simulation.Buildings;
using Game.Simulation.Production;
using Game.Simulation.Resources;
using Game.Simulation.Work;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class P109ConstructionClearanceTests
    {
        private static readonly ResourceId Wood = new ResourceId("wood_log");

        [Test, Category("WM12")]
        public void PlacementEvaluationDistinguishesFreeClearanceAndBlockedSites()
        {
            var blocker = Id(1);
            var free = new PlacementSurfaceEvaluation(true, null);
            var clearance = new PlacementSurfaceEvaluation(true, new[] { blocker });
            var blocked = new PlacementSurfaceEvaluation(false, null, "Участок занят.");

            Assert.That(free.CanPlace, Is.True);
            Assert.That(free.RequiresClearance, Is.False);
            Assert.That(clearance.CanPlace, Is.True);
            Assert.That(clearance.RequiresClearance, Is.True);
            Assert.That(clearance.ClearableSources, Is.EquivalentTo(new[] { blocker }));
            Assert.That(blocked.CanPlace, Is.False);
            Assert.That(blocked.RequiresClearance, Is.False);
            Assert.That(blocked.BlockedReason, Is.EqualTo("Участок занят."));
        }

        [Test]
        public void FreeSiteSchedulesDeliveryAndBuildImmediately()
        {
            var fixture = CreateFixture();

            fixture.Clearance.Register(fixture.Building, Array.Empty<StableEntityId>(), WorkPriority.Normal);

            Assert.That(fixture.Work.Jobs.Any(x => x.Type == WorkJobType.Haul && !x.IsTerminal), Is.True);
            Assert.That(fixture.Work.Jobs.Any(x => x.Type == WorkJobType.Build && !x.IsTerminal), Is.True);
            Assert.That(fixture.Clearance.TryGetStatus(fixture.Building.Id, out var status), Is.True);
            Assert.That(status.IsClearing, Is.False);
            Assert.That(status.DeliveriesScheduled, Is.True);
            Assert.That(status.BuildScheduled, Is.True);
        }

        [Test]
        public void ClearedSiteWaitsWithoutBuildJobWhenMaterialsAreShort()
        {
            var fixture = CreateFixture(fundingUnits: 1);

            fixture.Clearance.Register(fixture.Building, Array.Empty<StableEntityId>(), WorkPriority.Normal);

            Assert.That(fixture.Work.Jobs.Any(x => !x.IsTerminal &&
                (x.Type == WorkJobType.Haul || x.Type == WorkJobType.Build)), Is.False);
            Assert.That(fixture.Clearance.TryGetStatus(fixture.Building.Id, out var status), Is.True);
            Assert.That(status.DeliveriesScheduled, Is.False);
            Assert.That(status.BuildScheduled, Is.False);
        }

        [Test, Category("WM12")]
        public void BlockedSiteClearsBeforeLogisticsAndBuild()
        {
            var fixture = CreateFixture(withBlocker: true);

            fixture.Clearance.Register(fixture.Building, new[] { fixture.Blocker.Id }, WorkPriority.High);

            Assert.That(fixture.Work.Jobs.Single(x => x.Target.EntityId == fixture.Blocker.Id).Type,
                Is.EqualTo(WorkJobType.Chop));
            Assert.That(fixture.Work.Jobs.Any(x => x.Type == WorkJobType.Haul), Is.False);
            Assert.That(fixture.Work.Jobs.Any(x => x.Type == WorkJobType.Build), Is.False);

            fixture.Blocker.CompleteWork(ProductionTaskKind.Gather, harvestEntireSource: true);
            fixture.Clearance.Advance();

            Assert.That(fixture.Work.Jobs.Any(x => x.Type == WorkJobType.Haul && !x.IsTerminal), Is.True);
            Assert.That(fixture.Work.Jobs.Any(x => x.Type == WorkJobType.Build && !x.IsTerminal), Is.True);
        }

        [Test, Category("WM12")]
        public void AuthoredPolicyRejectsNonClearableSourceWithoutCreatingWorkOrChangingStock()
        {
            var policy = TiledWorldDefinitionLoader.LoadProposedWorldMap().MapData.Buildability;
            var fixture = CreateFixture(withBlocker: true, blockerRecipe: "gather_medicinal_plants",
                clearancePolicy: site => policy.IsClearableRecipe(site.RecipeId.ToString()));

            Assert.Throws<InvalidOperationException>(() => fixture.Clearance.Register(fixture.Building,
                new[] { fixture.Blocker.Id }, WorkPriority.Urgent));

            Assert.That(fixture.Work.Jobs, Is.Empty);
            Assert.That(fixture.Blocker.RemainingAmount, Is.EqualTo(1));
            Assert.That(fixture.Clearance.ActiveSiteCount, Is.Zero);
        }

        [Test]
        public void PriorityPropagatesToClearanceDeliveryAndBuildJobs()
        {
            var fixture = CreateFixture(withBlocker: true);
            fixture.Clearance.Register(fixture.Building, new[] { fixture.Blocker.Id }, WorkPriority.Low);

            Assert.That(fixture.Clearance.SetPriority(fixture.Building.Id, WorkPriority.Urgent), Is.True);
            Assert.That(fixture.Work.Jobs.Where(x => !x.IsTerminal).All(x => x.Priority == WorkPriority.Urgent),
                Is.True);

            fixture.Blocker.CompleteWork(ProductionTaskKind.Gather, harvestEntireSource: true);
            fixture.Clearance.Advance();

            Assert.That(fixture.Work.Jobs.Where(x => !x.IsTerminal).All(x => x.Priority == WorkPriority.Urgent),
                Is.True);
        }

        [Test]
        public void CancelStopsEveryLinkedJob()
        {
            var fixture = CreateFixture(withBlocker: true);
            fixture.Clearance.Register(fixture.Building, new[] { fixture.Blocker.Id }, WorkPriority.Normal);

            fixture.Clearance.Cancel(fixture.Building.Id);

            Assert.That(fixture.Clearance.ActiveSiteCount, Is.Zero);
            Assert.That(fixture.Work.Jobs.Where(x => x.Target.EntityId == fixture.Blocker.Id)
                .All(x => x.Status == WorkJobStatus.Cancelled), Is.True);
        }

        [Test]
        public void RebindAfterLoadDerivesBlockersAndAvoidsDuplicateExclusiveJobs()
        {
            var fixture = CreateFixture(withBlocker: true);
            fixture.Clearance.Register(fixture.Building, new[] { fixture.Blocker.Id }, WorkPriority.High);
            var firstCount = fixture.Work.Jobs.Count(x => !x.IsTerminal && x.Target.EntityId == fixture.Blocker.Id);

            fixture.Clearance.RebindAfterLoad(new[] { fixture.Building });

            Assert.That(fixture.Work.Jobs.Count(x => !x.IsTerminal && x.Target.EntityId == fixture.Blocker.Id),
                Is.EqualTo(firstCount));
            Assert.That(fixture.Clearance.TryGetStatus(fixture.Building.Id, out var status), Is.True);
            Assert.That(status.Remaining, Is.EqualTo(1));
        }

        private static Fixture CreateFixture(bool withBlocker = false, long fundingUnits = 20,
            string blockerRecipe = "chop_wood", Func<ProductionSite, bool> clearancePolicy = null)
        {
            var resources = new ResourceCatalog(new[]
            {
                new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction)
            });
            var inventories = new ResourceInventoryRegistry();
            var piles = new WorldPileService(resources, inventories);
            var pile = piles.Create(Wood, new ResourceQuantity(fundingUnits), new GridCoordinate(-2, 0), Id(10));
            var buildings = new BuildingRegistry();
            var occupancy = new BuildingOccupancyGrid();
            var placement = new BuildingPlacementService(buildings, occupancy,
                new BuildingGridBounds(-20, -20, 20, 20));
            var definition = Definition();
            var building = Building.CreateNew(definition, new GridCoordinate(0, 0), BuildingOrientation.North,
                new CombatSideId("player"), Id(20));
            buildings.Add(building);
            var sites = new ConstructionSiteService(resources, inventories);
            sites.Initialize(building, definition, new SettlementId(Id(30)));
            var work = new WorkManager(new CharacterRegistry());
            var funding = new ConstructionFundingService(placement, inventories, resources,
                new StorageService(inventories, resources)) { RequiresWorker = true };
            funding.ConfigureLogistics(sites, work, piles, new SettlementId(Id(30)), new HaulClaimRegistry());

            var recipe = new ProductionRecipe(new ProductionRecipeId(blockerRecipe), "Clear blocker", null,
                new[] { new ResourceAmount(Wood, new ResourceQuantity(1)) }, TimeSpan.FromHours(1),
                ProductionSiteKind.NaturalSource, harvestEntireSource: true);
            var production = new ProductionService(new ProductionCatalog(new[] { recipe }, resources), resources,
                inventories, buildings, new ResourceTransferService(inventories),
                new ProductionSettings(TimeSpan.FromHours(1), TimeSpan.FromDays(1), TimeSpan.FromDays(1),
                    Array.Empty<ResourceStockTarget>()));
            ProductionSite blocker = null;
            if (withBlocker)
            {
                blocker = ProductionSite.Restore(new ProductionSiteState(Id(40),
                    new ProductionRecipeId(blockerRecipe), ProductionSiteKind.NaturalSource,
                    new WorldPosition(0, 0, 0), 1, 1, CropPlotPhase.Unplanted, 0, 0));
                production.AddSite(blocker);
            }

            var clearance = new ConstructionClearanceCoordinator(buildings, production, work, funding,
                _ => definition, () => new[] { pile.InventoryOwner },
                value => new WorldPosition(value.Coordinate.X, 0, value.Coordinate.Z),
                1f, new WorldPosition(0, 0, 0), clearancePolicy);
            return new Fixture(building, blocker, work, clearance);
        }

        private static BuildingDefinition Definition() => new BuildingDefinition(
            new BuildingDefinitionId("clearance_test_hut"), "Clearance hut", BuildingCategory.Shelter,
            BuildingEra.StoneAge, "", "", null,
            new ConstructionSpec(new[]
            {
                new ResourceAmount(Wood, new ResourceQuantity(3))
            }, TimeSpan.FromTicks(20), 1),
            new PlacementSpec(new BuildingFootprint(2, 2)));

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));

        private sealed class Fixture
        {
            public Fixture(Building building, ProductionSite blocker, WorkManager work,
                ConstructionClearanceCoordinator clearance)
            {
                Building = building;
                Blocker = blocker;
                Work = work;
                Clearance = clearance;
            }

            public Building Building { get; }
            public ProductionSite Blocker { get; }
            public WorkManager Work { get; }
            public ConstructionClearanceCoordinator Clearance { get; }
        }
    }
}
