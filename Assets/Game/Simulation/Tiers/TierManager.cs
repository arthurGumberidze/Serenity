using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;

namespace Game.Simulation.Tiers
{
    public interface ICharacterTierLookup
    {
        bool TryGetTier(StableEntityId characterId, out CharacterSimulationTier tier);
    }

    public readonly struct TierTransitionResult
    {
        public TierTransitionResult(StableEntityId characterId, CharacterSimulationTier source,
            CharacterSimulationTier target, bool changed)
        {
            CharacterId = characterId;
            SourceTier = source;
            TargetTier = target;
            Changed = changed;
        }

        public StableEntityId CharacterId { get; }
        public CharacterSimulationTier SourceTier { get; }
        public CharacterSimulationTier TargetTier { get; }
        public bool Changed { get; }
    }

    /// <summary>
    /// Session-owned one-active-representation coordinator. Character remains the durable authority;
    /// adapters own only tier-specific runtime projections.
    /// </summary>
    public sealed class TierManager : ICharacterTierLookup
    {
        private readonly CharacterRegistry characters;
        private readonly Dictionary<CharacterSimulationTier, ICharacterTierAdapter> adapters;
        private readonly Dictionary<StableEntityId, CharacterSimulationTier> tiers =
            new Dictionary<StableEntityId, CharacterSimulationTier>();
        private readonly HashSet<StableEntityId> transitioning = new HashSet<StableEntityId>();

        public TierManager(CharacterRegistry characterRegistry, params ICharacterTierAdapter[] tierAdapters)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            if (tierAdapters == null) throw new ArgumentNullException(nameof(tierAdapters));
            adapters = tierAdapters.ToDictionary(x => x?.Tier ?? throw new ArgumentNullException(nameof(tierAdapters)));
            foreach (CharacterSimulationTier tier in Enum.GetValues(typeof(CharacterSimulationTier)))
                if (!adapters.ContainsKey(tier))
                    throw new ArgumentException("An adapter is required for " + tier + ".", nameof(tierAdapters));
        }

        public int Count => tiers.Count;
        public IReadOnlyList<StableEntityId> ManagedIds => tiers.Keys
            .OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray();

        public void RegisterExisting(StableEntityId characterId, CharacterSimulationTier tier)
        {
            ValidateTier(tier);
            characters.Get(characterId);
            if (tiers.ContainsKey(characterId))
                throw new InvalidOperationException("Character is already managed by this TierManager.");
            if (!adapters[tier].IsActive(characterId) || CountActiveRepresentations(characterId) != 1)
                throw new InvalidOperationException("Exactly the declared representation must be active when registering.");
            var state = adapters[tier].Capture(characterId);
            RequireMatchingIdentity(characterId, state);
            tiers.Add(characterId, tier);
        }

        public void Materialize(Character character, CharacterSimulationTier tier, CharacterRuntimeState state)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            ValidateTier(tier);
            if (!characters.TryGet(character.Id, out var canonical) || !ReferenceEquals(canonical, character))
                throw new InvalidOperationException("TierManager can only manage the canonical registered Character instance.");
            RequireMatchingIdentity(character.Id, state);
            if (tiers.ContainsKey(character.Id) || CountActiveRepresentations(character.Id) != 0)
                throw new InvalidOperationException("Character already has a managed or active representation.");
            adapters[tier].ValidateMaterialization(character, state);
            adapters[tier].Materialize(character, state);
            if (!adapters[tier].IsActive(character.Id))
                throw new InvalidOperationException("Tier adapter did not activate the requested representation.");
            tiers.Add(character.Id, tier);
        }

        public CharacterSimulationTier GetTier(StableEntityId characterId) => tiers.TryGetValue(characterId, out var tier)
            ? tier : throw new KeyNotFoundException("Character is not managed by this TierManager.");

        public bool TryGetTier(StableEntityId characterId, out CharacterSimulationTier tier) =>
            tiers.TryGetValue(characterId, out tier);

        public CharacterRuntimeState GetRuntimeState(StableEntityId characterId)
        {
            var tier = GetTier(characterId);
            AssertExactlyOneActive(characterId, tier);
            var state = adapters[tier].Capture(characterId);
            RequireMatchingIdentity(characterId, state);
            return state;
        }

        public TierTransitionResult Transition(StableEntityId characterId, CharacterSimulationTier targetTier)
        {
            ValidateTier(targetTier);
            var sourceTier = GetTier(characterId);
            AssertExactlyOneActive(characterId, sourceTier);
            if (sourceTier == targetTier)
                return new TierTransitionResult(characterId, sourceTier, targetTier, false);
            if (!transitioning.Add(characterId))
                throw new InvalidOperationException("A re-entrant transition for the same character is not allowed.");

            try
            {
                var character = characters.Get(characterId);
                var source = adapters[sourceTier];
                var target = adapters[targetTier];
                if (target.IsActive(characterId))
                    throw new InvalidOperationException("Target representation is already active.");

                var captured = source.Capture(characterId);
                RequireMatchingIdentity(characterId, captured);
                target.ValidateMaterialization(character, captured);
                source.Dematerialize(characterId);
                if (source.IsActive(characterId))
                    throw new InvalidOperationException("Source adapter did not release its active representation.");

                try
                {
                    target.Materialize(character, captured);
                    if (!target.IsActive(characterId))
                        throw new InvalidOperationException("Target adapter did not activate its representation.");
                    tiers[characterId] = targetTier;
                    AssertExactlyOneActive(characterId, targetTier);
                    return new TierTransitionResult(characterId, sourceTier, targetTier, true);
                }
                catch (Exception targetFailure)
                {
                    try
                    {
                        if (target.IsActive(characterId)) target.Dematerialize(characterId);
                        source.ValidateMaterialization(character, captured);
                        source.Materialize(character, captured);
                        AssertExactlyOneActive(characterId, sourceTier);
                        throw new TierTransitionException(characterId, sourceTier, targetTier, true, targetFailure);
                    }
                    catch (TierTransitionException)
                    {
                        throw;
                    }
                    catch (Exception rollbackFailure)
                    {
                        throw new TierTransitionException(characterId, sourceTier, targetTier, false,
                            targetFailure, rollbackFailure);
                    }
                }
            }
            finally
            {
                transitioning.Remove(characterId);
            }
        }

        public int CountActiveRepresentations(StableEntityId characterId) =>
            adapters.Values.Count(adapter => adapter.IsActive(characterId));

        private void AssertExactlyOneActive(StableEntityId characterId, CharacterSimulationTier expected)
        {
            if (!adapters[expected].IsActive(characterId) || CountActiveRepresentations(characterId) != 1)
                throw new InvalidOperationException("Tier invariant violated: exactly one declared representation must be active.");
        }

        private static void RequireMatchingIdentity(StableEntityId characterId, CharacterRuntimeState state)
        {
            if (state.CharacterId != characterId)
                throw new InvalidOperationException("A tier adapter returned a different stable identity.");
        }

        private static void ValidateTier(CharacterSimulationTier tier)
        {
            if (!Enum.IsDefined(typeof(CharacterSimulationTier), tier))
                throw new ArgumentOutOfRangeException(nameof(tier));
        }
    }
}
