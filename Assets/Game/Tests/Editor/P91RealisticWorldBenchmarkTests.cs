using System.Linq;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P91")]
    public sealed class P91RealisticWorldBenchmarkTests
    {
        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);

        [Test]
        public void BenchmarkUsesOfficialTerrainContentAndPreservesExistingMap()
        {
            var benchmark = Object.FindAnyObjectByType<P91RealisticWorldBenchmark>();
            Assert.That(benchmark, Is.Not.Null);
            Assert.DoesNotThrow(benchmark.ValidateConfiguration);
            Assert.That(benchmark.SourceAsset, Does.Contain("213197"));
            Assert.That(benchmark.Terrain.terrainData.terrainLayers, Has.Length.GreaterThanOrEqualTo(4));
            Assert.That(benchmark.Terrain.terrainData.terrainLayers.All(x =>
                x != null && UnityEditor.AssetDatabase.GetAssetPath(x).StartsWith("Assets/TerrainDemoScene_URP/")), Is.True);
            Assert.That(GameObject.Find("P3 Reference World"), Is.Not.Null, "P9.1 must not delete the legacy map.");
        }

        [Test]
        public void BenchmarkHasDenseValidEnvironmentAndPhysicalTerrain()
        {
            var benchmark = Object.FindAnyObjectByType<P91RealisticWorldBenchmark>();
            Assert.That(benchmark.Trees.Count, Is.GreaterThanOrEqualTo(24));
            Assert.That(benchmark.Rocks.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(benchmark.Foliage.Count, Is.GreaterThanOrEqualTo(48));
            Assert.That(benchmark.Terrain.GetComponent<TerrainCollider>(), Is.Not.Null);
            Assert.That(benchmark.Trees.Concat(benchmark.Rocks).All(x =>
                x.GetComponentInChildren<Collider>(true) != null), Is.True);
            Assert.That(benchmark.WaterRenderer.sharedMaterial.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
        }
    }
}
