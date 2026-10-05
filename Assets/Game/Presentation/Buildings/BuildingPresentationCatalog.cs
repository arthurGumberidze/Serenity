using System;
using System.Collections.Generic;
using Game.Domain.Buildings;
using UnityEngine;

namespace Game.Presentation.Buildings
{
    [CreateAssetMenu(menuName = "Serenity/Building Presentation Catalog")]
    public sealed class BuildingPresentationCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string definitionId;
            [SerializeField] private string displayName;
            [SerializeField] private BuildingCategory category;
            [SerializeField, Min(1)] private int footprintWidth = 1;
            [SerializeField, Min(1)] private int footprintDepth = 1;
            [SerializeField] private BuildingPlacementMode placementMode = BuildingPlacementMode.Grid;
            [SerializeField] private bool allowsOverlap;
            [SerializeField] private GameObject prefab;

            public string DefinitionId => definitionId;
            public GameObject Prefab => prefab;

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

            public BuildingDefinition CreateDefinition() => new BuildingDefinition(new BuildingDefinitionId(definitionId),
                displayName, category, new BuildingFootprint(footprintWidth, footprintDepth), placementMode, allowsOverlap);
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        [SerializeField] private string defaultDefinitionId = "primitive_shelter";

        public BuildingDefinitionId DefaultDefinitionId => new BuildingDefinitionId(defaultDefinitionId);
        public IReadOnlyList<Entry> Entries => entries;

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
