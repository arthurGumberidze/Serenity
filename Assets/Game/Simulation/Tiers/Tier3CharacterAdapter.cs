using System;
using Game.Domain;
using Game.Domain.Characters;

namespace Game.Simulation.Tiers
{
    public sealed class Tier3CharacterAdapter : ICharacterTierAdapter
    {
        private readonly Tier3CharacterRegistry records;

        public Tier3CharacterAdapter(Tier3CharacterRegistry registry)
        {
            records = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public CharacterSimulationTier Tier => CharacterSimulationTier.Tier3;
        public bool IsActive(StableEntityId characterId) => records.TryGet(characterId, out _);
        public CharacterRuntimeState Capture(StableEntityId characterId) => records.Get(characterId).State;

        public void ValidateMaterialization(Character character, CharacterRuntimeState state)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (character.Id != state.CharacterId)
                throw new ArgumentException("Character and Tier 3 state must share one identity.", nameof(state));
            if (IsActive(character.Id))
                throw new InvalidOperationException("Tier 3 representation is already active.");
        }

        public void Materialize(Character character, CharacterRuntimeState state)
        {
            ValidateMaterialization(character, state);
            records.Add(new Tier3CharacterRecord(state));
        }

        public void Dematerialize(StableEntityId characterId)
        {
            if (!records.Remove(characterId))
                throw new InvalidOperationException("Tier 3 representation is not active.");
        }
    }
}
