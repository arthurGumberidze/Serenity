using System.Linq;
using Game.Infrastructure;
using Game.Presentation.Environment;
using Game.Simulation.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P5")]
    public sealed class P5InhabitableRegionTests
    {
        [Test]
        public void LocalSceneContainsOneCoherentAuthoredRegion()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var root = Object.FindAnyObjectByType<LocalSceneCompositionRoot>();
            var region = root != null ? root.GetComponent<P5InhabitableRegionPresentation>() : null;
            Assert.That(region, Is.Not.Null);
            Assert.DoesNotThrow(region.ValidateConfiguration);
            Assert.That(region.TreeCount, Is.GreaterThanOrEqualTo(32));
            Assert.That(region.RockCount, Is.GreaterThanOrEqualTo(18));
            Assert.That(region.GrassCount, Is.GreaterThanOrEqualTo(72));
            Assert.That(region.ResourceAnchors.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(region.RouteWaypoints.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(region.BuildableAreaAnchors.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(region.ObstacleAnchors.Count, Is.GreaterThanOrEqualTo(40));
            Assert.That(GameObject.Find("P5 Rival Territory enemy_settlement"), Is.Not.Null);
            Assert.That(GameObject.Find("P5 Player Buildable Clearing"), Is.Not.Null);
            Assert.That(GameObject.Find("P5 South Meadow Buildable Area"), Is.Not.Null);
        }

        [Test]
        public void VisibleNaturalNodesAreAuthoredViewsWithCanonicalRecipes()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            var recipes = region.ResourceAnchors.GroupBy(x => x.RecipeId)
                .ToDictionary(x => x.Key, x => x.Count());
            Assert.That(recipes["gather_berries"], Is.GreaterThanOrEqualTo(3));
            Assert.That(recipes["gather_fiber"], Is.GreaterThanOrEqualTo(3));
            Assert.That(recipes["chop_wood"], Is.GreaterThanOrEqualTo(3));
            Assert.That(recipes["gather_stone"], Is.GreaterThanOrEqualTo(3));
            Assert.That(region.ResourceAnchors.Select(x => x.StableIndex).Distinct().Count(),
                Is.EqualTo(region.ResourceAnchors.Count));
            var primitiveMeshNames = new[] { "Cube", "Sphere", "Capsule", "Cylinder", "Plane", "Quad" };
            foreach (var anchor in region.ResourceAnchors)
            {
                Assert.That(anchor.VisualRoot, Is.Not.Null);
                Assert.That(anchor.VisualRoot.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                Assert.That(anchor.VisualRoot.GetComponentsInChildren<Collider>(true), Is.Not.Empty);
                Assert.That(anchor.VisualRoot.GetComponentsInChildren<MeshFilter>(true)
                    .All(x => x.sharedMesh != null && !primitiveMeshNames.Contains(x.sharedMesh.name)), Is.True,
                    anchor.VisualRoot.name);
            }
        }

        [Test]
        public void LocalTerritoriesCorrespondToCanonicalStarterMap()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            var map = StarterGlobalMap.Create(0, 51005);
            region.BindCanonicalMap(map.Locations.Select(x => x.Id.Value));
            Assert.That(region.HasCanonicalMapCorrespondence, Is.True);
            Assert.That(map.Locations.Single(x => x.Id.Value == "starter_settlement").Type,
                Is.EqualTo(Game.Domain.World.WorldLocationType.Settlement));
            Assert.That(map.Locations.Single(x => x.Id.Value == "enemy_settlement").Type,
                Is.EqualTo(Game.Domain.World.WorldLocationType.Settlement));
            Assert.That(map.Routes.Count, Is.GreaterThanOrEqualTo(region.RouteWaypoints.Count));
        }

        [Test]
        public void P5UsesOnlyApprovedWrappersAndProjectOwnedAssets()
        {
            foreach (var path in new[]
            {
                "Assets/Game/Art/Meshes/P3/P5_RouteSegment.asset",
                "Assets/Game/Art/Meshes/P3/P5_SupplyCache.asset",
                "Assets/Game/Art/Meshes/P3/P5_TerritoryTotem.asset",
                "Assets/Game/Art/Materials/P3/P5_RouteEarth.mat",
                "Assets/Game/Art/Materials/P3/P5_BerryBush.mat",
                "Assets/Game/Art/Materials/P3/P5_FiberPatch.mat",
                "Assets/Game/Art/Materials/P3/P5_RivalTerritory.mat"
            }) Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);

            EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            var authoredWorld = GameObject.Find("P3 Reference World");
            Assert.That(authoredWorld, Is.Not.Null);
            var prefabPaths = authoredWorld.GetComponentsInChildren<Transform>(true)
                .Where(x => PrefabUtility.IsAnyPrefabInstanceRoot(x.gameObject) &&
                    PrefabUtility.GetOutermostPrefabInstanceRoot(x.gameObject) == x.gameObject)
                .Select(x => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x.gameObject))
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToArray();
            Assert.That(prefabPaths, Is.Not.Empty);
            Assert.That(prefabPaths.All(path => path.StartsWith("Assets/Game/Art/Prefabs/")), Is.True);
        }
    }
}
