using System;
using Game.Domain;
using Game.Domain.Characters;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.ECS.Tier2
{
    /// <summary>Unmanaged 16-byte projection of the canonical domain identity.</summary>
    public struct Tier2StableIdentity : IComponentData, IEquatable<Tier2StableIdentity>
    {
        public ulong Low;
        public ulong High;

        public Tier2StableIdentity(ulong low, ulong high)
        {
            Low = low;
            High = high;
        }

        public static Tier2StableIdentity FromDomain(StableEntityId id)
        {
            if (!id.IsValid) throw new ArgumentException("Stable identity must be valid.", nameof(id));
            var bytes = Guid.ParseExact(id.ToString(), "N").ToByteArray();
            return new Tier2StableIdentity(BitConverter.ToUInt64(bytes, 0), BitConverter.ToUInt64(bytes, 8));
        }

        public StableEntityId ToDomain()
        {
            var bytes = new byte[16];
            Array.Copy(BitConverter.GetBytes(Low), 0, bytes, 0, 8);
            Array.Copy(BitConverter.GetBytes(High), 0, bytes, 8, 8);
            return StableEntityId.Parse(new Guid(bytes).ToString("N"));
        }

        public bool Equals(Tier2StableIdentity other) => Low == other.Low && High == other.High;
        public override bool Equals(object obj) => obj is Tier2StableIdentity other && Equals(other);
        public override int GetHashCode() => unchecked(((int)Low * 397) ^ (int)High);
        public static bool operator ==(Tier2StableIdentity left, Tier2StableIdentity right) => left.Equals(right);
        public static bool operator !=(Tier2StableIdentity left, Tier2StableIdentity right) => !left.Equals(right);
    }

    /// <summary>Only the lifecycle fields needed by the Tier 2 projection.</summary>
    public struct Tier2BiologicalState : IComponentData
    {
        public long BirthTick;
        public long DeathTick;
        public CharacterSex Sex;
        public CharacterLifeState LifeState;
    }

    public struct Tier2Position : IComponentData
    {
        public float3 Value;
    }

    public struct Tier2Movement : IComponentData
    {
        public float3 UnitsPerGameDay;
        public byte IsMoving;
    }

    /// <summary>Evidence and future extraction boundary for explicit deterministic steps.</summary>
    public struct Tier2SimulationProgress : IComponentData
    {
        public long ProcessedCalendarTicks;
        public long ProcessedBiologicalTicks;
        public uint StepCount;
    }

    /// <summary>One world-level command written by the explicit GameTime bridge.</summary>
    public struct Tier2SimulationStep : IComponentData
    {
        public long CalendarDeltaTicks;
        public long BiologicalDeltaTicks;
        public uint Sequence;
    }
}
