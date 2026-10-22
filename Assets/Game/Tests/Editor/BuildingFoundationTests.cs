using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Combat;
using Game.Domain.Resources;
using Game.Domain.World;
using Game.Presentation.Buildings;
using Game.Presentation.Resources;
using Game.Simulation.Buildings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("U08")]
    public sealed class BuildingFoundationTests
    {
        private static readonly BuildingDefinition Shelter = new BuildingDefinition(
            new BuildingDefinitionId("primitive_shelter"), "Primitive Shelter", BuildingCategory.Shelter,
            new BuildingFootprint(2, 3));

        [Test]
        public void DefinitionIdIsSeparateValidatedContentIdentity()
        {
            var definitionId = new BuildingDefinitionId("primitive_shelter");
            Assert.That(definitionId.ToString(), Is.EqualTo("primitive_shelter"));
            Assert.Throws<ArgumentException>(() => new BuildingDefinitionId("Primitive Shelter"));
            Assert.Throws<ArgumentException>(() => new BuildingDefinitionId(string.Empty));
            Assert.That(StableIdText(Building.CreateNew(definitionId, new GridCoordinate(0, 0), BuildingOrientation.North)),
                Is.Not.EqualTo(definitionId.ToString()));
        }

        [Test]
        public void RectangularFootprintRotatesAndEnumeratesCells()
        {
            Assert.That(Shelter.Footprint.Rotate(BuildingOrientation.North), Is.EqualTo(new BuildingFootprint(2, 3)));
            Assert.That(Shelter.Footprint.Rotate(BuildingOrientation.East), Is.EqualTo(new BuildingFootprint(3, 2)));
            var cells = Shelter.Footprint.EnumerateCells(new GridCoordinate(4, 7), BuildingOrientation.East).ToArray();
            Assert.That(cells.Length, Is.EqualTo(6));
            Assert.That(cells, Does.Contain(new GridCoordinate(6, 8)));
        }

        [Test]
        public void GridBoundsRejectsAnyFootprintCellOutsideMap()
        {
            var service = NewService(out _, out _, new BuildingGridBounds(0, 0, 4, 4));
            Assert.That(service.Evaluate(Shelter, new GridCoordinate(2, 2), BuildingOrientation.North).IsValid, Is.True);
            var result = service.Evaluate(Shelter, new GridCoordinate(3, 3), BuildingOrientation.North);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.OutsideBuildableBounds));
        }

        [Test]
        public void ConfirmReservesLogicalOccupancyAndBlocksOverlap()
        {
            var service = NewService(out var registry, out var occupancy, new BuildingGridBounds(-10, -10, 10, 10));
            var building = service.Confirm(Shelter, new GridCoordinate(0, 0), BuildingOrientation.North);
            Assert.That(registry.Get(building.Id), Is.SameAs(building));
            Assert.That(occupancy.OccupiedCellCount, Is.EqualTo(6));
            var overlap = service.Evaluate(Shelter, new GridCoordinate(1, 2), BuildingOrientation.North);
            Assert.That(overlap.IsValid, Is.False);
            Assert.That(overlap.FailureReason, Is.EqualTo(PlacementFailureReason.Occupied));
            Assert.Throws<InvalidOperationException>(() => service.Confirm(Shelter, new GridCoordinate(1, 2), BuildingOrientation.North));
        }

        [Test]
        public void EnvironmentalValidationReceivesWholeRotatedFootprintAndIsRecheckedOnCommit()
        {
            var service = NewService(out var registry, out var occupancy, new BuildingGridBounds(-10, -10, 10, 10));
            var blocked = false;
            service.ConfigureEnvironment(cells =>
            {
                Assert.That(cells.Count, Is.EqualTo(6));
                Assert.That(cells, Does.Contain(new GridCoordinate(2, 1)));
                return !blocked;
            });
            Assert.That(service.Evaluate(Shelter, new GridCoordinate(0, 0), BuildingOrientation.East).IsValid, Is.True);
            blocked = true;
            Assert.That(service.Evaluate(Shelter, new GridCoordinate(0, 0), BuildingOrientation.East).FailureReason,
                Is.EqualTo(PlacementFailureReason.TerrainBlocked));
            Assert.Throws<InvalidOperationException>(() => service.Confirm(Shelter, new GridCoordinate(0, 0), BuildingOrientation.East));
            Assert.That(registry.Count, Is.Zero);
            Assert.That(occupancy.OccupiedCellCount, Is.Zero);
        }

        [Test]
        public void PlannedBuildingCompletesAndRestoresWithSameIdentity()
        {
            var original = Building.CreateNew(Shelter.Id, new GridCoordinate(3, -2, 1), BuildingOrientation.West);
            var id = original.Id;
            Assert.That(original.ConstructionState, Is.EqualTo(ConstructionState.Planned));
            original.MarkCompleted();
            var restored = Building.Restore(original.CaptureState());
            Assert.That(restored.Id, Is.EqualTo(id));
            Assert.That(restored.DefinitionId, Is.EqualTo(Shelter.Id));
            Assert.That(restored.Coordinate, Is.EqualTo(new GridCoordinate(3, -2, 1)));
            Assert.That(restored.Orientation, Is.EqualTo(BuildingOrientation.West));
            Assert.That(restored.ConstructionState, Is.EqualTo(ConstructionState.Completed));
        }

        [Test]
        public void RegistryRejectsDuplicateStableIdentity()
        {
            var building = Building.CreateNew(Shelter.Id, new GridCoordinate(0, 0), BuildingOrientation.North);
            var duplicate = Building.Restore(building.CaptureState());
            var registry = new BuildingRegistry();
            registry.Add(building);
            Assert.Throws<InvalidOperationException>(() => registry.Add(duplicate));
        }

        [Test]
        public void CatalogIsDataDrivenAndContainsStoneAgePlaceholders()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            catalog.Validate();
            var shelter = catalog.ResolveDefinition(new BuildingDefinitionId("primitive_shelter"));
            var storage = catalog.ResolveDefinition(new BuildingDefinitionId("storage_basket"));
            Assert.That(shelter.Footprint, Is.EqualTo(new BuildingFootprint(4, 3)));
            Assert.That(storage.Footprint, Is.EqualTo(new BuildingFootprint(2, 1)));
            Assert.That(catalog.ResolvePrefab(shelter.Id).GetComponent<BuildingPresenter>(), Is.Not.Null);
            Assert.That(catalog.ResolvePrefab(storage.Id).GetComponent<BuildingPresenter>(), Is.Not.Null);
        }

        [Test]
        public void CatalogContainsFivePlayableBaseBuildingsWithAuthoredStages()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var baseIds = new[] { "primitive_shelter", "stone_age_hut", "campfire", "storage_basket",
                "stone_tool_workshop" }.Select(x => new BuildingDefinitionId(x)).ToArray();

            foreach (var id in baseIds)
            {
                var definition = catalog.ResolveDefinition(id);
                var prefab = catalog.ResolvePrefab(id);
                Assert.That(definition.Construction.Cost, Is.Not.Empty, id.ToString());
                Assert.That(definition.Construction.StageCount, Is.GreaterThanOrEqualTo(2), id.ToString());
                Assert.That(prefab.GetComponent<BuildingPresenter>(), Is.Not.Null, id.ToString());
                Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null, id.ToString());
            }
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("primitive_shelter")).Housing.ResidentCapacity,
                Is.EqualTo(2));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("stone_age_hut")).Housing.ResidentCapacity,
                Is.EqualTo(4));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("storage_basket")).Storage.Capacity,
                Is.EqualTo(40));
            var campfire = catalog.ResolveDefinition(new BuildingDefinitionId("campfire"));
            Assert.That(campfire.Workplace.MaximumWorkers, Is.EqualTo(1));
            Assert.That(campfire.Production.RecipeIds.Select(x => x.ToString()),
                Is.EquivalentTo(new[] { "cook_meat", "cook_fish", "cook_berries" }));
            Assert.That(campfire.Production.InputCapacity, Is.EqualTo(24));
            Assert.That(campfire.Production.OutputCapacity, Is.EqualTo(24));
            foreach (var id in new[] { "stone_age_hut", "campfire", "stone_tool_workshop" })
                Assert.That(catalog.ResolvePrefab(new BuildingDefinitionId(id))
                    .GetComponentsInChildren<BuildingConstructionStageVisual>(true).Length, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void CatalogContainsCompleteStageDHuntingAndPreservationChain()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var ids = new[] { "butcher_station", "hunting_camp", "food_storage", "drying_rack", "hide_workshop" };
            foreach (var text in ids)
            {
                var id = new BuildingDefinitionId(text);
                var definition = catalog.ResolveDefinition(id);
                var prefab = catalog.ResolvePrefab(id);
                Assert.That(definition.RequiredTechnologies.Select(x => x.ToString()),
                    Does.Contain(text == "butcher_station" || text == "hunting_camp" ? "hunting" : "food_preservation"));
                Assert.That(prefab.GetComponent<BuildingPresenter>(), Is.Not.Null, text);
                Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null, text);
                Assert.That(prefab.GetComponentsInChildren<BuildingConstructionStageVisual>(true).Length,
                    Is.GreaterThanOrEqualTo(2), text);
            }
            var foodStorage = catalog.ResolveDefinition(new BuildingDefinitionId("food_storage"));
            Assert.That(foodStorage.Storage.Capacity, Is.EqualTo(120));
            Assert.That(foodStorage.Storage.AcceptedCategories, Is.EquivalentTo(new[] { ResourceCategory.Food }));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("drying_rack")).Production.RecipeIds
                .Select(x => x.ToString()), Is.EqualTo(new[] { "dry_meat" }));
            Assert.That(catalog.ResolveDefinition(new BuildingDefinitionId("hide_workshop")).Production.RecipeIds
                .Select(x => x.ToString()), Is.EqualTo(new[] { "cure_hide" }));
        }

        [Test]
        public void AuthoredConstructionStageVisualsFollowInstalledMaterialStages()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var resourcesAsset = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(
                "Assets/Game/Art/Config/ResourceCatalog.asset");
            var resources = resourcesAsset.CreateCatalog();
            var definition = catalog.ResolveDefinition(new BuildingDefinitionId("stone_age_hut"));
            var inventories = new ResourceInventoryRegistry();
            var source = new ResourceInventory(new InventoryOwner(InventoryOwnerKind.BuildingStorage,
                StableEntityId.NewId()), resources, 100);
            foreach (var amount in definition.Construction.Cost) source.TryAdd(amount.ResourceId, amount.Quantity);
            inventories.Add(source);
            var building = Building.CreateNew(definition, default, BuildingOrientation.North,
                new CombatSideId("player"));
            var sites = new ConstructionSiteService(resources, inventories);
            sites.Initialize(building, definition, new SettlementId(StableEntityId.NewId()));
            var presenter = new BuildingPresentationSpawner(catalog, new BuildingPresentationRegistry(), 1f,
                Vector3.zero, inventoryRegistry: inventories).Spawn(building);
            try
            {
                var stages = presenter.GetComponentsInChildren<BuildingConstructionStageVisual>(true);
                Assert.That(stages.All(x => !x.gameObject.activeSelf), Is.True);
                foreach (var amount in building.NextStageMaterialCost())
                    Assert.That(sites.Deliver(building, source.Owner, amount.ResourceId, amount.Quantity),
                        Is.EqualTo(Game.Simulation.Resources.TransferFailureReason.None));
                Assert.That(sites.CommitReadyStage(building), Is.True);
                presenter.RefreshStructuralPresentation();
                Assert.That(stages.Where(x => x.RequiredStage == 1).All(x => x.gameObject.activeSelf), Is.True);
                Assert.That(stages.Where(x => x.RequiredStage > 1).All(x => !x.gameObject.activeSelf), Is.True);
            }
            finally
            {
                presenter.Unbind();
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [Test]
        public void PresentationRespawnBindsSameDomainBuildingId()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var building = Building.CreateNew(Shelter.Id, new GridCoordinate(8, 9), BuildingOrientation.South);
            var id = building.Id;
            var registry = new BuildingPresentationRegistry();
            var spawner = new BuildingPresentationSpawner(catalog, registry, 1f, Vector3.zero);
            var first = spawner.Spawn(building);
            first.Unbind();
            UnityEngine.Object.DestroyImmediate(first.gameObject);
            var second = spawner.Spawn(building);
            try
            {
                Assert.That(second.BuildingId, Is.EqualTo(id));
                Assert.That(second.Building, Is.SameAs(building));
            }
            finally
            {
                second.Unbind();
                UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }

        private static BuildingPlacementService NewService(out BuildingRegistry registry,
            out BuildingOccupancyGrid occupancy, BuildingGridBounds bounds)
        {
            registry = new BuildingRegistry();
            occupancy = new BuildingOccupancyGrid();
            return new BuildingPlacementService(registry, occupancy, bounds);
        }

        private static string StableIdText(Building building) => building.Id.ToString();
    }
}
