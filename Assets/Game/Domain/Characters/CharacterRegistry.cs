using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Characters
{
    /// <summary>Session-owned in-memory index. It is not a database repository or global singleton.</summary>
    public sealed class CharacterRegistry
    {
        private readonly Dictionary<StableEntityId, Character> characters =
            new Dictionary<StableEntityId, Character>();

        public int Count => characters.Count;

        public void Add(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (!characters.TryAdd(character.Id, character))
                throw new InvalidOperationException("A character with stable ID " + character.Id + " is already active.");
        }

        public bool Remove(StableEntityId id)
        {
            ValidateId(id, nameof(id));
            return characters.Remove(id);
        }

        public bool TryGet(StableEntityId id, out Character character)
        {
            ValidateId(id, nameof(id));
            return characters.TryGetValue(id, out character);
        }

        public Character Get(StableEntityId id)
        {
            ValidateId(id, nameof(id));
            if (!characters.TryGetValue(id, out var character))
                throw new KeyNotFoundException("No active character has stable ID " + id + ".");
            return character;
        }

        public IReadOnlyList<Character> GetAll()
        {
            return characters.Values.OrderBy(character => character.Id.ToString(), StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyList<CharacterState> CaptureStates() => GetAll().Select(character => character.CaptureState()).ToArray();

        public void RestoreStates(IEnumerable<CharacterState> states)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            var restored = new Dictionary<StableEntityId, Character>();
            foreach (var state in states)
            {
                var character = Character.Restore(state);
                if (!restored.TryAdd(character.Id, character))
                    throw new ArgumentException("Character state IDs must be unique.", nameof(states));
            }

            if (characters.Count == 0)
            {
                foreach (var pair in restored) characters.Add(pair.Key, pair.Value);
                return;
            }

            if (characters.Count != restored.Count || characters.Keys.Any(id => !restored.ContainsKey(id)))
                throw new InvalidOperationException("Restoring an active registry requires the same stable character identities.");
            foreach (var pair in restored)
            {
                var existing = characters[pair.Key];
                if (existing.Sex != pair.Value.Sex || existing.BiologicalBirthTick != pair.Value.BiologicalBirthTick)
                    throw new InvalidOperationException("Restored immutable character biography does not match the active canonical instance.");
            }
            foreach (var pair in restored)
                characters[pair.Key].RestoreMutableState(pair.Value.CaptureState());
        }

        public IReadOnlyList<Character> GetChildren(StableEntityId parentId)
        {
            ValidateId(parentId, nameof(parentId));
            return characters.Values.Where(character => character.HasParent(parentId))
                .OrderBy(character => character.Id.ToString(), StringComparer.Ordinal).ToArray();
        }

        public void LinkSpouses(StableEntityId firstId, StableEntityId secondId)
        {
            if (firstId == secondId) throw new ArgumentException("A character cannot be its own spouse.", nameof(secondId));
            var first = Get(firstId);
            var second = Get(secondId);
            if (first.SpouseId.HasValue && first.SpouseId.Value != secondId)
                throw new InvalidOperationException("The first character already has a different spouse.");
            if (second.SpouseId.HasValue && second.SpouseId.Value != firstId)
                throw new InvalidOperationException("The second character already has a different spouse.");
            first.SetSpouseFromRegistry(secondId);
            second.SetSpouseFromRegistry(firstId);
        }

        public void UnlinkSpouses(StableEntityId firstId, StableEntityId secondId)
        {
            var first = Get(firstId);
            var second = Get(secondId);
            if (first.SpouseId != secondId || second.SpouseId != firstId)
                throw new InvalidOperationException("The characters do not have a mutual spouse link.");
            first.SetSpouseFromRegistry(null);
            second.SetSpouseFromRegistry(null);
        }

        private static void ValidateId(StableEntityId id, string parameterName)
        {
            if (!id.IsValid) throw new ArgumentException("Character ID must be valid.", parameterName);
        }
    }
}
