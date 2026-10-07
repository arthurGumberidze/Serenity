using System;

namespace Game.Domain
{
    /// <summary>Domain identity; independent of every presentation or simulation tier.</summary>
    public readonly struct StableEntityId : IEquatable<StableEntityId>, IComparable<StableEntityId>
    {
        private readonly Guid value;
        private StableEntityId(Guid value) { this.value = value; }
        public bool IsValid => value != Guid.Empty;
        public static StableEntityId NewId() => new StableEntityId(Guid.NewGuid());
        /// <summary>
        /// Narrow creation path for simulation-derived identities. Callers must supply two
        /// deterministic 64-bit hashes; ordinary authored/player-created entities still use NewId().
        /// </summary>
        public static StableEntityId FromDeterministicHash(ulong first, ulong second)
        {
            if (first == 0UL && second == 0UL) second = 1UL;
            var bytes = new byte[16];
            WriteUInt64(bytes, 0, first);
            WriteUInt64(bytes, 8, second);
            return new StableEntityId(new Guid(bytes));
        }
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
        public int CompareTo(StableEntityId other) => value.CompareTo(other.value);
        public Guid ToGuid() => value;
        public override bool Equals(object obj) => obj is StableEntityId other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => value.ToString("N");
        public static bool operator ==(StableEntityId left, StableEntityId right) => left.Equals(right);
        public static bool operator !=(StableEntityId left, StableEntityId right) => !left.Equals(right);

        private static void WriteUInt64(byte[] bytes, int offset, ulong value)
        {
            for (var i = 0; i < 8; i++) bytes[offset + i] = (byte)(value >> (i * 8));
        }
    }
}
