using System.Linq;
using Game.Domain.AI;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("WM02")]
    public sealed class WM02WorldMapTerrainGeneratorTests
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
        public void TerrainContractsExposeAuthoredOctavesAnchorsMasksAndResolution()
        {
            var terrain = world.MapData.Terrain;
            Assert.That(terrain.NoiseOctaves.Select(value => value.Id),
                Is.EqualTo(new[] { "macro", "meso", "detail" }));
            Assert.That(terrain.NoiseOctaves.Sum(value => value.Weight), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(terrain.Anchors, Has.Count.EqualTo(8));
            Assert.That(terrain.HeightMasks, Has.Count.EqualTo(5));
            Assert.That(terrain.WorldResolution, Is.EqualTo(1025));
            Assert.That(terrain.TileResolution, Is.EqualTo(257));
            Assert.That(terrain.SharedBorderSamples, Is.True);
            Assert.That(heights.HeightmapResolution, Is.EqualTo(257));
            Assert.That(heights.MaximumTerrainHeightMeters, Is.EqualTo(96f));
        }

        [Test]
        public void EverySharedTerrainEdgeIsBitIdenticalAndReplayIsDeterministic()
        {
            var maps = world.Tiles.ToDictionary(tile => tile,
                tile => heights.CreateNormalizedHeights(tile, heights.HeightmapResolution));
            foreach (var tile in world.Tiles)
            {
                if (tile.X + 1 < world.Columns)
                {
                    var east = maps[new WorldTileId(tile.X + 1, tile.Z)];
                    for (var z = 0; z < heights.HeightmapResolution; z++)
                        Assert.That(maps[tile][z, heights.HeightmapResolution - 1],
                            Is.EqualTo(east[z, 0]), $"east edge {tile}, sample {z}");
                }
                if (tile.Z + 1 < world.Rows)
                {
                    var north = maps[new WorldTileId(tile.X, tile.Z + 1)];
                    for (var x = 0; x < heights.HeightmapResolution; x++)
                        Assert.That(maps[tile][heights.HeightmapResolution - 1, x],
                            Is.EqualTo(north[0, x]), $"north edge {tile}, sample {x}");
                }
            }

            var replay = new TiledWorldHeightSampler(world, 20261023L)
                .CreateNormalizedHeights(new WorldTileId(2, 2), heights.HeightmapResolution);
            Assert.That(replay.Cast<float>().SequenceEqual(maps[new WorldTileId(2, 2)].Cast<float>()), Is.True);
            var otherSeed = new TiledWorldHeightSampler(world, 20261024L)
                .CreateNormalizedHeights(new WorldTileId(2, 2), heights.HeightmapResolution);
            Assert.That(otherSeed.Cast<float>().SequenceEqual(replay.Cast<float>()), Is.False);
        }

        [Test]
        public void PlateauRidgePassAndMarshAnchorsShapeTheAuthoredTerrain()
        {
            AssertHeight(new WorldMapPoint(185f, 840f), 18f, 0.001f, "start plateau");
            AssertHeight(new WorldMapPoint(770f, 655f), 38f, 0.001f, "ridge pass");
            AssertHeight(new WorldMapPoint(885f, 125f), 10.75f, 2.25f, "marsh basin");

            var ridge = SampleDesign(new WorldMapPoint(860f, 880f));
            var ridgeFlank = SampleDesign(new WorldMapPoint(670f, 880f));
            Assert.That(ridge, Is.InRange(76f, 88f));
            Assert.That(ridge, Is.GreaterThan(ridgeFlank + 20f));
            Assert.That(heights.SampleSlopeDegrees(world.PlayerStart.X, world.PlayerStart.Z), Is.LessThanOrEqualTo(8f));
        }

        [Test]
        public void SlopeHistogramMeetsAuthoredBudget()
        {
            var histogram = heights.AnalyzeSlopeBudget(129);
            var sampledHeights = Enumerable.Range(0, 129).SelectMany(z => Enumerable.Range(0, 129).Select(x =>
                heights.SampleHeight(world.Origin.X + world.WidthMeters * x / 128f,
                    world.Origin.Z + world.DepthMeters * z / 128f))).ToArray();
            var clamped = sampledHeights.Count(value => value <= world.MapData.Terrain.MinimumHeightMeters + 0.001f ||
                                                        value >= world.MapData.Terrain.MaximumHeightMeters - 0.001f);
            TestContext.WriteLine($"<=12: {histogram.SlopeUnder12Percent:0.00}%, " +
                                  $"<=18: {histogram.BuildableSlopePercent:0.00}%, " +
                                  $">32: {histogram.SlopeOver32Percent:0.00}%, " +
                                  $"height: {sampledHeights.Min():0.00}..{sampledHeights.Max():0.00} m, " +
                                  $"clamped: {100f * clamped / sampledHeights.Length:0.00}%");
            Assert.That(histogram.SampleCount, Is.EqualTo(129 * 129));
            Assert.That(histogram.BuildableSlopePercent,
                Is.GreaterThanOrEqualTo(histogram.Targets.BuildablePercentMinimum));
            Assert.That(histogram.SlopeUnder12Percent,
                Is.InRange(histogram.Targets.SlopeUnder12PercentMinimum,
                    histogram.Targets.SlopeUnder12PercentMaximum));
            Assert.That(histogram.SlopeOver32Percent,
                Is.LessThanOrEqualTo(histogram.Targets.SlopeOver32PercentMaximum));
            Assert.That(100f * clamped / sampledHeights.Length, Is.LessThan(2f),
                "The slope budget must not be met by creating large flat areas at the height clamps.");
            Assert.That(histogram.MeetsBudget, Is.True,
                $"<=12: {histogram.SlopeUnder12Percent:0.00}%, <=18: {histogram.BuildableSlopePercent:0.00}%, " +
                $">32: {histogram.SlopeOver32Percent:0.00}%");
        }

        private void AssertHeight(WorldMapPoint design, float expected, float tolerance, string label) =>
            Assert.That(SampleDesign(design), Is.EqualTo(expected).Within(tolerance), label);

        private float SampleDesign(WorldMapPoint design)
        {
            var runtime = world.MapData.CoordinateTransform.ToUnity(design);
            return heights.SampleHeight(runtime.X, runtime.Z);
        }
    }
}
