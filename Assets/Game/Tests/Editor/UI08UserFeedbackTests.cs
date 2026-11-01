using System.IO;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Production;
using Game.Presentation.Buildings;
using Game.Presentation.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [TestFixture, Category("UI08")]
    public sealed class UI08UserFeedbackTests
    {
        private static string Source => File.ReadAllText(Path.Combine(Application.dataPath,
            "Game/Presentation/UI/GameplayUiController.cs"));

        [Test]
        public void ResearchAndDynastyAreRenderedAsDependencyAndGenerationTrees()
        {
            StringAssert.Contains("research-dependency-tree", Source);
            StringAssert.Contains("GetResearchDepth", Source);
            StringAssert.Contains("research-prerequisite-connectors", Source);
            StringAssert.Contains("BezierCurveTo", Source);
            StringAssert.Contains("research-state-", Source);
            StringAssert.Contains("ShouldDrawResearchConnector", Source);
            StringAssert.Contains("tree.Insert(0, connectors)", Source);
            StringAssert.Contains("research-branch-columns", Source);
            StringAssert.Contains("research-selected-detail", Source);
            StringAssert.Contains("dynasty-generation-connector-", Source);
            StringAssert.Contains("dynasty-spouse-connector-", Source);
            StringAssert.Contains("GetDynastyGeneration", Source);
        }

        [Test]
        public void MilitaryRecruitmentIsAvailableInsideTheMilitaryWorkspace()
        {
            StringAssert.Contains("military-recruit-roster", Source);
            StringAssert.Contains("ToggleMilitaryRecruit", Source);
            StringAssert.Contains("militaryDraftSelection", Source);
            StringAssert.DoesNotContain("var roster = context.Selection.SelectedCharacterIds.ToArray()", Source);
            StringAssert.Contains("GetActiveUnitLimit", Source);
            StringAssert.Contains("IsEligibleMilitaryMember", Source);
            StringAssert.DoesNotContain("ещё два открывает исследование", Source);
            StringAssert.Contains("recruitList.style.display = DisplayStyle.None", Source);
            StringAssert.Contains("unitList.style.display = DisplayStyle.None", Source);
        }

        [Test]
        public void CharacterInspectorIsCompactStructuredAndDoesNotCompeteWithTheMiniMap()
        {
            StringAssert.Contains("character-identity-metrics", Source);
            StringAssert.Contains("character-wellbeing-card", Source);
            StringAssert.Contains("character-skills-foldout", Source);
            StringAssert.Contains("character-family-foldout", Source);
            StringAssert.Contains("!peopleRisk && !hasSelection", Source);
            StringAssert.Contains("serenity-inspector-progress", Source);
            StringAssert.Contains("unity-progress-bar__background", Source);
            StringAssert.Contains("unity-progress-bar__progress", Source);
        }

        [Test]
        public void SettlementAvoidsWorldSourceEnumerationAndDeprecatedWaterPresentation()
        {
            StringAssert.DoesNotContain("AddSection(mainPanel, \"Производство\")", Source);
            StringAssert.DoesNotContain("AddPrimaryResourceChip(\"water\"", Source);
            StringAssert.DoesNotContain("TotalResource(new ResourceId(\"water\"))", Source);
            StringAssert.Contains("IsPlayerManagedResource", Source);
            StringAssert.Contains("resident-roster-open", Source);
        }

        [Test]
        public void UiToolkitPickingBlocksWorldZoomOverNestedMenuContent()
        {
            StringAssert.Contains("root.panel?.Pick(panelPoint)", Source);
            StringAssert.Contains("picked != null && picked != root", Source);
        }

        [Test]
        public void CookedFishExistsAndIsAvailableAtCampfireAndKitchen()
        {
            var resourceAsset = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(
                "Assets/Game/Art/Config/ResourceCatalog.asset");
            var buildings = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            Assert.That(resourceAsset, Is.Not.Null);
            Assert.That(buildings, Is.Not.Null);
            var recipe = ProductionCatalog.CreateStoneAge(resourceAsset.CreateCatalog())
                .Get(new ProductionRecipeId("cook_fish"));
            Assert.That(recipe.Outputs.Single().ResourceId.ToString(), Is.EqualTo("cooked_fish"));
            foreach (var buildingId in new[] { "campfire", "stone_age_kitchen" })
                Assert.That(buildings.ResolveDefinition(new BuildingDefinitionId(buildingId)).Production.RecipeIds
                    .Select(id => id.ToString()), Does.Contain("cook_fish"));
        }
    }
}
