using System;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    public sealed class BuildingPresentationSpawner
    {
        private readonly BuildingPresentationCatalog catalog;
        private readonly BuildingPresentationRegistry registry;
        private readonly Transform parent;
        private readonly float gridSize;
        private readonly Vector3 origin;
        private readonly ResourceInventoryRegistry inventories;

        public BuildingPresentationSpawner(BuildingPresentationCatalog catalog, BuildingPresentationRegistry registry,
            float gridSize, Vector3 origin, Transform parent = null, ResourceInventoryRegistry inventoryRegistry = null)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (gridSize <= 0f) throw new ArgumentOutOfRangeException(nameof(gridSize));
            this.gridSize = gridSize;
            this.origin = origin;
            this.parent = parent;
            inventories = inventoryRegistry;
        }

        public BuildingPresenter Spawn(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var prefab = catalog.ResolvePrefab(building.DefinitionId);
            var definition = catalog.ResolveDefinition(building.DefinitionId);
            var position = GridToWorld(building.Coordinate, definition, building.Orientation);
            var rotation = Quaternion.Euler(0f, building.Orientation.Degrees(), 0f);
            var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
            var presenter = instance.GetComponent<BuildingPresenter>();
            if (presenter == null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
                else UnityEngine.Object.DestroyImmediate(instance);
                throw new InvalidOperationException("Building prefab requires a BuildingPresenter.");
            }
            try
            {
                ResourceInventory storage = null;
                inventories?.TryGet(new InventoryOwner(InventoryOwnerKind.BuildingStorage, building.Id), out storage);
                presenter.Bind(building, registry, storage);
            }
            catch
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
                else UnityEngine.Object.DestroyImmediate(instance);
                throw;
            }
            return presenter;
        }

        public void Despawn(BuildingPresenter presenter)
        {
            if (presenter == null) return;
            presenter.Unbind();
            UnityEngine.Object.Destroy(presenter.gameObject);
        }

        public Vector3 GridToWorld(GridCoordinate coordinate) => origin + new Vector3(coordinate.X * gridSize,
            coordinate.Level * gridSize, coordinate.Z * gridSize);

        public Vector3 GridToWorld(GridCoordinate coordinate, BuildingDefinition definition, BuildingOrientation orientation)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var footprint = definition.Footprint.Rotate(orientation);
            return origin + new Vector3((coordinate.X + (footprint.Width - 1) * 0.5f) * gridSize,
                coordinate.Level * gridSize, (coordinate.Z + (footprint.Depth - 1) * 0.5f) * gridSize);
        }
    }
}
