using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.Characters
{
    /// <summary>Canonical persistent person. It owns no GameObject, ECS Entity, asset or storage handle.</summary>
    public sealed class Character
    {
        public const int MinimumTraitCount = 4;
        public const int MaximumTraitCount = 5;
        public const long BiologicalTicksPerYear = TimeSpan.TicksPerDay * 365L;

        private readonly Dictionary<SkillId, SkillState> skills = new Dictionary<SkillId, SkillState>();
        private readonly Dictionary<StableEntityId, RelationshipState> relationships =
            new Dictionary<StableEntityId, RelationshipState>();
        private ParentLink[] parents;
        private TraitId[] traits;

        public StableEntityId Id { get; }
        public CharacterName Name { get; private set; }
        public CharacterSex Sex { get; }
        public long BiologicalBirthTick { get; }
        public CharacterLifeState LifeState { get; private set; }
        public long? BiologicalDeathTick { get; private set; }
        public StableEntityId? SpouseId { get; private set; }
        public StableEntityId? FamilyId { get; private set; }
        public StableEntityId? DynastyId { get; private set; }
        public ProfessionId? ProfessionId { get; private set; }
        public CharacterAttributes Attributes { get; private set; }
        public BodyHealth Health { get; private set; }
        public Wealth Wealth { get; private set; }
        public Influence Influence { get; private set; }
        public IReadOnlyList<ParentLink> Parents => Array.AsReadOnly(parents);
        public IReadOnlyList<TraitId> Traits => Array.AsReadOnly(traits);

        private Character(CharacterState state)
        {
            Validate(state);
            Id = state.Id;
            Name = state.Name;
            Sex = state.Sex;
            BiologicalBirthTick = state.BiologicalBirthTick;
            LifeState = state.LifeState;
            BiologicalDeathTick = state.BiologicalDeathTick;
            parents = state.Parents.ToArray();
            SpouseId = state.SpouseId;
            FamilyId = state.FamilyId;
            DynastyId = state.DynastyId;
            ProfessionId = state.ProfessionId;
            traits = state.Traits.ToArray();
            foreach (var skill in state.Skills) skills.Add(skill.Id, skill);
            Attributes = state.Attributes;
            Health = state.Health;
            Wealth = state.Wealth;
            Influence = state.Influence;
            foreach (var relationship in state.Relationships)
                relationships.Add(relationship.OtherCharacterId, relationship);
        }

        public static Character CreateNew(CharacterCreationData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return new Character(data.WithIdentity(StableEntityId.NewId()));
        }

        public static Character Restore(CharacterState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return new Character(state);
        }

        public int AgeInCompletedYears(long currentBiologicalTick)
        {
            if (currentBiologicalTick < BiologicalBirthTick)
                throw new ArgumentOutOfRangeException(nameof(currentBiologicalTick), "Current biological tick cannot precede birth.");
            if (BiologicalDeathTick.HasValue)
            {
                if (currentBiologicalTick < BiologicalDeathTick.Value)
                    throw new ArgumentOutOfRangeException(nameof(currentBiologicalTick), "Current biological tick cannot precede recorded death.");
                currentBiologicalTick = BiologicalDeathTick.Value;
            }
            return checked((int)((currentBiologicalTick - BiologicalBirthTick) / BiologicalTicksPerYear));
        }

        public bool HasParent(StableEntityId parentId) => parents.Any(parent => parent.ParentId == parentId);

        public bool TryGetSkill(SkillId id, out SkillState skill) => skills.TryGetValue(id, out skill);

        public bool TryGetRelationship(StableEntityId otherCharacterId, out RelationshipState relationship) =>
            relationships.TryGetValue(otherCharacterId, out relationship);

        public void Rename(CharacterName name)
        {
            if (!name.IsValid) throw new ArgumentException("Character name must be valid.", nameof(name));
            Name = name;
        }

        public void AssignProfession(ProfessionId? professionId)
        {
            if (professionId.HasValue && !professionId.Value.IsValid)
                throw new ArgumentException("Profession ID must be valid.", nameof(professionId));
            ProfessionId = professionId;
        }

        public void ReplaceParents(IEnumerable<ParentLink> newParents)
        {
            var replacement = newParents == null ? Array.Empty<ParentLink>() : newParents.ToArray();
            ValidateParents(Id, replacement);
            parents = replacement;
        }

        public void ReplaceTraits(IEnumerable<TraitId> newTraits)
        {
            var replacement = newTraits == null ? Array.Empty<TraitId>() : newTraits.ToArray();
            ValidateTraits(replacement);
            traits = replacement;
        }

        public void SetSkill(SkillState skill)
        {
            if (!skill.Id.IsValid) throw new ArgumentException("Skill ID must be valid.", nameof(skill));
            skills[skill.Id] = skill;
        }

        public void SetRelationship(RelationshipState relationship)
        {
            if (!relationship.OtherCharacterId.IsValid || relationship.OtherCharacterId == Id)
                throw new ArgumentException("A relationship target must be valid and cannot reference the character itself.", nameof(relationship));
            relationships[relationship.OtherCharacterId] = relationship;
        }

        public void SetFamily(StableEntityId? familyId, StableEntityId? dynastyId)
        {
            ValidateOptionalId(familyId, nameof(familyId));
            ValidateOptionalId(dynastyId, nameof(dynastyId));
            FamilyId = familyId;
            DynastyId = dynastyId;
        }

        public void SetAttributes(CharacterAttributes attributes) => Attributes = attributes;
        public void SetHealth(BodyHealth health) => Health = health;
        public void SetWealth(Wealth wealth) => Wealth = wealth;
        public void SetInfluence(Influence influence) => Influence = influence;

        public void MarkDead(long biologicalDeathTick)
        {
            if (LifeState == CharacterLifeState.Dead) throw new InvalidOperationException("Character is already dead.");
            if (biologicalDeathTick < BiologicalBirthTick)
                throw new ArgumentOutOfRangeException(nameof(biologicalDeathTick));
            BiologicalDeathTick = biologicalDeathTick;
            LifeState = CharacterLifeState.Dead;
        }

        public CharacterState CaptureState()
        {
            return new CharacterState(Id, Name, Sex, BiologicalBirthTick, LifeState, BiologicalDeathTick,
                parents.OrderBy(parent => parent.Role).ThenBy(parent => parent.Kind)
                    .ThenBy(parent => parent.ParentId.ToString(), StringComparer.Ordinal),
                SpouseId, FamilyId, DynastyId, ProfessionId,
                traits.OrderBy(trait => trait.Value, StringComparer.Ordinal),
                skills.Values.OrderBy(skill => skill.Id.Value, StringComparer.Ordinal), Attributes, Health,
                Wealth, Influence,
                relationships.Values.OrderBy(relation => relation.OtherCharacterId.ToString(), StringComparer.Ordinal));
        }

        internal void SetSpouseFromRegistry(StableEntityId? spouseId)
        {
            ValidateOptionalId(spouseId, nameof(spouseId));
            if (spouseId == Id) throw new ArgumentException("A character cannot be its own spouse.", nameof(spouseId));
            SpouseId = spouseId;
        }

        private static void Validate(CharacterState state)
        {
            if (!state.Id.IsValid) throw new ArgumentException("Character ID must be valid.", nameof(state));
            if (!state.Name.IsValid) throw new ArgumentException("Character name must be valid.", nameof(state));
            if (!Enum.IsDefined(typeof(CharacterSex), state.Sex)) throw new ArgumentOutOfRangeException(nameof(state));
            if (state.BiologicalBirthTick < 0) throw new ArgumentOutOfRangeException(nameof(state));
            if (!Enum.IsDefined(typeof(CharacterLifeState), state.LifeState)) throw new ArgumentOutOfRangeException(nameof(state));
            if (state.LifeState == CharacterLifeState.Alive && state.BiologicalDeathTick.HasValue)
                throw new ArgumentException("A living character cannot have a death tick.", nameof(state));
            if (state.LifeState == CharacterLifeState.Dead &&
                (!state.BiologicalDeathTick.HasValue || state.BiologicalDeathTick.Value < state.BiologicalBirthTick))
                throw new ArgumentException("A dead character requires a death tick at or after birth.", nameof(state));
            ValidateParents(state.Id, state.Parents);
            ValidateOptionalId(state.SpouseId, nameof(state.SpouseId));
            if (state.SpouseId == state.Id) throw new ArgumentException("A character cannot be its own spouse.", nameof(state));
            ValidateOptionalId(state.FamilyId, nameof(state.FamilyId));
            ValidateOptionalId(state.DynastyId, nameof(state.DynastyId));
            if (state.ProfessionId.HasValue && !state.ProfessionId.Value.IsValid)
                throw new ArgumentException("Profession ID must be valid.", nameof(state));
            ValidateTraits(state.Traits);
            var skillIds = new HashSet<SkillId>();
            foreach (var skill in state.Skills)
                if (!skill.Id.IsValid || !skillIds.Add(skill.Id))
                    throw new ArgumentException("Skill IDs must be valid and unique.", nameof(state));
            var relatedIds = new HashSet<StableEntityId>();
            foreach (var relationship in state.Relationships)
                if (!relationship.OtherCharacterId.IsValid || relationship.OtherCharacterId == state.Id ||
                    !relatedIds.Add(relationship.OtherCharacterId))
                    throw new ArgumentException("Relationship IDs must be valid, unique and not self-referential.", nameof(state));
        }

        private static void ValidateParents(StableEntityId characterId, IEnumerable<ParentLink> parentLinks)
        {
            var roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parent in parentLinks)
            {
                if (!parent.ParentId.IsValid || parent.ParentId == characterId)
                    throw new ArgumentException("Parent IDs must be valid and cannot reference the character itself.", nameof(parentLinks));
                var role = ((int)parent.Role).ToString() + ":" + ((int)parent.Kind).ToString();
                if (!roles.Add(role))
                    throw new ArgumentException("Only one parent link is allowed per role and parentage kind.", nameof(parentLinks));
            }
        }

        private static void ValidateTraits(IEnumerable<TraitId> traitValues)
        {
            var values = traitValues.ToArray();
            if (values.Length < MinimumTraitCount || values.Length > MaximumTraitCount)
                throw new ArgumentException("A Full NPC must have four or five traits.", nameof(traitValues));
            var unique = new HashSet<TraitId>();
            foreach (var trait in values)
                if (!trait.IsValid || !unique.Add(trait))
                    throw new ArgumentException("Trait IDs must be valid and unique.", nameof(traitValues));
        }

        private static void ValidateOptionalId(StableEntityId? id, string parameterName)
        {
            if (id.HasValue && !id.Value.IsValid)
                throw new ArgumentException("Optional stable IDs must be valid when present.", parameterName);
        }
    }
}
