using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Game.Domain.Characters
{
    public readonly struct AppearanceProfileId : IEquatable<AppearanceProfileId>, IComparable<AppearanceProfileId>
    {
        public string Value { get; }
        public bool IsValid => AppearanceIdRules.IsValid(Value);

        public AppearanceProfileId(string value)
        {
            if (!AppearanceIdRules.IsValid(value))
                throw new ArgumentException("Appearance profile IDs must be lowercase stable content keys.", nameof(value));
            Value = value;
        }

        public int CompareTo(AppearanceProfileId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(AppearanceProfileId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AppearanceProfileId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AppearanceProfileId left, AppearanceProfileId right) => left.Equals(right);
        public static bool operator !=(AppearanceProfileId left, AppearanceProfileId right) => !left.Equals(right);
    }

    public readonly struct AppearancePartId : IEquatable<AppearancePartId>
    {
        public string Value { get; }
        public bool IsValid => AppearanceIdRules.IsValid(Value);

        public AppearancePartId(string value)
        {
            if (!AppearanceIdRules.IsValid(value))
                throw new ArgumentException("Appearance part IDs must be lowercase stable content keys.", nameof(value));
            Value = value;
        }

        public bool Equals(AppearancePartId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AppearancePartId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AppearancePartId left, AppearancePartId right) => left.Equals(right);
        public static bool operator !=(AppearancePartId left, AppearancePartId right) => !left.Equals(right);
    }

    internal static class AppearanceIdRules
    {
        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64 || value != value.Trim()) return false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '_' && c != '-') return false;
            }
            return true;
        }
    }

    /// <summary>Canonical data-only appearance for one persistent Character.</summary>
    public sealed class AppearanceState
    {
        public AppearanceState(StableEntityId characterId, AppearanceProfileId profileId,
            AppearancePartId bodyId, AppearancePartId faceId, AppearancePartId skinId,
            AppearancePartId hairId, AppearancePartId beardId, AppearancePartId hairColorId,
            AppearancePartId clothingId)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character ID is required.", nameof(characterId));
            if (!profileId.IsValid || !bodyId.IsValid || !faceId.IsValid || !skinId.IsValid ||
                !hairId.IsValid || !beardId.IsValid || !hairColorId.IsValid || !clothingId.IsValid)
                throw new ArgumentException("Every appearance field requires a stable content ID.");
            CharacterId = characterId;
            ProfileId = profileId;
            BodyId = bodyId;
            FaceId = faceId;
            SkinId = skinId;
            HairId = hairId;
            BeardId = beardId;
            HairColorId = hairColorId;
            ClothingId = clothingId;
        }

        public StableEntityId CharacterId { get; }
        public AppearanceProfileId ProfileId { get; }
        public AppearancePartId BodyId { get; }
        public AppearancePartId FaceId { get; }
        public AppearancePartId SkinId { get; }
        public AppearancePartId HairId { get; }
        public AppearancePartId BeardId { get; }
        public AppearancePartId HairColorId { get; }
        public AppearancePartId ClothingId { get; }
    }

    public sealed class AppearanceProfileDefinition
    {
        public AppearanceProfileDefinition(AppearanceProfileId id, CharacterSex sex,
            AppearancePartId bodyId, AppearancePartId faceId, AppearancePartId skinId,
            AppearancePartId hairId, AppearancePartId beardId, AppearancePartId hairColorId,
            AppearancePartId clothingId)
        {
            if (!id.IsValid) throw new ArgumentException("Profile ID is required.", nameof(id));
            if (!Enum.IsDefined(typeof(CharacterSex), sex)) throw new ArgumentOutOfRangeException(nameof(sex));
            if (!bodyId.IsValid || !faceId.IsValid || !skinId.IsValid || !hairId.IsValid || !beardId.IsValid ||
                !hairColorId.IsValid || !clothingId.IsValid)
                throw new ArgumentException("Every appearance profile part requires a stable content ID.");
            Id = id;
            Sex = sex;
            BodyId = bodyId;
            FaceId = faceId;
            SkinId = skinId;
            HairId = hairId;
            BeardId = beardId;
            HairColorId = hairColorId;
            ClothingId = clothingId;
        }

        public AppearanceProfileId Id { get; }
        public CharacterSex Sex { get; }
        public AppearancePartId BodyId { get; }
        public AppearancePartId FaceId { get; }
        public AppearancePartId SkinId { get; }
        public AppearancePartId HairId { get; }
        public AppearancePartId BeardId { get; }
        public AppearancePartId HairColorId { get; }
        public AppearancePartId ClothingId { get; }

        public AppearanceState CreateState(StableEntityId characterId) => new AppearanceState(characterId, Id,
            BodyId, FaceId, SkinId, HairId, BeardId, HairColorId, ClothingId);
    }

    public sealed class AppearanceDefinitionCatalog
    {
        private readonly Dictionary<AppearanceProfileId, AppearanceProfileDefinition> byId;
        private readonly ReadOnlyCollection<AppearanceProfileDefinition> profiles;

        public AppearanceDefinitionCatalog(IEnumerable<AppearanceProfileDefinition> definitions)
        {
            var values = (definitions ?? throw new ArgumentNullException(nameof(definitions)))
                .OrderBy(x => x.Id).ToArray();
            if (values.Length == 0) throw new ArgumentException("At least one appearance profile is required.", nameof(definitions));
            byId = new Dictionary<AppearanceProfileId, AppearanceProfileDefinition>();
            foreach (var value in values)
            {
                if (value == null || byId.ContainsKey(value.Id))
                    throw new ArgumentException("Appearance profiles must be non-null and unique.", nameof(definitions));
                byId.Add(value.Id, value);
            }
            foreach (CharacterSex sex in Enum.GetValues(typeof(CharacterSex)))
                if (!values.Any(x => x.Sex == sex))
                    throw new ArgumentException("Every supported Character sex requires an appearance profile.", nameof(definitions));
            profiles = new ReadOnlyCollection<AppearanceProfileDefinition>(values);
        }

        public IReadOnlyList<AppearanceProfileDefinition> Profiles => profiles;
        public bool TryGet(AppearanceProfileId id, out AppearanceProfileDefinition definition) =>
            byId.TryGetValue(id, out definition);
        public AppearanceProfileDefinition Get(AppearanceProfileId id) => byId.TryGetValue(id, out var value)
            ? value : throw new KeyNotFoundException("Unknown appearance profile: " + id);
        public IReadOnlyList<AppearanceProfileDefinition> ForSex(CharacterSex sex) =>
            profiles.Where(x => x.Sex == sex).ToArray();
        public AppearanceProfileDefinition DefaultFor(CharacterSex sex) =>
            profiles.FirstOrDefault(x => x.Sex == sex) ??
            throw new InvalidOperationException("No appearance profile is available for " + sex + ".");
    }

    public static class StoneAgeAppearanceProfiles
    {
        public static readonly AppearanceProfileId AdultA = new AppearanceProfileId("hodaart_adult_a");
        public static readonly AppearanceProfileId AdultB = new AppearanceProfileId("hodaart_adult_b");

        public static AppearanceDefinitionCatalog CreateCatalog() => new AppearanceDefinitionCatalog(new[]
        {
            new AppearanceProfileDefinition(AdultA, CharacterSex.Male,
                Part("hodaart_a_body"), Part("hodaart_a_face"), Part("hodaart_a_skin"),
                Part("hodaart_a_hair"), Part("hodaart_a_beard"), Part("hodaart_a_hair_color"),
                Part("hodaart_a_stone_age_clothing")),
            new AppearanceProfileDefinition(AdultB, CharacterSex.Female,
                Part("hodaart_b_body"), Part("hodaart_b_face"), Part("hodaart_b_skin"),
                Part("hodaart_b_hair"), Part("none"), Part("hodaart_b_hair_color"),
                Part("hodaart_b_stone_age_clothing"))
        });

        private static AppearancePartId Part(string value) => new AppearancePartId(value);
    }

    public sealed class AppearanceWorldSaveState
    {
        public AppearanceWorldSaveState(IEnumerable<AppearanceState> characters)
        {
            var unsorted = (characters ?? Array.Empty<AppearanceState>()).ToArray();
            if (unsorted.Any(x => x == null))
                throw new ArgumentException("Appearance states must be non-null and unique by Character ID.", nameof(characters));
            var values = unsorted.OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal).ToArray();
            if (values.Select(x => x.CharacterId).Distinct().Count() != values.Length)
                throw new ArgumentException("Appearance states must be non-null and unique by Character ID.", nameof(characters));
            Characters = new ReadOnlyCollection<AppearanceState>(values);
        }

        public IReadOnlyList<AppearanceState> Characters { get; }
    }
}
