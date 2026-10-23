using System.Collections;
using Game.Domain;
using Game.Domain.Production;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Presentation.Production;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM12")]
    public sealed class WM12WorldMapBuildabilityPlayModeTests
    {
        private const long Seed = 20261023L;

        [UnityTest]
        public IEnumerator SurfaceAdapterProjectsGreenAmberAndRedWithoutClearingAnything()
        {
            var world = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(world, Seed);
            var start = new TiledWorldPopulationPlanner(world, heights, Seed).Create().StartTerritory;
            var position = start.FirstConstructionSites[0].Position;
            var origin = new Vector3(position.X, 0f, position.Z);
            var cell = new[] { new Game.Domain.Buildings.GridCoordinate(0, 0) };
            var surfaceObject = new GameObject("WM12 placement surface");
            var surface = surfaceObject.AddComponent<P102WorldPlacementSurface>();
            surface.Configure(heights);

            GameObject blockerObject = null;
            GameObject hardObject = null;
            try
            {
                var green = surface.EvaluateFootprint(cell, 1f, origin);
                Assert.That(green.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Green));

                blockerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blockerObject.name = "WM12 canonical tree blocker";
                blockerObject.transform.position = new Vector3(position.X,
                    heights.SampleHeight(position.X, position.Z) + 1.1f, position.Z);
                var site = ProductionSite.Restore(new ProductionSiteState(Id(12),
                    new ProductionRecipeId("chop_wood"), ProductionSiteKind.NaturalSource,
                    position, 3, 3, CropPlotPhase.Unplanted, 0, 0));
                blockerObject.AddComponent<ProductionSourcePresenter>().Bind(site, Color.green);
                Physics.SyncTransforms();

                var amber = surface.EvaluateFootprint(cell, 1f, origin);
                Assert.That(amber.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Amber));
                Assert.That(amber.ClearableSources, Is.EqualTo(new[] { site.Id }));
                Assert.That(site.RemainingAmount, Is.EqualTo(3), "Preview must not clear or consume the source.");

                Object.Destroy(blockerObject);
                blockerObject = null;
                yield return null;

                hardObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hardObject.name = "WM12 impassable geometry";
                hardObject.transform.position = new Vector3(position.X,
                    heights.SampleHeight(position.X, position.Z) + 1.1f, position.Z);
                Physics.SyncTransforms();
                var red = surface.EvaluateFootprint(cell, 1f, origin);
                Assert.That(red.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Red));
                Assert.That(red.BlockedReason, Does.Contain("неразрушаемым"));

                var poi = world.MapData.PointsOfInterest[0];
                var poiWorld = world.MapData.CoordinateTransform.ToUnity(poi.Position);
                var protectedPoi = surface.EvaluateFootprint(cell, 1f,
                    new Vector3(poiWorld.X, 0f, poiWorld.Z));
                Assert.That(protectedPoi.Verdict, Is.EqualTo(WorldMapPlacementVerdict.Red));
                Assert.That(protectedPoi.BlockedReason, Does.Contain("точка интереса"));
            }
            finally
            {
                if (blockerObject != null) Object.Destroy(blockerObject);
                if (hardObject != null) Object.Destroy(hardObject);
                Object.Destroy(surfaceObject);
            }
            yield return null;
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
