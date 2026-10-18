using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Game.Domain.Characters
{
    public enum CharacterClothingRole
    {
        Civilian = 0,
        Work = 1,
        Military = 2
    }

    /// <summary>Immutable content definition. Availability describes admitted visual content, not save state.</summary>
    public sealed class CharacterClothingDefinition
    {
        public CharacterClothingDefinition(AppearancePartId id, CharacterClothingRole role,
            IEnumerable<AppearanceProfileId> compatibleProfiles, bool visualContentAvailable)
        {
            if (!id.IsValid) throw new ArgumentException("Clothing ID is required.", nameof(id));
            if (!Enum.IsDefined(typeof(CharacterClothingRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            var profiles = (compatibleProfiles ?? throw new ArgumentNullException(nameof(compatibleProfiles)))
                .Distinct().OrderBy(x => x).ToArray();
            if (profiles.Length == 0 || profiles.Any(x => !x.IsValid))
                throw new ArgumentException("At least one compatible appearance profile is required.", nameof(compatibleProfiles));
            Id = id;
            Role = role;
            CompatibleProfiles = new ReadOnlyCollection<AppearanceProfileId>(profiles);
            VisualContentAvailable = visualContentAvailable;
        }

        public AppearancePartId Id { get; }
        public CharacterClothingRole Role { get; }
        public IReadOnlyList<AppearanceProfileId> CompatibleProfiles { get; }
        public bool VisualContentAvailable { get; }
        public bool Supports(AppearanceProfileId profileId) => CompatibleProfiles.Contains(profileId);
    }

    public sealed class CharacterClothingCatalog
    {
        private readonly Dictionary<AppearancePartId, CharacterClothingDefinition> byId;
        private readonly ReadOnlyCollection<CharacterClothingDefinition> definitions;

        public CharacterClothingCatalog(IEnumerable<CharacterClothingDefinition> values)
        {
            var copy = (values ?? throw new ArgumentNullException(nameof(values))).OrderBy(x => x.Id.Value).ToArray();
            if (copy.Length == 0 || copy.Any(x => x == null) || copy.Select(x => x.Id).Distinct().Count() != copy.Length)
                throw new ArgumentException("Clothing definitions must be non-empty and unique.", nameof(values));
            definitions = new ReadOnlyCollection<CharacterClothingDefinition>(copy);
            byId = copy.ToDictionary(x => x.Id);
        }

        public IReadOnlyList<CharacterClothingDefinition> Definitions => definitions;
        public CharacterClothingDefinition Get(AppearancePartId id) => byId.TryGetValue(id, out var value)
            ? value : throw new KeyNotFoundException("Unknown clothing definition: " + id);
        public bool TryGet(AppearancePartId id, out CharacterClothingDefinition value) => byId.TryGetValue(id, out value);
    }

    public static class StoneAgeClothingDefinitions
    {
        public static CharacterClothingCatalog CreateCatalog()
        {
            var profiles = new[] { StoneAgeAppearanceProfiles.UmaMale, StoneAgeAppearanceProfiles.UmaFemale };
            return new CharacterClothingCatalog(new[]
            {
                new CharacterClothingDefinition(StoneAgeAppearanceProfiles.CivilianClothing,
                    CharacterClothingRole.Civilian, profiles, true),
                new CharacterClothingDefinition(StoneAgeAppearanceProfiles.WorkerClothing,
                    CharacterClothingRole.Work, profiles, false),
                new CharacterClothingDefinition(StoneAgeAppearanceProfiles.MilitaryClothing,
                    CharacterClothingRole.Military, profiles, false)
            });
        }
    }
}
