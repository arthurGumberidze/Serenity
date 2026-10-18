using System;
using System.Linq;
using Game.Presentation.Environment;
using Game.Infrastructure.Performance;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Tests.Editor
{
    [Category("P109")]
    public sealed class P109IntegrationStabilizationTests
    {
        [Test]
        public void ConnectedRoutesAreNarrowTerrainConformingAndDoNotCastBroadShadows()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity");
            var world = UnityEngine.Object.FindAnyObjectByType<P102ConnectedWorldPresentation>();
            Assert.That(world, Is.Not.Null);
            var routes = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                .Where(x => x.name.StartsWith("P10.2 Physical Route ", StringComparison.Ordinal))
                .OrderBy(x => x.name).ToArray();

            Assert.That(routes, Has.Length.EqualTo(13));
            foreach (var route in routes)
            {
                Assert.That(route.widthMultiplier, Is.InRange(0.5f, 0.8f), route.name);
                Assert.That(route.positionCount, Is.GreaterThanOrEqualTo(3), route.name);
                Assert.That(route.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), route.name);
                Assert.That(route.receiveShadows, Is.False, route.name);
                for (var index = 0; index < route.positionCount; index++)
                {
                    var point = route.GetPosition(index);
                    var terrainY = world.Terrain.SampleHeight(point) + world.Terrain.transform.position.y;
                    Assert.That(Mathf.Abs(point.y - terrainY), Is.LessThan(0.08f), route.name);
                    if (index > 0)
                    {
                        var previous = route.GetPosition(index - 1);
                        Assert.That(Vector2.Distance(new Vector2(previous.x, previous.z), new Vector2(point.x, point.z)),
                            Is.LessThanOrEqualTo(3.2f), route.name);
                    }
                }
            }
        }

        [Test]
        public void StarterHasFourStableNaturalSourcesWithinPhysicalWalkingDistance()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity");
            var biome = UnityEngine.Object.FindAnyObjectByType<P93PopulatedBiomeWorld>();
            var region = UnityEngine.Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            Assert.That(biome, Is.Not.Null);
            Assert.That(region, Is.Not.Null);

            var starter = region.PlayerTerritoryAnchor.position;
            var sources = biome.ResourceFormations.OrderBy(x => x.StableIndex).Take(4).ToArray();
            Assert.That(sources.Select(x => x.StableIndex), Is.EqualTo(Enumerable.Range(0, 4)));
            Assert.That(sources.All(x => Vector3.Distance(x.ClickableRoot.position, starter) <= 20f), Is.True);
            Assert.That(sources.Select(x => x.ClickableRoot.position).Distinct().Count(), Is.EqualTo(4));
        }

        [Test]
        public void PerformanceProfileRequiresExactFullHdWithoutChangingTheU29Default()
        {
            var config = U29BenchmarkConfig.Parse(new[]
            {
                "Serenity.exe", "-u29Benchmark", "-p109Performance", "-u29Population", "100",
                "-u29Repeat", "1", "-u29RepeatCount", "3", "-u29WarmupSeconds", "60",
                "-u29MeasurementSeconds", "300", "-u29Seed", "10918", "-u29Width", "1920",
                "-u29Height", "1080", "-u29ConfigId", "p109-full-hd", "-u29BaseCommit", "test",
                "-u29OutputDirectory", "C:\\p109-test"
            });
            Assert.That(config.p109IntegrationProfile, Is.True);
            Assert.That(config.width, Is.EqualTo(1920));
            Assert.That(config.height, Is.EqualTo(1080));

            Assert.Throws<InvalidOperationException>(() => U29BenchmarkConfig.Parse(new[]
            {
                "Serenity.exe", "-u29Benchmark", "-u29Population", "100", "-u29Repeat", "1",
                "-u29RepeatCount", "3", "-u29WarmupSeconds", "60", "-u29MeasurementSeconds", "300",
                "-u29Seed", "10918", "-u29Width", "1920", "-u29Height", "1080",
                "-u29ConfigId", "wrong", "-u29BaseCommit", "test", "-u29OutputDirectory", "C:\\wrong"
            }));
        }
    }
}
