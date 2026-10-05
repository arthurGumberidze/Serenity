using System;
using System.Collections.Generic;
using Game.Domain.Buildings;
using Game.Domain.Resources;
using Game.Presentation.Resources;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    [CreateAssetMenu(menuName = "Serenity/Building Presentation Catalog")]
    public sealed class BuildingPresentationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Serializable]
            public sealed class CostEntry
            {
                [SerializeField] private string resourceId;
                [SerializeField, Min(1)] private long quantity;
                public CostEntry Configure(string id, long units) { resourceId = id; quantity = units; return this; }
                public ResourceAmount CreateAmount() => new ResourceAmount(new ResourceId(resourceId), new ResourceQuantity(quantity));
            }

            [SerializeField] private string definitionId;
            [SerializeField] private string displayName;
            [SerializeField] private BuildingCategory category;
            [SerializeField, Min(1)] private int footprintWidth = 1;
            [SerializeField, Min(1)] private int footprintDepth = 1;
            [SerializeField] private BuildingPlacementMode placementMode = BuildingPlacementMode.Grid;
            [SerializeField] private bool allowsOverlap;
            [SerializeField] private GameObject prefab;
            [SerializeField] private List<CostEntry> constructionCost = new List<CostEntry>();
            [SerializeField, Min(0)] private long storageCapacity;
            [SerializeField] private List<ResourceCategory> storageCategories = new List<ResourceCategory>();

            public string DefinitionId => definitionId;
            public GameObject Prefab => prefab;

            public Entry ConfigureEconomy(IEnumerable<CostEntry> cost, long capacity = 0,
                IEnumerable<ResourceCategory> categories = null)
            {
                constructionCost = new List<CostEntry>(cost ?? throw new ArgumentNullException(nameof(cost)));
                storageCapacity = capacity;
                storageCategories = new List<ResourceCategory>(categories ?? Enum.GetValues(typeof(ResourceCategory))
                    as ResourceCategory[] ?? Array.Empty<ResourceCategory>());
                return this;
            }

            public Entry Configure(string id, string name, BuildingCategory buildingCategory, int width, int depth,
                GameObject presentationPrefab, BuildingPlacementMode mode = BuildingPlacementMode.Grid, bool overlap = false)
            {
                definitionId = id;
                displayName = name;
                category = buildingCategory;
                footprintWidth = width;
                footprintDepth = depth;
                placementMode = mode;
                allowsOverlap = overlap;
                prefab = presentationPrefab;
                return this;
            }

            public BuildingDefinition CreateDefinition()
            {
                var cost = new List<ResourceAmount>();
                foreach (var entry in constructionCost ?? new List<CostEntry>())
                    cost.Add(entry != null ? entry.CreateAmount() : throw new InvalidOperationException("Null cost entry."));
                return new BuildingDefinition(new BuildingDefinitionId(definitionId), displayName, category,
                    new BuildingFootprint(footprintWidth, footprintDepth), placementMode, allowsOverlap,
                    cost, storageCapacity, storageCategories == null || storageCategories.Count == 0 ? null : storageCategories);
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private string defaultDefinitionId = "primitive_shelter";
        [SerializeField] private ResourceCatalogAsset resourceCatalog;

        public BuildingDefinitionId DefaultDefinitionId => new BuildingDefinitionId(defaultDefinitionId);
        public IReadOnlyList<Entry> Entries => entries;
        public ResourceCatalogAsset ResourceCatalog => resourceCatalog;

        public void ConfigureResources(ResourceCatalogAsset catalog) =>
            resourceCatalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));

        public void Configure(IEnumerable<Entry> configuredEntries, string defaultId)
        {
            entries = new List<Entry>(configuredEntries ?? throw new ArgumentNullException(nameof(configuredEntries)));
            defaultDefinitionId = defaultId;
            Validate();
        }

        public BuildingDefinition ResolveDefinition(BuildingDefinitionId id) => ResolveEntry(id).CreateDefinition();
        public GameObject ResolvePrefab(BuildingDefinitionId id)
        {
            var prefab = ResolveEntry(id).Prefab;
            if (prefab == null) throw new InvalidOperationException("Building presentation prefab is missing for " + id + ".");
            return prefab;
        }

        public void Validate()
        {
            if (entries == null || entries.Count == 0) throw new InvalidOperationException("At least one building definition is required.");
            var ids = new HashSet<BuildingDefinitionId>();
            foreach (var entry in entries)
            {
                if (entry == null) throw new InvalidOperationException("Building catalog contains a null entry.");
                var definition = entry.CreateDefinition();
                if (!ids.Add(definition.Id)) throw new InvalidOperationException("Building definition IDs must be unique.");
                if (entry.Prefab == null) throw new InvalidOperationException("Building prefab is required for " + definition.Id + ".");
            }
            if (!ids.Contains(DefaultDefinitionId)) throw new InvalidOperationException("Default building definition is not in the catalog.");
            resourceCatalog?.Validate();
        }

        private Entry ResolveEntry(BuildingDefinitionId id)
        {
            if (!id.IsValid) throw new ArgumentException("A valid building definition ID is required.", nameof(id));
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.DefinitionId, id.ToString(), StringComparison.Ordinal)) return entry;
            throw new KeyNotFoundException("Unknown building definition: " + id + ".");
        }
    }
}
