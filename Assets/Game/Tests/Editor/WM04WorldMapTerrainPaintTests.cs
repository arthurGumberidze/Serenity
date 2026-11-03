using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("WM04")]
    public sealed class WM04WorldMapTerrainPaintTests
    {
        private TiledWorldDefinition world;
        private WorldMapTerrainPaintSampler paint;

        [SetUp]
        public void SetUp()
        {
            world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            paint = new WorldMapTerrainPaintSampler(world, new TiledWorldHeightSampler(world, 20261023L));
        }

        [Test]
        public void BiomeContractsRetainEightRegionsAndAuthoredEcotones()
        {
            var biomes = world.MapData.Biomes;
            Assert.That(biomes.Regions, Has.Count.EqualTo(8));
            Assert.That(biomes.Regions.Select(value => value.Code),
                Is.EqualTo(new[] { "A", "B", "C", "D", "E", "F", "G", "H" }));
            Assert.That(biomes.Regions.All(value => value.Biomes.Count > 0 && value.Soils.Count > 0), Is.True);
            Assert.That(biomes.Transitions.MinimumBlendWidthMeters, Is.EqualTo(24f));
            Assert.That(biomes.Transitions.PreferredMinimumBlendWidthMeters, Is.EqualTo(35f));
            Assert.That(biomes.Transitions.PreferredMaximumBlendWidthMeters, Is.EqualTo(70f));
            Assert.That(biomes.Transitions.NoRectangularVisualEdges, Is.True);
        }

        [Test]
        public void RegionHeightSlopeAndMoistureProduceReadableLayerDominance()
        {
            var forest = SampleDesign(520f, 850f);
            var ridge = SampleDesign(860f, 880f);
            var marsh = SampleDesign(885f, 125f);
            var meadow = SampleDesign(230f, 180f);

            Assert.That(forest[(int)WorldMapTerrainPaintLayer.ForestMoss],
                Is.GreaterThan(forest[(int)WorldMapTerrainPaintLayer.Shore]));
            Assert.That(ridge[(int)WorldMapTerrainPaintLayer.Rock], Is.EqualTo(ridge.Max()));
            Assert.That(marsh[(int)WorldMapTerrainPaintLayer.Wetland], Is.EqualTo(marsh.Max()));
            Assert.That(meadow[(int)WorldMapTerrainPaintLayer.Grass] +
                        meadow[(int)WorldMapTerrainPaintLayer.Soil], Is.GreaterThan(0.5f));
        }

        [Test]
        public void ShoreAndDryInteriorUseDifferentMoistureProfiles()
        {
            var shore = SampleDesign(514f, 209f);
            var dry = SampleDesign(285f, 175f);
            var shoreMoisture = shore[(int)WorldMapTerrainPaintLayer.Shore] +
                                shore[(int)WorldMapTerrainPaintLayer.Wetland];
            var dryMoisture = dry[(int)WorldMapTerrainPaintLayer.Shore] +
                              dry[(int)WorldMapTerrainPaintLayer.Wetland];
            Assert.That(shoreMoisture, Is.GreaterThan(dryMoisture + 0.12f));
        }

        [Test]
        public void PaintWeightsAreNormalizedAndTransitionsStayContinuous()
        {
            var maximumNeighbourDelta = 0f;
            var maximumAt = string.Empty;
            var maximumPair = string.Empty;
            float[] previous = null;
            for (var z = 0; z < 65; z++)
            {
                previous = null;
                for (var x = 0; x < 65; x++)
                {
                    var weights = paint.SampleWeights(world.Origin.X + world.WidthMeters * x / 64f,
                        world.Origin.Z + world.DepthMeters * z / 64f);
                    Assert.That(weights, Has.Length.EqualTo(WorldMapTerrainPaintSampler.LayerCount));
                    Assert.That(weights.Sum(), Is.EqualTo(1f).Within(0.00001f));
                    Assert.That(weights.All(value => value >= 0f && value <= 1f), Is.True);
                    if (previous != null)
                    {
                        var delta = weights.Zip(previous, (a, b) => System.Math.Abs(a - b)).Sum();
                        if (delta > maximumNeighbourDelta)
                        {
                            maximumNeighbourDelta = delta;
                            maximumAt = $"design({x * world.WidthMeters / 64f:0.0}, {z * world.DepthMeters / 64f:0.0})";
                            maximumPair = $"[{string.Join(",", previous.Select(value => value.ToString("0.000")))}] -> " +
                                          $"[{string.Join(",", weights.Select(value => value.ToString("0.000")))}]";
                        }
                    }
                    previous = weights;
                }
            }
            TestContext.WriteLine($"maximum horizontal L1 paint delta={maximumNeighbourDelta:0.0000} at {maximumAt} {maximumPair}");
            Assert.That(maximumNeighbourDelta, Is.LessThan(1.6f),
                "Only authored cliff bands may approach a full material shift at this 15.625 m audit spacing.");
        }

        [Test]
        public void EveryTilePaintEdgeIsBitIdenticalAndMaskHashIsDeterministic()
        {
            const int resolution = 33;
            var maps = world.Tiles.ToDictionary(tile => tile, tile => paint.CreateTileWeights(tile, resolution));
            foreach (var tile in world.Tiles)
            {
                if (tile.X + 1 < world.Columns)
                {
                    var east = maps[new WorldTileId(tile.X + 1, tile.Z)];
                    for (var z = 0; z < resolution; z++)
                    for (var layer = 0; layer < WorldMapTerrainPaintSampler.LayerCount; layer++)
                        Assert.That(maps[tile][z, resolution - 1, layer], Is.EqualTo(east[z, 0, layer]),
                            $"east edge {tile}, sample {z}, layer {layer}");
                }
                if (tile.Z + 1 < world.Rows)
                {
                    var north = maps[new WorldTileId(tile.X, tile.Z + 1)];
                    for (var x = 0; x < resolution; x++)
                    for (var layer = 0; layer < WorldMapTerrainPaintSampler.LayerCount; layer++)
                        Assert.That(maps[tile][resolution - 1, x, layer], Is.EqualTo(north[0, x, layer]),
                            $"north edge {tile}, sample {x}, layer {layer}");
                }
            }

            var hash = paint.CalculateMaskHash(65);
            var replay = new WorldMapTerrainPaintSampler(world,
                new TiledWorldHeightSampler(world, 20261023L)).CalculateMaskHash(65);
            var otherSeed = new WorldMapTerrainPaintSampler(world,
                new TiledWorldHeightSampler(world, 20261024L)).CalculateMaskHash(65);
            TestContext.WriteLine($"WM04 mask hash={hash:X16}");
            Assert.That(hash, Is.EqualTo(11588388288129154093UL));
            Assert.That(replay, Is.EqualTo(hash));
            Assert.That(otherSeed, Is.Not.EqualTo(hash));
        }

        private float[] SampleDesign(float x, float z)
        {
            var point = world.MapData.CoordinateTransform.ToUnity(new WorldMapPoint(x, z));
            return paint.SampleWeights(point.X, point.Z);
        }
    }
}
