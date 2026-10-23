using System;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("WM12")]
    public sealed class WM12WorldMapBuildabilityTests
    {
        private const long Seed = 20261023L;
        private TiledWorldDefinition world;
        private TiledWorldHeightSampler heights;
        private WorldMapBuildabilityEvaluator evaluator;

        [OneTimeSetUp]
        public void CreateFixture()
        {
            world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            heights = new TiledWorldHeightSampler(world, Seed);
            evaluator = new WorldMapBuildabilityEvaluator(world, heights);
        }

        [Test]
        public void AuthoredPolicyRetainsMasksAndExactBlockerTaxonomy()
        {
            var policy = world.MapData.Buildability;
            Assert.That(policy.Zones, Has.Count.EqualTo(8));
            Assert.That(policy.GridCellSizeMeters, Is.EqualTo(2f));
            Assert.That(policy.ClearableBlockerKinds, Is.EquivalentTo(new[]
            {
                "tree", "bush", "berry_bush", "loose_stone", "branches", "plant_fiber"
            }));
            Assert.That(policy.HardBlockerKinds, Is.EquivalentTo(new[]
            {
                "water", "cliff", "unique_poi", "cave_entrance", "deep_marsh", "ridge_monolith"
            }));
            Assert.That(policy.IsClearableRecipe("chop_wood"), Is.True);
            Assert.That(policy.IsClearableRecipe("gather_medicinal_plants"), Is.False);
        }

        [Test]
        public void GreenAmberRedVerdictsAreDeterministicAndFailClosed()
        {
            var green = evaluator.ClassifyBlockers(Array.Empty<string>());
            var amberA = evaluator.ClassifyBlockers(new[] { "tree", "loose_stone", "tree" });
            var amberB = evaluator.ClassifyBlockers(new[] { "loose_stone", "tree" });
            var water = evaluator.ClassifyBlockers(new[] { "water" });
            var unknown = evaluator.ClassifyBlockers(new[] { "decorative_unknown" });

            Assert.That(green.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Green));
            Assert.That(amberA.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Amber));
            Assert.That(amberB.Verdict, Is.EqualTo(amberA.Verdict));
            Assert.That(amberB.Reason, Is.EqualTo(amberA.Reason));
            Assert.That(water.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Red));
            Assert.That(unknown.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Red));
        }

        [Test]
        public void Wm10FirstConstructionSitesRemainGreen()
        {
            var start = new TiledWorldPopulationPlanner(world, heights, Seed).Create().StartTerritory;
            foreach (var site in start.FirstConstructionSites)
            {
                var result = Evaluate(site.Position.X, site.Position.Z, 0.4f);
                Assert.That(result.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Green),
                    site.DefinitionId + ": " + result.Reason);
                Assert.That(result.MaximumSlopeDegrees, Is.LessThanOrEqualTo(8f), site.DefinitionId);
            }
        }

        [Test]
        public void WaterRestrictedMasksAndEveryPoiAreHardRed()
        {
            var river = world.MapData.Hydrology.MainRiver.Spline.Points[5].Position;
            var riverWorld = world.MapData.CoordinateTransform.ToUnity(river);
            Assert.That(Evaluate(riverWorld.X, riverWorld.Z, 0.2f).Verdict,
                Is.EqualTo(WorldMapPlacementVerdict.Red));
            Assert.That(Evaluate(riverWorld.X, riverWorld.Z, 0.2f).Reason,
                Is.EqualTo(WorldMapBuildabilityEvaluator.Water));

            var restricted = FindResult(result => result.Reason.StartsWith(
                WorldMapBuildabilityEvaluator.RestrictedMask, StringComparison.Ordinal));
            Assert.That(restricted.HasValue, Is.True, "An authored restricted zone must produce a red verdict.");
            Assert.That(restricted.Value.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Red));

            foreach (var poi in world.MapData.PointsOfInterest)
            {
                var position = world.MapData.CoordinateTransform.ToUnity(poi.Position);
                var result = Evaluate(position.X, position.Z, 0.2f);
                Assert.That(result.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Red), poi.Id);
                Assert.That(result.Reason, Does.StartWith(WorldMapBuildabilityEvaluator.ProtectedPoi), poi.Id);
            }
        }

        [Test]
        public void ExcessiveSlopeIsADataDrivenHardRejection()
        {
            var steep = FindResult(result => result.Reason == WorldMapBuildabilityEvaluator.ExcessiveSlope);
            Assert.That(steep.HasValue, Is.True, "The authored terrain must expose at least one excessive-slope sample.");
            Assert.That(steep.Value.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Red));
        }

        [Test]
        public void ActiveCompactWorldRemainsOnItsLegacyGenerationAndHasNoV2Policy()
        {
            var active = TiledWorldDefinitionLoader.LoadReference();
            Assert.That(active.GenerationId, Is.EqualTo(TiledWorldDefinition.ReferenceGenerationId));
            Assert.That(active.MapData, Is.Null);
        }

        private WorldMapBuildabilityResult Evaluate(float x, float z, float halfExtent) =>
            evaluator.EvaluateFootprint(new[] { new WorldMapFootprintCell(x, z, halfExtent) });

        private WorldMapBuildabilityResult? FindResult(Func<WorldMapBuildabilityResult, bool> predicate)
        {
            for (var z = world.Origin.Z + 10f; z < world.Origin.Z + world.DepthMeters - 10f; z += 10f)
            for (var x = world.Origin.X + 10f; x < world.Origin.X + world.WidthMeters - 10f; x += 10f)
            {
                var result = Evaluate(x, z, 0.2f);
                if (predicate(result)) return result;
            }
            return null;
        }
    }
}
