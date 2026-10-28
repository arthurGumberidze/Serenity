using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Campaign;
using Game.Domain.Production;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Presentation.Resources;
using Game.Simulation.Production;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM06")]
    public sealed class WM06WorldMapResourcesTests
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
        public void TypedContractRetainsCanonicalMasksAndVisibilityPolicy()
        {
            var resources = world.MapData.Resources;
            Assert.That(resources.Types, Has.Count.EqualTo(14));
            Assert.That(resources.Zones, Has.Count.EqualTo(9));
            Assert.That(resources.Types.Count(value => value.Visible), Is.EqualTo(10));
            Assert.That(resources.Types.Count(value => value.IsHidden), Is.EqualTo(4));
            Assert.That(resources.Zones.Select(value => value.Id),
                Is.EquivalentTo(world.MapData.ResourceMasks.Select(value => value.Id)));
            var oil = resources.Type("oil");
            Assert.That(oil.IsHidden, Is.True);
            Assert.That(oil.UnlockTechnologyId, Is.EqualTo("oil_extraction"));
            Assert.That(resources.Type("wood").RenewalPolicy, Is.EqualTo("sapling_regrowth"));
            Assert.That(resources.Type("stone").RenewalPolicy, Is.EqualTo("none"));
        }

        [Test]
        public void SeededMaskPlanIsDeterministicStableAndTileOrderIndependent()
        {
            var replay = new TiledWorldPopulationPlanner(world,
                new TiledWorldHeightSampler(world, Seed), Seed).Create();
            var other = new TiledWorldPopulationPlanner(world,
                new TiledWorldHeightSampler(world, Seed + 1), Seed + 1).Create();
            var first = Ids(plan, Seed);
            TestContext.WriteLine($"WM06 nodes: total={plan.Resources.Count}, visible={plan.VisibleResources.Count}, " +
                                  $"hidden={plan.HiddenDeposits.Count}, stable-first={first.First()}");
            Assert.That(Ids(replay, Seed), Is.EqualTo(first));
            Assert.That(Ids(other, Seed + 1), Is.Not.EqualTo(first));
            Assert.That(first.Distinct().Count(), Is.EqualTo(first.Length));
            var reverseTiles = world.Tiles.Reverse()
                .SelectMany(tile => plan.Resources.Where(value => value.Tile.Equals(tile)))
                .Select(value => value.CreateStableId(Seed, world.GenerationId)).OrderBy(value => value).ToArray();
            Assert.That(reverseTiles, Is.EqualTo(first.OrderBy(value => value)));
        }

        [Test]
        public void LegacyPopulationKeepsItsExistingStableIdFormula()
        {
            var legacy = TiledWorldDefinitionLoader.LoadReference();
            var legacyPlan = new TiledWorldPopulationPlanner(legacy,
                new TiledWorldHeightSampler(legacy, Seed), Seed).Create();
            var node = legacyPlan.Resources.First();
            Assert.That(node.CreateStableId(Seed, legacy.GenerationId), Is.EqualTo(
                DeterministicStableId.Create(Seed, "p109_world_source:" + legacy.GenerationId,
                    node.StableIndex)));
        }

        [Test]
        public void EveryNodeHonorsItsMaskTerrainAffinityAndStartGuarantee()
        {
            var zones = world.MapData.Resources.Zones.ToDictionary(value => value.Id, StringComparer.Ordinal);
            var types = world.MapData.Resources.Types.ToDictionary(value => value.Id, StringComparer.Ordinal);
            foreach (var node in plan.Resources.Where(value => value.ResourceTypeId != "fish"))
            {
                var design = world.MapData.CoordinateTransform.ToDesign(node.Position);
                Assert.That(zones[node.SourceZoneId].Shape.Contains(design), Is.True, node.StableKey);
                Assert.That(zones[node.SourceZoneId].ResourceIds, Does.Contain(node.ResourceTypeId), node.StableKey);
                Assert.That(heights.IsWater(node.Position.X, node.Position.Z), Is.False, node.StableKey);
                Assert.That(node.Hidden, Is.EqualTo(types[node.ResourceTypeId].IsHidden), node.StableKey);
                Assert.That(node.Capacity, Is.InRange(20, 50), node.StableKey);
            }

            foreach (var type in world.MapData.Resources.Types.Where(value => value.Visible && value.Id != "fish"))
                Assert.That(plan.VisibleResources.Any(value => value.ResourceTypeId == type.Id), Is.True, type.Id);
            foreach (var pair in new Dictionary<string, int>
                     {
                         ["wood"] = 8, ["branches"] = 6, ["berries"] = 6,
                         ["stone"] = 5, ["medicinal_plants"] = 4
                     })
                Assert.That(plan.VisibleResources.Count(value => value.SourceZoneId == "res_start_basics" &&
                                                                  value.ResourceTypeId == pair.Key),
                    Is.GreaterThanOrEqualTo(pair.Value), pair.Key);
        }

        [Test]
        public void HiddenDepositsExistButHaveNoStoneAgeSurfaceOrRecipeAvailability()
        {
            Assert.That(plan.HiddenDeposits.Count, Is.EqualTo(19));
            Assert.That(plan.HiddenDeposits.Select(value => value.ResourceTypeId).Distinct(),
                Is.EquivalentTo(new[] { "copper_ore", "iron_ore", "gold_ore", "oil" }));
            Assert.That(plan.HiddenDeposits.All(value => value.Hidden &&
                                                        !string.IsNullOrWhiteSpace(value.UnlockTechnologyId)), Is.True);
            Assert.That(plan.VisibleResources.Any(value => value.ResourceTypeId == "oil"), Is.False);
            Assert.That(plan.VisibleResources.Any(value => value.ResourceTypeId.EndsWith("_ore",
                StringComparison.Ordinal)), Is.False);
            var resourceAsset = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(
                "Assets/Game/Art/Config/ResourceCatalog.asset");
            var recipes = ProductionCatalog.CreateStoneAge(resourceAsset.CreateCatalog());
            Assert.That(recipes.All.Select(value => value.Id.ToString()),
                Does.Not.Contain("gather_copper").And.Not.Contain("extract_oil"));
        }

        [Test]
        public void NonRenewableDepletionPersistsWhileAuditedAssetCatalogUsesExistingPrefabs()
        {
            var stone = plan.VisibleResources.First(value => value.ResourceTypeId == "stone");
            var site = ProductionSite.Restore(new ProductionSiteState(
                stone.CreateStableId(Seed, world.GenerationId), new ProductionRecipeId(stone.RecipeId),
                ProductionSiteKind.NaturalSource, stone.Position, 1, stone.Capacity,
                CropPlotPhase.Unplanted, 0, 0));
            site.CompleteWork(ProductionTaskKind.Gather);
            var ecology = new ResourceEcologyService(TimeSpan.FromDays(1));
            ecology.Register(new NaturalNodeRegistration(site, stone.Biome, new[] { stone.Position }, false));
            ecology.AdvanceTo(0, new FreeSurface());
            ecology.AdvanceTo(TimeSpan.FromDays(30).Ticks, new FreeSurface());
            Assert.That(site.IsDepleted, Is.True);
            Assert.That(ecology.CaptureState().Single().DepletedAtTick, Is.EqualTo(0));

            var art = AssetDatabase.LoadAssetAtPath<WorldMapResourceArtCatalog>(
                "Assets/Game/Resources/World/WorldMapResourceArt.asset");
            Assert.That(art, Is.Not.Null);
            Assert.That(art.Entries.Select(value => value.ResourceTypeId), Is.SupersetOf(new[]
            {
                "wood", "branches", "berries", "mushrooms", "medicinal_plants",
                "plant_fiber", "stone", "flint", "clay", "fish"
            }));
            foreach (var entry in art.Entries)
            {
                Assert.That(entry.Prefab, Is.Not.Null, entry.ResourceTypeId);
                Assert.That(entry.Prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty,
                    entry.ResourceTypeId);
                Assert.That(AssetDatabase.GetAssetPath(entry.Prefab), Does.Not.Contain("WorldMapV2/Resources"),
                    entry.ResourceTypeId);
            }
        }

        private StableEntityId[] Ids(TiledWorldPopulationPlan value, long seed) => value.Resources
            .Select(node => node.CreateStableId(seed, world.GenerationId)).ToArray();

        private sealed class FreeSurface : IResourceRespawnSurface
        {
            public bool IsFree(NaturalResourceBiome biome, WorldPosition position, float footprintRadius) => true;
        }
    }
}
