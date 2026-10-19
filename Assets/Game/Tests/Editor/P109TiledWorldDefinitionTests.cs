using System;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P109")]
    public sealed class P109TiledWorldDefinitionTests
    {
        private TiledWorldDefinition world;
        [SetUp] public void LoadDefinition() => world = TiledWorldDefinitionLoader.LoadReference();

        [Test]
        public void ReferenceHas144UniqueKilometreTilesAnd144SquareKilometres()
        {
            Assert.That(world.GenerationId, Is.EqualTo("p109-tiled-world-v2"));
            Assert.That(world.Tiles.Count, Is.EqualTo(144));
            Assert.That(world.Tiles.Distinct().Count(), Is.EqualTo(144));
            Assert.That(world.TileSizeMeters, Is.EqualTo(1000));
            Assert.That(world.WidthMeters, Is.EqualTo(12000));
            Assert.That(world.DepthMeters, Is.EqualTo(12000));
            Assert.That(world.AreaSquareMeters, Is.EqualTo(144000000L));
        }

        [TestCase(129)]
        [TestCase(257)]
        [TestCase(513)]
        public void NeighbourHeightmapBordersUseExactlyTheSameWorldSamples(int resolution)
        {
            foreach (var tile in world.Tiles)
            for (var sample = 0; sample < resolution; sample++)
            {
                if (tile.X + 1 < world.Columns)
                    Assert.That(world.SamplePosition(tile, resolution - 1, sample, resolution),
                        Is.EqualTo(world.SamplePosition(new WorldTileId(tile.X + 1, tile.Z), 0, sample, resolution)));
                if (tile.Z + 1 < world.Rows)
                    Assert.That(world.SamplePosition(tile, sample, resolution - 1, resolution),
                        Is.EqualTo(world.SamplePosition(new WorldTileId(tile.X, tile.Z + 1), sample, 0, resolution)));
            }
        }

        [Test]
        public void InternalBordersHaveOneOwnerAndOutsideCoordinatesAreNotClampedToLand()
        {
            var origin = world.TileOrigin(new WorldTileId(5, 7));
            Assert.That(world.TryGetTile(origin.X, origin.Z, out var tile), Is.True);
            Assert.That(tile, Is.EqualTo(new WorldTileId(5, 7)));
            Assert.That(world.TryGetTile(world.Origin.X - 1, world.Origin.Z, out _), Is.False);
            Assert.That(world.TryGetTile(world.Origin.X + world.WidthMeters, world.Origin.Z, out _), Is.False);
            Assert.That(world.TryGetTile(float.NaN, 0, out _), Is.False);
            Assert.That(world.TryGetTile(float.PositiveInfinity, 0, out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => world.TileOrigin(new WorldTileId(12, 0)));
        }

        [Test]
        public void StreamingSelectionRetainsDistantPhysicalActorsAndIsIndependentOfEnumerationOrder()
        {
            var camera = world.SamplePosition(new WorldTileId(1, 1), 64, 64, 129);
            var actor = world.SamplePosition(new WorldTileId(10, 10), 64, 64, 129);
            var tiles = world.RequiredTiles(new[] { actor, camera, actor }, camera);
            Assert.That(tiles.Count, Is.EqualTo(18));
            Assert.That(tiles, Does.Contain(new WorldTileId(10, 10)));
            Assert.That(tiles, Is.EqualTo(world.RequiredTiles(new[] { camera, actor }, camera)));
            Assert.That(world.RequiredTiles(Array.Empty<WorldPosition>(), world.Origin).Count, Is.EqualTo(4));
        }

        [Test]
        public void CorrectedLegendAllowsOverlappingHuntingBerriesAndAgricultureWithoutMakingThemSettlements()
        {
            var combined = world.Zones.Single(x => x.Id == "northern_hunting_berries");
            var uses = world.UsesAt(combined.Center.X, combined.Center.Z);
            Assert.That(uses.HasFlag(WorldZoneUse.Hunting), Is.True);
            Assert.That(uses.HasFlag(WorldZoneUse.Berries), Is.True);
            Assert.That(uses.HasFlag(WorldZoneUse.Settlement), Is.False);
            Assert.That(world.Zones.Where(x => x.Id.StartsWith("farmland_"))
                .All(x => x.Uses == WorldZoneUse.Agriculture), Is.True);
            Assert.That(world.Zones.Count(x => x.Uses == WorldZoneUse.Settlement), Is.EqualTo(3));
            Assert.That(world.Zones.Count(x => x.Uses == WorldZoneUse.Fishing), Is.GreaterThanOrEqualTo(8));
            Assert.That(world.River.First().Z, Is.EqualTo(world.Origin.Z + world.DepthMeters));
            Assert.That(world.River.Last().Z, Is.EqualTo(world.Origin.Z));
        }

        [Test]
        public void PlayerStartsInTheLowerCentralGreySettlement()
        {
            var start = world.Zones.Single(x => x.Id == "starter_settlement");
            Assert.That(start.Contains(world.PlayerStart.X, world.PlayerStart.Z), Is.True);
            Assert.That(world.PlayerStart.Z, Is.LessThan(world.Origin.Z + world.DepthMeters * 0.25f));
            Assert.That((world.PlayerStart.X - world.Origin.X) / world.WidthMeters, Is.InRange(0.45f, 0.65f));
        }
    }
}
