using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Game.Domain.Combat;
using Game.Domain.Resources;

namespace Game.Domain.Buildings
{
    public enum BuildingStructuralStatus { Intact = 1, Damaged = 2, Critical = 3, Destroyed = 4 }
    public enum BuildingDamageType { Generic = 1, Combat = 2, Fire = 3 }
    public enum BuildingFireStatus { None = 0, Burning = 1, Extinguished = 2, BurnedOut = 3 }
    public enum BuildingEventKind { Damaged = 1, Ignited = 2, Extinguished = 3, Destroyed = 4, Captured = 5 }
    public enum BuildingDamageFailure { None = 0, MissingBuilding = 1, InvalidAmount = 2, AlreadyDestroyed = 3, DuplicateEvent = 4 }
    public enum BuildingFireFailure { None = 0, MissingBuilding = 1, InvalidIntensity = 2, AlreadyDestroyed = 3, AlreadyBurning = 4, NotBurning = 5 }
    public enum BuildingCaptureFailure
    {
        None = 0, MissingBuilding = 1, InvalidOwner = 2, SameOwner = 3, Destroyed = 4,
        NotCapturable = 5, MissingBattle = 6, BattleNotFinished = 7, NotVictoriousSide = 8
    }

    public sealed class BuildingEventState
    {
        public BuildingEventState(ulong sequence, long calendarTick, BuildingEventKind kind,
            StableEntityId? damageEventId = null, BuildingDamageType? damageType = null,
            int amount = 0, CombatSideId? ownerSideId = null)
        {
            if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (calendarTick < 0) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            if (!Enum.IsDefined(typeof(BuildingEventKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (damageEventId.HasValue && !damageEventId.Value.IsValid)
                throw new ArgumentException("Damage event identity must be valid.", nameof(damageEventId));
            if (damageType.HasValue && !Enum.IsDefined(typeof(BuildingDamageType), damageType.Value))
                throw new ArgumentOutOfRangeException(nameof(damageType));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (ownerSideId.HasValue && !ownerSideId.Value.IsValid)
                throw new ArgumentException("Owner side identity must be valid.", nameof(ownerSideId));
            Sequence = sequence;
            CalendarTick = calendarTick;
            Kind = kind;
            DamageEventId = damageEventId;
            DamageType = damageType;
            Amount = amount;
            OwnerSideId = ownerSideId;
        }

        public ulong Sequence { get; }
        public long CalendarTick { get; }
        public BuildingEventKind Kind { get; }
        public StableEntityId? DamageEventId { get; }
        public BuildingDamageType? DamageType { get; }
        public int Amount { get; }
        public CombatSideId? OwnerSideId { get; }
    }

    public readonly struct BuildingDamageResult
    {
        public BuildingDamageResult(BuildingDamageFailure failure, int requested, int applied,
            int remainingIntegrity, bool destroyedNow)
        {
            Failure = failure; Requested = requested; Applied = applied;
            RemainingIntegrity = remainingIntegrity; DestroyedNow = destroyedNow;
        }
        public BuildingDamageFailure Failure { get; }
        public int Requested { get; }
        public int Applied { get; }
        public int RemainingIntegrity { get; }
        public bool DestroyedNow { get; }
        public bool Succeeded => Failure == BuildingDamageFailure.None;
    }

    public readonly struct BuildingFireResult
    {
        public BuildingFireResult(BuildingFireFailure failure, BuildingFireStatus status)
        { Failure = failure; Status = status; }
        public BuildingFireFailure Failure { get; }
        public BuildingFireStatus Status { get; }
        public bool Succeeded => Failure == BuildingFireFailure.None;
    }

    public readonly struct BuildingCaptureResult
    {
        public BuildingCaptureResult(BuildingCaptureFailure failure, CombatSideId ownerSideId)
        { Failure = failure; OwnerSideId = ownerSideId; }
        public BuildingCaptureFailure Failure { get; }
        public CombatSideId OwnerSideId { get; }
        public bool Succeeded => Failure == BuildingCaptureFailure.None;
    }

    public sealed class BuildingWorldSaveState
    {
        public BuildingWorldSaveState(IEnumerable<BuildingState> buildings,
            IEnumerable<WorldResourcePileState> worldPiles, long lastFireTick)
        {
            if (lastFireTick < 0) throw new ArgumentOutOfRangeException(nameof(lastFireTick));
            var buildingArray = (buildings ?? Array.Empty<BuildingState>()).OrderBy(x => x.Id).ToArray();
            if (buildingArray.Select(x => x.Id).Distinct().Count() != buildingArray.Length)
                throw new ArgumentException("Building save state contains duplicate identities.", nameof(buildings));
            var pileArray = (worldPiles ?? Array.Empty<WorldResourcePileState>()).OrderBy(x => x.Id).ToArray();
            if (pileArray.Select(x => x.Id).Distinct().Count() != pileArray.Length)
                throw new ArgumentException("World-pile save state contains duplicate identities.", nameof(worldPiles));
            Buildings = new ReadOnlyCollection<BuildingState>(buildingArray);
            WorldPiles = new ReadOnlyCollection<WorldResourcePileState>(pileArray);
            LastFireTick = lastFireTick;
        }
        public IReadOnlyList<BuildingState> Buildings { get; }
        public IReadOnlyList<WorldResourcePileState> WorldPiles { get; }
        public long LastFireTick { get; }
    }
}
