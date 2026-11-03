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
            Assert.That(StartupSaveProfile.Active, Is.SameAs(StartupSaveProfile.SR02Sample));
            Assert.That(StartupSaveProfile.Active.WorldGenerationId, Is.EqualTo(active.GenerationId));
            Assert.That(StartupSaveProfile.Active.FormatVersion, Is.EqualTo(SaveSnapshot.WorldMapV2Version));
            Assert.That(StartupSaveProfile.Active.ResolveDirectory("C:/profile"),
                Does.EndWith("SR02SampleV1\\Saves"));
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

        [Test]
        public void WildlifeSpawnsDoNotOverlapOrOccupyTheStarterCamp()
        {
            var world = TiledWorldDefinitionLoader.LoadActive();
            var plan = new WorldMapWildlifePlanner(world, new TiledWorldHeightSampler(world, 20261021L), 20261021L).Create();
            for (var i = 0; i < plan.Individuals.Count; i++)
            {
                var a = plan.Individuals[i].Position;
                var dx = a.X - world.PlayerStart.X; var dz = a.Z - world.PlayerStart.Z;
                Assert.That(dx * dx + dz * dz, Is.GreaterThan(60f * 60f));
                for (var j = i + 1; j < plan.Individuals.Count; j++)
                {
                    var b = plan.Individuals[j].Position;
                    dx = a.X - b.X; dz = a.Z - b.Z;
                    Assert.That(dx * dx + dz * dz, Is.GreaterThanOrEqualTo(9f), "Overlapping animal pair");
                }
            }
        }

        [Test]
        public void EveryPoiEventHasLocalizedTitleBodyAndChoice()
        {
            var world = TiledWorldDefinitionLoader.LoadActive();
            var heights = new TiledWorldHeightSampler(world, 20261021L);
            var roads = new WorldMapRoadNetworkPlanner(world, heights).Create();
            var plan = new WorldMapPoiPlanner(world, heights, roads, 20261021L).Create();
            foreach (var point in plan.Points)
            {
                var id = Game.Simulation.World.WorldMapPoiEventCatalog.DefinitionId(point).ToString();
                Assert.That(Game.Presentation.Localization.RussianTextCatalog.EventTitle(id, "discovered"),
                    Is.Not.EqualTo(Game.Presentation.Localization.RussianTextCatalog.MissingText));
                Assert.That(Game.Presentation.Localization.RussianTextCatalog.EventDescription(id, "discovered"),
                    Is.Not.EqualTo(Game.Presentation.Localization.RussianTextCatalog.MissingText));
                Assert.That(Game.Presentation.Localization.RussianTextCatalog.EventChoice(id, "discovered", "acknowledge"),
                    Is.EqualTo("Отметить открытие"));
            }
        }
    }
}
