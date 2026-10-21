using System;
using System.Collections.Generic;
using Game.Domain.Buildings;
using Game.Domain.Research;
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
            [SerializeField, TextArea] private string description;
            [SerializeField] private string iconKey;
            [SerializeField] private BuildingCategory category;
            [SerializeField] private BuildingEra era = BuildingEra.StoneAge;
            [SerializeField] private List<string> requiredTechnologyIds = new List<string>();
            [SerializeField, Min(1)] private int footprintWidth = 1;
            [SerializeField, Min(1)] private int footprintDepth = 1;
            [SerializeField] private BuildingPlacementMode placementMode = BuildingPlacementMode.Grid;
            [SerializeField] private bool allowsOverlap;
            [SerializeField] private BuildingSpatialForm spatialForm = BuildingSpatialForm.Structure;
            [SerializeField] private BuildingPlacementConstraint placementConstraints = BuildingPlacementConstraint.ReachableApproach;
            [SerializeField, Min(0)] private int maximumHeightDeltaMillimetres = 750;
            [SerializeField, Min(0)] private int approachClearanceMillimetres = 750;
            [SerializeField] private GameObject prefab;
            [SerializeField] private List<CostEntry> constructionCost = new List<CostEntry>();
            [SerializeField, Min(1)] private int constructionWorkMinutes = 30;
            [SerializeField, Range(1, 32)] private int constructionStageCount = 3;
            [SerializeField, Min(0)] private long storageCapacity;
            [SerializeField] private List<ResourceCategory> storageCategories = new List<ResourceCategory>();
            [SerializeField, Min(1)] private int maxIntegrity = 1000;
            [SerializeField, Range(1, 2000)] private int fireDamagePerThousand = 1000;
            [SerializeField, Range(1, 2000)] private int combatDamagePerThousand = 1000;
            [SerializeField] private bool capturable = true;

            public string DefinitionId => definitionId;
            public GameObject Prefab => prefab;

            public Entry ConfigureMetadata(BuildingEra buildingEra, string configuredDescription,
                string configuredIconKey = null, IEnumerable<string> technologyIds = null)
            {
                era = buildingEra;
                description = configuredDescription;
                iconKey = configuredIconKey;
                requiredTechnologyIds = new List<string>(technologyIds ?? Array.Empty<string>());
                return this;
            }

            public Entry ConfigureConstruction(int workMinutes, int stageCount = 3)
            {
                constructionWorkMinutes = workMinutes;
                constructionStageCount = stageCount;
                return this;
            }

            public Entry ConfigurePlacement(BuildingSpatialForm form, BuildingPlacementConstraint constraints,
                int maximumHeightDeltaMm = 750, int approachClearanceMm = 750)
            {
                spatialForm = form;
                placementConstraints = constraints;
                maximumHeightDeltaMillimetres = maximumHeightDeltaMm;
                approachClearanceMillimetres = approachClearanceMm;
                return this;
            }

            public Entry ConfigureEconomy(IEnumerable<CostEntry> cost, long capacity = 0,
                IEnumerable<ResourceCategory> categories = null)
            {
                constructionCost = new List<CostEntry>(cost ?? throw new ArgumentNullException(nameof(cost)));
                storageCapacity = capacity;
                storageCategories = new List<ResourceCategory>(categories ?? Enum.GetValues(typeof(ResourceCategory))
                    as ResourceCategory[] ?? Array.Empty<ResourceCategory>());
                return this;
            }

            public Entry ConfigureStructure(int integrity, int fireDamageModifier = 1000,
                int combatDamageModifier = 1000, bool canBeCaptured = true)
            {
                maxIntegrity = integrity;
                fireDamagePerThousand = fireDamageModifier;
                combatDamagePerThousand = combatDamageModifier;
                capturable = canBeCaptured;
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
                var technologies = new List<TechnologyId>();
                foreach (var id in requiredTechnologyIds ?? new List<string>()) technologies.Add(new TechnologyId(id));
                var construction = new ConstructionSpec(cost,
                    TimeSpan.FromMinutes(constructionWorkMinutes <= 0 ? 30 : constructionWorkMinutes),
                    constructionStageCount <= 0 ? 1 : constructionStageCount);
                var placement = new PlacementSpec(new BuildingFootprint(footprintWidth, footprintDepth),
                    placementMode, allowsOverlap, spatialForm, placementConstraints,
                    maximumHeightDeltaMillimetres < 0 ? 0 : maximumHeightDeltaMillimetres,
                    approachClearanceMillimetres < 0 ? 0 : approachClearanceMillimetres);
                var storage = storageCapacity > 0 ? new StorageSpec(storageCapacity,
                    storageCategories == null || storageCategories.Count == 0
                        ? Enum.GetValues(typeof(ResourceCategory)) as ResourceCategory[]
                        : storageCategories) : null;
                return new BuildingDefinition(new BuildingDefinitionId(definitionId), displayName, category,
                    era, description, iconKey, technologies, construction, placement, storage: storage,
                    maxIntegrity: maxIntegrity <= 0 ? 1000 : maxIntegrity,
                    fireDamagePerThousand: fireDamagePerThousand <= 0 ? 1000 : fireDamagePerThousand,
                    combatDamagePerThousand: combatDamagePerThousand <= 0 ? 1000 : combatDamagePerThousand,
                    capturable: capturable);
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
            var definitions = new List<BuildingDefinition>();
            foreach (var entry in entries)
            {
                if (entry == null) throw new InvalidOperationException("Building catalog contains a null entry.");
                var definition = entry.CreateDefinition();
                if (!ids.Add(definition.Id)) throw new InvalidOperationException("Building definition IDs must be unique.");
                if (entry.Prefab == null) throw new InvalidOperationException("Building prefab is required for " + definition.Id + ".");
                definitions.Add(definition);
            }
            if (!ids.Contains(DefaultDefinitionId)) throw new InvalidOperationException("Default building definition is not in the catalog.");
            resourceCatalog?.Validate();
            _ = new BuildingDefinitionCatalog(definitions, resourceCatalog?.CreateCatalog());
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
