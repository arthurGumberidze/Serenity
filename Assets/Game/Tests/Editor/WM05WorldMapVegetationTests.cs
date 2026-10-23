using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("WM05")]
    public sealed class WM05WorldMapVegetationTests
    {
        private const long Seed = 20261023L;
        private TiledWorldDefinition world;
        private TiledWorldHeightSampler heights;
        private WorldMapVegetationPlan plan;
        private WorldMapVegetationPlan replay;
        private WorldMapVegetationPlan otherSeed;
        private long firstPlanMilliseconds;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            heights = new TiledWorldHeightSampler(world, Seed);
            var timer = Stopwatch.StartNew();
            plan = CreatePlan(Seed);
            timer.Stop();
            firstPlanMilliseconds = timer.ElapsedMilliseconds;
            replay = CreatePlan(Seed);
            otherSeed = CreatePlan(Seed + 1);
        }

        [Test]
        public void TypedContractRetainsAuthoredClassesZonesDistributionAndBudgets()
        {
            var vegetation = world.MapData.Vegetation;
            Assert.That(vegetation, Is.Not.Null);
            Assert.That(vegetation.Classes, Has.Count.EqualTo(6));
            Assert.That(vegetation.Zones, Has.Count.EqualTo(7));
            Assert.That(vegetation.Distribution.Method,
                Is.EqualTo("seeded_poisson_disc_plus_density_masks"));
            Assert.That(vegetation.Distribution.InteractiveTreeSpacingMeters.Minimum, Is.EqualTo(4.5f));
            Assert.That(vegetation.Distribution.InteractiveTreeSpacingMeters.Maximum, Is.EqualTo(8f));
            Assert.That(vegetation.Distribution.StartClearingExclusionRadiusMeters, Is.EqualTo(58f));
            Assert.That(vegetation.Distribution.MainTrailClearanceMeters, Is.EqualTo(5f));
            Assert.That(vegetation.Distribution.FootpathClearanceMeters, Is.EqualTo(2f));
            Assert.That(vegetation.Budget.InteractiveTrees.Minimum, Is.EqualTo(3500f));
            Assert.That(vegetation.Budget.InteractiveTrees.Maximum, Is.EqualTo(6000f));
            Assert.That(vegetation.Budget.DecorativeTreesAndShrubs.Minimum, Is.EqualTo(9000f));
            Assert.That(vegetation.Budget.DecorativeTreesAndShrubs.Maximum, Is.EqualTo(17000f));
            Assert.That(vegetation.Budget.ActiveTreeColliders.Minimum, Is.EqualTo(300f));
            Assert.That(vegetation.Budget.ActiveTreeColliders.Maximum, Is.EqualTo(650f));
            Assert.That(vegetation.Budget.DecorativeGroundInstances.Minimum, Is.EqualTo(18000f));
            Assert.That(vegetation.Budget.DecorativeGroundInstances.Maximum, Is.EqualTo(35000f));
            Assert.That(vegetation.Zones.Single(value => value.Id == "veg_start_open").Clearings.Single()
                .RadiusMeters, Is.EqualTo(72f));
        }

        [Test]
        public void SeededPlanIsStableUniqueAndIndependentOfTileReadOrder()
        {
            TestContext.WriteLine($"WM05 counts: interactive={plan.InteractiveTreeCount}, " +
                                  $"decorative={plan.DecorativeTreeAndShrubCount}, " +
                                  $"ground={plan.GroundCoverCount}, hash={plan.CalculateHash():X16}, " +
                                  $"first-plan-ms={firstPlanMilliseconds}");
            Assert.That(replay.CalculateHash(), Is.EqualTo(plan.CalculateHash()));
            Assert.That(otherSeed.CalculateHash(), Is.Not.EqualTo(plan.CalculateHash()));
            Assert.That(plan.Instances.Select(value => value.StableId).Distinct().Count(),
                Is.EqualTo(plan.Instances.Count));
            Assert.That(plan.Instances.All(value => value.StableId.IsValid && world.IsValid(value.Tile)), Is.True);

            var reverseRead = world.Tiles.Reverse().SelectMany(plan.ForTile)
                .OrderBy(value => value.StableId).Select(value => value.StableId).ToArray();
            Assert.That(reverseRead, Is.EqualTo(plan.Instances.Select(value => value.StableId)));
        }

        [Test]
        public void CountsRespectPerformanceBudgetsAndDensityIntent()
        {
            var budget = world.MapData.Vegetation.Budget;
            Assert.That(plan.InteractiveTreeCount,
                Is.InRange((int)budget.InteractiveTrees.Minimum, (int)budget.InteractiveTrees.Maximum));
            Assert.That(plan.DecorativeTreeAndShrubCount,
                Is.InRange((int)budget.DecorativeTreesAndShrubs.Minimum,
                    (int)budget.DecorativeTreesAndShrubs.Maximum));
            Assert.That(plan.GroundCoverCount,
                Is.InRange((int)budget.DecorativeGroundInstances.Minimum,
                    (int)budget.DecorativeGroundInstances.Maximum));

            var north = plan.Instances.Count(value => value.ZoneId == "veg_north_dense" &&
                                                       value.Kind == WorldMapVegetationKind.InteractiveTree) /
                        AreaHectares(world.MapData.Vegetation.Zones.Single(value => value.Id == "veg_north_dense"));
            var east = plan.Instances.Count(value => value.ZoneId == "veg_east_open" &&
                                                      value.Kind == WorldMapVegetationKind.InteractiveTree) /
                       AreaHectares(world.MapData.Vegetation.Zones.Single(value => value.Id == "veg_east_open"));
            Assert.That(north, Is.GreaterThan(east * 2f));
        }

        [Test]
        public void InteractiveTreesKeepGlobalMinimumSpacingAndAllInstancesHonorExclusions()
        {
            var planner = new WorldMapVegetationPlanner(world, heights, Seed);
            var zones = world.MapData.Vegetation.Zones.ToDictionary(value => value.Id, StringComparer.Ordinal);
            foreach (var instance in plan.Instances)
            {
                var design = world.MapData.CoordinateTransform.ToDesign(instance.Position);
                Assert.That(zones[instance.ZoneId].Shape.Contains(design), Is.True, instance.StableId.ToString());
                Assert.That(planner.IsExcluded(zones[instance.ZoneId], design, instance.Kind), Is.False,
                    instance.StableId.ToString());
            }

            const float minimum = 4.5f;
            var grid = new Dictionary<long, List<WorldMapPoint>>();
            foreach (var instance in plan.Instances.Where(value =>
                         value.Kind == WorldMapVegetationKind.InteractiveTree))
            {
                var point = world.MapData.CoordinateTransform.ToDesign(instance.Position);
                var cellX = (int)Math.Floor(point.X / minimum);
                var cellZ = (int)Math.Floor(point.Z / minimum);
                for (var z = cellZ - 1; z <= cellZ + 1; z++)
                for (var x = cellX - 1; x <= cellX + 1; x++)
                    if (grid.TryGetValue(CellKey(x, z), out var neighbours))
                    foreach (var other in neighbours)
                        Assert.That(SquaredDistance(point, other), Is.GreaterThanOrEqualTo(minimum * minimum - 0.001f));
                var key = CellKey(cellX, cellZ);
                if (!grid.TryGetValue(key, out var values)) grid.Add(key, values = new List<WorldMapPoint>());
                values.Add(point);
            }
        }

        private WorldMapVegetationPlan CreatePlan(long seed) =>
            new WorldMapVegetationPlanner(world, new TiledWorldHeightSampler(world, seed), seed).Create();

        private static float AreaHectares(WorldMapVegetationZone zone)
        {
            if (zone.Shape.Kind != WorldMapShapeKind.Polygon)
                return (float)(Math.PI * zone.Shape.RadiusX * zone.Shape.RadiusZ / 10000d);
            double twiceArea = 0d;
            var points = zone.Shape.Polygon.Points;
            for (var index = 0; index < points.Count; index++)
            {
                var next = points[(index + 1) % points.Count];
                twiceArea += (double)points[index].X * next.Z - (double)next.X * points[index].Z;
            }
            return (float)(Math.Abs(twiceArea) * 0.5d / 10000d);
        }

        private static float SquaredDistance(WorldMapPoint a, WorldMapPoint b)
        {
            var x = a.X - b.X;
            var z = a.Z - b.Z;
            return x * x + z * z;
        }

        private static long CellKey(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
