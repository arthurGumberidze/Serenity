using System.Collections;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM03")]
    public sealed class WM03WorldMapHydrologyPlayModeTests
    {
        [UnityTest]
        public IEnumerator AllFourLakeBanksStayDryAtRuntime()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, 20261023L);
            Assert.That(world.MapData.Hydrology.Lakes, Has.Count.EqualTo(4));
            foreach (var lake in world.MapData.Hydrology.Lakes)
            foreach (var landing in lake.FishingLandings)
            {
                var runtime = world.MapData.CoordinateTransform.ToUnity(landing);
                Assert.That(heights.TrySampleWaterSurface(runtime.X, runtime.Z, out _), Is.False,
                    lake.Id + " bank " + landing);
                Assert.That(heights.SampleHeight(runtime.X, runtime.Z), Is.GreaterThan(lake.SurfaceY + 0.75f),
                    lake.Id + " bank " + landing);
            }
            yield return null;
        }
    }
}
