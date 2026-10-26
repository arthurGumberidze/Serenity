using System;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Production;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Simulation.Production;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM07")]
    public sealed class WM07WorldMapFisheryTests
    {
        private const long Seed = 20261023L;
        private TiledWorldDefinition world;
        private TiledWorldHeightSampler heights;
        private TiledWorldPopulationPlan plan;

        [OneTimeSetUp]
        public void SetUpPlan()
        {
            world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            heights = new TiledWorldHeightSampler(world, Seed);
            plan = new TiledWorldPopulationPlanner(world, heights, Seed).Create();
        }

        [Test]
        public void ContractContainsExactlySixteenLakeAndEightRiverPopulations()
        {
            var fishery = world.MapData.Resources.Fishery;
            Assert.That(fishery.GrowthModel, Is.EqualTo("logistic_growth_with_harvest_pressure"));
            Assert.That(fishery.Populations, Has.Count.EqualTo(24));
            Assert.That(fishery.Populations.Count(value => !value.IsRiver), Is.EqualTo(16));
            Assert.That(fishery.Populations.Count(value => value.IsRiver), Is.EqualTo(8));
            Assert.That(fishery.Populations.Where(value => !value.IsRiver)
                .All(value => value.CarryingCapacityUnits == 48), Is.True);
            Assert.That(fishery.Populations.Where(value => value.IsRiver)
                .All(value => value.CarryingCapacityUnits == 42), Is.True);
            foreach (var lake in world.MapData.Hydrology.Lakes)
                Assert.That(fishery.Populations.Count(value => !value.IsRiver && value.WaterBodyId == lake.Id),
                    Is.EqualTo(4), lake.Id);
        }

        [Test]
        public void PlannerCreatesStableDryReachableLandingsWithWaterVisuals()
        {
            var nodes = plan.Resources.Where(value => value.ResourceTypeId == "fish").ToArray();
            Assert.That(nodes, Has.Length.EqualTo(5));
            Assert.That(nodes.Select(value => value.StableKey).Distinct().Count(), Is.EqualTo(5));
            Assert.That(nodes.Select(value => value.CreateStableId(Seed, world.GenerationId)).Distinct().Count(),
                Is.EqualTo(5));
            foreach (var node in nodes)
            {
                Assert.That(heights.IsWater(node.Position.X, node.Position.Z), Is.False, node.StableKey);
                Assert.That(heights.IsBuildable(node.Position.X, node.Position.Z, 30f), Is.True, node.StableKey);
                Assert.That(HasDryApproach(node.Position), Is.True, node.StableKey);
                Assert.That(heights.IsWater(node.VisualPosition.X, node.VisualPosition.Z), Is.True, node.StableKey);
                Assert.That(node.Capacity, Is.GreaterThanOrEqualTo(2000), node.StableKey);
                Assert.That(node.CarryingCapacityGrowthPermillePerDay, Is.Zero, node.StableKey);
                Assert.That(node.MinimumRecoveryUnits, Is.Zero, node.StableKey);
            }
        }

        [Test]
        public void EveryLakeHasExactlyOneSharedCanonicalPopulation()
        {
            var nodes=plan.Resources.Where(value=>value.ResourceTypeId=="fish").ToArray();
            foreach(var lake in world.MapData.Hydrology.Lakes)
                Assert.That(nodes.Count(value=>value.SourceZoneId==lake.Id),Is.EqualTo(1),lake.Id);
        }

        [Test]
        public void FiniteFishStockDoesNotRegenerateWithCalendarTime()
        {
            var node = plan.Resources.First(value => value.ResourceTypeId == "fish");
            var site = ProductionSite.Restore(new ProductionSiteState(
                node.CreateStableId(Seed, world.GenerationId), new ProductionRecipeId(node.RecipeId),
                ProductionSiteKind.NaturalSource, node.Position, node.Capacity, node.Capacity,
                CropPlotPhase.Unplanted, 0, 0));
            for (var i = 0; i < 12; i++) site.CompleteWork(ProductionTaskKind.Gather);
            var depletedAmount = site.RemainingAmount;
            var ecology = new ResourceEcologyService();
            ecology.Register(new NaturalNodeRegistration(site, node.Biome, new[] { node.Position }, false));
            ecology.AdvanceTo(0, new FreeSurface());
            ecology.AdvanceTo(TimeSpan.TicksPerDay, new FreeSurface());
            Assert.That(site.RemainingAmount, Is.EqualTo(depletedAmount));
            ecology.AdvanceTo(TimeSpan.TicksPerDay * 200L, new FreeSurface());
            Assert.That(site.RemainingAmount, Is.EqualTo(depletedAmount));
            ecology.AdvanceTo(TimeSpan.TicksPerDay * 201L, new FreeSurface());
            Assert.That(site.RemainingAmount, Is.EqualTo(depletedAmount));
        }

        [Test]
        public void ExistingAuditedFishPrefabIsReused()
        {
            var art = AssetDatabase.LoadAssetAtPath<WorldMapResourceArtCatalog>(
                "Assets/Game/Resources/World/WorldMapResourceArt.asset");
            var fish = art.PrefabFor("fish");
            Assert.That(fish, Is.Not.Null);
            Assert.That(fish.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            Assert.That(AssetDatabase.GetAssetPath(fish),
                Is.EqualTo("Assets/PolygonNatureBiomes/PNB_Tropical_Jungle/FX/FX_Prefabs/FX_Fish_Small_01.prefab"));
        }

        private bool HasDryApproach(WorldPosition position)
        {
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Math.PI * 0.25d;
                var x = position.X + (float)Math.Cos(angle) * 3f;
                var z = position.Z + (float)Math.Sin(angle) * 3f;
                if (heights.IsBuildable(x, z, 30f) &&
                    Math.Abs(heights.SampleHeight(x, z) - position.Y) <= 1.5f) return true;
            }
            return false;
        }

        private static float DistanceSquared(WorldPosition value, WorldPosition other)
        {
            var dx = value.X - other.X;
            var dz = value.Z - other.Z;
            return dx * dx + dz * dz;
        }

        private sealed class FreeSurface : IResourceRespawnSurface
        {
            public bool IsFree(NaturalResourceBiome biome, WorldPosition position, float footprintRadius) => true;
        }
    }
}
