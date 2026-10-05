using System;
using System.Collections.Generic;
using Game.Domain.Resources;
using UnityEngine;

namespace Game.Presentation.Resources
{
    [CreateAssetMenu(menuName = "Serenity/Resource Catalog")]
    public sealed class ResourceCatalogAsset : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private ResourceCategory category;

            public Entry Configure(string resourceId, string name, ResourceCategory resourceCategory)
            { id = resourceId; displayName = name; category = resourceCategory; return this; }
            public ResourceDefinition CreateDefinition() => new ResourceDefinition(new ResourceId(id), displayName, category);
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public void Configure(IEnumerable<Entry> definitions)
        { entries = new List<Entry>(definitions ?? throw new ArgumentNullException(nameof(definitions))); Validate(); }

        public ResourceCatalog CreateCatalog()
        {
            var definitions = new List<ResourceDefinition>();
            foreach (var entry in entries)
                definitions.Add(entry != null ? entry.CreateDefinition() : throw new InvalidOperationException("Null resource entry."));
            return new ResourceCatalog(definitions);
        }

        public void Validate()
        { if (entries == null || entries.Count == 0) throw new InvalidOperationException("Resource catalog is empty."); CreateCatalog(); }
    }
}
