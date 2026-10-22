using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Func<Vector3, float> groundHeight;

        public BuildingPresentationSpawner(BuildingPresentationCatalog catalog, BuildingPresentationRegistry registry,
            float gridSize, Vector3 origin, Transform parent = null, ResourceInventoryRegistry inventoryRegistry = null,
            Func<Vector3, float> groundHeightResolver = null)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            if (gridSize <= 0f) throw new ArgumentOutOfRangeException(nameof(gridSize));
            this.gridSize = gridSize;
            this.origin = origin;
            this.parent = parent;
            inventories = inventoryRegistry;
            groundHeight = groundHeightResolver;
        }

        public BuildingPresenter Spawn(Building building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var prefab = catalog.ResolvePrefab(building.DefinitionId);
            var definition = catalog.ResolveDefinition(building.DefinitionId);
            var position = definition.Placement.SpatialForm != BuildingSpatialForm.Structure &&
                building.PlacementCells.Count > 0
                ? GridToWorld(building.PlacementCells)
                : GridToWorld(building.Coordinate, definition, building.Orientation);
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
                presenter.Bind(building, registry, storage, gridSize);
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

        public bool TryGetPresenter(Game.Domain.StableEntityId buildingId, out BuildingPresenter presenter) =>
            registry.TryGet(buildingId, out presenter);

        public Vector3 GridToWorld(GridCoordinate coordinate) => ResolveGround(origin + new Vector3(
            coordinate.X * gridSize, coordinate.Level * gridSize, coordinate.Z * gridSize), coordinate.Level);

        public Vector3 GridToWorld(GridCoordinate coordinate, BuildingDefinition definition, BuildingOrientation orientation)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var footprint = definition.Footprint.Rotate(orientation);
            return ResolveGround(origin + new Vector3((coordinate.X + (footprint.Width - 1) * 0.5f) * gridSize,
                coordinate.Level * gridSize, (coordinate.Z + (footprint.Depth - 1) * 0.5f) * gridSize),
                coordinate.Level);
        }

        public Vector3 GridToWorld(IReadOnlyList<GridCoordinate> cells)
        {
            if (cells == null || cells.Count == 0) throw new ArgumentException("Placement cells are required.", nameof(cells));
            var minX = cells.Min(x => x.X);
            var maxX = cells.Max(x => x.X);
            var minZ = cells.Min(x => x.Z);
            var maxZ = cells.Max(x => x.Z);
            return ResolveGround(origin + new Vector3((minX + maxX) * .5f * gridSize,
                cells[0].Level * gridSize, (minZ + maxZ) * .5f * gridSize), cells[0].Level);
        }

        private Vector3 ResolveGround(Vector3 position, int level)
        {
            if (groundHeight == null || level != 0) return position;
            position.y = groundHeight(position);
            return position;
        }
    }
}
