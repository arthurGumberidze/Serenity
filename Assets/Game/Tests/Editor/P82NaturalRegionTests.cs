using System.Linq;
using Game.Domain.Production;
using Game.Presentation.Environment;
using Game.Presentation.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("P82")]
    public sealed class P82NaturalRegionTests
    {
        [SetUp]
        public void OpenScene() => EditorSceneManager.OpenScene("Assets/Scenes/LocalGameplay.unity", OpenSceneMode.Single);

        [Test]
        public void NaturalMapKeepsFiniteBoundaryOnTheSupersedingTerrain()
        {
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            var realistic = Object.FindAnyObjectByType<P92RealisticGameplayRegion>();
            Assert.That(region, Is.Not.Null);
            Assert.That(realistic, Is.Not.Null);
            Assert.DoesNotThrow(region.ValidateGeography);
            Assert.That(region.LandBoundary.Count, Is.GreaterThanOrEqualTo(12));
            Assert.That(region.AuthoredPlacementAnchors.All(x => region.ContainsLandPoint(x.position)), Is.True);
            Assert.That(realistic.Terrain.GetComponent<Collider>(), Is.Not.Null);
            Assert.That(realistic.Terrain.GetComponent<Game.Presentation.Interaction.BuildableGround>(), Is.Not.Null);
            Assert.That(GameObject.Find("P3 Buildable Forest Clearing"), Is.Null);
            Assert.That(GameObject.Find("Ground Placeholder"), Is.Null);
        }

        [Test]
        public void SixResourceFamiliesHaveReadablePbrViewsCollidersAndStableIndices()
        {
            var region = Object.FindAnyObjectByType<P5InhabitableRegionPresentation>();
            var required = new[] { "gather_berries", "gather_fiber", "chop_wood", "gather_stone", "gather_iron", "gather_gold" };
            Assert.That(region.ResourceAnchors.Count, Is.EqualTo(24));
            Assert.That(required.All(recipe => region.ResourceAnchors.Count(x => x.RecipeId == recipe) >= 4), Is.True);
            Assert.That(region.ResourceAnchors.Select(x => x.StableIndex).OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 24)));
            foreach (var anchor in region.ResourceAnchors)
            {
                Assert.That(anchor.VisualRoot.GetComponentInChildren<Collider>(), Is.Not.Null, anchor.VisualRoot.name);
                Assert.That(anchor.VisualRoot.GetComponentsInChildren<Renderer>(true).All(x =>
                    x.sharedMaterial != null && x.sharedMaterial.shader != null &&
                    x.sharedMaterial.shader.name != "Hidden/InternalErrorShader"), Is.True, anchor.VisualRoot.name);
            }
            Assert.That(region.TrunkRenderers.Count, Is.GreaterThanOrEqualTo(32));
            Assert.That(region.TrunkRenderers.All(x => x.sharedMaterial != null), Is.True);
        }

        [Test]
        public void CanonicalCatalogAndRecipesIncludeIronAndGold()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>("Assets/Game/Art/Config/ResourceCatalog.asset");
            var catalog = asset.CreateCatalog();
            Assert.That(catalog.Get(new Game.Domain.Resources.ResourceId("iron_ore")).DisplayName, Is.EqualTo("Iron Ore"));
            Assert.That(catalog.Get(new Game.Domain.Resources.ResourceId("gold_ore")).DisplayName, Is.EqualTo("Gold Ore"));
            var recipes = ProductionCatalog.CreateStoneAge(catalog);
            Assert.That(recipes.Get(new ProductionRecipeId("gather_iron")).Outputs[0].ResourceId.ToString(), Is.EqualTo("iron_ore"));
            Assert.That(recipes.Get(new ProductionRecipeId("gather_gold")).Outputs[0].ResourceId.ToString(), Is.EqualTo("gold_ore"));
        }
    }
}
