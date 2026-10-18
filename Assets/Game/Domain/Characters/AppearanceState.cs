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
        public AppearanceProfileId(string value) { if (!AppearanceIdRules.IsValid(value)) throw new ArgumentException("Appearance profile IDs must be lowercase stable content keys.", nameof(value)); Value = value; }
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
        public AppearancePartId(string value) { if (!AppearanceIdRules.IsValid(value)) throw new ArgumentException("Appearance part IDs must be lowercase stable content keys.", nameof(value)); Value = value; }
        public bool Equals(AppearancePartId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AppearancePartId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AppearancePartId left, AppearancePartId right) => left.Equals(right);
        public static bool operator !=(AppearancePartId left, AppearancePartId right) => !left.Equals(right);
    }

    public readonly struct AppearanceParameterId : IEquatable<AppearanceParameterId>, IComparable<AppearanceParameterId>
    {
        public string Value { get; }
        public bool IsValid => AppearanceIdRules.IsValid(Value);
        public AppearanceParameterId(string value) { if (!AppearanceIdRules.IsValid(value)) throw new ArgumentException("Appearance parameter IDs must be lowercase stable content keys.", nameof(value)); Value = value; }
        public int CompareTo(AppearanceParameterId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(AppearanceParameterId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AppearanceParameterId other && Equals(other);
        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AppearanceParameterId left, AppearanceParameterId right) => left.Equals(right);
        public static bool operator !=(AppearanceParameterId left, AppearanceParameterId right) => !left.Equals(right);
    }

    public readonly struct AppearanceParameterValue : IEquatable<AppearanceParameterValue>
    {
        public const int Min = 0;
        public const int Max = 1000;
        public AppearanceParameterValue(AppearanceParameterId id, int value)
        {
            if (!id.IsValid) throw new ArgumentException("Parameter ID is required.", nameof(id));
            if (value < Min || value > Max) throw new ArgumentOutOfRangeException(nameof(value));
            Id = id; Value = value;
        }
        public AppearanceParameterId Id { get; }
        public int Value { get; }
        public float Normalized => Value / 1000f;
        public bool Equals(AppearanceParameterValue other) => Id.Equals(other.Id) && Value == other.Value;
        public override bool Equals(object obj) => obj is AppearanceParameterValue other && Equals(other);
        public override int GetHashCode() => (Id.GetHashCode() * 397) ^ Value;
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
            : this(characterId, profileId, bodyId, faceId, skinId, hairId, beardId, hairColorId,
                new AppearancePartId("eye_brown"), new AppearancePartId("age_adult"), clothingId,
                Array.Empty<AppearanceParameterValue>()) { }

        public AppearanceState(StableEntityId characterId, AppearanceProfileId profileId,
            AppearancePartId bodyId, AppearancePartId faceId, AppearancePartId skinId,
            AppearancePartId hairId, AppearancePartId beardId, AppearancePartId hairColorId,
            AppearancePartId eyeColorId, AppearancePartId ageId, AppearancePartId clothingId,
            IEnumerable<AppearanceParameterValue> parameters)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character ID is required.", nameof(characterId));
            if (!profileId.IsValid || !bodyId.IsValid || !faceId.IsValid || !skinId.IsValid || !hairId.IsValid ||
                !beardId.IsValid || !hairColorId.IsValid || !eyeColorId.IsValid || !ageId.IsValid || !clothingId.IsValid)
                throw new ArgumentException("Every appearance field requires a stable content ID.");
            var values = (parameters ?? throw new ArgumentNullException(nameof(parameters))).OrderBy(x => x.Id).ToArray();
            if (values.Select(x => x.Id).Distinct().Count() != values.Length)
                throw new ArgumentException("Appearance parameters must be unique.", nameof(parameters));
            CharacterId = characterId; ProfileId = profileId; BodyId = bodyId; FaceId = faceId; SkinId = skinId;
            HairId = hairId; BeardId = beardId; HairColorId = hairColorId; EyeColorId = eyeColorId; AgeId = ageId;
            ClothingId = clothingId; Parameters = new ReadOnlyCollection<AppearanceParameterValue>(values);
        }

        public StableEntityId CharacterId { get; }
        public AppearanceProfileId ProfileId { get; }
        public AppearancePartId BodyId { get; }
        public AppearancePartId FaceId { get; }
        public AppearancePartId SkinId { get; }
        public AppearancePartId HairId { get; }
        public AppearancePartId BeardId { get; }
        public AppearancePartId HairColorId { get; }
        public AppearancePartId EyeColorId { get; }
        public AppearancePartId AgeId { get; }
        public AppearancePartId ClothingId { get; }
        public IReadOnlyList<AppearanceParameterValue> Parameters { get; }

        public int Parameter(AppearanceParameterId id, int fallback = 500)
        {
            var match = Parameters.FirstOrDefault(x => x.Id == id);
            return match.Id.IsValid ? match.Value : fallback;
        }

        public AppearanceState With(StableEntityId? characterId = null, AppearancePartId? skinId = null,
            AppearancePartId? hairId = null, AppearancePartId? beardId = null,
            AppearancePartId? hairColorId = null, AppearancePartId? eyeColorId = null,
            AppearancePartId? ageId = null, AppearancePartId? clothingId = null,
            IEnumerable<AppearanceParameterValue> parameters = null) =>
            new AppearanceState(characterId ?? CharacterId, ProfileId, BodyId, FaceId, skinId ?? SkinId,
                hairId ?? HairId, beardId ?? BeardId, hairColorId ?? HairColorId, eyeColorId ?? EyeColorId,
                ageId ?? AgeId, clothingId ?? ClothingId, parameters ?? Parameters);
    }

    public sealed class AppearanceProfileDefinition
    {
        public AppearanceProfileDefinition(AppearanceProfileId id, CharacterSex sex,
            AppearancePartId bodyId, AppearancePartId faceId, AppearancePartId skinId,
            AppearancePartId hairId, AppearancePartId beardId, AppearancePartId hairColorId,
            AppearancePartId clothingId)
            : this(id, sex, bodyId, faceId, skinId, hairId, beardId, hairColorId,
                Part("eye_brown"), Part("age_adult"), clothingId,
                new[] { skinId }, new[] { hairId }, new[] { beardId }, new[] { hairColorId },
                new[] { Part("eye_brown") }, new[] { Part("age_adult") }, new[] { clothingId },
                Array.Empty<AppearanceParameterId>()) { }

        public AppearanceProfileDefinition(AppearanceProfileId id, CharacterSex sex,
            AppearancePartId bodyId, AppearancePartId faceId, AppearancePartId skinId,
            AppearancePartId hairId, AppearancePartId beardId, AppearancePartId hairColorId,
            AppearancePartId eyeColorId, AppearancePartId ageId, AppearancePartId clothingId,
            IEnumerable<AppearancePartId> skins, IEnumerable<AppearancePartId> hairs,
            IEnumerable<AppearancePartId> beards, IEnumerable<AppearancePartId> hairColors,
            IEnumerable<AppearancePartId> eyeColors, IEnumerable<AppearancePartId> ages,
            IEnumerable<AppearancePartId> clothings,
            IEnumerable<AppearanceParameterId> parameters)
        {
            if (!id.IsValid) throw new ArgumentException("Profile ID is required.", nameof(id));
            if (!Enum.IsDefined(typeof(CharacterSex), sex)) throw new ArgumentOutOfRangeException(nameof(sex));
            Id = id; Sex = sex; BodyId = bodyId; FaceId = faceId; SkinId = skinId; HairId = hairId;
            BeardId = beardId; HairColorId = hairColorId; EyeColorId = eyeColorId; AgeId = ageId; ClothingId = clothingId;
            Skins = CopyOptions(skins, skinId, nameof(skins)); Hairs = CopyOptions(hairs, hairId, nameof(hairs));
            Beards = CopyOptions(beards, beardId, nameof(beards)); HairColors = CopyOptions(hairColors, hairColorId, nameof(hairColors));
            EyeColors = CopyOptions(eyeColors, eyeColorId, nameof(eyeColors)); Ages = CopyOptions(ages, ageId, nameof(ages));
            Clothings = CopyOptions(clothings, clothingId, nameof(clothings));
            var parameterValues = (parameters ?? throw new ArgumentNullException(nameof(parameters))).OrderBy(x => x).ToArray();
            if (parameterValues.Any(x => !x.IsValid) || parameterValues.Distinct().Count() != parameterValues.Length)
                throw new ArgumentException("Parameter IDs must be valid and unique.", nameof(parameters));
            Parameters = new ReadOnlyCollection<AppearanceParameterId>(parameterValues);
        }

        public AppearanceProfileId Id { get; }
        public CharacterSex Sex { get; }
        public AppearancePartId BodyId { get; }
        public AppearancePartId FaceId { get; }
        public AppearancePartId SkinId { get; }
        public AppearancePartId HairId { get; }
        public AppearancePartId BeardId { get; }
        public AppearancePartId HairColorId { get; }
        public AppearancePartId EyeColorId { get; }
        public AppearancePartId AgeId { get; }
        public AppearancePartId ClothingId { get; }
        public IReadOnlyList<AppearancePartId> Skins { get; }
        public IReadOnlyList<AppearancePartId> Hairs { get; }
        public IReadOnlyList<AppearancePartId> Beards { get; }
        public IReadOnlyList<AppearancePartId> HairColors { get; }
        public IReadOnlyList<AppearancePartId> EyeColors { get; }
        public IReadOnlyList<AppearancePartId> Ages { get; }
        public IReadOnlyList<AppearancePartId> Clothings { get; }
        public IReadOnlyList<AppearanceParameterId> Parameters { get; }

        public AppearanceState CreateState(StableEntityId characterId) => new AppearanceState(characterId, Id,
            BodyId, FaceId, SkinId, HairId, BeardId, HairColorId, EyeColorId, AgeId, ClothingId,
            Array.Empty<AppearanceParameterValue>());

        public AppearanceState CreateDetailedState(StableEntityId characterId) => new AppearanceState(characterId, Id,
            BodyId, FaceId, SkinId, HairId, BeardId, HairColorId, EyeColorId, AgeId, ClothingId,
            Parameters.Select(x => new AppearanceParameterValue(x, 500)));

        public bool Supports(AppearanceState state) => state != null && state.ProfileId == Id &&
            state.BodyId == BodyId && state.FaceId == FaceId && Clothings.Contains(state.ClothingId) &&
            Skins.Contains(state.SkinId) && Hairs.Contains(state.HairId) && Beards.Contains(state.BeardId) &&
            HairColors.Contains(state.HairColorId) && EyeColors.Contains(state.EyeColorId) && Ages.Contains(state.AgeId) &&
            (state.Parameters.Count == 0 || state.Parameters.Select(x => x.Id).SequenceEqual(Parameters));

        private static IReadOnlyList<AppearancePartId> CopyOptions(IEnumerable<AppearancePartId> source,
            AppearancePartId selected, string name)
        {
            var values = (source ?? throw new ArgumentNullException(name)).Distinct().ToArray();
            if (values.Length == 0 || values.Any(x => !x.IsValid) || !values.Contains(selected))
                throw new ArgumentException("Appearance options must be valid and contain the selected default.", name);
            return new ReadOnlyCollection<AppearancePartId>(values);
        }
        private static AppearancePartId Part(string value) => new AppearancePartId(value);
    }

    public sealed class AppearanceDefinitionCatalog
    {
        private readonly Dictionary<AppearanceProfileId, AppearanceProfileDefinition> byId;
        private readonly ReadOnlyCollection<AppearanceProfileDefinition> profiles;
        public AppearanceDefinitionCatalog(IEnumerable<AppearanceProfileDefinition> definitions)
        {
            var values = (definitions ?? throw new ArgumentNullException(nameof(definitions))).OrderBy(x => x.Id).ToArray();
            if (values.Length == 0) throw new ArgumentException("At least one appearance profile is required.", nameof(definitions));
            byId = new Dictionary<AppearanceProfileId, AppearanceProfileDefinition>();
            foreach (var value in values) { if (value == null || byId.ContainsKey(value.Id)) throw new ArgumentException("Appearance profiles must be non-null and unique.", nameof(definitions)); byId.Add(value.Id, value); }
            foreach (CharacterSex sex in Enum.GetValues(typeof(CharacterSex))) if (!values.Any(x => x.Sex == sex)) throw new ArgumentException("Every supported Character sex requires an appearance profile.", nameof(definitions));
            profiles = new ReadOnlyCollection<AppearanceProfileDefinition>(values);
        }
        public IReadOnlyList<AppearanceProfileDefinition> Profiles => profiles;
        public bool TryGet(AppearanceProfileId id, out AppearanceProfileDefinition definition) => byId.TryGetValue(id, out definition);
        public AppearanceProfileDefinition Get(AppearanceProfileId id) => byId.TryGetValue(id, out var value) ? value : throw new KeyNotFoundException("Unknown appearance profile: " + id);
        public IReadOnlyList<AppearanceProfileDefinition> ForSex(CharacterSex sex) => profiles.Where(x => x.Sex == sex).ToArray();
        public AppearanceProfileDefinition DefaultFor(CharacterSex sex) => profiles.FirstOrDefault(x => x.Sex == sex) ?? throw new InvalidOperationException("No appearance profile is available for " + sex + ".");
    }

    public static class StoneAgeAppearanceProfiles
    {
        public static readonly AppearanceProfileId UmaMale = new AppearanceProfileId("uma_human_male_30");
        public static readonly AppearanceProfileId UmaFemale = new AppearanceProfileId("uma_human_female_30");
        // Compatibility names retained for older callers; P8.4 replaces the old immutable Hodaart profiles.
        public static readonly AppearanceProfileId AdultA = UmaMale;
        public static readonly AppearanceProfileId AdultB = UmaFemale;
        public static readonly AppearancePartId CivilianClothing = Part("uma_stone_age_clothing");
        public static readonly AppearancePartId WorkerClothing = Part("uma_stone_age_worker");
        public static readonly AppearancePartId MilitaryClothing = Part("uma_stone_age_military");
        public static readonly AppearanceParameterId Head = Parameter("head_proportion");
        public static readonly AppearanceParameterId Jaw = Parameter("jaw_shape");
        public static readonly AppearanceParameterId Chin = Parameter("chin_shape");
        public static readonly AppearanceParameterId Nose = Parameter("nose_shape");
        public static readonly AppearanceParameterId Eyes = Parameter("eye_size");
        public static readonly AppearanceParameterId Brows = Parameter("brow_height");
        public static readonly AppearanceParameterId Lips = Parameter("lip_size");
        public static readonly AppearanceParameterId Height = Parameter("height");
        public static readonly AppearanceParameterId Build = Parameter("body_build");

        public static AppearanceDefinitionCatalog CreateCatalog()
        {
            var parameters = new[] { Head, Jaw, Chin, Nose, Eyes, Brows, Lips, Height, Build };
            var skins = Parts("skin_light", "skin_warm", "skin_deep");
            var hairs = Parts("hair_very_short", "hair_bun", "hair_messy", "hair_none");
            var colors = Parts("hair_black", "hair_brown", "hair_auburn", "hair_blond", "hair_gray");
            var eyes = Parts("eye_brown", "eye_hazel", "eye_green", "eye_blue", "eye_gray");
            var ages = Parts("age_adult", "age_mature", "age_aged");
            var clothings = new[] { CivilianClothing, WorkerClothing, MilitaryClothing };
            return new AppearanceDefinitionCatalog(new[]
            {
                new AppearanceProfileDefinition(UmaMale, CharacterSex.Male, Part("uma_male_body"), Part("uma_male_face"),
                    skins[1], hairs[0], Part("beard_none"), colors[1], eyes[0], ages[0], CivilianClothing,
                    skins, hairs, Parts("beard_none", "beard_trimmed", "beard_goatee", "beard_neckbeard"), colors, eyes, ages,
                    clothings, parameters),
                new AppearanceProfileDefinition(UmaFemale, CharacterSex.Female, Part("uma_female_body"), Part("uma_female_face"),
                    skins[1], hairs[1], Part("beard_none"), colors[1], eyes[0], ages[0], CivilianClothing,
                    skins, hairs, Parts("beard_none"), colors, eyes, ages, clothings, parameters)
            });
        }
        private static AppearancePartId Part(string value) => new AppearancePartId(value);
        private static AppearancePartId[] Parts(params string[] values) => values.Select(Part).ToArray();
        private static AppearanceParameterId Parameter(string value) => new AppearanceParameterId(value);
    }

    public sealed class AppearanceWorldSaveState
    {
        public AppearanceWorldSaveState(IEnumerable<AppearanceState> characters)
        {
            var unsorted = (characters ?? Array.Empty<AppearanceState>()).ToArray();
            if (unsorted.Any(x => x == null)) throw new ArgumentException("Appearance states must be non-null and unique by Character ID.", nameof(characters));
            var values = unsorted.OrderBy(x => x.CharacterId.ToString(), StringComparer.Ordinal).ToArray();
            if (values.Select(x => x.CharacterId).Distinct().Count() != values.Length) throw new ArgumentException("Appearance states must be non-null and unique by Character ID.", nameof(characters));
            Characters = new ReadOnlyCollection<AppearanceState>(values);
        }
        public IReadOnlyList<AppearanceState> Characters { get; }
    }
}
