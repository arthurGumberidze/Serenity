using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation.Environment
{
    /// <summary>Presentation-only mapping from authored POI IDs to audited imported assets.</summary>
    public sealed class WorldMapPoiArtCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string poiId;
            [SerializeField] private GameObject primaryPrefab;
            [SerializeField] private GameObject accentPrefab;
            [SerializeField] private GameObject effectPrefab;
            [SerializeField] private float visualScale = 1f;
            [SerializeField] private int primaryCount = 1;
            [SerializeField] private float primaryRadius;
            [SerializeField] private int accentCount;
            [SerializeField] private float accentRadius;

            public string PoiId => poiId;
            public GameObject PrimaryPrefab => primaryPrefab;
            public GameObject AccentPrefab => accentPrefab;
            public GameObject EffectPrefab => effectPrefab;
            public float VisualScale => visualScale;
            public int PrimaryCount => primaryCount;
            public float PrimaryRadius => primaryRadius;
            public int AccentCount => accentCount;
            public float AccentRadius => accentRadius;

            public Entry Configure(string id, GameObject primary, float scale = 1f, int count = 1,
                float radius = 0f, GameObject accent = null, int accents = 0, float accentRingRadius = 0f,
                GameObject effect = null)
            {
                if (string.IsNullOrWhiteSpace(id) || primary == null || scale <= 0f || count <= 0 || radius < 0f ||
                    accents < 0 || accentRingRadius < 0f || accents > 0 && accent == null)
                    throw new ArgumentException(nameof(id));
                poiId = id;
                primaryPrefab = primary;
                accentPrefab = accent;
                effectPrefab = effect;
                visualScale = scale;
                primaryCount = count;
                primaryRadius = radius;
                accentCount = accents;
                accentRadius = accentRingRadius;
                return this;
            }
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;

        public void Configure(IEnumerable<Entry> values)
        {
            entries = new List<Entry>(values ?? throw new ArgumentNullException(nameof(values)));
        }

        public Entry For(string poiId)
        {
            foreach (var entry in entries)
                if (entry != null && string.Equals(entry.PoiId, poiId, StringComparison.Ordinal)) return entry;
            return null;
        }
    }
}
