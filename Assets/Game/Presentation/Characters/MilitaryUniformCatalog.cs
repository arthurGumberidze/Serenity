using System;
using Game.Domain.Characters;
using Game.Domain.Military;
using UnityEngine;

namespace Game.Presentation.Characters
{
    /// <summary>Presentation definitions only; rank remains owned by MilitaryTrainingService.</summary>
    [CreateAssetMenu(menuName = "Serenity/Military Uniform Catalog")]
    public sealed class MilitaryUniformCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public MilitaryRank MinimumRank;
            public GameObject Male;
            public GameObject Female;
        }
        public Entry[] Entries = Array.Empty<Entry>();
        public GameObject Resolve(MilitaryRank rank, CharacterSex sex)
        {
            Entry selected = null;
            foreach (var entry in Entries)
                if (entry != null && entry.MinimumRank <= rank &&
                    (selected == null || entry.MinimumRank > selected.MinimumRank)) selected = entry;
            return selected == null ? null : sex == CharacterSex.Male ? selected.Male : selected.Female;
        }
    }
}
