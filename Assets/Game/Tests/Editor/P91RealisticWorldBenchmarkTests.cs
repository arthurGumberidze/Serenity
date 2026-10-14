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
        public void OfficialTerrainContentIsPromotedIntoTheGameplayMap()
        {
            var region = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            Assert.That(region, Is.Not.Null);
            Assert.DoesNotThrow(region.ValidateConfiguration);
            Assert.That(region.SourceAsset, Does.Contain("213197"));
            Assert.That(region.Terrain.terrainData.terrainLayers, Has.Length.GreaterThanOrEqualTo(4));
            Assert.That(region.Terrain.terrainData.terrainLayers.All(x =>
                x != null && UnityEditor.AssetDatabase.GetAssetPath(x).StartsWith("Assets/TerrainDemoScene_URP/")), Is.True);
            Assert.That(GameObject.Find("P9.1 Realistic World Benchmark"), Is.Null);
            Assert.That(GameObject.Find("P3 Reference World"), Is.Null);
        }

        [Test]
        public void PromotedTerrainHasDenseValidEnvironmentAndPhysicalTerrain()
        {
            var region = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            Assert.That(region.Trees.Count, Is.GreaterThanOrEqualTo(40));
            Assert.That(region.Rocks.Count, Is.GreaterThanOrEqualTo(22));
            Assert.That(region.Foliage.Count, Is.GreaterThanOrEqualTo(96));
            Assert.That(region.Terrain.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(region.Trees.Concat(region.Rocks).All(x =>
                x.GetComponentInChildren<Collider>(true) != null), Is.True);
            Assert.That(region.WaterRenderer.sharedMaterial.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
        }
    }
}
