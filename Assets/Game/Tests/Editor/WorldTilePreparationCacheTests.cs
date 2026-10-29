using System;
using System.IO;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("WM15")]
    public sealed class WorldTilePreparationCacheTests
    {
        private string temporaryRoot;

        [SetUp]
        public void SetUp() => temporaryRoot = Path.Combine(Path.GetTempPath(), "SerenityWorldTileCacheTests",
            Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryRoot)) Directory.Delete(temporaryRoot, true);
        }

        [Test]
        public void PreparedTileAndOverviewRoundTripAndCompletionMarkerAreSeedScoped()
        {
            var world = TiledWorldDefinitionLoader.LoadReference();
            var sampler = new TiledWorldHeightSampler(world, 29102026L);
            const int paintResolution = 4;
            const int paintLayers = 2;
            var cache = new WorldTilePreparationCache(temporaryRoot, world, sampler,
                paintResolution, paintLayers);
            var tile = new WorldTileId(0, 0);
            var heights = new float[sampler.HeightmapResolution, sampler.HeightmapResolution];
            heights[3, 7] = 0.375f;
            var paint = new float[paintResolution, paintResolution, paintLayers];
            paint[2, 1, 0] = 0.25f;
            paint[2, 1, 1] = 0.75f;

            Assert.That(cache.IsComplete(new[] { tile }), Is.False);
            cache.Write(tile, heights, paint);
            Assert.That(cache.TryRead(tile, out var restoredHeights, out var restoredPaint), Is.True);
            Assert.That(restoredHeights[3, 7], Is.EqualTo(0.375f));
            Assert.That(restoredPaint[2, 1, 0], Is.EqualTo(0.25f));
            Assert.That(restoredPaint[2, 1, 1], Is.EqualTo(0.75f));

            var overview = new byte[4 * 4 * 4];
            overview[17] = 211;
            cache.WriteOverview(4, overview);
            Assert.That(cache.TryReadOverview(4, out var restoredOverview), Is.True);
            Assert.That(restoredOverview[17], Is.EqualTo(211));

            cache.MarkComplete(new[] { tile });
            Assert.That(cache.IsComplete(new[] { tile }), Is.True);

            var otherSeed = new WorldTilePreparationCache(temporaryRoot, world,
                new TiledWorldHeightSampler(world, 29102027L), paintResolution, paintLayers);
            Assert.That(otherSeed.DirectoryPath, Is.Not.EqualTo(cache.DirectoryPath));
            Assert.That(otherSeed.TryRead(tile, out _, out _), Is.False);
            Assert.That(otherSeed.IsComplete(new[] { tile }), Is.False);
        }
    }
}
