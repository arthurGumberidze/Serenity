using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Buildings;

namespace Game.Domain.Domestic
{
    public enum SleepDenialReason
    {
        None = 0,
        CharacterUnavailable = 1,
        NoAssignedHome = 2,
        HomeUnavailable = 3,
        HomeIncomplete = 4,
        HomeNotClosed = 5,
        HomeUninhabitable = 6,
        AccessDenied = 7,
        NoAssignedBed = 8,
        BedOutsideHome = 9,
        BedOccupied = 10,
        BedAssignedToAnotherResident = 11,
        ProtectedActivity = 12,
        CriticalHunger = 13
    }

    public enum FamilyInteractionDenialReason
    {
        None = 0,
        CharacterUnavailable = 1,
        SameCharacter = 2,
        NotAdult = 3,
        ConsentMissing = 4,
        NotMutualSpouses = 5,
        RelationshipTooWeak = 6,
        HealthPreventsPregnancy = 7,
        PregnancyAlreadyActive = 8
    }

    public readonly struct IndoorZoneState : IEquatable<IndoorZoneState>
    {
        public IndoorZoneState(int minX, int minZ, int maxX, int maxZ, bool isClosed, bool isHabitable)
        {
            if (minX > maxX || minZ > maxZ) throw new ArgumentException("Indoor bounds are inverted.");
            MinX = minX; MinZ = minZ; MaxX = maxX; MaxZ = maxZ;
            IsClosed = isClosed; IsHabitable = isHabitable;
        }
        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }
        public bool IsClosed { get; }
        public bool IsHabitable { get; }
        public bool Contains(GridCoordinate coordinate) => coordinate.X >= MinX && coordinate.X <= MaxX &&
            coordinate.Z >= MinZ && coordinate.Z <= MaxZ;
        public bool Equals(IndoorZoneState other) => MinX == other.MinX && MinZ == other.MinZ &&
            MaxX == other.MaxX && MaxZ == other.MaxZ && IsClosed == other.IsClosed && IsHabitable == other.IsHabitable;
        public override bool Equals(object obj) => obj is IndoorZoneState other && Equals(other);
        public override int GetHashCode() => (((((MinX * 397) ^ MinZ) * 397 ^ MaxX) * 397 ^ MaxZ) * 397 ^
            IsClosed.GetHashCode()) * 397 ^ IsHabitable.GetHashCode();
    }

    public sealed class HomeState
    {
        public HomeState(StableEntityId buildingId, StableEntityId ownerId, IndoorZoneState indoorZone,
            IEnumerable<StableEntityId> residentIds = null)
        {
            if (!buildingId.IsValid || !ownerId.IsValid) throw new ArgumentException("Home and owner IDs are required.");
            var residents = (residentIds ?? Array.Empty<StableEntityId>()).Distinct().OrderBy(x => x).ToArray();
            if (residents.Any(x => !x.IsValid)) throw new ArgumentException("Resident IDs must be valid.");
            BuildingId = buildingId; OwnerId = ownerId; IndoorZone = indoorZone;
            ResidentIds = Array.AsReadOnly(residents);
        }
        public StableEntityId BuildingId { get; }
        public StableEntityId OwnerId { get; }
        public IndoorZoneState IndoorZone { get; }
        public IReadOnlyList<StableEntityId> ResidentIds { get; }
        public bool Allows(StableEntityId characterId) => OwnerId == characterId || ResidentIds.Contains(characterId);
    }

    public sealed class BedState
    {
        public BedState(StableEntityId id, StableEntityId homeBuildingId, GridCoordinate coordinate,
            WorldPosition sleepPosition, StableEntityId? assignedResidentId = null,
            StableEntityId? occupiedResidentId = null)
        {
            if (!id.IsValid || !homeBuildingId.IsValid) throw new ArgumentException("Bed and home IDs are required.");
            if (assignedResidentId.HasValue && !assignedResidentId.Value.IsValid)
                throw new ArgumentException("Assigned resident ID is invalid.");
            if (occupiedResidentId.HasValue && !occupiedResidentId.Value.IsValid)
                throw new ArgumentException("Occupied resident ID is invalid.");
            Id = id; HomeBuildingId = homeBuildingId; Coordinate = coordinate; SleepPosition = sleepPosition;
            AssignedResidentId = assignedResidentId; OccupiedResidentId = occupiedResidentId;
        }
        public StableEntityId Id { get; }
        public StableEntityId HomeBuildingId { get; }
        public GridCoordinate Coordinate { get; }
        public WorldPosition SleepPosition { get; }
        public StableEntityId? AssignedResidentId { get; }
        public StableEntityId? OccupiedResidentId { get; }
    }

    public sealed class RelationshipConsentState
    {
        public RelationshipConsentState(StableEntityId firstId, StableEntityId secondId,
            bool firstConsents, bool secondConsents)
        {
            if (!firstId.IsValid || !secondId.IsValid || firstId == secondId)
                throw new ArgumentException("Consent requires two different valid characters.");
            if (firstId.CompareTo(secondId) > 0) throw new ArgumentException("Consent pair must use canonical ID order.");
            FirstId = firstId; SecondId = secondId; FirstConsents = firstConsents; SecondConsents = secondConsents;
        }
        public StableEntityId FirstId { get; }
        public StableEntityId SecondId { get; }
        public bool FirstConsents { get; }
        public bool SecondConsents { get; }
        public bool IsMutual => FirstConsents && SecondConsents;
    }

    public sealed class FamilyInteractionState
    {
        public FamilyInteractionState(StableEntityId id, StableEntityId firstId, StableEntityId secondId,
            long calendarTick, StableEntityId pregnancyId)
        {
            if (!id.IsValid || !firstId.IsValid || !secondId.IsValid || !pregnancyId.IsValid || firstId == secondId)
                throw new ArgumentException("Family interaction IDs are invalid.");
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            Id = id; FirstId = firstId; SecondId = secondId; CalendarTick = calendarTick; PregnancyId = pregnancyId;
        }
        public StableEntityId Id { get; }
        public StableEntityId FirstId { get; }
        public StableEntityId SecondId { get; }
        public long CalendarTick { get; }
        public StableEntityId PregnancyId { get; }
    }

    public readonly struct SleepEligibility
    {
        public SleepEligibility(SleepDenialReason reason, BedState bed = null)
        { Reason = reason; Bed = bed; }
        public SleepDenialReason Reason { get; }
        public BedState Bed { get; }
        public bool Allowed => Reason == SleepDenialReason.None && Bed != null;
    }

    public readonly struct FamilyInteractionEligibility
    {
        public FamilyInteractionEligibility(FamilyInteractionDenialReason reason)
        { Reason = reason; }
        public FamilyInteractionDenialReason Reason { get; }
        public bool Allowed => Reason == FamilyInteractionDenialReason.None;
    }

    public sealed class DomesticWorldSaveState
    {
        public DomesticWorldSaveState(IEnumerable<HomeState> homes, IEnumerable<BedState> beds,
            IEnumerable<RelationshipConsentState> consents, IEnumerable<FamilyInteractionState> interactions)
        {
            Homes = Array.AsReadOnly((homes ?? Array.Empty<HomeState>()).OrderBy(x => x.BuildingId).ToArray());
            Beds = Array.AsReadOnly((beds ?? Array.Empty<BedState>()).OrderBy(x => x.Id).ToArray());
            Consents = Array.AsReadOnly((consents ?? Array.Empty<RelationshipConsentState>())
                .OrderBy(x => x.FirstId).ThenBy(x => x.SecondId).ToArray());
            Interactions = Array.AsReadOnly((interactions ?? Array.Empty<FamilyInteractionState>())
                .OrderBy(x => x.Id).ToArray());
            if (Homes.Any(x => x == null) || Homes.Select(x => x.BuildingId).Distinct().Count() != Homes.Count ||
                Beds.Any(x => x == null) || Beds.Select(x => x.Id).Distinct().Count() != Beds.Count ||
                Consents.Any(x => x == null) || Consents.Select(x => x.FirstId.ToString() + ":" + x.SecondId).Distinct().Count() != Consents.Count ||
                Interactions.Any(x => x == null) || Interactions.Select(x => x.Id).Distinct().Count() != Interactions.Count)
                throw new ArgumentException("Domestic save state contains null or duplicate records.");
        }
        public IReadOnlyList<HomeState> Homes { get; }
        public IReadOnlyList<BedState> Beds { get; }
        public IReadOnlyList<RelationshipConsentState> Consents { get; }
        public IReadOnlyList<FamilyInteractionState> Interactions { get; }
    }
}
