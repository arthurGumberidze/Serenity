using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Presentation-only references to audited existing assets for World Map V2 resource nodes.</summary>
    public sealed class WorldMapResourceArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string resourceTypeId;
            [SerializeField] private GameObject prefab;

            public string ResourceTypeId => resourceTypeId;
            public GameObject Prefab => prefab;

            public Entry Configure(string id, GameObject value)
            {
                resourceTypeId = id;
                prefab = value;
                return this;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public void Configure(IEnumerable<Entry> values)
        {
            entries = new List<Entry>(values ?? throw new ArgumentNullException(nameof(values)));
        }

        public GameObject PrefabFor(string resourceTypeId)
        {
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.ResourceTypeId, resourceTypeId, StringComparison.Ordinal))
                    return entry.Prefab;
            return null;
        }
    }
}
