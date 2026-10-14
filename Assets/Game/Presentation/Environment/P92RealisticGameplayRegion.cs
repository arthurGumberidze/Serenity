using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>
    /// Disposable presentation marker for the P9.2 gameplay terrain. Canonical locations, resources,
    /// construction and saves remain owned by Domain and Simulation services.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class P92RealisticGameplayRegion : MonoBehaviour
    {
        [Serializable]
        public sealed class LocationAnchor
        {
            [SerializeField] private string locationId;
            [SerializeField] private Transform anchor;

            public string LocationId => locationId;
            public Transform Anchor => anchor;

            public LocationAnchor Configure(string id, Transform value)
            {
                locationId = id;
                anchor = value;
                return this;
            }
        }

        [SerializeField] private Terrain terrain;
        [SerializeField] private Renderer waterRenderer;
        [SerializeField] private GameObject[] trees = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] rocks = Array.Empty<GameObject>();
        [SerializeField] private GameObject[] foliage = Array.Empty<GameObject>();
        [SerializeField] private LocationAnchor[] locations = Array.Empty<LocationAnchor>();
        [SerializeField] private string sourceAsset = "Unity URP Terrain Sample 1.0.3 (Asset Store 213197)";

        public Terrain Terrain => terrain;
        public Renderer WaterRenderer => waterRenderer;
        public IReadOnlyList<GameObject> Trees => trees;
        public IReadOnlyList<GameObject> Rocks => rocks;
        public IReadOnlyList<GameObject> Foliage => foliage;
        public IReadOnlyList<LocationAnchor> Locations => locations;
        public string SourceAsset => sourceAsset;

        public void Configure(Terrain terrainComponent, Renderer water,
            IEnumerable<GameObject> treeObjects, IEnumerable<GameObject> rockObjects,
            IEnumerable<GameObject> foliageObjects, IEnumerable<LocationAnchor> locationAnchors)
        {
            terrain = terrainComponent;
            waterRenderer = water;
            trees = Compact(treeObjects);
            rocks = Compact(rockObjects);
            foliage = Compact(foliageObjects);
            locations = (locationAnchors ?? Enumerable.Empty<LocationAnchor>())
                .Where(x => x != null && x.Anchor != null && !string.IsNullOrWhiteSpace(x.LocationId)).ToArray();
        }

        public Vector3 ProjectToGround(Vector3 worldPosition, float offset = 0f)
        {
            if (terrain == null) return worldPosition;
            worldPosition.y = terrain.SampleHeight(worldPosition) + terrain.transform.position.y + offset;
            return worldPosition;
        }

        public bool TryGetLocationAnchor(string locationId, out Transform result)
        {
            var match = locations.FirstOrDefault(x => string.Equals(x.LocationId, locationId,
                StringComparison.Ordinal));
            result = match?.Anchor;
            return result != null;
        }

        public void ValidateConfiguration()
        {
            if (terrain == null || terrain.terrainData == null || terrain.GetComponent<TerrainCollider>() == null)
                throw new InvalidOperationException("P9.2 requires a physical Unity Terrain.");
            if (terrain.terrainData.terrainLayers == null || terrain.terrainData.terrainLayers.Length < 4)
                throw new InvalidOperationException("P9.2 requires at least four official PBR terrain layers.");
            if (waterRenderer == null || waterRenderer.sharedMaterial == null)
                throw new InvalidOperationException("P9.2 water presentation is missing.");
            if (trees.Length < 40 || rocks.Length < 22 || foliage.Length < 96)
                throw new InvalidOperationException("P9.2 environment density is incomplete.");
            var required = new[]
            {
                "starter_settlement", "enemy_settlement", "north_forest",
                "river_ford", "east_hills", "south_field"
            };
            if (locations.Select(x => x.LocationId).Distinct(StringComparer.Ordinal).Count() != locations.Length ||
                required.Any(id => !locations.Any(x => string.Equals(x.LocationId, id, StringComparison.Ordinal))))
                throw new InvalidOperationException("P9.2 requires all six canonical local location anchors.");
            if (!ValidShaders(trees.Concat(rocks).Concat(foliage).Append(waterRenderer.gameObject)))
                throw new InvalidOperationException("P9.2 contains a missing or error shader.");
        }

        private static GameObject[] Compact(IEnumerable<GameObject> values) =>
            (values ?? Enumerable.Empty<GameObject>()).Where(x => x != null).ToArray();

        private static bool ValidShaders(IEnumerable<GameObject> roots) => roots
            .SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).All(renderer =>
                renderer.sharedMaterials.Length > 0 && renderer.sharedMaterials.All(material =>
                    material != null && material.shader != null &&
                    !string.Equals(material.shader.name, "Hidden/InternalErrorShader", StringComparison.Ordinal)));
    }
}
