using System;
using System.Collections;
using System.Linq;
using Game.Domain.Campaign;
using Game.Domain.World;
using Game.Infrastructure;
using Game.Simulation.Campaign;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    [Category("WM10")]
    public sealed class WM10WorldMapStartTerritoryPlayModeTests
    {
        [UnityTest]
        [Timeout(360000)]
        public IEnumerator NewGameStartHasResidentsResourcesBuildSitesAndACompletePathToTheRiver()
        {
            const long seed = 20261023L;
            var definition = TiledWorldDefinitionLoader.LoadProposedWorldMap();
            var heights = new TiledWorldHeightSampler(definition, seed);
            var population = new TiledWorldPopulationPlanner(definition, heights, seed).Create();
            var newGame = NewGameBootstrap.Create(new NewGameConfiguration(seed), populationPlan: population);
            var start = newGame.StartTerritory;

            Assert.That(newGame.PlayerCharacters, Has.Count.EqualTo(10));
            Assert.That(start.ResidentSpawns, Has.Count.EqualTo(10));
            Assert.That(start.StartingResources, Has.Count.GreaterThanOrEqualTo(29));
            Assert.That(start.StartingObjects, Has.Count.EqualTo(2));
            Assert.That(start.FirstConstructionSites, Has.Count.EqualTo(4));
            Assert.That(start.ExpansionCorridors, Has.Count.EqualTo(3));

            var rootObject = new GameObject("WM10 Start Territory Smoke");
            var world = rootObject.AddComponent<TiledTerrainWorld>();
            try
            {
                world.Initialize(definition, heights, null);
                var riverRoute = world.RoadNetworkPlan.Routes.Single(value => value.Id == start.RiverRouteId);
                var destination = riverRoute.Samples.Last().Position;
                world.RefreshNow(start.Center, new[] { start.Center, start.RiverAccessPoint, destination },
                    new[] { Tuple.Create(start.Center, destination) });
                world.BuildNavigation();
                yield return null;

                Assert.That(NavMesh.SamplePosition(ToVector(start.Center), out var startHit, 8f,
                    NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(ToVector(start.RiverAccessPoint), out var riverHit, 8f,
                    NavMesh.AllAreas), Is.True);
                Assert.That(NavMesh.SamplePosition(ToVector(destination), out var destinationHit, 8f,
                    NavMesh.AllAreas), Is.True);

                var pathToRiver = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(startHit.position, riverHit.position,
                    NavMesh.AllAreas, pathToRiver), Is.True);
                Assert.That(pathToRiver.status, Is.EqualTo(NavMeshPathStatus.PathComplete));

                var pathAcrossFord = new NavMeshPath();
                Assert.That(NavMesh.CalculatePath(startHit.position, destinationHit.position,
                    NavMesh.AllAreas, pathAcrossFord), Is.True);
                Assert.That(pathAcrossFord.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
                Assert.That(world.RoadNetworkPlan.Crossings.Single(value =>
                    value.Definition.Id == start.RiverCrossingId).Link, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
            yield return null;
        }

        private static Vector3 ToVector(Game.Domain.AI.WorldPosition value) =>
            new Vector3(value.X, value.Y, value.Z);
    }
}
