using System;
using System.Globalization;
using System.Linq;
using Game.Domain.Campaign;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Buildings;
using Game.Presentation.Environment;
using Game.Simulation.Campaign;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM10")]
    public sealed class WM10WorldMapStartTerritoryTests
    {
        private const long Seed = 20261023L;

        [Test]
        public void ProposedPopulationBuildsCanonicalPreparedAndDeterministicStart()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            var first = new TiledWorldPopulationPlanner(world, heights, Seed).Create();
            var second = new TiledWorldPopulationPlanner(world, heights, Seed).Create();
            var start = first.StartTerritory;

            Assert.That(start, Is.Not.Null);
            Assert.That(start.Center.X, Is.EqualTo(-385f).Within(0.001f));
            Assert.That(start.Center.Z, Is.EqualTo(720f).Within(0.001f));
            Assert.That(start.ClearingRadiusMeters, Is.EqualTo(72.5f).Within(0.001f));
            Assert.That(start.InitialRevealRadiusMeters, Is.EqualTo(135f).Within(0.001f));
            Assert.That(start.StartingObjects.Select(value => value.DefinitionId), Is.EquivalentTo(new[]
            {
                "primitive_shelter", "storage_basket"
            }));
            Assert.That(start.FirstConstructionSites.Select(value => value.DefinitionId), Is.EquivalentTo(new[]
            {
                "campfire", "stone_age_hut", "stone_tool_workshop", "primitive_garden"
            }));
            Assert.That(start.ExpansionCorridors.OrderBy(value => value.Priority).Select(value => value.Id),
                Is.EqualTo(new[] { "expand_east", "expand_west", "expand_south" }));
            Assert.That(start.ResidentSpawns, Has.Count.EqualTo(10));
            Assert.That(start.RiverRouteId, Is.EqualTo("path_start_west_lake"));
            Assert.That(start.RiverCrossingId, Is.EqualTo("crossing_start_ford"));
            Assert.That(heights.IsWater(start.RiverAccessPoint.X, start.RiverAccessPoint.Z), Is.False);

            foreach (var type in new[] { "wood", "branches", "berries", "stone", "medicinal_plants", "mushrooms", "plant_fiber" })
                Assert.That(start.StartingResources.Count(value => value.ResourceTypeId == type),
                    Is.GreaterThanOrEqualTo(TiledWorldPopulationPlanner.StartGuarantee(type)), type);
            Assert.That(start.StartingResources.All(value =>
                !world.MapData.StartTerritory.IsPrepared(world.MapData.CoordinateTransform.ToDesign(value.Position))),
                Is.True, "Starting resources must stay available around, not inside, prepared building/corridor space.");
            Assert.That(Fingerprint(start), Is.EqualTo(Fingerprint(second.StartTerritory)));
            Assert.That(start.StartingObjects.Select(value => value.Id).Distinct().Count(),
                Is.EqualTo(start.StartingObjects.Count));
        }

        [Test]
        public void NewGameBootstrapConsumesThePopulationPlanWithoutChangingLegacyStartup()
        {
            var proposed = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var population = new TiledWorldPopulationPlanner(proposed,
                new TiledWorldHeightSampler(proposed, Seed), Seed).Create();
            var configuration = new NewGameConfiguration(Seed);
            var newGame = NewGameBootstrap.Create(configuration, populationPlan: population);

            Assert.That(newGame.PlayerCharacters, Has.Count.EqualTo(10));
            Assert.That(newGame.StartTerritory, Is.SameAs(population.StartTerritory));
            Assert.That(newGame.StartTerritory.WorldSeed, Is.EqualTo(configuration.WorldSeed));
            Assert.That(newGame.PlayerCharacters.Select(value => value.Id).Distinct().Count(), Is.EqualTo(10));

            var legacyWorld = TiledWorldDefinitionLoader.LoadReference();
            var legacyNewGame = NewGameBootstrap.Create(configuration);
            Assert.That(legacyWorld.GenerationId, Is.EqualTo(TiledWorldDefinition.ReferenceGenerationId));
            Assert.That(legacyWorld.MapData, Is.Null);
            Assert.That(legacyNewGame.StartTerritory, Is.Null);
        }

        [Test]
        public void ExistingSettlementResourceAndVegetationAssetsCoverTheStartPlan()
        {
            var buildings = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            Assert.That(buildings, Is.Not.Null);
            foreach (var id in new[]
                     {
                         "primitive_shelter", "storage_basket", "campfire", "stone_age_hut",
                         "stone_tool_workshop", "primitive_garden"
                     })
            {
                var prefab = buildings.ResolvePrefab(new Game.Domain.Buildings.BuildingDefinitionId(id));
                Assert.That(prefab, Is.Not.Null, id);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Does.StartWith("Assets/Game/Art/"), id);
                Assert.That(prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, id);
            }

            var resources = Resources.Load<WorldMapResourceArtCatalog>("World/WorldMapResourceArt");
            Assert.That(resources, Is.Not.Null);
            foreach (var id in new[] { "wood", "branches", "berries", "stone", "medicinal_plants", "mushrooms", "plant_fiber" })
            {
                var prefab = resources.PrefabFor(id);
                Assert.That(prefab, Is.Not.Null, id);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.Not.Empty, id);
            }

            var nature = Resources.Load<StarterNatureCatalog>("World/StarterNature");
            Assert.That(nature, Is.Not.Null);
            Assert.That(nature.WorldMapTrees, Has.Length.GreaterThanOrEqualTo(4));
            Assert.That(nature.WorldMapShrubs, Is.Not.Empty);
            Assert.That(nature.WorldMapGroundCover, Is.Not.Empty);
            Assert.That(nature.WorldMapSmallProps, Is.Not.Empty);
            Assert.That(nature.WorldMapTrees.Concat(nature.WorldMapShrubs)
                .Concat(nature.WorldMapGroundCover).Concat(nature.WorldMapSmallProps).All(value => value != null),
                Is.True);
        }

        private static string Fingerprint(WorldMapStartTerritoryPlan plan) => string.Join("|",
            new[] { plan.Id.ToString(), Point(plan.Center), Point(plan.RiverAccessPoint) }
                .Concat(plan.StartingObjects.Select(value => value.Id + ":" + value.DefinitionId + ":" +
                    Point(value.Position)))
                .Concat(plan.FirstConstructionSites.Select(value => value.DefinitionId + ":" +
                    Point(value.Position)))
                .Concat(plan.ExpansionCorridors.Select(value => value.Id + ":" + value.Priority + ":" +
                    Point(value.Start) + ":" + Point(value.End)))
                .Concat(plan.StartingResources.Select(value => value.CreateStableId(Seed,
                    TiledWorldDefinition.ProposedWorldMapGenerationId).ToString())));

        private static string Point(Game.Domain.AI.WorldPosition value) =>
            value.X.ToString("0.000", CultureInfo.InvariantCulture) + "," +
            value.Y.ToString("0.000", CultureInfo.InvariantCulture) + "," +
            value.Z.ToString("0.000", CultureInfo.InvariantCulture);
    }
}
