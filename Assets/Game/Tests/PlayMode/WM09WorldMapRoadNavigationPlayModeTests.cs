using System;
using System.Collections;
using System.Linq;
using Game.Domain.World;
using Game.Infrastructure;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM09")]
    public sealed class WM09WorldMapRoadNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerRouteCrossesTheNorthwestFordOnTheExistingNavMesh()
        {
            var definition = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(definition, 20261023L);
            var rootObject = new GameObject("WM09 Player Route Smoke");
            var world = rootObject.AddComponent<TiledTerrainWorld>();
            try
            {
                world.Initialize(definition, heights, null);
                Assert.That(world.RoadNetworkPlan.Routes, Has.Count.EqualTo(7));
                Assert.That(world.RoadPresentationRoot.GetComponentsInChildren<LineRenderer>(true),
                    Has.Length.EqualTo(7));
                Assert.That(world.RoadPresentationRoot.GetComponentsInChildren<MeshCollider>(true)
                    .Count(value => value.name == "Navigation Ribbon"), Is.EqualTo(7));
                Assert.That(world.RoadPresentationRoot.transform.Cast<Transform>().Count(value =>
                    value.name.StartsWith("World Map V2 Crossing ", StringComparison.Ordinal)), Is.EqualTo(3));

                var route = world.RoadNetworkPlan.Routes.Single(value => value.Id == "path_start_west_lake");
                var start = route.Samples.First().Position;
                var destination = route.Samples.Last().Position;
                world.RefreshNow(start, new[] { start, destination }, new[] { Tuple.Create(start, destination) });
                world.BuildNavigation();
                yield return null;

                Assert.That(NavMesh.SamplePosition(new Vector3(start.X, start.Y, start.Z), out var startHit,
                    8f, NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(new Vector3(destination.X, destination.Y, destination.Z),
                    out var destinationHit, 8f, NavMesh.AllAreas), Is.True);
                var path = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(startHit.position, destinationHit.position,
                    NavMesh.AllAreas, path), Is.True);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(world.RoadNetworkPlan.Crossings.Single(value =>
                    value.Definition.Id == "crossing_start_ford").Link, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
            yield return null;
        }
    }
}
