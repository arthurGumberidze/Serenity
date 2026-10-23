using System.Globalization;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("WM09")]
    public sealed class WM09WorldMapRoadNavigationTests
    {
        [Test]
        public void CanonicalPlanHasSevenRoutesThreeCrossingsAndReachableRegions()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, 20261023L);
            var plan = new WorldMapRoadNetworkPlanner(world, heights).Create();

            Assert.That(plan.Routes, Has.Count.EqualTo(7));
            Assert.That(plan.Crossings, Has.Count.EqualTo(3));
            Assert.That(plan.Crossings.Select(value => value.Definition.Id), Is.EquivalentTo(new[]
            {
                "crossing_start_ford", "crossing_main_bridge", "crossing_lake_footbridge"
            }));
            Assert.That(plan.Links.Select(value => value.Id).Distinct().Count(), Is.EqualTo(plan.Links.Count));
            Assert.That(plan.WaterTraversalIsAuthored, Is.True);
            Assert.That(plan.AllRoutesMeetSlopeBudget, Is.True, string.Join("; ", plan.Routes.Select(value =>
                value.Id + "=" + value.MaximumDrySlopeDegrees.ToString("0.00", CultureInfo.InvariantCulture) +
                "/" + value.MaximumSlopeDegrees.ToString("0.00", CultureInfo.InvariantCulture))));
            Assert.That(world.MapData.Regions.All(region => plan.IsRegionReachable(region.Id)), Is.True,
                "Every authored region must be connected to the player-start region.");

            foreach (var route in plan.Routes)
            {
                foreach (var sample in route.Samples)
                {
                    if (sample.IsWater)
                    {
                        Assert.That(sample.CrossingId, Is.Not.Empty, route.Id + " " + sample.DesignPosition);
                        Assert.That(heights.TrySampleWaterSurface(sample.Position.X, sample.Position.Z,
                            out var surface), Is.True);
                        Assert.That(sample.Position.Y, Is.GreaterThan(surface));
                    }
                    else
                        Assert.That(sample.Position.Y, Is.GreaterThanOrEqualTo(
                            heights.SampleHeight(sample.Position.X, sample.Position.Z) + 0.075f));
                }
            }

            foreach (var crossing in plan.Crossings)
            {
                Assert.That(heights.IsWater(crossing.Link.Start.X, crossing.Link.Start.Z), Is.False,
                    crossing.Definition.Id + " start");
                Assert.That(heights.IsWater(crossing.Link.End.X, crossing.Link.End.Z), Is.False,
                    crossing.Definition.Id + " end");
            }
        }

        [Test]
        public void PlanAndNavigationLinksAreDeterministic()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, 20261023L);
            var a = new WorldMapRoadNetworkPlanner(world, heights).Create();
            var b = new WorldMapRoadNetworkPlanner(world, heights).Create();
            Assert.That(Fingerprint(a), Is.EqualTo(Fingerprint(b)));
        }

        [Test]
        public void ExistingImportedRoadCrossingAndPassAssetsAreReused()
        {
            var catalog = Resources.Load<StarterNatureCatalog>("World/StarterNature");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(catalog.WorldMapRoadMaterial),
                Is.EqualTo("Assets/Game/Art/Materials/P3/P5_RouteEarth.mat"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WorldMapBridge),
                Is.EqualTo("Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/Props/SM_Prop_Bridge_01.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WorldMapFootbridge),
                Is.EqualTo("Assets/PolygonNatureBiomes/PNB_Tropical_Jungle/Prefabs/SM_Bld_RopeBridge_01.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WorldMapFordMarker),
                Is.EqualTo("Assets/Toon Farm Pack/Prefabs/Ground/TFP_Stone_Step_01A.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WorldMapPassStep),
                Is.EqualTo("Assets/Toon Farm Pack/Prefabs/Ground/TFP_Stone_Step_01A.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WorldMapSign), Does.EndWith("SM_Prop_Sign_01.prefab"));
            Assert.That(AssetDatabase.GetAssetPath(catalog.WorldMapFence),
                Does.EndWith("SM_Prop_Meadow_Fence_01.prefab"));
        }

        private static string Fingerprint(WorldMapRoadNetworkPlan plan)
        {
            return string.Join("|", plan.Routes.SelectMany(route => route.Samples.Select(sample =>
                    route.Id + ":" + sample.Position.X.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                    sample.Position.Y.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                    sample.Position.Z.ToString("0.000", CultureInfo.InvariantCulture) + ":" + sample.CrossingId))
                .Concat(plan.Links.Select(link => link.Id + ":" +
                    link.Start.X.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                    link.Start.Z.ToString("0.000", CultureInfo.InvariantCulture) + "->" +
                    link.End.X.ToString("0.000", CultureInfo.InvariantCulture) + "," +
                    link.End.Z.ToString("0.000", CultureInfo.InvariantCulture))));
        }
    }
}
