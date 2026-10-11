using System;
using System.Collections.Generic;

namespace Game.Domain.Characters
{
    public sealed class Tier3CharacterRegistry
    {
        private readonly Dictionary<StableEntityId, Tier3CharacterRecord> records =
            new Dictionary<StableEntityId, Tier3CharacterRecord>();
        private readonly List<Tier3CharacterRecord> orderedRecords = new List<Tier3CharacterRecord>();

        public int Count => records.Count;
        public IReadOnlyList<Tier3CharacterRecord> All => orderedRecords;

        public void Add(Tier3CharacterRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!records.TryAdd(record.CharacterId, record))
                throw new InvalidOperationException("A Tier 3 record already exists for this character.");
            var index = orderedRecords.BinarySearch(record, CharacterRecordComparer.Instance);
            if (index < 0) index = ~index;
            orderedRecords.Insert(index, record);
        }

        public bool TryGet(StableEntityId characterId, out Tier3CharacterRecord record)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            return records.TryGetValue(characterId, out record);
        }

        public Tier3CharacterRecord Get(StableEntityId characterId) => TryGet(characterId, out var record)
            ? record : throw new KeyNotFoundException("No Tier 3 record exists for this character.");

        public bool Remove(StableEntityId characterId)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character identity required.", nameof(characterId));
            if (!records.TryGetValue(characterId, out var record) || !records.Remove(characterId)) return false;
            orderedRecords.Remove(record);
            return true;
        }

        private sealed class CharacterRecordComparer : IComparer<Tier3CharacterRecord>
        {
            public static readonly CharacterRecordComparer Instance = new CharacterRecordComparer();
            public int Compare(Tier3CharacterRecord left, Tier3CharacterRecord right) =>
                left.CharacterId.CompareTo(right.CharacterId);
        }
    }
}
