using System;
using System.Linq;
using Game.Domain.Campaign;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM11")]
    public sealed class WM11WorldMapPoiExplorationTests
    {
        private const long Seed = 20261023;
        private Fixture fixture;

        [OneTimeSetUp]
        public void BuildCanonicalFixture()
        {
            fixture = Create();
        }

        [Test]
        public void CanonicalPlanHasNineExactUniqueProtectedPoints()
        {
            Assert.That(fixture.Plan.Points, Has.Count.EqualTo(9));
            Assert.That(fixture.Plan.Points.Select(value => value.Id).Distinct().Count(), Is.EqualTo(9));
            Assert.That(fixture.Plan.Points.Select(value => value.StableId).Distinct().Count(), Is.EqualTo(9));
            foreach (var point in fixture.Plan.Points)
            {
                var authored = fixture.World.MapData.PointsOfInterest.Single(value => value.Id == point.Id);
                var expected = fixture.World.MapData.CoordinateTransform.ToUnity(authored.Position);
                Assert.That(point.Position.X, Is.EqualTo(expected.X).Within(0.001f), point.Id);
                Assert.That(point.Position.Z, Is.EqualTo(expected.Z).Within(0.001f), point.Id);
                Assert.That(point.RegionId, Is.EqualTo(authored.RegionId), point.Id);
                Assert.That(point.ProtectionRadiusMeters, Is.EqualTo(authored.ProtectionRadiusMeters), point.Id);
                Assert.That(point.StableId, Is.EqualTo(DeterministicStableId.Create(Seed,
                    "world_poi:" + fixture.World.GenerationId + ":" + point.Id)), point.Id);
                Assert.That(point.AllowsAutomaticClearance, Is.False, point.Id);
                Assert.That(fixture.Plan.IsProtected(point.Position.X, point.Position.Z), Is.True, point.Id);
                Assert.That(fixture.Plan.IsProtected(point.Position.X + point.ProtectionRadiusMeters + 0.1f,
                    point.Position.Z), Is.False, point.Id);
            }
        }

        [Test]
        public void EveryPointHasDrySlopeBoundedApproachFromReachableWm09Route()
        {
            foreach (var point in fixture.Plan.Points)
            {
                Assert.That(fixture.Roads.IsRegionReachable(point.RegionId), Is.True, point.Id);
                Assert.That(fixture.Roads.Routes.Any(value => value.Id == point.AccessRouteId), Is.True, point.Id);
                Assert.That(point.ApproachRoute, Has.Count.GreaterThanOrEqualTo(2), point.Id);
                foreach (var sample in point.ApproachRoute)
                {
                    Assert.That(fixture.Heights.IsWater(sample.X, sample.Z), Is.False, point.Id);
                    Assert.That(fixture.Heights.SampleSlopeDegrees(sample.X, sample.Z),
                        Is.LessThanOrEqualTo(WorldMapPoiPlanner.MaximumApproachSlopeDegrees + 0.001f), point.Id);
                }
                for (var index = 1; index < point.ApproachRoute.Count; index++)
                {
                    var a = point.ApproachRoute[index - 1];
                    var b = point.ApproachRoute[index];
                    var horizontal = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
                    var grade = Math.Atan(Math.Abs(a.Y - b.Y) / Math.Max(0.001, horizontal)) * 180d / Math.PI;
                    Assert.That(horizontal, Is.LessThanOrEqualTo(WorldMapPoiPlanner.ApproachGridMeters * 1.415f), point.Id);
                    Assert.That(grade, Is.LessThanOrEqualTo(WorldMapPoiPlanner.MaximumApproachSlopeDegrees + 0.01f), point.Id);
                }
                var dx = point.ArrivalPosition.X - point.Position.X;
                var dz = point.ArrivalPosition.Z - point.Position.Z;
                Assert.That(Math.Sqrt(dx * dx + dz * dz),
                    Is.LessThanOrEqualTo(Math.Max(6f, point.ProtectionRadiusMeters) + 0.01f), point.Id);
            }
        }

        [Test]
        public void VegetationResourcesAndPlacementProtectionCannotAutoClearPoiFootprints()
        {
            var vegetation = new WorldMapVegetationPlanner(fixture.World, fixture.Heights, Seed).Create();
            Assert.That(vegetation.Instances.Any(instance => fixture.Plan.IsProtected(
                instance.Position.X, instance.Position.Z)), Is.False);
            var population = new TiledWorldPopulationPlanner(fixture.World, fixture.Heights, Seed).Create();
            Assert.That(population.VisibleResources.Any(resource => fixture.Plan.IsProtected(
                resource.Position.X, resource.Position.Z, 2f)), Is.False);

            var point = fixture.Plan.Points[0];
            var root = new GameObject("WM11 protection test");
            try
            {
                root.transform.position = new Vector3(point.Position.X, point.Position.Y, point.Position.Z);
                var protection = root.AddComponent<WorldMapPoiProtectionVolume>();
                protection.Configure(point);
                Assert.That(protection.AllowsAutomaticClearance, Is.False);
                Assert.That(protection.OverlapsHorizontalBox(root.transform.position, 1f), Is.True);
                Assert.That(protection.OverlapsHorizontalBox(root.transform.position +
                    Vector3.right * (point.ProtectionRadiusMeters + 2f), 1f), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuditedCatalogCoversEveryPoiWithImportedRenderableAssets()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<WorldMapPoiArtCatalog>(
                "Assets/Game/Resources/World/WorldMapPoiArt.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Entries, Has.Count.EqualTo(9));
            foreach (var point in fixture.Plan.Points)
            {
                var entry = catalog.For(point.Id);
                Assert.That(entry, Is.Not.Null, point.Id);
                Assert.That(entry.PrimaryPrefab, Is.Not.Null, point.Id);
                Assert.That(entry.PrimaryPrefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty, point.Id);
                Assert.That(entry.PrimaryPrefab.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(value => value.sharedMaterials), Has.None.Null, point.Id);
            }
        }

        private static Fixture Create()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            var roads = new WorldMapRoadNetworkPlanner(world, heights).Create();
            return new Fixture(world, heights, roads,
                new WorldMapPoiPlanner(world, heights, roads, Seed).Create());
        }

        private readonly struct Fixture
        {
            public Fixture(TiledWorldDefinition world, TiledWorldHeightSampler heights,
                WorldMapRoadNetworkPlan roads, WorldMapPoiPlan plan)
            { World = world; Heights = heights; Roads = roads; Plan = plan; }
            public TiledWorldDefinition World { get; }
            public TiledWorldHeightSampler Heights { get; }
            public WorldMapRoadNetworkPlan Roads { get; }
            public WorldMapPoiPlan Plan { get; }
        }
    }
}
