using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    public sealed class P109CompactWorldTests
    {
        [Test]
        public void IntegratedArtHasRenderableMaterialsAndNoVendorGameplayScripts()
        {
            var art = UnityEngine.Resources.Load<Game.Presentation.Environment.CompactWorldArtCatalog>("World/CompactWorldArt");
            Assert.That(art, Is.Not.Null);
            Assert.That(art.FishPattern, Is.Not.Null);
            foreach (var prefab in new[] { art.HuntingDeer, art.StoneOutcrop, art.FieldFence })
            {
                Assert.That(prefab, Is.Not.Null);
                Assert.That(prefab.GetComponentsInChildren<UnityEngine.MonoBehaviour>(true), Is.Empty);
                var renderers = prefab.GetComponentsInChildren<UnityEngine.Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, prefab.name);
                foreach (var material in renderers.SelectMany(x => x.sharedMaterials))
                {
                    Assert.That(material, Is.Not.Null, prefab.name);
                    Assert.That(material.shader.name, Does.StartWith("Universal Render Pipeline/"), prefab.name);
                }
            }
            var animator = art.HuntingDeer.GetComponentInChildren<UnityEngine.Animator>(true);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [TestCase(20261021L)]
        [TestCase(166496655L)]
        [TestCase(1L)]
        [TestCase(91234L)]
        public void EveryLakeHasFourDistinctSchoolsWithDryWorkAndOutputPositions(long seed)
        {
            var world = TiledWorldDefinitionLoader.LoadReference();
            var heights = new TiledWorldHeightSampler(world, seed);
            var plan = new TiledWorldPopulationPlanner(world, heights, seed).Create();
            foreach (var lake in world.Zones.Where(x => x.Uses.HasFlag(WorldZoneUse.Fishing)))
            {
                var schools = plan.Resources.Where(x => x.RecipeId == "fish_water" && x.SourceZoneId == lake.Id).ToArray();
                Assert.That(schools.Length, Is.EqualTo(4), lake.Id);
                Assert.That(schools.Select(x => x.Position).Distinct().Count(), Is.EqualTo(4));
                Assert.That(schools.Select(x => x.VisualPosition).Distinct().Count(), Is.EqualTo(4));
                foreach (var school in schools)
                {
                    Assert.That(heights.IsWater(school.VisualPosition.X, school.VisualPosition.Z), Is.True, lake.Id);
                    Assert.That(heights.IsWater(school.Position.X, school.Position.Z), Is.False, lake.Id);
                    Assert.That(heights.IsBuildable(school.Position.X, school.Position.Z, 30f), Is.True, lake.Id);
                    Assert.That(school.Position.Y, Is.GreaterThan(TiledWorldHeightSampler.WaterLevelMeters + 0.75f));
                }
            }
            Assert.That(plan.Resources.All(x => world.TryGetTile(x.Position.X, x.Position.Z, out _)), Is.True);
            Assert.That(plan.Resources.Where(x => x.RecipeId != "fish_water")
                .All(x => !heights.IsWater(x.Position.X, x.Position.Z)), Is.True);
        }

        [Test]
        public void OriginalLandmarksKeepTheirRelativePositionsAndLegacyGenerationIsNotReused()
        {
            var world = TiledWorldDefinitionLoader.LoadReference();
            Assert.That(world.GenerationId, Is.Not.EqualTo("p109-worksite-v2"));
            Assert.That(world.Origin.X, Is.EqualTo(-570));
            Assert.That(world.Origin.Z, Is.EqualTo(-120));
            var northwest = world.Zones.Single(x => x.Id == "northwest_settlement");
            var eastOre = world.Zones.Single(x => x.Id == "ore_east");
            Assert.That(northwest.Center.X, Is.EqualTo(-4680f / 12));
            Assert.That(northwest.Center.Z, Is.EqualTo(9360f / 12));
            Assert.That(eastOre.Center.X, Is.EqualTo(4440f / 12));
            Assert.That(eastOre.Center.Z, Is.EqualTo(4080f / 12));
            Assert.That(world.River.Count, Is.EqualTo(11));
            Assert.That(world.Zones.Count(x => x.Uses == WorldZoneUse.Settlement), Is.EqualTo(3));
        }
    }
}
