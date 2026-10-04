using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Game.Domain.Characters
{
    /// <summary>Detached, data-only input for creating a new character.</summary>
    public sealed class CharacterCreationData
    {
        public CharacterName Name { get; }
        public CharacterSex Sex { get; }
        public long BiologicalBirthTick { get; }
        public IReadOnlyList<ParentLink> Parents { get; }
        public StableEntityId? SpouseId { get; }
        public StableEntityId? FamilyId { get; }
        public StableEntityId? DynastyId { get; }
        public ProfessionId? ProfessionId { get; }
        public IReadOnlyList<TraitId> Traits { get; }
        public IReadOnlyList<SkillState> Skills { get; }
        public CharacterAttributes Attributes { get; }
        public BodyHealth Health { get; }
        public Wealth Wealth { get; }
        public Influence Influence { get; }
        public IReadOnlyList<RelationshipState> Relationships { get; }

        public CharacterCreationData(CharacterName name, CharacterSex sex, long biologicalBirthTick,
            IEnumerable<ParentLink> parents, StableEntityId? spouseId, StableEntityId? familyId,
            StableEntityId? dynastyId, ProfessionId? professionId, IEnumerable<TraitId> traits,
            IEnumerable<SkillState> skills, CharacterAttributes attributes, BodyHealth health,
            Wealth wealth, Influence influence, IEnumerable<RelationshipState> relationships)
        {
            Name = name;
            Sex = sex;
            BiologicalBirthTick = biologicalBirthTick;
            Parents = Snapshot(parents);
            SpouseId = spouseId;
            FamilyId = familyId;
            DynastyId = dynastyId;
            ProfessionId = professionId;
            Traits = Snapshot(traits);
            Skills = Snapshot(skills);
            Attributes = attributes;
            Health = health;
            Wealth = wealth;
            Influence = influence;
            Relationships = Snapshot(relationships);
        }

        internal CharacterState WithIdentity(StableEntityId id) => new CharacterState(id, Name, Sex,
            BiologicalBirthTick, CharacterLifeState.Alive, null, Parents, SpouseId, FamilyId, DynastyId,
            ProfessionId, Traits, Skills, Attributes, Health, Wealth, Influence, Relationships);

        private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T> values)
        {
            if (values == null) return new ReadOnlyCollection<T>(Array.Empty<T>());
            return new ReadOnlyCollection<T>(new List<T>(values).ToArray());
        }
    }

    /// <summary>Detached character snapshot suitable for an explicit future save schema or tier projection.</summary>
    public sealed class CharacterState
    {
        public StableEntityId Id { get; }
        public CharacterName Name { get; }
        public CharacterSex Sex { get; }
        public long BiologicalBirthTick { get; }
        public CharacterLifeState LifeState { get; }
        public long? BiologicalDeathTick { get; }
        public IReadOnlyList<ParentLink> Parents { get; }
        public StableEntityId? SpouseId { get; }
        public StableEntityId? FamilyId { get; }
        public StableEntityId? DynastyId { get; }
        public ProfessionId? ProfessionId { get; }
        public IReadOnlyList<TraitId> Traits { get; }
        public IReadOnlyList<SkillState> Skills { get; }
        public CharacterAttributes Attributes { get; }
        public BodyHealth Health { get; }
        public Wealth Wealth { get; }
        public Influence Influence { get; }
        public IReadOnlyList<RelationshipState> Relationships { get; }

        public CharacterState(StableEntityId id, CharacterName name, CharacterSex sex, long biologicalBirthTick,
            CharacterLifeState lifeState, long? biologicalDeathTick, IEnumerable<ParentLink> parents,
            StableEntityId? spouseId, StableEntityId? familyId, StableEntityId? dynastyId,
            ProfessionId? professionId, IEnumerable<TraitId> traits, IEnumerable<SkillState> skills,
            CharacterAttributes attributes, BodyHealth health, Wealth wealth, Influence influence,
            IEnumerable<RelationshipState> relationships)
        {
            Id = id;
            Name = name;
            Sex = sex;
            BiologicalBirthTick = biologicalBirthTick;
            LifeState = lifeState;
            BiologicalDeathTick = biologicalDeathTick;
            Parents = Snapshot(parents);
            SpouseId = spouseId;
            FamilyId = familyId;
            DynastyId = dynastyId;
            ProfessionId = professionId;
            Traits = Snapshot(traits);
            Skills = Snapshot(skills);
            Attributes = attributes;
            Health = health;
            Wealth = wealth;
            Influence = influence;
            Relationships = Snapshot(relationships);
        }

        private static IReadOnlyList<T> Snapshot<T>(IEnumerable<T> values)
        {
            if (values == null) return new ReadOnlyCollection<T>(Array.Empty<T>());
            return new ReadOnlyCollection<T>(new List<T>(values).ToArray());
        }
    }
}
