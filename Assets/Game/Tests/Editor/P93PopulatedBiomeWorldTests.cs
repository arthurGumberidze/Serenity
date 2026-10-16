using System.Linq;
using Game.Presentation.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P93")]
    public sealed class P93PopulatedBiomeWorldTests
    {
        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity",
            OpenSceneMode.Single);

        [Test]
        public void WholeAccessibleTerrainHasDistinctDeterministicBiomeZonesAndOfficialLayers()
        {
            var marker = Object.FindAnyObjectByType<P93PopulatedBiomeWorld>();
            Assert.That(marker, Is.Not.Null);
            Assert.DoesNotThrow(marker.ValidateConfiguration);
            Assert.That(marker.SourceAsset, Does.Contain("213197"));
            Assert.That(marker.ScatterSeed, Is.EqualTo(93031));
            Assert.That(marker.Zones.Select(x => x.Kind).Distinct().Count(), Is.EqualTo(6));
            Assert.That(marker.Zones.Count, Is.GreaterThanOrEqualTo(7));
            Assert.That(marker.CoverageRatio, Is.GreaterThanOrEqualTo(0.92f));
            Assert.That(marker.LayoutHash, Is.EqualTo(marker.ComputeLayoutHash()));
            Assert.That(marker.BaseRegion.Terrain.terrainData.terrainLayers.Length, Is.GreaterThanOrEqualTo(7));
            Assert.That(marker.BaseRegion.Terrain.terrainData.terrainLayers.All(layer => layer != null &&
                AssetDatabase.GetAssetPath(layer).StartsWith("Assets/TerrainDemoScene_URP/")), Is.True);
        }

        [Test]
        public void LargeObstaclesUseOfficialLodAndCollisionWhileGroundCoverRemainsTraversable()
        {
            var marker = Object.FindAnyObjectByType<P93PopulatedBiomeWorld>();
            Assert.That(marker.CanopyTrees.Count, Is.GreaterThanOrEqualTo(170));
            Assert.That(marker.MajorRocks.Count, Is.GreaterThanOrEqualTo(90));
            Assert.That(marker.GroundCover.Count, Is.GreaterThanOrEqualTo(900));
            Assert.That(marker.CanopyTrees.All(x => x.GetComponentInChildren<LODGroup>(true) != null &&
                x.GetComponentInChildren<Collider>(true) != null), Is.True);
            Assert.That(marker.MajorRocks.All(x => x.GetComponentInChildren<LODGroup>(true) != null &&
                x.GetComponentInChildren<Collider>(true) != null), Is.True);
            Assert.That(marker.GroundCover.All(x => x.GetComponentInChildren<Collider>(true) == null), Is.True);
            foreach (var instance in marker.CanopyTrees.Concat(marker.MajorRocks).Concat(marker.GroundCover))
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
                Assert.That(source, Is.Not.Null, instance.name);
                Assert.That(AssetDatabase.GetAssetPath(source), Does.StartWith("Assets/TerrainDemoScene_URP/"));
            }
        }

        [Test]
        public void EveryCanonicalResourceKeepsOneClickableIdentityInsideAReadableFormation()
        {
            var marker = Object.FindAnyObjectByType<P93PopulatedBiomeWorld>();
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            Assert.That(marker.ResourceFormations.Count, Is.EqualTo(24));
            Assert.That(marker.ResourceFormations.Select(x => x.StableIndex),
                Is.EqualTo(Enumerable.Range(0, 24)));
            Assert.That(marker.ResourceFormations.Select(x => x.RecipeId).Distinct().Count(), Is.EqualTo(6));
            foreach (var formation in marker.ResourceFormations)
            {
                var anchor = region.ResourceAnchors.Single(x => x.StableIndex == formation.StableIndex);
                Assert.That(formation.ClickableRoot, Is.SameAs(anchor.VisualRoot));
                Assert.That(formation.RecipeId, Is.EqualTo(anchor.RecipeId));
                Assert.That(formation.ClickableRoot.GetComponentInChildren<Collider>(true), Is.Not.Null);
                Assert.That(formation.DecorativeSatellites.Count, Is.GreaterThanOrEqualTo(4));
                Assert.That(formation.DecorativeSatellites.All(x =>
                    x.GetComponentInChildren<Collider>(true) == null), Is.True);
            }
            Assert.That(marker.BaseRegion.Locations.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(region.ResourceAnchors.Select(x => x.StableIndex).OrderBy(x => x),
                Is.EqualTo(Enumerable.Range(0, 24)));
        }
    }
}
