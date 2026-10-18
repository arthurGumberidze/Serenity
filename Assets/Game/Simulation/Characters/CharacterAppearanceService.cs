using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Characters;
using Game.Simulation.Tiers;

namespace Game.Simulation.Characters
{
    /// <summary>Canonical appearance authority, independent from every presentation/simulation tier.</summary>
    public sealed class CharacterAppearanceService
    {
        public const uint GenerationStream = 0x50340001u;

        private readonly CharacterRegistry characters;
        private readonly AppearanceDefinitionCatalog catalog;
        private readonly long worldSeed;
        private readonly Dictionary<StableEntityId, AppearanceState> states =
            new Dictionary<StableEntityId, AppearanceState>();

        public CharacterAppearanceService(CharacterRegistry characterRegistry,
            AppearanceDefinitionCatalog definitionCatalog, long seed)
        {
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            catalog = definitionCatalog ?? throw new ArgumentNullException(nameof(definitionCatalog));
            worldSeed = seed;
        }

        public AppearanceDefinitionCatalog Catalog => catalog;

        public AppearanceState GetOrCreate(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (!characters.TryGet(character.Id, out var canonical) || !ReferenceEquals(character, canonical))
                throw new InvalidOperationException("Appearance can only be resolved for a canonical registered Character.");
            if (states.TryGetValue(character.Id, out var state)) return state;
            state = Generate(character);
            states.Add(character.Id, state);
            return state;
        }

        public AppearanceState Get(StableEntityId characterId)
        {
            if (!characters.TryGet(characterId, out var character))
                throw new KeyNotFoundException("Character is not registered: " + characterId);
            return GetOrCreate(character);
        }

        public bool TryGet(StableEntityId characterId, out AppearanceState appearance) =>
            states.TryGetValue(characterId, out appearance);

        public bool Remove(StableEntityId characterId) => states.Remove(characterId);

        public void Set(AppearanceState appearance)
        {
            Validate(appearance, allowUnknownProfile: false);
            states[appearance.CharacterId] = appearance;
        }

        public AppearanceState SetClothing(StableEntityId characterId, AppearancePartId clothingId)
        {
            var current = Get(characterId);
            var definition = StoneAgeClothingDefinitions.CreateCatalog().Get(clothingId);
            if (!definition.Supports(current.ProfileId))
                throw new InvalidOperationException("Clothing is incompatible with the Character appearance profile.");
            var replacement = current.With(clothingId: clothingId);
            Validate(replacement, allowUnknownProfile: false);
            states[characterId] = replacement;
            return replacement;
        }

        public void EnsureDefaults(IEnumerable<Character> values)
        {
            foreach (var character in (values ?? throw new ArgumentNullException(nameof(values)))
                         .OrderBy(x => x.Id.ToString(), StringComparer.Ordinal))
                GetOrCreate(character);
        }

        public AppearanceWorldSaveState CaptureState()
        {
            EnsureDefaults(characters.GetAll());
            return new AppearanceWorldSaveState(states.Values);
        }

        public void RestoreState(AppearanceWorldSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var replacement = new Dictionary<StableEntityId, AppearanceState>();
            foreach (var appearance in state.Characters)
            {
                Validate(appearance, allowUnknownProfile: true);
                if (replacement.ContainsKey(appearance.CharacterId))
                    throw new ArgumentException("Duplicate appearance Character ID.", nameof(state));
                replacement.Add(appearance.CharacterId, appearance);
            }
            foreach (var character in characters.GetAll())
                if (!replacement.ContainsKey(character.Id)) replacement.Add(character.Id, Generate(character));
            states.Clear();
            foreach (var pair in replacement) states.Add(pair.Key, pair.Value);
        }

        public void RestoreLegacyDefaults()
        {
            states.Clear();
            EnsureDefaults(characters.GetAll());
        }

        private AppearanceState Generate(Character character)
        {
            var compatible = catalog.ForSex(character.Sex);
            var sample = DeterministicKeyedRandom.Sample64(worldSeed, character.Id, 0, GenerationStream);
            var profile = compatible[(int)(sample % (ulong)compatible.Count)];
            var stream = 1u;
            AppearancePartId Pick(IReadOnlyList<AppearancePartId> options) => options[(int)
                (DeterministicKeyedRandom.Sample64(worldSeed, character.Id, 0, GenerationStream + stream++) %
                 (ulong)options.Count)];
            var parameters = profile.Parameters.Select(parameter => new AppearanceParameterValue(parameter,
                180 + (int)(DeterministicKeyedRandom.Sample64(worldSeed, character.Id, 0,
                    GenerationStream + stream++) % 641UL))).ToArray();
            return new AppearanceState(character.Id, profile.Id, profile.BodyId, profile.FaceId,
                Pick(profile.Skins), Pick(profile.Hairs), Pick(profile.Beards), Pick(profile.HairColors),
                Pick(profile.EyeColors), Pick(profile.Ages), StoneAgeAppearanceProfiles.CivilianClothing, parameters);
        }

        private void Validate(AppearanceState appearance, bool allowUnknownProfile)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            if (!characters.TryGet(appearance.CharacterId, out var character))
                throw new ArgumentException("Appearance references a missing Character.", nameof(appearance));
            if (!catalog.TryGet(appearance.ProfileId, out var definition))
            {
                if (allowUnknownProfile) return; // Content may be missing; Presentation owns the safe visual fallback.
                throw new ArgumentException("Appearance profile is not available in the active catalog.", nameof(appearance));
            }
            if (definition.Sex != character.Sex)
                throw new ArgumentException("Appearance profile is incompatible with Character sex.", nameof(appearance));
            if (!definition.Supports(appearance))
                throw new ArgumentException("Appearance contains a value unsupported by its profile.", nameof(appearance));
        }
    }
}
