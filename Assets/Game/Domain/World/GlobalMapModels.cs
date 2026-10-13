using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.Combat;
using Game.Domain.Military;

namespace Game.Domain.World
{
    public readonly struct WorldLocationId : IEquatable<WorldLocationId>, IComparable<WorldLocationId>
    {
        public WorldLocationId(string value)
        {
            if (!IsCanonical(value))
                throw new ArgumentException("World location ID must be canonical lowercase text.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool IsValid => IsCanonical(Value);
        public int CompareTo(WorldLocationId other) => string.Compare(Value, other.Value, StringComparison.Ordinal);
        public bool Equals(WorldLocationId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is WorldLocationId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(WorldLocationId left, WorldLocationId right) => left.Equals(right);
        public static bool operator !=(WorldLocationId left, WorldLocationId right) => !left.Equals(right);

        private static bool IsCanonical(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64 &&
            value == value.Trim() && value.All(c => char.IsLower(c) || char.IsDigit(c) || c == '_');
    }

    public readonly struct SettlementId : IEquatable<SettlementId>, IComparable<SettlementId>
    {
        public SettlementId(StableEntityId value)
        {
            if (!value.IsValid) throw new ArgumentException("Settlement ID must be valid.", nameof(value));
            Value = value;
        }
        public StableEntityId Value { get; }
        public bool IsValid => Value.IsValid;
        public int CompareTo(SettlementId other) => Value.CompareTo(other.Value);
        public bool Equals(SettlementId other) => Value.Equals(other.Value);
        public override bool Equals(object obj) => obj is SettlementId other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode();
        public override string ToString() => Value.ToString();
        public static bool operator ==(SettlementId left, SettlementId right) => left.Equals(right);
        public static bool operator !=(SettlementId left, SettlementId right) => !left.Equals(right);
    }

    public enum WorldLocationType { Settlement = 1, Wilderness = 2, BattleSite = 3 }
    public enum GameWorldMode { Local = 1, Global = 2 }
    public enum SettlementOccupationEventKind { Started = 1, Interrupted = 2, Completed = 3 }
    public enum SettlementOccupationInterruptionReason
    {
        None = 0,
        DefendersArrived = 1,
        ClaimantUnavailable = 2,
        HostileContested = 3,
        OwnerChanged = 4,
        CaptureRejected = 5
    }

    public sealed class WorldLocationState
    {
        public WorldLocationState(WorldLocationId id, string name, WorldLocationType type, int mapX, int mapY)
        {
            if (!id.IsValid) throw new ArgumentException("Location ID required.", nameof(id));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 96 || name != name.Trim())
                throw new ArgumentException("Location name must be trimmed and at most 96 characters.", nameof(name));
            if (!Enum.IsDefined(typeof(WorldLocationType), type)) throw new ArgumentOutOfRangeException(nameof(type));
            Id = id;
            Name = name;
            Type = type;
            MapX = mapX;
            MapY = mapY;
        }
        public WorldLocationId Id { get; }
        public string Name { get; }
        public WorldLocationType Type { get; }
        public int MapX { get; }
        public int MapY { get; }
    }

    public sealed class WorldRouteState
    {
        public WorldRouteState(WorldLocationId from, WorldLocationId to, long travelDurationTicks)
        {
            if (!from.IsValid || !to.IsValid || from == to)
                throw new ArgumentException("Route endpoints must be valid and distinct.");
            if (travelDurationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(travelDurationTicks));
            From = from;
            To = to;
            TravelDurationTicks = travelDurationTicks;
        }
        public WorldLocationId From { get; }
        public WorldLocationId To { get; }
        public long TravelDurationTicks { get; }
        public bool Connects(WorldLocationId first, WorldLocationId second) =>
            (From == first && To == second) || (From == second && To == first);
    }

    public sealed class SettlementState
    {
        public SettlementState(SettlementId id, WorldLocationId locationId, CombatSideId ownerSideId,
            IEnumerable<StableEntityId> buildingIds, IEnumerable<StableEntityId> residentCharacterIds)
        {
            if (!id.IsValid || !locationId.IsValid || !ownerSideId.IsValid)
                throw new ArgumentException("Settlement identity, location and owner are required.");
            Id = id;
            LocationId = locationId;
            OwnerSideId = ownerSideId;
            BuildingIds = OrderedUnique(buildingIds, nameof(buildingIds));
            ResidentCharacterIds = OrderedUnique(residentCharacterIds, nameof(residentCharacterIds));
        }
        public SettlementId Id { get; }
        public WorldLocationId LocationId { get; }
        public CombatSideId OwnerSideId { get; }
        public IReadOnlyList<StableEntityId> BuildingIds { get; }
        public IReadOnlyList<StableEntityId> ResidentCharacterIds { get; }

        private static IReadOnlyList<StableEntityId> OrderedUnique(IEnumerable<StableEntityId> source, string name)
        {
            var values = (source ?? Enumerable.Empty<StableEntityId>()).ToArray();
            if (values.Any(x => !x.IsValid) || values.Distinct().Count() != values.Length)
                throw new ArgumentException("Settlement references must be valid and unique.", name);
            return Array.AsReadOnly(values.OrderBy(x => x.ToString(), StringComparer.Ordinal).ToArray());
        }
    }

    public sealed class WorldTravelState
    {
        public WorldTravelState(MilitaryUnitId unitId, WorldLocationId origin, WorldLocationId destination,
            long departureTick, long arrivalTick)
        {
            if (!unitId.IsValid || !origin.IsValid || !destination.IsValid || origin == destination)
                throw new ArgumentException("Travel requires a valid unit and distinct locations.");
            if (departureTick < 0 || arrivalTick <= departureTick)
                throw new ArgumentOutOfRangeException(nameof(arrivalTick));
            UnitId = unitId;
            Origin = origin;
            Destination = destination;
            DepartureTick = departureTick;
            ArrivalTick = arrivalTick;
        }
        public MilitaryUnitId UnitId { get; }
        public WorldLocationId Origin { get; }
        public WorldLocationId Destination { get; }
        public long DepartureTick { get; }
        public long ArrivalTick { get; }
    }

    /// <summary>Canonical in-progress settlement occupation. It is not a synthetic battle.</summary>
    public sealed class SettlementOccupationState
    {
        public SettlementOccupationState(StableEntityId occupationId, SettlementId settlementId,
            WorldLocationId locationId, MilitaryUnitId claimantUnitId, CombatSideId claimantSideId,
            long startTick, long requiredDurationTicks, long lastEvaluatedTick)
        {
            if (!occupationId.IsValid || !settlementId.IsValid || !locationId.IsValid ||
                !claimantUnitId.IsValid || !claimantSideId.IsValid)
                throw new ArgumentException("Occupation identity and references are required.");
            if (startTick < 0 || requiredDurationTicks <= 0 || lastEvaluatedTick < startTick ||
                lastEvaluatedTick > checked(startTick + requiredDurationTicks))
                throw new ArgumentOutOfRangeException(nameof(lastEvaluatedTick));
            OccupationId = occupationId;
            SettlementId = settlementId;
            LocationId = locationId;
            ClaimantUnitId = claimantUnitId;
            ClaimantSideId = claimantSideId;
            StartTick = startTick;
            RequiredDurationTicks = requiredDurationTicks;
            LastEvaluatedTick = lastEvaluatedTick;
        }

        public StableEntityId OccupationId { get; }
        public SettlementId SettlementId { get; }
        public WorldLocationId LocationId { get; }
        public MilitaryUnitId ClaimantUnitId { get; }
        public CombatSideId ClaimantSideId { get; }
        public long StartTick { get; }
        public long RequiredDurationTicks { get; }
        public long LastEvaluatedTick { get; }
        public long CompletionTick => checked(StartTick + RequiredDurationTicks);
        public long ProgressTicks => Math.Min(RequiredDurationTicks, Math.Max(0, LastEvaluatedTick - StartTick));

        public SettlementOccupationState EvaluateAt(long tick) => new SettlementOccupationState(OccupationId,
            SettlementId, LocationId, ClaimantUnitId, ClaimantSideId, StartTick, RequiredDurationTicks,
            Math.Min(CompletionTick, Math.Max(LastEvaluatedTick, tick)));
    }

    public sealed class SettlementOccupationEventState
    {
        public SettlementOccupationEventState(ulong sequence, StableEntityId occupationId,
            SettlementId settlementId, MilitaryUnitId claimantUnitId, CombatSideId claimantSideId,
            SettlementOccupationEventKind kind, SettlementOccupationInterruptionReason interruptionReason,
            long calendarTick, long progressTicks)
        {
            if (sequence == 0 || !occupationId.IsValid || !settlementId.IsValid ||
                !claimantUnitId.IsValid || !claimantSideId.IsValid)
                throw new ArgumentException("Occupation event identity is invalid.");
            if (!Enum.IsDefined(typeof(SettlementOccupationEventKind), kind) ||
                !Enum.IsDefined(typeof(SettlementOccupationInterruptionReason), interruptionReason))
                throw new ArgumentOutOfRangeException();
            if ((kind == SettlementOccupationEventKind.Interrupted) !=
                (interruptionReason != SettlementOccupationInterruptionReason.None))
                throw new ArgumentException("Only interrupted occupation events carry an interruption reason.");
            if (calendarTick < 0 || progressTicks < 0) throw new ArgumentOutOfRangeException();
            Sequence = sequence;
            OccupationId = occupationId;
            SettlementId = settlementId;
            ClaimantUnitId = claimantUnitId;
            ClaimantSideId = claimantSideId;
            Kind = kind;
            InterruptionReason = interruptionReason;
            CalendarTick = calendarTick;
            ProgressTicks = progressTicks;
        }

        public ulong Sequence { get; }
        public StableEntityId OccupationId { get; }
        public SettlementId SettlementId { get; }
        public MilitaryUnitId ClaimantUnitId { get; }
        public CombatSideId ClaimantSideId { get; }
        public SettlementOccupationEventKind Kind { get; }
        public SettlementOccupationInterruptionReason InterruptionReason { get; }
        public long CalendarTick { get; }
        public long ProgressTicks { get; }
    }

    public sealed class GlobalMapSaveState
    {
        public GlobalMapSaveState(string graphId, GameWorldMode mode, WorldLocationId activeLocationId,
            IEnumerable<WorldLocationState> locations, IEnumerable<WorldRouteState> routes,
            IEnumerable<SettlementState> settlements, IEnumerable<WorldTravelState> travels, long lastProcessedTick,
            IEnumerable<SettlementOccupationState> occupations = null,
            IEnumerable<SettlementOccupationEventState> occupationEvents = null,
            ulong occupationEventSequence = 0)
        {
            if (string.IsNullOrWhiteSpace(graphId) || graphId.Length > 64 || graphId != graphId.Trim())
                throw new ArgumentException("Graph ID must be trimmed and at most 64 characters.", nameof(graphId));
            if (!Enum.IsDefined(typeof(GameWorldMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (lastProcessedTick < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedTick));
            var locationValues = (locations ?? throw new ArgumentNullException(nameof(locations))).ToArray();
            if (locationValues.Length == 0 || locationValues.Any(x => x == null) ||
                locationValues.Select(x => x.Id).Distinct().Count() != locationValues.Length)
                throw new ArgumentException("Locations must be non-empty and uniquely identified.", nameof(locations));
            if (!locationValues.Any(x => x.Id == activeLocationId))
                throw new ArgumentException("Active location must exist in the graph.", nameof(activeLocationId));
            var locationSet = new HashSet<WorldLocationId>(locationValues.Select(x => x.Id));
            var routeValues = (routes ?? throw new ArgumentNullException(nameof(routes))).ToArray();
            if (routeValues.Any(x => x == null || !locationSet.Contains(x.From) || !locationSet.Contains(x.To)))
                throw new ArgumentException("Every route must reference known locations.", nameof(routes));
            if (routeValues.Select(RouteKey).Distinct(StringComparer.Ordinal).Count() != routeValues.Length)
                throw new ArgumentException("Duplicate routes are not allowed.", nameof(routes));
            var settlementValues = (settlements ?? throw new ArgumentNullException(nameof(settlements))).ToArray();
            if (settlementValues.Any(x => x == null || !locationSet.Contains(x.LocationId) ||
                locationValues.Single(location => location.Id == x.LocationId).Type != WorldLocationType.Settlement) ||
                settlementValues.Select(x => x.Id).Distinct().Count() != settlementValues.Length ||
                settlementValues.Select(x => x.LocationId).Distinct().Count() != settlementValues.Length)
                throw new ArgumentException("Settlements must uniquely reference settlement locations.", nameof(settlements));
            var travelValues = (travels ?? Enumerable.Empty<WorldTravelState>()).ToArray();
            if (travelValues.Any(x => x == null || !locationSet.Contains(x.Origin) ||
                !locationSet.Contains(x.Destination) || !routeValues.Any(route => route.Connects(x.Origin, x.Destination))) ||
                travelValues.Select(x => x.UnitId).Distinct().Count() != travelValues.Length)
                throw new ArgumentException("Travel states must be unique and follow a configured route.", nameof(travels));
            var occupationValues = (occupations ?? Enumerable.Empty<SettlementOccupationState>()).ToArray();
            if (occupationValues.Any(x => x == null || !settlementValues.Any(s => s.Id == x.SettlementId &&
                    s.LocationId == x.LocationId) || x.LastEvaluatedTick > lastProcessedTick) ||
                occupationValues.Select(x => x.OccupationId).Distinct().Count() != occupationValues.Length ||
                occupationValues.Select(x => x.SettlementId).Distinct().Count() != occupationValues.Length)
                throw new ArgumentException("Occupation states must be unique and reference their settlement.", nameof(occupations));
            var occupationEventValues = (occupationEvents ?? Enumerable.Empty<SettlementOccupationEventState>()).
                OrderBy(x => x.Sequence).ToArray();
            if (occupationEventValues.Any(x => x == null || !settlementValues.Any(s => s.Id == x.SettlementId) ||
                    x.CalendarTick > lastProcessedTick) ||
                occupationEventValues.Select(x => x.Sequence).Distinct().Count() != occupationEventValues.Length ||
                occupationEventValues.Where((x, i) => i > 0 && x.Sequence <= occupationEventValues[i - 1].Sequence).Any() ||
                occupationEventValues.Any(x => x.Sequence > occupationEventSequence))
                throw new ArgumentException("Occupation history is invalid.", nameof(occupationEvents));

            GraphId = graphId;
            Mode = mode;
            ActiveLocationId = activeLocationId;
            Locations = Array.AsReadOnly(locationValues.OrderBy(x => x.Id).ToArray());
            Routes = Array.AsReadOnly(routeValues.OrderBy(RouteKey, StringComparer.Ordinal).ToArray());
            Settlements = Array.AsReadOnly(settlementValues.OrderBy(x => x.Id).ToArray());
            Travels = Array.AsReadOnly(travelValues.OrderBy(x => x.UnitId).ToArray());
            Occupations = Array.AsReadOnly(occupationValues.OrderBy(x => x.SettlementId).ToArray());
            OccupationEvents = Array.AsReadOnly(occupationEventValues);
            OccupationEventSequence = occupationEventSequence;
            LastProcessedTick = lastProcessedTick;
        }

        public string GraphId { get; }
        public GameWorldMode Mode { get; }
        public WorldLocationId ActiveLocationId { get; }
        public IReadOnlyList<WorldLocationState> Locations { get; }
        public IReadOnlyList<WorldRouteState> Routes { get; }
        public IReadOnlyList<SettlementState> Settlements { get; }
        public IReadOnlyList<WorldTravelState> Travels { get; }
        public IReadOnlyList<SettlementOccupationState> Occupations { get; }
        public IReadOnlyList<SettlementOccupationEventState> OccupationEvents { get; }
        public ulong OccupationEventSequence { get; }
        public long LastProcessedTick { get; }

        private static string RouteKey(WorldRouteState route) => route.From.CompareTo(route.To) < 0
            ? route.From + "|" + route.To : route.To + "|" + route.From;
    }
}
