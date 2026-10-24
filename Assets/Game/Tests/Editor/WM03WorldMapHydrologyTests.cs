using System.Linq;
using System.Reflection;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM03")]
    public sealed class WM03WorldMapHydrologyTests
    {
        private TiledWorldDefinition world;
        private TiledWorldHeightSampler heights;

        [SetUp]
        public void SetUp()
        {
            world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            heights = new TiledWorldHeightSampler(world, 20261023L);
        }

        [Test]
        public void CanonicalHydrologyRetainsWidthsBasinsAndLevelPolicies()
        {
            var hydrology = world.MapData.Hydrology;
            Assert.That(hydrology.Lakes, Has.Count.EqualTo(4));
            Assert.That(hydrology.MainRiver.Spline.Points.Select(value => value.WidthMeters),
                Is.EqualTo(new[] { 18f, 20f, 18f, 22f, 24f, 22f, 24f, 26f, 24f, 26f, 24f, 28f }));
            Assert.That(hydrology.MainRiver.MinimumDepthMeters, Is.EqualTo(0.4f));
            Assert.That(hydrology.MainRiver.MaximumDepthMeters, Is.EqualTo(3.2f));
            Assert.That(hydrology.MainRiver.MinimumBankBlendMeters, Is.EqualTo(14f));
            Assert.That(hydrology.MainRiver.MaximumBankBlendMeters, Is.EqualTo(28f));
            Assert.That(hydrology.MainRiver.SurfaceLevelPolicy.Kind,
                Is.EqualTo(WorldMapWaterLevelPolicyKind.Reference));
            Assert.That(hydrology.Tributaries, Has.Count.EqualTo(4));
            foreach (var tributary in hydrology.Tributaries)
            {
                Assert.That(tributary.Spline.Points.First().WidthMeters,
                    Is.EqualTo(tributary.Spline.MinimumWidthMeters));
                Assert.That(tributary.Spline.Points.Last().WidthMeters,
                    Is.EqualTo(tributary.Spline.MaximumWidthMeters));
                Assert.That(tributary.SurfaceLevelPolicy.Kind,
                    Is.EqualTo(WorldMapWaterLevelPolicyKind.LinearDrainage));
                Assert.That(tributary.SurfaceLevelPolicy.DownstreamMeters,
                    Is.LessThanOrEqualTo(tributary.SurfaceLevelPolicy.UpstreamMeters));
            }
            Assert.That(hydrology.Lakes.Select(value => value.SurfaceY),
                Is.EquivalentTo(new[] { 8f, 8f, 17f, 36f }));
            Assert.That(hydrology.Lakes.All(value => value.SurfaceLevelPolicy.Kind ==
                WorldMapWaterLevelPolicyKind.Fixed), Is.True);
        }

        [Test]
        public void EveryLakeAndCourseIsCarvedBelowItsDeterministicSurface()
        {
            var hydrology = world.MapData.Hydrology;
            foreach (var lake in hydrology.Lakes)
            {
                var runtime = world.MapData.CoordinateTransform.ToUnity(lake.Center);
                Assert.That(heights.TrySampleWaterSurface(runtime.X, runtime.Z, out var surface), Is.True, lake.Id);
                Assert.That(surface, Is.EqualTo(lake.SurfaceY).Within(0.001f), lake.Id);
                Assert.That(heights.SampleHeight(runtime.X, runtime.Z), Is.LessThan(surface - 0.3f), lake.Id);
            }

            foreach (var course in new[] { hydrology.MainRiver }.Concat(hydrology.Tributaries))
            foreach (var point in course.Spline.Points)
            {
                var runtime = world.MapData.CoordinateTransform.ToUnity(point.Position);
                Assert.That(heights.TrySampleWaterSurface(runtime.X, runtime.Z, out var surface), Is.True,
                    course.Id + " " + point.Position);
                Assert.That(heights.SampleHeight(runtime.X, runtime.Z), Is.LessThan(surface - 0.2f),
                    course.Id + " " + point.Position);
            }
        }

        [Test]
        public void AuthoredFishingLandingsRemainDryAboveTheirOwnLakeSurface()
        {
            foreach (var lake in world.MapData.Hydrology.Lakes)
            foreach (var landing in lake.FishingLandings)
            {
                var runtime = world.MapData.CoordinateTransform.ToUnity(landing);
                Assert.That(heights.IsWater(runtime.X, runtime.Z), Is.False, lake.Id + " " + landing);
                Assert.That(heights.SampleHeight(runtime.X, runtime.Z), Is.GreaterThan(lake.SurfaceY + 0.75f),
                    lake.Id + " " + landing);
            }
        }

        [Test]
        public void DrainageGraphAndLevelsReachTheMainRiverWithoutUphillFlow()
        {
            var hydrology = world.MapData.Hydrology;
            Assert.That(heights.WaterSurfaceLevel("river_main"), Is.EqualTo(8f));
            Assert.That(heights.WaterSurfaceLevel("stream_mountain", 0f), Is.EqualTo(36f));
            Assert.That(heights.WaterSurfaceLevel("stream_mountain", 1f), Is.EqualTo(17f));
            Assert.That(heights.WaterSurfaceLevel("stream_central_outlet", 0f), Is.EqualTo(17f));
            Assert.That(heights.WaterSurfaceLevel("stream_central_outlet", 1f), Is.EqualTo(8f));
            Assert.That(hydrology.Tributaries.SelectMany(value => value.Connections)
                .Count(value => value == hydrology.MainRiver.Id), Is.EqualTo(3));

            var replay = new TiledWorldHeightSampler(world, 20261023L);
            foreach (var lake in hydrology.Lakes)
            {
                var runtime = world.MapData.CoordinateTransform.ToUnity(lake.Center);
                Assert.That(replay.SampleHeight(runtime.X, runtime.Z),
                    Is.EqualTo(heights.SampleHeight(runtime.X, runtime.Z)));
            }
        }

        [Test]
        public void LocalLakeWaterContainsNonDegenerateTrianglesAtPlayerResolution()
        {
            var root = new GameObject("WM03 local water regression");
            var data = new TerrainData { heightmapResolution = 257 };
            Mesh mesh = null;
            try
            {
                var projection = root.AddComponent<TiledTerrainWorld>();
                typeof(TiledTerrainWorld).GetProperty("Definition").SetValue(projection, world);
                typeof(TiledTerrainWorld).GetProperty("HeightSampler").SetValue(projection, heights);
                var center = world.MapData.CoordinateTransform.ToUnity(world.MapData.Hydrology.Lakes[0].Center);
                Assert.That(world.TryGetTile(center.X, center.Z, out var tile), Is.True);
                var water = (GameObject)typeof(TiledTerrainWorld).GetMethod("CreateAuthoredLocalWater",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(projection, new object[] { tile, root.transform, data });
                Assert.That(water, Is.Not.Null);
                mesh = water.GetComponent<MeshFilter>().sharedMesh;
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                Assert.That(triangles.Length, Is.GreaterThan(0));
                for (var i = 0; i < triangles.Length; i += 3)
                    Assert.That(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                        vertices[triangles[i + 2]] - vertices[triangles[i]]).sqrMagnitude,
                        Is.GreaterThan(.1f), "Collapsed water triangle " + i);
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void TiledTerrainWorldProjectsTheCanonicalDistinctWaterLevels()
        {
            var root = new GameObject("WM03 projection test");
            Mesh mesh = null;
            try
            {
                var projection = root.AddComponent<TiledTerrainWorld>();
                typeof(TiledTerrainWorld).GetProperty(nameof(TiledTerrainWorld.Definition),
                        BindingFlags.Instance | BindingFlags.Public)
                    ?.SetValue(projection, world);
                mesh = (Mesh)typeof(TiledTerrainWorld).GetMethod("BuildAuthoredOverviewWaterMesh",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(projection, null);
                Assert.That(mesh, Is.Not.Null);
                var levels = mesh.vertices.Select(value => value.y).Distinct().OrderBy(value => value).ToArray();
                Assert.That(levels.First(), Is.EqualTo(8f).Within(0.001f));
                Assert.That(levels.Any(value => Mathf.Abs(value - 17f) < 0.001f), Is.True);
                Assert.That(levels.Last(), Is.EqualTo(36f).Within(0.001f));
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(root);
            }
        }
    }
}
