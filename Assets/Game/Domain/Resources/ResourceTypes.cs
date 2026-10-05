using System;

namespace Game.Domain.Resources
{
    public readonly struct ResourceId : IEquatable<ResourceId>, IComparable<ResourceId>
    {
        private readonly string value;

        public ResourceId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
                throw new ArgumentException("Resource ID must contain 1-64 characters.", nameof(value));
            foreach (var c in value)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-')
                    throw new ArgumentException("Resource ID must use lowercase ASCII, digits, '_' or '-'.", nameof(value));
            this.value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(value);
        public int CompareTo(ResourceId other) => StringComparer.Ordinal.Compare(value, other.value);
        public bool Equals(ResourceId other) => StringComparer.Ordinal.Equals(value, other.value);
        public override bool Equals(object obj) => obj is ResourceId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(ResourceId left, ResourceId right) => left.Equals(right);
        public static bool operator !=(ResourceId left, ResourceId right) => !left.Equals(right);
    }

    public readonly struct ResourceQuantity : IEquatable<ResourceQuantity>
    {
        public ResourceQuantity(long units)
        {
            if (units < 0) throw new ArgumentOutOfRangeException(nameof(units));
            Units = units;
        }

        public long Units { get; }
        public bool IsPositive => Units > 0;
        public bool Equals(ResourceQuantity other) => Units == other.Units;
        public override bool Equals(object obj) => obj is ResourceQuantity other && Equals(other);
        public override int GetHashCode() => Units.GetHashCode();
        public override string ToString() => Units.ToString();
        public static bool operator ==(ResourceQuantity left, ResourceQuantity right) => left.Equals(right);
        public static bool operator !=(ResourceQuantity left, ResourceQuantity right) => !left.Equals(right);
    }

    public readonly struct ResourceAmount
    {
        public ResourceAmount(ResourceId resourceId, ResourceQuantity quantity)
        {
            if (!resourceId.IsValid) throw new ArgumentException("Valid resource ID required.", nameof(resourceId));
            if (!quantity.IsPositive) throw new ArgumentOutOfRangeException(nameof(quantity));
            ResourceId = resourceId;
            Quantity = quantity;
        }

        public ResourceId ResourceId { get; }
        public ResourceQuantity Quantity { get; }
    }

    public enum ResourceCategory { Construction = 0, Food = 1, Other = 2 }

    public enum InventoryOwnerKind { WorldPile = 0, Character = 1, BuildingStorage = 2 }

    public readonly struct InventoryOwner : IEquatable<InventoryOwner>
    {
        public InventoryOwner(InventoryOwnerKind kind, StableEntityId id)
        {
            if (!Enum.IsDefined(typeof(InventoryOwnerKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!id.IsValid) throw new ArgumentException("Valid owner identity required.", nameof(id));
            Kind = kind;
            Id = id;
        }

        public InventoryOwnerKind Kind { get; }
        public StableEntityId Id { get; }
        public bool Equals(InventoryOwner other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is InventoryOwner other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Id.GetHashCode();
        public static bool operator ==(InventoryOwner left, InventoryOwner right) => left.Equals(right);
        public static bool operator !=(InventoryOwner left, InventoryOwner right) => !left.Equals(right);
    }
}
