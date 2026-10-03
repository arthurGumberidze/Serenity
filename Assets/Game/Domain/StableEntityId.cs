using System;

namespace Game.Domain
{
    /// <summary>Domain identity; independent of every presentation or simulation tier.</summary>
    public readonly struct StableEntityId : IEquatable<StableEntityId>
    {
        private readonly Guid value;
        private StableEntityId(Guid value) { this.value = value; }
        public bool IsValid => value != Guid.Empty;
        public static StableEntityId NewId() => new StableEntityId(Guid.NewGuid());
        public static StableEntityId Parse(string text)
        {
            if (!TryParse(text, out var id)) throw new FormatException("Expected a non-empty GUID in N format.");
            return id;
        }
        public static bool TryParse(string text, out StableEntityId id)
        {
            id = default;
            if (text == null || text.Length != 32 || !Guid.TryParseExact(text, "N", out var parsed) || parsed == Guid.Empty) return false;
            id = new StableEntityId(parsed);
            return true;
        }
        public bool Equals(StableEntityId other) => value.Equals(other.value);
        public override bool Equals(object obj) => obj is StableEntityId other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value.ToString("N");
        public static bool operator ==(StableEntityId left, StableEntityId right) => left.Equals(right);
        public static bool operator !=(StableEntityId left, StableEntityId right) => !left.Equals(right);
    }
}
