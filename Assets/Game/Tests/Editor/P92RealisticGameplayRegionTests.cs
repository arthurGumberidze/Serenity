using System.Linq;
using Game.Presentation.Environment;
using Game.Presentation.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P92")]
    public sealed class P92RealisticGameplayRegionTests
    {
        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);

        [Test]
        public void ProductionTerrainUsesOfficialPbrSourceAndRemovesSupersededVisibleMaps()
        {
            var marker = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            Assert.That(marker, Is.Not.Null);
            Assert.DoesNotThrow(marker.ValidateConfiguration);
            Assert.That(marker.SourceAsset, Does.Contain("213197"));
            Assert.That(marker.Terrain.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(marker.Terrain.GetComponent<BuildableGround>(), Is.Not.Null);
            Assert.That(marker.Terrain.terrainData.terrainLayers.All(x => x != null &&
                AssetDatabase.GetAssetPath(x).StartsWith("Assets/TerrainDemoScene_URP/")), Is.True);
            Assert.That(GameObject.Find("P3 Reference World"), Is.Null);
            Assert.That(GameObject.Find("P3 Buildable Forest Clearing"), Is.Null);
            Assert.That(GameObject.Find("P8.1 Visual Prototype"), Is.Null);
            Assert.That(GameObject.Find("P9.1 Realistic World Benchmark"), Is.Null);
        }

        [Test]
        public void TerrainHasReliefTrailsClearingsWaterAndPreservedCanonicalLocationAnchors()
        {
            var marker = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            Assert.That(marker.Locations.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(marker.Locations.Select(x => x.LocationId), Is.SupersetOf(new[]
            {
                "starter_settlement", "enemy_settlement", "north_forest",
                "river_ford", "east_hills", "south_field"
            }));
            Assert.That(region.RouteWaypoints.Count, Is.EqualTo(6));
            Assert.That(region.BuildableAreaAnchors.Count, Is.EqualTo(3));
            Assert.That(region.SeasonalTerrain, Is.SameAs(marker.Terrain));
            Assert.That(marker.WaterRenderer.sharedMaterial.shader.name, Is.Not.EqualTo("Hidden/InternalErrorShader"));
            var low = marker.ProjectToGround(new Vector3(0f, 0f, 0f)).y;
            var high = marker.ProjectToGround(new Vector3(20f, 0f, 31f)).y;
            Assert.That(Mathf.Abs(high - low), Is.GreaterThan(2f), "The migrated map needs meaningful height variation.");
            foreach (var clearing in region.BuildableAreaAnchors)
            {
                var center = marker.ProjectToGround(clearing.position).y;
                var nearby = marker.ProjectToGround(clearing.position + new Vector3(2f, 0f, 2f)).y;
                Assert.That(Mathf.Abs(center - nearby), Is.LessThan(0.8f), clearing.name);
            }
        }

        [Test]
        public void AllStableResourceIndicesUseOfficialSamplePrefabViewsAndPhysicalSelection()
        {
            var marker = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            var required = new[]
            {
                "gather_berries", "gather_fiber", "chop_wood",
                "gather_stone", "gather_iron", "gather_gold"
            };
            Assert.That(region.ResourceAnchors.Count, Is.EqualTo(24));
            Assert.That(region.ResourceAnchors.Select(x => x.StableIndex).OrderBy(x => x),
                Is.EqualTo(Enumerable.Range(0, 24)));
            Assert.That(required.All(recipe => region.ResourceAnchors.Count(x => x.RecipeId == recipe) == 4), Is.True);
            foreach (var anchor in region.ResourceAnchors)
            {
                Assert.That(anchor.VisualRoot.GetComponentInChildren<Collider>(true), Is.Not.Null, anchor.VisualRoot.name);
                var prefab = anchor.VisualRoot.GetComponentsInChildren<Transform>(true)
                    .Select(x => PrefabUtility.GetCorrespondingObjectFromSource(x.gameObject))
                    .FirstOrDefault(x => x != null);
                Assert.That(prefab, Is.Not.Null, anchor.VisualRoot.name);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Does.StartWith("Assets/TerrainDemoScene_URP/"));
            }
            Assert.That(marker.Trees.All(x => x.GetComponentInChildren<Collider>(true) != null), Is.True);
            Assert.That(marker.Rocks.All(x => x.GetComponentInChildren<Collider>(true) != null), Is.True);
        }
    }
}
