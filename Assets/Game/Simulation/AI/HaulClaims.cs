using System;
using System.Collections.Generic;
using Game.Domain;
using Game.Domain.Resources;

namespace Game.Simulation.AI
{
    public readonly struct HaulTaskId : IEquatable<HaulTaskId>
    {
        public HaulTaskId(long value)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public long Value { get; }
        public bool Equals(HaulTaskId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is HaulTaskId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
    }

    public enum HaulClaimPhase { ReservedAtSource = 0, Carrying = 1 }

    public sealed class HaulClaim
    {
        internal HaulClaim(HaulTaskId id, StableEntityId claimant, InventoryOwner source, InventoryOwner destination,
            ResourceId resourceId, ResourceQuantity quantity)
        {
            Id = id;
            Claimant = claimant;
            Source = source;
            Destination = destination;
            ResourceId = resourceId;
            Quantity = quantity;
        }
        public HaulTaskId Id { get; }
        public StableEntityId Claimant { get; }
        public InventoryOwner Source { get; }
        public InventoryOwner Destination { get; internal set; }
        public ResourceId ResourceId { get; }
        public ResourceQuantity Quantity { get; }
        public HaulClaimPhase Phase { get; internal set; }
    }

    public sealed class HaulClaimRegistry
    {
        private readonly Dictionary<long, HaulClaim> claims = new Dictionary<long, HaulClaim>();
        private long nextId = 1;

        public int Count => claims.Count;

        public HaulClaim[] CaptureActive()
        {
            var result = new List<HaulClaim>(claims.Values);
            result.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return result.ToArray();
        }

        public HaulClaim[] CaptureForSource(InventoryOwner source)
        {
            var result = new List<HaulClaim>();
            foreach (var claim in claims.Values)
                if (claim.Source == source) result.Add(claim);
            result.Sort((left, right) => left.Id.Value.CompareTo(right.Id.Value));
            return result.ToArray();
        }

        public bool TryGetForClaimant(StableEntityId claimant, out HaulClaim claim)
        {
            claim = null;
            foreach (var candidate in claims.Values)
            {
                if (candidate.Claimant != claimant || claim != null && candidate.Id.Value >= claim.Id.Value) continue;
                claim = candidate;
            }
            return claim != null;
        }

        public long ReservedAtSource(InventoryOwner source, ResourceId resourceId)
        {
            long sum = 0;
            foreach (var claim in claims.Values)
                if (claim.Phase == HaulClaimPhase.ReservedAtSource && claim.Source == source && claim.ResourceId == resourceId)
                    sum = checked(sum + claim.Quantity.Units);
            return sum;
        }

        public long ReservedAtDestination(InventoryOwner destination)
        {
            long sum = 0;
            foreach (var claim in claims.Values)
                if (claim.Destination == destination) sum = checked(sum + claim.Quantity.Units);
            return sum;
        }

        public bool TryClaim(StableEntityId claimant, InventoryOwner source, InventoryOwner destination,
            ResourceId resourceId, ResourceQuantity quantity, long sourceAvailable, long destinationAvailable,
            out HaulClaim claim)
        {
            claim = null;
            if (!claimant.IsValid || !quantity.IsPositive) return false;
            if (quantity.Units > sourceAvailable - ReservedAtSource(source, resourceId)) return false;
            if (quantity.Units > destinationAvailable - ReservedAtDestination(destination)) return false;
            var id = new HaulTaskId(nextId++);
            claim = new HaulClaim(id, claimant, source, destination, resourceId, quantity);
            claims.Add(id.Value, claim);
            return true;
        }

        public void MarkPickedUp(HaulClaim claim)
        {
            RequireActive(claim);
            claim.Phase = HaulClaimPhase.Carrying;
        }

        public bool TryChangeDestination(HaulClaim claim, InventoryOwner destination, long destinationAvailable)
        {
            RequireActive(claim);
            if (claim.Phase != HaulClaimPhase.Carrying) return false;
            var reservedElsewhere = ReservedAtDestination(destination);
            if (claim.Destination == destination) reservedElsewhere -= claim.Quantity.Units;
            if (claim.Quantity.Units > destinationAvailable - reservedElsewhere) return false;
            claim.Destination = destination;
            return true;
        }

        public bool Release(HaulClaim claim)
        {
            if (claim == null) return false;
            return claims.TryGetValue(claim.Id.Value, out var active) && ReferenceEquals(active, claim) && claims.Remove(claim.Id.Value);
        }

        public int ReleaseForAgent(StableEntityId claimant)
        {
            var ids = new List<long>();
            foreach (var pair in claims)
                if (pair.Value.Claimant == claimant) ids.Add(pair.Key);
            foreach (var id in ids) claims.Remove(id);
            return ids.Count;
        }

        public bool IsActive(HaulClaim claim) => claim != null && claims.TryGetValue(claim.Id.Value, out var active) && ReferenceEquals(active, claim);

        private void RequireActive(HaulClaim claim)
        {
            if (!IsActive(claim)) throw new InvalidOperationException("Haul claim is not active.");
        }
    }
}
