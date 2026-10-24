using Game.Domain;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using System.Linq;

namespace Game.Tests.Editor
{
    [Category("WM15")]
    public sealed class WM15WorldMapActivationTests
    {
        [Test]
        public void NormalNewGameUsesWorldMapV2AndItsIsolatedSaveProfile()
        {
            var active = TiledWorldDefinitionLoader.LoadActive();

            Assert.That(active.GenerationId, Is.EqualTo(TiledWorldDefinition.ProposedWorldMapGenerationId));
            Assert.That(active.MapData, Is.Not.Null);
            Assert.That(StartupSaveProfile.Active, Is.SameAs(StartupSaveProfile.WorldMapV2Proposed));
            Assert.That(StartupSaveProfile.Active.WorldGenerationId, Is.EqualTo(active.GenerationId));
            Assert.That(StartupSaveProfile.Active.FormatVersion, Is.EqualTo(SaveSnapshot.WorldMapV2Version));
            Assert.That(StartupSaveProfile.Active.ResolveDirectory("C:/profile"),
                Does.EndWith("WorldMapV2\\Saves"));
            Assert.That(StartupSaveProfile.Current.ResolveDirectory("C:/profile"),
                Is.Not.EqualTo(StartupSaveProfile.Active.ResolveDirectory("C:/profile")));
        }

        [TestCase(20261006L)]
        [TestCase(20261021L)]
        [TestCase(20261023L)]
        public void NormalNewGameSeedsHavePreparedStartResourcesAndExpansionGround(long seed)
        {
            var active = TiledWorldDefinitionLoader.LoadActive();
            var heights = new TiledWorldHeightSampler(active, seed);

            Assert.DoesNotThrow(() =>
                new TiledWorldPopulationPlanner(active, heights, seed).Create());
        }

        [Test]
        public void StartExpansionThatRequiresClearingHasVisibleVegetation()
        {
            var active = TiledWorldDefinitionLoader.LoadActive();
            var heights = new TiledWorldHeightSampler(active, 20261021L);
            var plan = new WorldMapVegetationPlanner(active, heights, 20261021L).Create();
            var start = active.MapData.StartTerritory.DesignPosition;

            Assert.That(plan.Instances.Count(value =>
            {
                if (value.Kind == WorldMapVegetationKind.GroundCover) return false;
                var point = active.MapData.CoordinateTransform.ToDesign(value.Position);
                var x = point.X - start.X;
                var z = point.Z - start.Z;
                var distanceSquared = x * x + z * z;
                return distanceSquared >= 58f * 58f && distanceSquared <= 150f * 150f;
            }), Is.GreaterThan(0));
        }
    }
}
