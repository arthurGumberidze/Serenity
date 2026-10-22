using System;

namespace Game.Domain.Characters
{
    public enum CharacterSex
    {
        Male = 1,
        Female = 2
    }

    public enum CharacterLifeState
    {
        Alive = 1,
        Dead = 2
    }

    public enum BodyPart
    {
        Head = 1,
        Torso = 2,
        LeftArm = 3,
        RightArm = 4,
        LeftLeg = 5,
        RightLeg = 6
    }

    public readonly struct CharacterName : IEquatable<CharacterName>
    {
        public const int MaxPartLength = 64;

        public string GivenName { get; }
        public string FamilyName { get; }
        public bool IsValid => IsCanonicalPart(GivenName, false) && IsCanonicalPart(FamilyName, true);
        public string DisplayName => string.IsNullOrEmpty(FamilyName) ? GivenName : GivenName + " " + FamilyName;

        public CharacterName(string givenName, string familyName = "")
        {
            ValidatePart(givenName, nameof(givenName), false);
            familyName = familyName ?? string.Empty;
            ValidatePart(familyName, nameof(familyName), true);
            GivenName = givenName;
            FamilyName = familyName;
        }

        public bool Equals(CharacterName other) =>
            string.Equals(GivenName, other.GivenName, StringComparison.Ordinal) &&
            string.Equals(FamilyName, other.FamilyName, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CharacterName other && Equals(other);
        public override int GetHashCode() => ((GivenName != null ? GivenName.GetHashCode() : 0) * 397) ^
                                             (FamilyName != null ? FamilyName.GetHashCode() : 0);
        public override string ToString() => DisplayName;
        public static bool operator ==(CharacterName left, CharacterName right) => left.Equals(right);
        public static bool operator !=(CharacterName left, CharacterName right) => !left.Equals(right);

        private static void ValidatePart(string value, string parameterName, bool allowEmpty)
        {
            if (!IsCanonicalPart(value, allowEmpty))
                throw new ArgumentException("Name parts must be canonical, printable text no longer than 64 characters.", parameterName);
        }

        private static bool IsCanonicalPart(string value, bool allowEmpty)
        {
            if (value == null || value.Length > MaxPartLength) return false;
            if (value.Length == 0) return allowEmpty;
            if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal)) return false;
            for (var i = 0; i < value.Length; i++)
                if (char.IsControl(value[i])) return false;
            return true;
        }
    }

    public readonly struct TraitId : IEquatable<TraitId>
    {
        private readonly string value;
        public string Value => value ?? string.Empty;
        public bool IsValid => DefinitionKey.IsValid(value);
        public TraitId(string value) { DefinitionKey.Validate(value, nameof(value)); this.value = value; }
        public bool Equals(TraitId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is TraitId other && Equals(other);
        public override int GetHashCode() => value != null ? value.GetHashCode() : 0;
        public override string ToString() => Value;
    }

    public readonly struct SkillId : IEquatable<SkillId>
    {
        private readonly string value;
        public string Value => value ?? string.Empty;
        public bool IsValid => DefinitionKey.IsValid(value);
        public SkillId(string value) { DefinitionKey.Validate(value, nameof(value)); this.value = value; }
        public bool Equals(SkillId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SkillId other && Equals(other);
        public override int GetHashCode() => value != null ? value.GetHashCode() : 0;
        public override string ToString() => Value;
    }

    public readonly struct ProfessionId : IEquatable<ProfessionId>
    {
        private readonly string value;
        public string Value => value ?? string.Empty;
        public bool IsValid => DefinitionKey.IsValid(value);
        public ProfessionId(string value) { DefinitionKey.Validate(value, nameof(value)); this.value = value; }
        public bool Equals(ProfessionId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ProfessionId other && Equals(other);
        public override int GetHashCode() => value != null ? value.GetHashCode() : 0;
        public override string ToString() => Value;
    }

    public readonly struct SkillLevel : IEquatable<SkillLevel>
    {
        public const int Minimum = 0;
        public const int Maximum = 100;
        public int Value { get; }
        public SkillLevel(int value)
        {
            if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(SkillLevel other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SkillLevel other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => Value.ToString();
    }

    public readonly struct CharacterAttributeValue : IEquatable<CharacterAttributeValue>
    {
        public const int Minimum = 0;
        public const int Maximum = 100;
        public int Value { get; }
        public CharacterAttributeValue(int value)
        {
            if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(CharacterAttributeValue other) => Value == other.Value;
        public override bool Equals(object obj) => obj is CharacterAttributeValue other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public readonly struct HealthValue : IEquatable<HealthValue>
    {
        public const int Minimum = 0;
        public const int Maximum = 100;
        public int Value { get; }
        public HealthValue(int value)
        {
            if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(HealthValue other) => Value == other.Value;
        public override bool Equals(object obj) => obj is HealthValue other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public readonly struct Wealth : IEquatable<Wealth>
    {
        public const int Minimum = 0;
        public const int Maximum = 1000;
        public int Value { get; }
        public Wealth(int value)
        {
            if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(Wealth other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Wealth other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public readonly struct Influence : IEquatable<Influence>
    {
        public const int Minimum = 0;
        public const int Maximum = 1000;
        public int Value { get; }
        public Influence(int value)
        {
            if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(Influence other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Influence other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public readonly struct RelationshipScore : IEquatable<RelationshipScore>
    {
        public const int Minimum = -100;
        public const int Maximum = 100;
        public int Value { get; }
        public RelationshipScore(int value)
        {
            if (value < Minimum || value > Maximum) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(RelationshipScore other) => Value == other.Value;
        public override bool Equals(object obj) => obj is RelationshipScore other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public readonly struct CharacterAttributes
    {
        public CharacterAttributeValue Intelligence { get; }
        public CharacterAttributeValue PhysicalStrength { get; }
        public CharacterAttributes(CharacterAttributeValue intelligence, CharacterAttributeValue physicalStrength)
        {
            Intelligence = intelligence;
            PhysicalStrength = physicalStrength;
        }
    }

    public readonly struct BodyHealth
    {
        public HealthValue Head { get; }
        public HealthValue Torso { get; }
        public HealthValue LeftArm { get; }
        public HealthValue RightArm { get; }
        public HealthValue LeftLeg { get; }
        public HealthValue RightLeg { get; }

        public BodyHealth(HealthValue head, HealthValue torso, HealthValue leftArm, HealthValue rightArm,
            HealthValue leftLeg, HealthValue rightLeg)
        {
            Head = head;
            Torso = torso;
            LeftArm = leftArm;
            RightArm = rightArm;
            LeftLeg = leftLeg;
            RightLeg = rightLeg;
        }

        public static BodyHealth Healthy => new BodyHealth(new HealthValue(100), new HealthValue(100),
            new HealthValue(100), new HealthValue(100), new HealthValue(100), new HealthValue(100));

        public HealthValue Get(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head: return Head;
                case BodyPart.Torso: return Torso;
                case BodyPart.LeftArm: return LeftArm;
                case BodyPart.RightArm: return RightArm;
                case BodyPart.LeftLeg: return LeftLeg;
                case BodyPart.RightLeg: return RightLeg;
                default: throw new ArgumentOutOfRangeException(nameof(part));
            }
        }
    }

    public readonly struct SkillState
    {
        public SkillId Id { get; }
        public SkillLevel Level { get; }
        public SkillState(SkillId id, SkillLevel level)
        {
            if (!id.IsValid) throw new ArgumentException("Skill ID must be valid.", nameof(id));
            Id = id;
            Level = level;
        }
    }

    public static class CharacterSkillRules
    {
        private const string FavoritePrefix = "favorite.";
        public static readonly SkillId Construction = new SkillId("construction");
        public static readonly SkillId Hunting = new SkillId("hunting");
        public static readonly SkillId Combat = new SkillId("combat");
        public static readonly SkillId Gathering = new SkillId("gathering");
        public static readonly SkillId Cooking = new SkillId("cooking");
        public static readonly SkillId Crafting = new SkillId("crafting");
        public static readonly SkillId Farming = new SkillId("farming");
        public static readonly SkillId Medicine = new SkillId("medicine");
        public static readonly SkillId Research = new SkillId("research");

        public static readonly SkillId[] StartingFavorites =
        {
            Construction, Hunting, Combat, Gathering, Cooking, Crafting, Farming, Medicine, Research
        };

        public static int Level(Character character, SkillId skillId) =>
            character != null && character.TryGetSkill(skillId, out var skill) ? skill.Level.Value : 0;

        public static SkillId Favorite(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            foreach (var skill in character.CaptureState().Skills)
            {
                if (!IsFavoriteMarker(skill.Id)) continue;
                var marked = new SkillId(skill.Id.Value.Substring(FavoritePrefix.Length));
                if (Array.IndexOf(StartingFavorites, marked) >= 0) return marked;
            }
            var favorite = StartingFavorites[0];
            var best = -1;
            foreach (var skill in StartingFavorites)
            {
                var level = Level(character, skill);
                if (level <= best) continue;
                best = level;
                favorite = skill;
            }
            return favorite;
        }

        public static SkillState FavoriteMarker(SkillId favorite)
        {
            if (Array.IndexOf(StartingFavorites, favorite) < 0)
                throw new ArgumentException("Favorite activity must come from the starting list.", nameof(favorite));
            return new SkillState(new SkillId(FavoritePrefix + favorite.Value), new SkillLevel(0));
        }

        public static bool IsFavoriteMarker(SkillId skillId) =>
            skillId.IsValid && skillId.Value.StartsWith(FavoritePrefix, StringComparison.Ordinal);

        public static int WorkRatePerThousand(Character character, SkillId skillId) =>
            1000 + Level(character, skillId) * 10;

        public static long CarryCapacity(Character character, long baseCapacity)
        {
            if (baseCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(baseCapacity));
            return Math.Max(1L, checked(baseCapacity * (1000L + Level(character, Gathering) * 10L) / 1000L));
        }

        public static bool GainWorkExperience(Character character, SkillId skillId, int levels = 1)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (levels <= 0) throw new ArgumentOutOfRangeException(nameof(levels));
            var old = Level(character, skillId);
            var next = Math.Min(SkillLevel.Maximum, checked(old + levels));
            if (next == old) return false;
            character.SetSkill(new SkillState(skillId, new SkillLevel(next)));
            return true;
        }
    }

    internal static class DefinitionKey
    {
        public const int MaxLength = 64;

        public static void Validate(string value, string parameterName)
        {
            if (!IsValid(value))
                throw new ArgumentException("Definition keys must use 1-64 lowercase ASCII letters, digits, '.', '_' or '-'.", parameterName);
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > MaxLength) return false;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if ((c < 'a' || c > 'z') && (c < '0' || c > '9') && c != '.' && c != '_' && c != '-')
                    return false;
            }
            return true;
        }
    }
}
