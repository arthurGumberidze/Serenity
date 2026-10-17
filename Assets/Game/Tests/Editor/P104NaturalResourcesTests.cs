using System;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Domain.Research;
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
    [Category("P104")]
    public sealed class P104NaturalResourcesTests
    {
        private sealed class RespawnSurface : IResourceRespawnSurface
        {
            public bool Free;
            public int Calls;
            public bool IsFree(NaturalResourceBiome biome, WorldPosition position, float footprintRadius)
            { Calls++; return Free; }
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
        private static ResourceAmount Amount(string id, long units) =>
            new ResourceAmount(new ResourceId(id), new ResourceQuantity(units));

        private static ResourceCatalog FullCatalog() => new ResourceCatalog(new[]
        {
            new ResourceDefinition(new ResourceId("wood_log"), "Wood", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("plant_fiber"), "Fiber", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("hide"), "Hide", ResourceCategory.Other),
            new ResourceDefinition(new ResourceId("berries"), "Berries", ResourceCategory.Food),
            new ResourceDefinition(new ResourceId("iron_ore"), "Iron", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("gold_ore"), "Gold", ResourceCategory.Other),
            new ResourceDefinition(new ResourceId("meat"), "Meat", ResourceCategory.Food),
            new ResourceDefinition(new ResourceId("fish"), "Fish", ResourceCategory.Food)
        });

        [Test]
        public void CanonicalCatalogContainsExactlyNineResourcesAndAnimalWaterRecipes()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(
                "Assets/Game/Art/Config/ResourceCatalog.asset");
            var resources = asset.CreateCatalog();
            Assert.That(resources.All.Select(x => x.Id.ToString()), Is.EquivalentTo(new[]
            {
                "wood_log", "stone", "plant_fiber", "hide", "berries", "iron_ore", "gold_ore", "meat", "fish"
            }));
            var recipes = ProductionCatalog.CreateStoneAge(resources);
            Assert.That(recipes.Get(new ProductionRecipeId("hunt_animal")).Outputs
                .Select(x => x.ResourceId.ToString()), Is.EquivalentTo(new[] { "hide", "meat" }));
            Assert.That(recipes.Get(new ProductionRecipeId("fish_water")).Outputs.Single().ResourceId.ToString(),
                Is.EqualTo("fish"));
        }

        [Test]
        public void SmallNodeValidationAndBlockedFifteenDayRespawnAreExact()
        {
            var site = ProductionSite.Restore(new ProductionSiteState(Id(1), new ProductionRecipeId("chop_wood"),
                ProductionSiteKind.NaturalSource, new WorldPosition(0, 0, 0), 20, 20,
                CropPlotPhase.Unplanted, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NaturalNodeRegistration(
                ProductionSite.Restore(new ProductionSiteState(Id(2), new ProductionRecipeId("chop_wood"),
                    ProductionSiteKind.NaturalSource, default, 51, 51, CropPlotPhase.Unplanted, 0, 0)),
                NaturalResourceBiome.Forest, new[] { default(WorldPosition) }));
            var ecology = new ResourceEcologyService();
            ecology.Register(new NaturalNodeRegistration(site, NaturalResourceBiome.Forest,
                new[] { new WorldPosition(5, 0, 0) }));
            for (var i = 0; i < 20; i++) site.CompleteWork(ProductionTaskKind.Gather);
            var surface = new RespawnSurface();
            ecology.AdvanceTo(0, surface);
            ecology.AdvanceTo(TimeSpan.FromDays(15).Ticks - 1, surface);
            Assert.That(surface.Calls, Is.Zero);
            ecology.AdvanceTo(TimeSpan.FromDays(15).Ticks, surface);
            Assert.That(site.IsDepleted, Is.True, "An occupied candidate must delay respawn without overlap.");
            surface.Free = true;
            ecology.AdvanceTo(TimeSpan.FromDays(15).Ticks + 1, surface);
            Assert.That(site.RemainingAmount, Is.EqualTo(20));
            Assert.That(site.Position.X, Is.EqualTo(5));
        }

        [Test]
        public void SevenEquivalentWorkersProduceExactlySevenTimesOneWorker()
        {
            Assert.That(ControlledWoodOutput(1), Is.EqualTo(4));
            Assert.That(ControlledWoodOutput(7), Is.EqualTo(28));
            Assert.That(ControlledWoodOutput(7), Is.EqualTo(ControlledWoodOutput(1) * 7));
        }

        [Test]
        public void IndustrialFacilitiesEnforceTechPlacementCostStorageCapacityAndRestoration()
        {
            var resources = FullCatalog();
            var inventories = new ResourceInventoryRegistry();
            var fundingOwner = new InventoryOwner(InventoryOwnerKind.Character, Id(10));
            var funding = new ResourceInventory(fundingOwner, resources, 100);
            inventories.Add(funding);
            funding.TryAdd(new ResourceId("wood_log"), new ResourceQuantity(20));
            funding.TryAdd(new ResourceId("stone"), new ResourceQuantity(20));
            var storageId = Id(11);
            inventories.Add(new ResourceInventory(
                new InventoryOwner(InventoryOwnerKind.BuildingStorage, storageId), resources, 100));
            var mine = new IndustrialZoneDefinition("test_mine", IndustrialFacilityKind.Mine,
                NaturalResourceBiome.Mountain, new[] { Amount("wood_log", 4), Amount("stone", 3) },
                new[] { Amount("iron_ore", 1) }, 8, 7, TimeSpan.FromDays(1),
                new TechnologyId("industrial_extraction"), new TechnologyId("mine_restoration"), 2);
            var workshop = new IndustrialZoneDefinition("test_workshop", IndustrialFacilityKind.Workshop,
                NaturalResourceBiome.Forest, Array.Empty<ResourceAmount>(), new[] { Amount("wood_log", 1) },
                10, 7, TimeSpan.FromDays(1), new TechnologyId("industrial_extraction"));
            var service = new IndustrialExtractionService(resources, inventories, new[] { mine, workshop });
            service.AddZone(Id(20), mine.Id, default);
            service.AddZone(Id(21), workshop.Id, default);

            Assert.That(service.BuildFacility(Id(20), fundingOwner, storageId, false, true),
                Is.EqualTo(IndustrialCommandFailure.TechnologyLocked));
            Assert.That(service.BuildFacility(Id(20), fundingOwner, storageId, true, false),
                Is.EqualTo(IndustrialCommandFailure.InvalidPlacement));
            Assert.That(service.BuildFacility(Id(20), fundingOwner, Id(99), true, true),
                Is.EqualTo(IndustrialCommandFailure.MissingStorage));
            Assert.That(service.BuildFacility(Id(20), fundingOwner, storageId, true, true),
                Is.EqualTo(IndustrialCommandFailure.None));
            Assert.That(service.Advance(Id(20), 8, TimeSpan.FromDays(1)).Failure,
                Is.EqualTo(IndustrialCommandFailure.WorkerCapacity));
            Assert.That(service.Advance(Id(20), 7, TimeSpan.FromDays(1)).Cycles, Is.EqualTo(7));
            Assert.That(service.Advance(Id(20), 1, TimeSpan.FromDays(1)).Cycles, Is.EqualTo(1));
            Assert.That(service.TryGetZone(Id(20), out var exhausted), Is.True);
            Assert.That(exhausted.Remaining, Is.Zero);
            Assert.That(service.InstallRestorativeBlock(Id(20), false),
                Is.EqualTo(IndustrialCommandFailure.TechnologyLocked));
            Assert.That(service.InstallRestorativeBlock(Id(20), true), Is.EqualTo(IndustrialCommandFailure.None));
            var restoredAndMined = service.Advance(Id(20), 1, TimeSpan.FromDays(1));
            Assert.That(restoredAndMined.Restored, Is.EqualTo(2));
            Assert.That(restoredAndMined.Cycles, Is.EqualTo(1));
            Assert.That(service.TryGetZone(Id(20), out var replenished), Is.True);
            Assert.That(replenished.Remaining, Is.EqualTo(1));
            Assert.That(service.BuildFacility(Id(21), fundingOwner, storageId, true, true),
                Is.EqualTo(IndustrialCommandFailure.None));
            Assert.That(service.InstallRestorativeBlock(Id(21), true),
                Is.EqualTo(IndustrialCommandFailure.RestorativeMineOnly));
        }

        [Test]
        public void VersionTwentyTwoRoundTripPreservesEcologyAndIndustryWhileV21RemainsReadable()
        {
            var node = new NaturalNodeLifecycleState(Id(30), NaturalResourceBiome.Wetland,
                TimeSpan.FromDays(4).Ticks, 3);
            var zone = new IndustrialZoneState(Id(31), "iron_mine", new WorldPosition(2, 3, 4),
                77, true, Id(32), true, 5, 6);
            var production = new ProductionSaveState(12, 1, 2, 3, null, null, null, null, null,
                new[] { node }, new IndustrialSaveState(new[] { zone }));
            var codec = new SaveXmlCodec();
            var snapshot = new SaveSnapshot(SaveSnapshot.CurrentVersion, Id(40),
                new GameTimeState(10, 20, 1, false), 400, production);
            var restored = codec.Deserialize(codec.Serialize(snapshot));
            Assert.That(restored.FormatVersion, Is.EqualTo(SaveSnapshot.ResourceIndustryVersion));
            Assert.That(restored.Production.NaturalLifecycles.Single().RespawnAttempt, Is.EqualTo(3));
            Assert.That(restored.Production.Industry.Zones.Single().Remaining, Is.EqualTo(77));
            Assert.That(restored.Production.Industry.Zones.Single().RestorativeInstalled, Is.True);

            var legacy = new SaveSnapshot(SaveSnapshot.ContextOrdersVersion, Id(41),
                new GameTimeState(10, 20, 1, false), 400, production);
            var legacyRestored = codec.Deserialize(codec.Serialize(legacy));
            Assert.That(legacyRestored.Production.NaturalLifecycles, Is.Empty);
            Assert.That(legacyRestored.Production.Industry, Is.Null);
        }

        private static long ControlledWoodOutput(int workerCount)
        {
            var resources = FullCatalog();
            var inventories = new ResourceInventoryRegistry();
            var production = new ProductionService(ProductionCatalog.CreateStoneAge(resources), resources,
                inventories, new BuildingRegistry(), new ResourceTransferService(inventories));
            var site = ProductionSite.Restore(new ProductionSiteState(Id(100 + workerCount),
                new ProductionRecipeId("chop_wood"), ProductionSiteKind.NaturalSource, default, 50, 50,
                CropPlotPhase.Unplanted, 0, 0));
            production.AddSite(site);
            for (var i = 0; i < workerCount; i++)
            {
                var worker = Id(200 + i);
                inventories.Add(new ResourceInventory(new InventoryOwner(InventoryOwnerKind.Character, worker),
                    resources, 100));
                Assert.That(production.TryReserveAtSite(worker, site.Id, out var job), Is.True);
                Assert.That(production.AdvanceTask(job.Task.Id, TimeSpan.FromHours(8)).Completed, Is.True);
            }
            return production.GetProducedTotal(new ResourceId("wood_log"));
        }
    }
}
