using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Simulation.Combat;
using Game.Simulation.Military;

namespace Game.Simulation.World
{
    public enum TravelOrderStatus
    {
        Issued = 1,
        UnitNotFound = 2,
        UnitNotAggregate = 3,
        UnitInBattle = 4,
        AlreadyTravelling = 5,
        UnknownDestination = 6,
        NoDirectRoute = 7,
        InvalidOrigin = 8
    }

    public readonly struct TravelOrderResult
    {
        public TravelOrderResult(TravelOrderStatus status, WorldTravelState travel = null)
        {
            Status = status;
            Travel = travel;
        }
        public TravelOrderStatus Status { get; }
        public WorldTravelState Travel { get; }
        public bool Issued => Status == TravelOrderStatus.Issued;
    }

    /// <summary>
    /// Canonical U25 location/travel authority. MilitaryUnitService still owns units and locations;
    /// BattleResolutionService still owns combat. This service adds no Character, army or battle copies.
    /// </summary>
    public sealed class GlobalMapService
    {
        private readonly MilitaryUnitService units;
        private readonly BattleResolutionService battles;
        private readonly Func<WorldLocationId, WorldLocationId, long, int> travelDurationModifier;
        private readonly Dictionary<WorldLocationId, WorldLocationState> locations =
            new Dictionary<WorldLocationId, WorldLocationState>();
        private readonly List<WorldRouteState> routes = new List<WorldRouteState>();
        private readonly Dictionary<SettlementId, SettlementState> settlements =
            new Dictionary<SettlementId, SettlementState>();
        private readonly Dictionary<MilitaryUnitId, WorldTravelState> travels =
            new Dictionary<MilitaryUnitId, WorldTravelState>();

        public GlobalMapService(GlobalMapSaveState initialState, MilitaryUnitService militaryUnits,
            BattleResolutionService battleResolution = null,
            Func<WorldLocationId, WorldLocationId, long, int> durationModifier = null)
        {
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
            battles = battleResolution;
            travelDurationModifier = durationModifier;
            RestoreState(initialState ?? throw new ArgumentNullException(nameof(initialState)));
        }

        public string GraphId { get; private set; }
        public GameWorldMode Mode { get; private set; }
        public WorldLocationId ActiveLocationId { get; private set; }
        public long LastProcessedTick { get; private set; }
        public IReadOnlyList<WorldLocationState> Locations => locations.Values.OrderBy(x => x.Id).ToArray();
        public IReadOnlyList<WorldRouteState> Routes => routes.ToArray();
        public IReadOnlyList<SettlementState> Settlements => settlements.Values.OrderBy(x => x.Id).ToArray();
        public IReadOnlyList<WorldTravelState> Travels => travels.Values.OrderBy(x => x.UnitId).ToArray();

        public WorldLocationState GetLocation(WorldLocationId id) => locations.TryGetValue(id, out var value)
            ? value : throw new KeyNotFoundException("Unknown world location: " + id);

        public SettlementState GetSettlementAt(WorldLocationId id) => settlements.Values
            .SingleOrDefault(x => x.LocationId == id);

        public IReadOnlyList<WorldRouteState> GetRoutes(WorldLocationId id) => routes
            .Where(x => x.From == id || x.To == id).OrderBy(RouteKey, StringComparer.Ordinal).ToArray();

        public IReadOnlyList<MilitaryUnitState> GetMilitaryPresence(WorldLocationId id) => units.Units
            .Where(x => string.Equals(x.AbstractLocationId, id.Value, StringComparison.Ordinal) &&
                        !travels.ContainsKey(x.Id))
            .OrderBy(x => x.Id).ToArray();

        public TravelOrderResult IssueTravelOrder(MilitaryUnitId unitId, WorldLocationId destination,
            long departureTick)
        {
            if (departureTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(departureTick));
            if (!units.TryGet(unitId, out var unit)) return new TravelOrderResult(TravelOrderStatus.UnitNotFound);
            if (unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                return new TravelOrderResult(TravelOrderStatus.UnitNotAggregate);
            if (unit.ActiveEncounterId.HasValue) return new TravelOrderResult(TravelOrderStatus.UnitInBattle);
            if (travels.ContainsKey(unitId)) return new TravelOrderResult(TravelOrderStatus.AlreadyTravelling);
            if (!locations.ContainsKey(destination)) return new TravelOrderResult(TravelOrderStatus.UnknownDestination);
            if (!TryLocation(unit.AbstractLocationId, out var origin))
                return new TravelOrderResult(TravelOrderStatus.InvalidOrigin);
            var route = routes.SingleOrDefault(x => x.Connects(origin, destination));
            if (route == null) return new TravelOrderResult(TravelOrderStatus.NoDirectRoute);
            var modifier = travelDurationModifier?.Invoke(origin, destination, departureTick) ?? 1000;
            if (modifier <= 0 || modifier > 10000)
                throw new InvalidOperationException("Global travel duration modifier must be in 1..10000 per-thousand.");
            var scaled = checked(route.TravelDurationTicks * (long)modifier);
            var duration = Math.Max(1L, checked((scaled + 999L) / 1000L));
            var travel = new WorldTravelState(unitId, origin, destination, departureTick,
                checked(departureTick + duration));
            travels.Add(unitId, travel);
            var target = locations[destination];
            units.SetOrder(unitId, MilitaryUnitOrderKind.Move,
                new Game.Domain.AI.WorldPosition(target.MapX, 0f, target.MapY), true);
            return new TravelOrderResult(TravelOrderStatus.Issued, travel);
        }

        public IReadOnlyList<StableEntityId> AdvanceTo(long calendarTick)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            var due = travels.Values.Where(x => x.ArrivalTick <= calendarTick)
                .OrderBy(x => x.ArrivalTick).ThenBy(x => x.UnitId).ToArray();
            var started = new List<StableEntityId>();
            foreach (var arrivalGroup in due.GroupBy(x => x.ArrivalTick).OrderBy(x => x.Key))
            {
                var arrivedLocations = new HashSet<WorldLocationId>();
                foreach (var travel in arrivalGroup)
                {
                    if (!travels.Remove(travel.UnitId)) continue;
                    units.SetAbstractLocation(travel.UnitId, travel.Destination.Value);
                    units.SetOrder(travel.UnitId, MilitaryUnitOrderKind.Idle);
                    arrivedLocations.Add(travel.Destination);
                }
                foreach (var location in arrivedLocations.OrderBy(x => x))
                    StartHostileEncounters(location, arrivalGroup.Key, started);
            }
            LastProcessedTick = calendarTick;
            return started;
        }

        public void SynchronizeSettlement(SettlementId id, WorldLocationId locationId, CombatSideId ownerSideId,
            IEnumerable<StableEntityId> buildingIds, IEnumerable<StableEntityId> residentIds)
        {
            var state = new SettlementState(id, locationId, ownerSideId, buildingIds, residentIds);
            if (!locations.TryGetValue(locationId, out var location) ||
                location.Type != WorldLocationType.Settlement)
                throw new InvalidOperationException("Settlement location is not part of this graph.");
            var collision = settlements.Values.FirstOrDefault(x => x.LocationId == locationId && x.Id != id);
            if (collision != null) throw new InvalidOperationException("A location already owns another settlement.");
            settlements[id] = state;
        }

        public GlobalMapSaveState CaptureState() => new GlobalMapSaveState(GraphId, Mode, ActiveLocationId,
            Locations, Routes, Settlements, Travels, LastProcessedTick);

        public void RestoreState(GlobalMapSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            foreach (var travel in state.Travels)
            {
                if (!units.TryGet(travel.UnitId, out var unit))
                    throw new ArgumentException("Saved travel references an unknown military unit.", nameof(state));
                if (!string.Equals(unit.AbstractLocationId, travel.Origin.Value, StringComparison.Ordinal))
                    throw new ArgumentException("Saved travel origin does not match the unit location.", nameof(state));
                if (unit.ActiveEncounterId.HasValue || unit.Representation != MilitaryUnitRepresentationMode.Aggregate)
                    throw new ArgumentException("Only idle aggregate units may have saved global travel.", nameof(state));
            }
            GraphId = state.GraphId;
            Mode = state.Mode;
            ActiveLocationId = state.ActiveLocationId;
            LastProcessedTick = state.LastProcessedTick;
            locations.Clear();
            foreach (var value in state.Locations) locations.Add(value.Id, value);
            routes.Clear();
            routes.AddRange(state.Routes);
            settlements.Clear();
            foreach (var value in state.Settlements) settlements.Add(value.Id, value);
            travels.Clear();
            foreach (var value in state.Travels) travels.Add(value.UnitId, value);
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            Add(ref hash, GraphId);
            Add(ref hash, ((int)Mode).ToString());
            Add(ref hash, ActiveLocationId.Value);
            Add(ref hash, LastProcessedTick.ToString());
            foreach (var location in Locations)
            {
                Add(ref hash, location.Id.Value); Add(ref hash, location.Name);
                Add(ref hash, ((int)location.Type).ToString()); Add(ref hash, location.MapX.ToString());
                Add(ref hash, location.MapY.ToString());
            }
            foreach (var route in Routes.OrderBy(RouteKey, StringComparer.Ordinal))
            {
                Add(ref hash, RouteKey(route)); Add(ref hash, route.TravelDurationTicks.ToString());
            }
            foreach (var settlement in Settlements)
            {
                Add(ref hash, settlement.Id.ToString()); Add(ref hash, settlement.LocationId.Value);
                Add(ref hash, settlement.OwnerSideId.Value);
                foreach (var id in settlement.BuildingIds) Add(ref hash, id.ToString());
                foreach (var id in settlement.ResidentCharacterIds) Add(ref hash, id.ToString());
            }
            foreach (var travel in Travels)
            {
                Add(ref hash, travel.UnitId.ToString()); Add(ref hash, travel.Origin.Value);
                Add(ref hash, travel.Destination.Value); Add(ref hash, travel.DepartureTick.ToString());
                Add(ref hash, travel.ArrivalTick.ToString());
            }
            return hash;
        }

        internal void CommitMode(GameWorldMode mode, WorldLocationId activeLocation)
        {
            if (!locations.ContainsKey(activeLocation)) throw new ArgumentException("Unknown active location.");
            Mode = mode;
            ActiveLocationId = activeLocation;
        }

        private void StartHostileEncounters(WorldLocationId location, long tick, ICollection<StableEntityId> started)
        {
            if (battles == null) return;
            var candidates = GetMilitaryPresence(location).Where(x => !x.ActiveEncounterId.HasValue &&
                x.Representation == MilitaryUnitRepresentationMode.Aggregate).OrderBy(x => x.Id).ToList();
            while (candidates.Count > 1)
            {
                var first = candidates[0];
                var enemyIndex = candidates.FindIndex(1, x => x.OwnerSideId != first.OwnerSideId);
                if (enemyIndex < 0) break;
                var second = candidates[enemyIndex];
                candidates.RemoveAt(enemyIndex);
                candidates.RemoveAt(0);
                var battleId = BattleId(first.Id, second.Id, location, tick);
                battles.StartAbstractBattle(first.Id, second.Id, battleId, tick);
                started.Add(battleId);
            }
        }

        private bool TryLocation(string value, out WorldLocationId id)
        {
            id = default;
            try { id = new WorldLocationId(value); }
            catch (ArgumentException) { return false; }
            return locations.ContainsKey(id);
        }

        private static StableEntityId BattleId(MilitaryUnitId first, MilitaryUnitId second,
            WorldLocationId location, long tick)
        {
            var a = first.CompareTo(second) < 0 ? first.ToString() : second.ToString();
            var b = first.CompareTo(second) < 0 ? second.ToString() : first.ToString();
            ulong firstHash = 14695981039346656037UL;
            Add(ref firstHash, "global_battle"); Add(ref firstHash, a); Add(ref firstHash, b);
            ulong secondHash = 14695981039346656037UL;
            Add(ref secondHash, location.Value); Add(ref secondHash, tick.ToString());
            return StableEntityId.FromDeterministicHash(firstHash, secondHash);
        }

        private static string RouteKey(WorldRouteState route) => route.From.CompareTo(route.To) < 0
            ? route.From + "|" + route.To : route.To + "|" + route.From;

        private static void Add(ref ulong hash, string value)
        {
            for (var i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 1099511628211UL;
            hash = (hash ^ 255UL) * 1099511628211UL;
        }
    }

    public static class StarterGlobalMap
    {
        public static GlobalMapSaveState Create(long initialTick = 0)
        {
            var player = new WorldLocationId("starter_settlement");
            var enemy = new WorldLocationId("enemy_settlement");
            var forest = new WorldLocationId("north_forest");
            var ford = new WorldLocationId("river_ford");
            var hills = new WorldLocationId("east_hills");
            var field = new WorldLocationId("south_field");
            var locations = new[]
            {
                new WorldLocationState(player, "Player Settlement", WorldLocationType.Settlement, -20, 0),
                new WorldLocationState(enemy, "Test Settlement", WorldLocationType.Settlement, 22, 5),
                new WorldLocationState(forest, "North Forest", WorldLocationType.Wilderness, -8, 18),
                new WorldLocationState(ford, "River Ford", WorldLocationType.Wilderness, 2, 4),
                new WorldLocationState(hills, "East Hills", WorldLocationType.Wilderness, 15, 18),
                new WorldLocationState(field, "South Field", WorldLocationType.Wilderness, -2, -17)
            };
            var hour = TimeSpan.TicksPerHour;
            var routes = new[]
            {
                new WorldRouteState(player, forest, 6 * hour), new WorldRouteState(player, ford, 8 * hour),
                new WorldRouteState(player, field, 5 * hour), new WorldRouteState(forest, hills, 7 * hour),
                new WorldRouteState(ford, hills, 5 * hour), new WorldRouteState(ford, enemy, 8 * hour),
                new WorldRouteState(hills, enemy, 6 * hour), new WorldRouteState(field, enemy, 12 * hour)
            };
            var settlements = new[]
            {
                new SettlementState(new SettlementId(Id(500)), player, new CombatSideId("player"), null, null),
                new SettlementState(new SettlementId(Id(501)), enemy, new CombatSideId("enemy"), null, null)
            };
            return new GlobalMapSaveState("starter_graph_v1", GameWorldMode.Local, player, locations, routes,
                settlements, null, initialTick);
        }

        private static StableEntityId Id(int value) => StableEntityId.Parse(value.ToString("x32"));
    }
}
