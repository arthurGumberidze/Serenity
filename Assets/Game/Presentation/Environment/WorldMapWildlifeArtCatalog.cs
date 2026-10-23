using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Presentation-only mappings to audited imported animal prefabs.</summary>
    public sealed class WorldMapWildlifeArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string speciesId;
            [SerializeField] private GameObject prefab;
            [SerializeField] private float scale = 1f;

            public string SpeciesId => speciesId;
            public GameObject Prefab => prefab;
            public float Scale => scale;

            public Entry Configure(string id, GameObject value, float visualScale)
            {
                if (string.IsNullOrWhiteSpace(id) || value == null || visualScale <= 0f)
                    throw new ArgumentException(nameof(id));
                speciesId = id;
                prefab = value;
                scale = visualScale;
                return this;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        public void Configure(IEnumerable<Entry> values)
        {
            entries = new List<Entry>(values ?? throw new ArgumentNullException(nameof(values)));
        }

        public Entry For(string speciesId)
        {
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.SpeciesId, speciesId, StringComparison.Ordinal)) return entry;
            return null;
        }
    }
}
