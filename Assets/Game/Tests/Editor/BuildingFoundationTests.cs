using System;
using System.Linq;
using Game.Domain.Buildings;
using Game.Presentation.Buildings;
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
