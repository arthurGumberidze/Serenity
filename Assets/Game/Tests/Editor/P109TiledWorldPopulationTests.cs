using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P109")]
    public sealed class P109TiledWorldPopulationTests
    {
        [Test]
        public void CanonicalPopulationIsDeterministicDenseAndCoversEveryTileAndZoneUse()
        {
            var world = TiledWorldDefinitionLoader.LoadReference();
            var heights = new TiledWorldHeightSampler(world, 20261019L);
            var first = new TiledWorldPopulationPlanner(world, heights, 20261019L).Create();
            var second = new TiledWorldPopulationPlanner(world, heights, 20261019L).Create();

            Assert.That(first.Resources.Count, Is.GreaterThanOrEqualTo(720));
            Assert.That(first.Resources.Select(value => value.StableIndex),
                Is.EqualTo(second.Resources.Select(value => value.StableIndex)));
            Assert.That(first.Resources.Select(value => value.Position),
                Is.EqualTo(second.Resources.Select(value => value.Position)));
            Assert.That(first.Resources.GroupBy(value => value.Tile).Count(), Is.EqualTo(144));
            Assert.That(first.Resources.GroupBy(value => value.Tile).All(group => group.Count() >= 5), Is.True);
            Assert.That(first.Resources.Select(value => value.RecipeId), Is.SupersetOf(new[]
            {
                "chop_wood", "gather_stone", "gather_berries", "gather_fiber",
                "hunt_animal", "fish_water", "gather_iron", "gather_gold"
            }));
            Assert.That(first.Resources.All(value => value.Capacity >= 20 && value.Capacity <= 50), Is.True);
            Assert.That(first.Resources.Select(value => value.StableIndex).Distinct().Count(),
                Is.EqualTo(first.Resources.Count));

            foreach (var zone in world.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Forest) ||
                         value.Uses.HasFlag(WorldZoneUse.Hunting) || value.Uses.HasFlag(WorldZoneUse.Ore) ||
                         value.Uses.HasFlag(WorldZoneUse.Fishing)))
                Assert.That(first.Resources.Any(value => value.SourceZoneId == zone.Id), Is.True, zone.Id);
            Assert.That(first.Industry.Select(value => value.SourceZoneId).Distinct(),
                Is.SupersetOf(world.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Forest) ||
                    value.Uses.HasFlag(WorldZoneUse.Hunting) || value.Uses.HasFlag(WorldZoneUse.Ore) ||
                    value.Uses.HasFlag(WorldZoneUse.Fishing)).Select(value => value.Id)));
        }

        [Test]
        public void AgriculturalCentersAreDryBuildableGentleGround()
        {
            var world = TiledWorldDefinitionLoader.LoadReference();
            var heights = new TiledWorldHeightSampler(world, 20261019L);
            foreach (var zone in world.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Agriculture)))
            {
                Assert.That(heights.IsWater(zone.Center.X, zone.Center.Z), Is.False, zone.Id);
                Assert.That(heights.IsBuildable(zone.Center.X, zone.Center.Z, 12f), Is.True, zone.Id);
            }
        }
    }
}
