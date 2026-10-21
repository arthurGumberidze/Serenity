using System;
using System.Linq;
using Game.Domain.Buildings;
using Game.Domain.Characters;
using Game.Domain.Production;
using Game.Domain.Research;
using Game.Domain.Resources;
using NUnit.Framework;

namespace Game.Tests.Editor
{
    [Category("P10.9")]
    public sealed class P109BuildingDefinitionTests
    {
        private static readonly ResourceId Wood = new ResourceId("wood_log");
        private static readonly ResourceId Stone = new ResourceId("stone");

        [Test]
        public void ExtensibleDefinitionKeepsImmutableCapabilitySpecifications()
        {
            var construction = new ConstructionSpec(new[] { Amount(Wood, 12), Amount(Stone, 4) },
                TimeSpan.FromHours(6), 3);
            var placement = new PlacementSpec(new BuildingFootprint(4, 3), BuildingPlacementMode.Grid,
                spatialForm: BuildingSpatialForm.Structure,
                constraints: BuildingPlacementConstraint.OwnedTerritory |
                    BuildingPlacementConstraint.ReachableApproach,
                maximumHeightDeltaMillimetres: 500, approachClearanceMillimetres: 900);
            var definition = new BuildingDefinition(new BuildingDefinitionId("stone_tool_workshop"),
                "Stone Tool Workshop", BuildingCategory.Production, BuildingEra.StoneAge,
                "Produces early stone tools.", "building_stone_tool_workshop",
                new[] { new TechnologyId("stone_tools") }, construction, placement,
                housing: null, storage: new StorageSpec(40, new[] { ResourceCategory.Construction }),
                workplace: new WorkplaceSpec(2, new ProfessionId("craftsperson"),
                    new SkillId("crafting"), 10),
                production: new ProductionSpec(new[] { new ProductionRecipeId("craft_stone_tool") }, 20, 20),
                maintenance: new MaintenanceSpec(TimeSpan.FromDays(1).Ticks, 2,
                    new[] { Amount(Wood, 1) }, TimeSpan.FromHours(1)),
                upgrades: new UpgradeSpec(new[] { new BuildingUpgradeOption(
                    new BuildingDefinitionId("advanced_tool_workshop"), new[] { Amount(Stone, 8) },
                    TimeSpan.FromHours(4)) }));

            Assert.That(definition.Era, Is.EqualTo(BuildingEra.StoneAge));
            Assert.That(definition.RequiredTechnologies.Single().ToString(), Is.EqualTo("stone_tools"));
            Assert.That(definition.Construction.WorkRequiredTicks, Is.EqualTo(TimeSpan.FromHours(6).Ticks));
            Assert.That(definition.Construction.StageCount, Is.EqualTo(3));
            Assert.That(definition.Footprint, Is.EqualTo(new BuildingFootprint(4, 3)));
            Assert.That((definition.Placement.Constraints & BuildingPlacementConstraint.OwnedTerritory) != 0, Is.True);
            Assert.That(definition.Workplace.MaximumWorkers, Is.EqualTo(2));
            Assert.That(definition.Production.RecipeIds.Single().ToString(), Is.EqualTo("craft_stone_tool"));
            Assert.That(definition.StorageCapacity, Is.EqualTo(40));
            Assert.That(definition.Maintenance.DemolitionReturnPerThousand, Is.EqualTo(500));
        }

        [Test]
        public void SpecificationsRejectInvalidAreasRecipesAndDuplicateReferences()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BuildingFootprint(0, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlacementSpec(new BuildingFootprint(1, 1),
                maximumHeightDeltaMillimetres: -1));
            var recipeId = new ProductionRecipeId("cook_meat");
            Assert.Throws<ArgumentException>(() => new ProductionSpec(new[] { recipeId, recipeId }, 10, 10));
            var technology = new TechnologyId("controlled_fire");
            Assert.Throws<ArgumentException>(() => Definition("campfire", upgrades: null,
                technologies: new[] { technology, technology }));
        }

        [Test]
        public void CatalogRejectsUnknownResourcesRecipesAndUpgradeTargets()
        {
            var resources = Resources();
            var recipes = Recipes(resources);
            var unknownCost = Definition("bad_cost", new ConstructionSpec(
                new[] { Amount(new ResourceId("missing"), 1) }, TimeSpan.FromHours(1)));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                new BuildingDefinitionCatalog(new[] { unknownCost }, resources, recipes));

            var unknownRecipe = Definition("bad_recipe", production: new ProductionSpec(
                new[] { new ProductionRecipeId("missing_recipe") }, 10, 10));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                new BuildingDefinitionCatalog(new[] { unknownRecipe }, resources, recipes));

            var unknownUpgrade = Definition("bad_upgrade", upgrades: new UpgradeSpec(new[]
            {
                new BuildingUpgradeOption(new BuildingDefinitionId("missing_building"), null, TimeSpan.FromHours(1))
            }));
            Assert.Throws<ArgumentException>(() =>
                new BuildingDefinitionCatalog(new[] { unknownUpgrade }, resources, recipes));
        }

        [Test]
        public void CatalogRejectsUpgradeCyclesAndReturnsStableOrderedDefinitions()
        {
            var first = Definition("shelter", upgrades: UpgradeTo("hut"));
            var second = Definition("hut", upgrades: UpgradeTo("shelter"));
            Assert.Throws<ArgumentException>(() => new BuildingDefinitionCatalog(new[] { first, second }, Resources()));

            var completed = Definition("clan_house");
            var standalone = Definition("shelter");
            var catalog = new BuildingDefinitionCatalog(new[] { standalone, completed }, Resources());
            Assert.That(catalog.All.Select(x => x.Id.ToString()), Is.EqualTo(new[] { "clan_house", "shelter" }));
            Assert.That(catalog.Get(standalone.Id), Is.SameAs(standalone));
        }

        [Test]
        public void LegacyConstructorMapsToNewSpecificationsWithoutChangingCallers()
        {
            var definition = new BuildingDefinition(new BuildingDefinitionId("legacy_storage"), "Legacy Storage",
                BuildingCategory.Storage, new BuildingFootprint(2, 1), constructionCost: new[] { Amount(Wood, 3) },
                storageCapacity: 20, storageCategories: new[] { ResourceCategory.Construction });

            Assert.That(definition.Construction.Cost.Single().Quantity.Units, Is.EqualTo(3));
            Assert.That(definition.Placement.Footprint, Is.EqualTo(new BuildingFootprint(2, 1)));
            Assert.That(definition.Storage.Capacity, Is.EqualTo(20));
            Assert.That(definition.Construction.WorkRequiredTicks, Is.EqualTo(1));
        }

        private static BuildingDefinition Definition(string id, ConstructionSpec construction = null,
            ProductionSpec production = null, UpgradeSpec upgrades = null,
            TechnologyId[] technologies = null) => new BuildingDefinition(new BuildingDefinitionId(id), id,
                BuildingCategory.Structure, BuildingEra.StoneAge, string.Empty, string.Empty, technologies,
                construction ?? new ConstructionSpec(new[] { Amount(Wood, 1) }, TimeSpan.FromHours(1)),
                new PlacementSpec(new BuildingFootprint(1, 1)), production: production, upgrades: upgrades);

        private static UpgradeSpec UpgradeTo(string id) => new UpgradeSpec(new[]
        {
            new BuildingUpgradeOption(new BuildingDefinitionId(id), null, TimeSpan.FromHours(1))
        });

        private static ResourceAmount Amount(ResourceId id, long units) =>
            new ResourceAmount(id, new ResourceQuantity(units));

        private static ResourceCatalog Resources() => new ResourceCatalog(new[]
        {
            new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
            new ResourceDefinition(Stone, "Stone", ResourceCategory.Construction)
        });

        private static ProductionCatalog Recipes(ResourceCatalog resources) => new ProductionCatalog(new[]
        {
            new ProductionRecipe(new ProductionRecipeId("craft_stone_tool"), "Craft Stone Tool",
                new[] { Amount(Stone, 1) }, new[] { Amount(Wood, 1) }, TimeSpan.FromHours(1),
                ProductionSiteKind.NaturalSource)
        }, resources);
    }
}
