using System;

namespace Game.Domain.Characters
{
    public enum ParentRole
    {
        Mother = 1,
        Father = 2
    }

    public enum ParentageKind
    {
        Biological = 1,
        Legal = 2,
        Adoptive = 3
    }

    public readonly struct ParentLink
    {
        public StableEntityId ParentId { get; }
        public ParentRole Role { get; }
        public ParentageKind Kind { get; }
        public bool IsKnownToCharacter { get; }

        public ParentLink(StableEntityId parentId, ParentRole role, ParentageKind kind, bool isKnownToCharacter)
        {
            if (!parentId.IsValid) throw new ArgumentException("Parent ID must be valid.", nameof(parentId));
            if (!Enum.IsDefined(typeof(ParentRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (!Enum.IsDefined(typeof(ParentageKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            ParentId = parentId;
            Role = role;
            Kind = kind;
            IsKnownToCharacter = isKnownToCharacter;
        }
    }

    public readonly struct RelationshipState
    {
        public StableEntityId OtherCharacterId { get; }
        public RelationshipScore Affinity { get; }

        public RelationshipState(StableEntityId otherCharacterId, RelationshipScore affinity)
        {
            if (!otherCharacterId.IsValid) throw new ArgumentException("Related character ID must be valid.", nameof(otherCharacterId));
            OtherCharacterId = otherCharacterId;
            Affinity = affinity;
        }
    }
}
