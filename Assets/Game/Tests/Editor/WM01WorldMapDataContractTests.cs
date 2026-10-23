using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM01")]
    public sealed class WM01WorldMapDataContractTests
    {
        private static readonly string[] Files =
        {
            "WORLD_MAP_COORDINATES", "WORLD_MAP_BIOMES", "WORLD_MAP_TERRAIN", "WORLD_MAP_WATER",
            "WORLD_MAP_VEGETATION", "WORLD_MAP_RESOURCES", "WORLD_MAP_WILDLIFE", "WORLD_MAP_ROADS",
            "WORLD_MAP_POI", "WORLD_MAP_BUILDABILITY", "WORLD_MAP_MANIFEST"
        };

        [Test]
        public void ProposedPackageLoadsAllContractLayersWithoutActivatingTheLegacyReference()
        {
            var proposed = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var legacy = TiledWorldDefinitionLoader.LoadReference();

            Assert.That(proposed.GenerationId, Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
            Assert.That(proposed.MapData, Is.Not.Null);
            Assert.That(proposed.MapData.MapVersion, Is.EqualTo("serenity-world-map-v1"));
            Assert.That(proposed.MapData.SchemaVersion, Is.EqualTo(1));
            Assert.That(proposed.MapData.Regions, Has.Count.EqualTo(8));
            Assert.That(proposed.MapData.River.Points, Has.Count.EqualTo(12));
            Assert.That(proposed.MapData.Tributaries, Has.Count.EqualTo(4));
            Assert.That(proposed.MapData.Lakes, Has.Count.EqualTo(4));
            Assert.That(proposed.MapData.Routes, Has.Count.EqualTo(7));
            Assert.That(proposed.MapData.Crossings, Has.Count.EqualTo(3));
            Assert.That(proposed.MapData.PointsOfInterest, Has.Count.EqualTo(9));
            Assert.That(proposed.MapData.ResourceMasks, Has.Count.EqualTo(9));
            Assert.That(proposed.MapData.WildlifeMasks, Has.Count.EqualTo(4));
            Assert.That(proposed.MapData.BuildabilityMasks, Has.Count.EqualTo(8));
            Assert.That(proposed.MapData.VegetationMasks, Has.Count.EqualTo(7));
            Assert.That(proposed.MapData.HeightMasks, Has.Count.EqualTo(5));

            Assert.That(legacy.GenerationId, Is.EqualTo(TiledWorldDefinition.ReferenceGenerationId));
            Assert.That(legacy.MapData, Is.Null);
            Assert.That(legacy.Zones, Is.Not.Empty);
        }

        [Test]
        public void DesignTransformMatchesAuthoredStartAndRoundTrips()
        {
            var transform = TiledWorldDefinitionLoader.LoadProposedWorldMap().MapData.CoordinateTransform;
            var design = new WorldMapPoint(185f, 840f);
            var unity = transform.ToUnity(design, 18f);
            Assert.That(unity, Is.EqualTo(new WorldPosition(-385f, 18f, 720f)));
            Assert.That(transform.ToDesign(unity), Is.EqualTo(design));
            Assert.Throws<ArgumentOutOfRangeException>(() => transform.ToUnity(new WorldMapPoint(1001f, 1f)));
        }

        [Test]
        public void PolygonRejectsSelfIntersectionAndOutOfBoundsShapes()
        {
            var bounds = new WorldMapBounds(0f, 1000f, 0f, 1000f);
            Assert.Throws<ArgumentException>(() => new WorldMapPolygon("bow_tie", new[]
            {
                new WorldMapPoint(10f, 10f), new WorldMapPoint(50f, 50f),
                new WorldMapPoint(10f, 50f), new WorldMapPoint(50f, 10f)
            }, bounds));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldMapShape.Ellipse(new WorldMapPoint(-1f, 5f), 10f, 10f, bounds));
        }

        [Test]
        public void ParserFailsClosedForMissingFilesVersionDriftDuplicateIdsAndBrokenReferences()
        {
            var missing = Documents();
            missing.Remove("WORLD_MAP_WATER.json");
            Assert.Throws<InvalidDataException>(() => TiledWorldDefinitionLoader.ParseWorldMapPackage(missing));

            var version = Documents();
            version["WORLD_MAP_TERRAIN.json"] = version["WORLD_MAP_TERRAIN.json"]
                .Replace("\"serenity-world-map-v1\"", "\"serenity-world-map-v2\"");
            Assert.Throws<InvalidDataException>(() => TiledWorldDefinitionLoader.ParseWorldMapPackage(version));

            var duplicate = Documents();
            duplicate["WORLD_MAP_RESOURCES.json"] = duplicate["WORLD_MAP_RESOURCES.json"]
                .Replace("\"res_north_forest\"", "\"res_start_basics\"");
            Assert.Throws<InvalidDataException>(() => TiledWorldDefinitionLoader.ParseWorldMapPackage(duplicate));

            var reference = Documents();
            reference["WORLD_MAP_ROADS.json"] = reference["WORLD_MAP_ROADS.json"]
                .Replace("\"path_start_west_lake\"", "\"missing_route\"");
            Assert.Throws<InvalidDataException>(() => TiledWorldDefinitionLoader.ParseWorldMapPackage(reference));

            var terrain = Documents();
            terrain["WORLD_MAP_TERRAIN.json"] = terrain["WORLD_MAP_TERRAIN.json"]
                .Replace("\"worldResolution\": 1025", "\"worldResolution\": 1024");
            Assert.Throws<InvalidDataException>(() => TiledWorldDefinitionLoader.ParseWorldMapPackage(terrain));

            var vegetation = Documents();
            vegetation["WORLD_MAP_VEGETATION.json"] = vegetation["WORLD_MAP_VEGETATION.json"]
                .Replace("\"classId\": \"open_woodland\"", "\"classId\": \"missing_class\"");
            Assert.Throws<InvalidDataException>(() => TiledWorldDefinitionLoader.ParseWorldMapPackage(vegetation));
        }

        [Test]
        public void AuthoredPolygonsSplinesLandingsPoisAndMasksAreBoundedAndCrossReferenced()
        {
            var data = TiledWorldDefinitionLoader.LoadProposedWorldMap().MapData;
            var bounds = data.CoordinateTransform.DesignBounds;
            Assert.That(data.Regions.SelectMany(value => value.Points).All(bounds.Contains), Is.True);
            Assert.That(data.River.Points.Concat(data.Tributaries.SelectMany(value => value.Points))
                .Concat(data.Routes.SelectMany(value => value.Points)).All(value => bounds.Contains(value.Position)), Is.True);
            Assert.That(data.Lakes.SelectMany(value => value.FishingLandings).All(bounds.Contains), Is.True);
            Assert.That(data.PointsOfInterest.All(value => bounds.Contains(value.Position)), Is.True);
            var routeIds = data.Routes.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
            Assert.That(data.Crossings.SelectMany(value => value.RouteIds).All(routeIds.Contains), Is.True);
            Assert.That(data.ResourceMasks.Concat(data.WildlifeMasks).Concat(data.BuildabilityMasks)
                .Concat(data.VegetationMasks)
                .All(value => value.References.Count > 0), Is.True);
        }

        private static Dictionary<string, string> Documents()
        {
            return Files.ToDictionary(file => file + ".json", file =>
            {
                var asset = Resources.Load<TextAsset>("World/WorldMapV2/" + file);
                Assert.That(asset, Is.Not.Null, file);
                return asset.text;
            }, StringComparer.Ordinal);
        }
    }
}
