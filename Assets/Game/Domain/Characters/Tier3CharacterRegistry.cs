using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Characters
{
    public sealed class Tier3CharacterRegistry
    {
        private readonly Dictionary<StableEntityId, Tier3CharacterRecord> records =
            new Dictionary<StableEntityId, Tier3CharacterRecord>();

        public int Count => records.Count;
        public IReadOnlyList<Tier3CharacterRecord> All => records.Values
            .OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal).ToArray();

        public void Add(Tier3CharacterRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (!records.TryAdd(record.CharacterId, record))
                throw new InvalidOperationException("A Tier 3 record already exists for this character.");
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
            return records.Remove(characterId);
        }
    }
}
