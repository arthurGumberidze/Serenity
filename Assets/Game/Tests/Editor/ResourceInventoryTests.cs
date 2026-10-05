using System;
using System.Linq;
using Game.Domain;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Presentation.Buildings;
using Game.Presentation.Resources;
using Game.Simulation.Buildings;
using Game.Simulation.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.Editor
{
    [Category("U09")]
    public sealed class ResourceInventoryTests
    {
        private static readonly ResourceId Wood = new ResourceId("wood_log");
        private static readonly ResourceId Fiber = new ResourceId("plant_fiber");

        [Test]
        public void ResourceIdentityQuantityAndCatalogAreValidated()
        {
            Assert.Throws<ArgumentException>(() => new ResourceId("Wood Log"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceQuantity(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceAmount(Wood, new ResourceQuantity(0)));
            Assert.Throws<ArgumentException>(() => new ResourceCatalog(new[]
            {
                new ResourceDefinition(Wood, "Wood", ResourceCategory.Construction),
                new ResourceDefinition(Wood, "Duplicate", ResourceCategory.Construction)
            }));
            Assert.That(NewCatalog().Get(Wood).DisplayName, Is.EqualTo("Wood Logs"));
        }

        [Test]
        public void InventoryAddRemoveCapacityUnknownAndZeroRepresentationAreSafe()
        {
            var inventory = NewInventory(StableEntityId.NewId(), InventoryOwnerKind.Character, 10);
            Assert.That(inventory.TotalUnits, Is.Zero);
            Assert.That(inventory.TryAdd(Wood, new ResourceQuantity(7)), Is.True);
            Assert.That(inventory.TryAdd(Fiber, new ResourceQuantity(4)), Is.False);
            Assert.That(inventory.GetAmount(Wood).Units, Is.EqualTo(7));
            Assert.That(inventory.TryRemove(Wood, new ResourceQuantity(8)), Is.False);
            Assert.That(inventory.TryRemove(Wood, new ResourceQuantity(7)), Is.True);
            Assert.That(inventory.GetAmount(Wood).Units, Is.Zero);
            Assert.That(inventory.CaptureState().Amounts, Is.Empty);
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                inventory.TryAdd(new ResourceId("unknown"), new ResourceQuantity(1)));
        }

        [Test]
        public void InventorySnapshotRestoresOwnerCapacityFilterAndExactAmounts()
        {
            var catalog = NewCatalog();
            var owner = new InventoryOwner(InventoryOwnerKind.BuildingStorage, StableEntityId.NewId());
            var inventory = new ResourceInventory(owner, catalog, 12, new[] { ResourceCategory.Construction });
            inventory.TryAdd(Wood, new ResourceQuantity(9));
            var restored = ResourceInventory.Restore(inventory.CaptureState(), catalog);
            Assert.That(restored.Owner, Is.EqualTo(owner));
            Assert.That(restored.Capacity, Is.EqualTo(12));
            Assert.That(restored.GetAmount(Wood).Units, Is.EqualTo(9));
            Assert.That(restored.TryAdd(new ResourceId("hide"), new ResourceQuantity(1)), Is.False);

            var unrestricted = NewInventory(StableEntityId.NewId(), InventoryOwnerKind.Character, 4, catalog);
            var unrestrictedState = unrestricted.CaptureState();
            Assert.That(unrestrictedState.AcceptsAllCategories, Is.True);
            Assert.That(ResourceInventory.Restore(unrestrictedState, catalog)
                .TryAdd(new ResourceId("hide"), new ResourceQuantity(1)), Is.True);

            var duplicateState = new ResourceInventoryState(owner, 12, null, new[]
            {
                new ResourceAmount(Wood, new ResourceQuantity(1)),
                new ResourceAmount(Wood, new ResourceQuantity(1))
            });
            Assert.Throws<ArgumentException>(() => ResourceInventory.Restore(duplicateState, catalog));
        }

        [Test]
        public void ExactTransferConservesTotalAndFailuresDoNotMutateEitherOwner()
        {
            var catalog = NewCatalog();
            var registry = new ResourceInventoryRegistry();
            var source = NewInventory(StableEntityId.NewId(), InventoryOwnerKind.WorldPile, 100, catalog);
            var destination = NewInventory(StableEntityId.NewId(), InventoryOwnerKind.Character, 5, catalog);
            registry.Add(source);
            registry.Add(destination);
            source.TryAdd(Wood, new ResourceQuantity(20));
            var transfers = new ResourceTransferService(registry);
            Assert.That(transfers.TransferExact(source.Owner, destination.Owner, Wood, new ResourceQuantity(5)),
                Is.EqualTo(TransferFailureReason.None));
            Assert.That(source.GetAmount(Wood).Units + destination.GetAmount(Wood).Units, Is.EqualTo(20));
            Assert.That(transfers.TransferExact(source.Owner, destination.Owner, Wood, new ResourceQuantity(1)),
                Is.EqualTo(TransferFailureReason.DestinationRejected));
            Assert.That(transfers.TransferExact(source.Owner, source.Owner, Wood, new ResourceQuantity(1)),
                Is.EqualTo(TransferFailureReason.SelfTransfer));
            Assert.That(source.GetAmount(Wood).Units, Is.EqualTo(15));
            Assert.That(destination.GetAmount(Wood).Units, Is.EqualTo(5));
        }

        [Test]
        public void WorldCharacterStorageTransferSequenceConservesCanonicalQuantity()
        {
            var catalog = NewCatalog();
            var registry = new ResourceInventoryRegistry();
            var piles = new WorldPileService(catalog, registry);
            var pile = piles.Create(Wood, new ResourceQuantity(20), new GridCoordinate(0, 0));
            var character = NewInventory(StableEntityId.NewId(), InventoryOwnerKind.Character, 10, catalog);
            var storage = NewInventory(StableEntityId.NewId(), InventoryOwnerKind.BuildingStorage, 10, catalog);
            registry.Add(character);
            registry.Add(storage);
            var transfers = new ResourceTransferService(registry);
            Assert.That(transfers.TransferExact(pile.InventoryOwner, character.Owner, Wood, new ResourceQuantity(7)),
                Is.EqualTo(TransferFailureReason.None));
            Assert.That(transfers.TransferExact(character.Owner, storage.Owner, Wood, new ResourceQuantity(5)),
                Is.EqualTo(TransferFailureReason.None));
            Assert.That(registry.Get(pile.InventoryOwner).GetAmount(Wood).Units, Is.EqualTo(13));
            Assert.That(character.GetAmount(Wood).Units, Is.EqualTo(2));
            Assert.That(storage.GetAmount(Wood).Units, Is.EqualTo(5));
            Assert.That(registry.All.Sum(x => x.GetAmount(Wood).Units), Is.EqualTo(20));
        }

        [Test]
        public void ConstructionRequiresAllCostsAndCommitsAtomically()
        {
            var setup = NewConstruction();
            setup.Source.TryAdd(Wood, new ResourceQuantity(10));
            setup.Source.TryAdd(Fiber, new ResourceQuantity(5));
            var result = setup.Funding.TryConfirm(setup.Shelter, new GridCoordinate(0, 0), BuildingOrientation.North,
                new[] { setup.Source.Owner }, out var building);
            Assert.That(result.IsValid, Is.True);
            Assert.That(building, Is.Not.Null);
            Assert.That(building.ConstructionState, Is.EqualTo(ConstructionState.Completed));
            Assert.That(setup.Source.GetAmount(Wood).Units, Is.EqualTo(2));
            Assert.That(setup.Source.GetAmount(Fiber).Units, Is.EqualTo(2));
            Assert.That(setup.Buildings.Count, Is.EqualTo(1));
            Assert.That(setup.Occupancy.OccupiedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void InsufficientOneResourceCreatesNoIdentityOccupancyOrPartialDeduction()
        {
            var setup = NewConstruction();
            setup.Source.TryAdd(Wood, new ResourceQuantity(7));
            setup.Source.TryAdd(Fiber, new ResourceQuantity(5));
            var result = setup.Funding.TryConfirm(setup.Shelter, new GridCoordinate(0, 0), BuildingOrientation.North,
                new[] { setup.Source.Owner }, out var building);
            Assert.That(result.Failure, Is.EqualTo(ConstructionFailureReason.InsufficientResources));
            Assert.That(result.MissingResource, Is.EqualTo(Wood));
            Assert.That(building, Is.Null);
            Assert.That(setup.Source.GetAmount(Wood).Units, Is.EqualTo(7));
            Assert.That(setup.Source.GetAmount(Fiber).Units, Is.EqualTo(5));
            Assert.That(setup.Buildings.Count, Is.Zero);
            Assert.That(setup.Occupancy.OccupiedCellCount, Is.Zero);
        }

        [Test]
        public void FailureAfterChargeRollsBackResourcesStorageBuildingAndOccupancy()
        {
            var setup = NewConstruction(storageCapacity: 40);
            setup.Source.TryAdd(Wood, new ResourceQuantity(10));
            setup.Source.TryAdd(Fiber, new ResourceQuantity(5));
            var result = setup.Funding.TryConfirm(setup.Shelter, new GridCoordinate(0, 0), BuildingOrientation.North,
                new[] { setup.Source.Owner }, out var building, _ => throw new InvalidOperationException("forced"));
            Assert.That(result.Failure, Is.EqualTo(ConstructionFailureReason.CommitFailed));
            Assert.That(building, Is.Null);
            Assert.That(setup.Source.GetAmount(Wood).Units, Is.EqualTo(10));
            Assert.That(setup.Source.GetAmount(Fiber).Units, Is.EqualTo(5));
            Assert.That(setup.Buildings.Count, Is.Zero);
            Assert.That(setup.Occupancy.OccupiedCellCount, Is.Zero);
            Assert.That(setup.Inventories.Count, Is.EqualTo(1));
        }

        [Test]
        public void StorageCapabilityAndPresenterRespawnUseBuildingIdentity()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var resources = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(
                "Assets/Game/Art/Config/ResourceCatalog.asset").CreateCatalog();
            var definition = catalog.ResolveDefinition(new BuildingDefinitionId("storage_basket"));
            var building = Building.CreateNew(definition.Id, new GridCoordinate(8, 9), BuildingOrientation.North);
            var inventories = new ResourceInventoryRegistry();
            var storage = new StorageService(inventories, resources).Attach(building, definition);
            storage.TryAdd(Wood, new ResourceQuantity(9));
            var views = new BuildingPresentationRegistry();
            var spawner = new BuildingPresentationSpawner(catalog, views, 1f, Vector3.zero, null, inventories);
            var first = spawner.Spawn(building);
            Assert.That(first.StorageInventory, Is.SameAs(storage));
            first.Unbind();
            UnityEngine.Object.DestroyImmediate(first.gameObject);
            var second = spawner.Spawn(building);
            try
            {
                Assert.That(second.BuildingId, Is.EqualTo(building.Id));
                Assert.That(second.StorageInventory, Is.SameAs(storage));
                Assert.That(second.StorageInventory.GetAmount(Wood).Units, Is.EqualTo(9));
            }
            finally { second.Unbind(); UnityEngine.Object.DestroyImmediate(second.gameObject); }
        }

        [Test]
        public void AuthoredStoneAgeDefinitionsHaveCostsAndOnlyBasketHasStorage()
        {
            var resourceAsset = AssetDatabase.LoadAssetAtPath<ResourceCatalogAsset>(
                "Assets/Game/Art/Config/ResourceCatalog.asset");
            var catalog = resourceAsset.CreateCatalog();
            foreach (var id in new[] { "wood_log", "stone", "plant_fiber", "hide" })
                Assert.That(catalog.Contains(new ResourceId(id)), Is.True);
            var buildings = AssetDatabase.LoadAssetAtPath<BuildingPresentationCatalog>(
                "Assets/Game/Art/Config/BuildingPresentationCatalog.asset");
            var shelter = buildings.ResolveDefinition(new BuildingDefinitionId("primitive_shelter"));
            var basket = buildings.ResolveDefinition(new BuildingDefinitionId("storage_basket"));
            Assert.That(shelter.ConstructionCost.Select(x => x.Quantity.Units), Is.EquivalentTo(new long[] { 8, 3 }));
            Assert.That(basket.ConstructionCost.Select(x => x.Quantity.Units), Is.EquivalentTo(new long[] { 3, 5 }));
            Assert.That(shelter.StorageCapacity, Is.Zero);
            Assert.That(basket.StorageCapacity, Is.EqualTo(40));
        }

        private static ResourceCatalog NewCatalog() => new ResourceCatalog(new[]
        {
            new ResourceDefinition(Wood, "Wood Logs", ResourceCategory.Construction),
            new ResourceDefinition(Fiber, "Plant Fiber", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("stone"), "Stone", ResourceCategory.Construction),
            new ResourceDefinition(new ResourceId("hide"), "Hide", ResourceCategory.Other)
        });

        private static ResourceInventory NewInventory(StableEntityId id, InventoryOwnerKind kind, long capacity,
            ResourceCatalog catalog = null) => new ResourceInventory(new InventoryOwner(kind, id), catalog ?? NewCatalog(), capacity);

        private static ConstructionSetup NewConstruction(long storageCapacity = 0)
        {
            var resources = NewCatalog();
            var inventories = new ResourceInventoryRegistry();
            var source = NewInventory(StableEntityId.NewId(), InventoryOwnerKind.WorldPile, 100, resources);
            inventories.Add(source);
            var buildings = new BuildingRegistry();
            var occupancy = new BuildingOccupancyGrid();
            var placement = new BuildingPlacementService(buildings, occupancy, new BuildingGridBounds(-10, -10, 10, 10));
            var storage = new StorageService(inventories, resources);
            var funding = new ConstructionFundingService(placement, inventories, resources, storage);
            var definition = new BuildingDefinition(new BuildingDefinitionId("primitive_shelter"), "Shelter",
                BuildingCategory.Shelter, new BuildingFootprint(2, 1), constructionCost: new[]
                {
                    new ResourceAmount(Wood, new ResourceQuantity(8)),
                    new ResourceAmount(Fiber, new ResourceQuantity(3))
                }, storageCapacity: storageCapacity);
            return new ConstructionSetup(resources, inventories, source, buildings, occupancy, funding, definition);
        }

        private sealed class ConstructionSetup
        {
            public ConstructionSetup(ResourceCatalog resources, ResourceInventoryRegistry inventories,
                ResourceInventory source, BuildingRegistry buildings, BuildingOccupancyGrid occupancy,
                ConstructionFundingService funding, BuildingDefinition shelter)
            { Resources = resources; Inventories = inventories; Source = source; Buildings = buildings;
                Occupancy = occupancy; Funding = funding; Shelter = shelter; }
            public ResourceCatalog Resources { get; }
            public ResourceInventoryRegistry Inventories { get; }
            public ResourceInventory Source { get; }
            public BuildingRegistry Buildings { get; }
            public BuildingOccupancyGrid Occupancy { get; }
            public ConstructionFundingService Funding { get; }
            public BuildingDefinition Shelter { get; }
        }
    }
}
