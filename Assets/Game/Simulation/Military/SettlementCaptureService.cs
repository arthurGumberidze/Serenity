using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.AI;
using Game.Domain.Buildings;
using Game.Domain.Campaign;
using Game.Domain.Characters;
using Game.Domain.Combat;
using Game.Domain.Military;
using Game.Domain.World;
using Game.Simulation.Buildings;
using Game.Simulation.World;

namespace Game.Simulation.Military
{
    /// <summary>Two-flag, shared-clock settlement capture and deterministic post-capture resolution.</summary>
    public sealed class SettlementFlagCaptureService
    {
        public const long DefaultRequiredTicks = 5L * TimeSpan.TicksPerHour;
        private readonly long worldSeed;
        private readonly GlobalMapService map;
        private readonly BuildingRegistry buildings;
        private readonly BuildingDamageService buildingDamage;
        private readonly CharacterRegistry characters;
        private readonly MilitaryUnitService units;
        private readonly Dictionary<SettlementId, SettlementCaptureState> states =
            new Dictionary<SettlementId, SettlementCaptureState>();

        public SettlementFlagCaptureService(long seed, GlobalMapService globalMap, BuildingRegistry buildingRegistry,
            BuildingDamageService damageService, CharacterRegistry characterRegistry,
            MilitaryUnitService militaryUnits)
        {
            worldSeed = seed;
            map = globalMap ?? throw new ArgumentNullException(nameof(globalMap));
            buildings = buildingRegistry ?? throw new ArgumentNullException(nameof(buildingRegistry));
            buildingDamage = damageService ?? throw new ArgumentNullException(nameof(damageService));
            characters = characterRegistry ?? throw new ArgumentNullException(nameof(characterRegistry));
            units = militaryUnits ?? throw new ArgumentNullException(nameof(militaryUnits));
        }

        public IReadOnlyList<SettlementCaptureState> States => states.Values.OrderBy(x => x.SettlementId).ToArray();

        public SettlementCaptureState Register(SettlementId settlementId, StableEntityId mainBuildingId,
            long initialTick, long requiredTicks = DefaultRequiredTicks)
        {
            var settlement = map.Settlements.SingleOrDefault(x => x.Id == settlementId) ??
                throw new KeyNotFoundException("Unknown settlement: " + settlementId);
            if (!settlement.BuildingIds.Contains(mainBuildingId) || !buildings.TryGet(mainBuildingId, out var main))
                throw new ArgumentException("Main building must belong to the settlement.", nameof(mainBuildingId));
            if (initialTick < 0 || requiredTicks <= 0) throw new ArgumentOutOfRangeException(nameof(initialTick));
            var barracks = ActiveBarracks(settlement);
            var state = new SettlementCaptureState(settlementId, settlement.OwnerSideId, null, main.Id,
                barracks?.Id, 0, requiredTicks, initialTick, SettlementCaptureClockStatus.Idle,
                CaptureSeed(settlementId));
            states[settlementId] = state;
            return state;
        }

        public SettlementCaptureState Get(SettlementId settlementId) => states.TryGetValue(settlementId, out var state)
            ? state : throw new KeyNotFoundException("Settlement capture is not registered: " + settlementId);

        public bool IsCaptureFlag(SettlementId settlementId, StableEntityId buildingId)
        {
            var state = Get(settlementId);
            var settlement = map.Settlements.Single(x => x.Id == settlementId);
            var barracks = ActiveBarracks(settlement);
            return state.MainBuildingId == buildingId || barracks != null && barracks.Id == buildingId;
        }

        public bool TryClaimFlag(SettlementId settlementId, StableEntityId buildingId,
            MilitaryUnitId claimantUnitId, long calendarTick)
        {
            if (!IsCaptureFlag(settlementId, buildingId) || !buildings.TryGet(buildingId, out var building) ||
                building.IsDestroyed || !units.TryGet(claimantUnitId, out var unit) ||
                unit.Status == MilitaryUnitStatus.Disbanded || unit.ActiveMemberIds.Count == 0) return false;
            var newOwner = new CombatSideId(unit.OwnerSideId.Value);
            if (building.OwnerSideId == newOwner) return true;
            building.Capture(newOwner, calendarTick);
            return true;
        }

        public int TryClaimFlagsNear(MilitaryUnitId claimantUnitId, WorldPosition position,
            float radius, long calendarTick)
        {
            if (radius <= 0f) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!units.TryGet(claimantUnitId, out var unit) || unit.Status == MilitaryUnitStatus.Disbanded ||
                unit.ActiveMemberIds.Count == 0) return 0;
            var claimed = 0;
            var radiusSquared = radius * radius;
            foreach (var state in States)
            {
                var ids = state.ActiveBarracksId.HasValue
                    ? new[] { state.MainBuildingId, state.ActiveBarracksId.Value }
                    : new[] { state.MainBuildingId };
                foreach (var id in ids.Distinct())
                {
                    if (!buildings.TryGet(id, out var building) || building.IsDestroyed) continue;
                    var dx = position.X - building.Coordinate.X;
                    var dz = position.Z - building.Coordinate.Z;
                    if (dx * dx + dz * dz > radiusSquared) continue;
                    var ownerBefore = building.OwnerSideId;
                    if (TryClaimFlag(state.SettlementId, id, claimantUnitId, calendarTick) &&
                        building.OwnerSideId != ownerBefore) claimed++;
                }
            }
            return claimed;
        }

        public int AdvanceTo(long calendarTick, long biologicalTick,
            Func<SettlementId, CombatSideId, bool> defenderContest = null)
        {
            var completed = 0;
            foreach (var settlementId in states.Keys.OrderBy(x => x).ToArray())
            {
                var before = states[settlementId];
                if (calendarTick < before.LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
                if (before.Status == SettlementCaptureClockStatus.Completed) continue;
                var settlement = map.Settlements.Single(x => x.Id == settlementId);
                if (!buildings.TryGet(before.MainBuildingId, out var main) || main.IsDestroyed)
                {
                    states[settlementId] = Next(before, null, null, 0, calendarTick,
                        SettlementCaptureClockStatus.Idle, null);
                    continue;
                }
                var barracks = ActiveBarracks(settlement);
                var mainOwner = main.OwnerSideId;
                var barracksOwner = barracks?.OwnerSideId;
                var requiredOwner = barracks == null || barracksOwner == mainOwner ? mainOwner : default;
                var attacker = before.AttackerSideId;

                if (attacker.HasValue && (mainOwner == before.DefenderSideId ||
                    barracksOwner.HasValue && barracksOwner.Value == before.DefenderSideId))
                {
                    states[settlementId] = Next(before, null, barracks?.Id, 0, calendarTick,
                        SettlementCaptureClockStatus.Idle, null);
                    continue;
                }
                if (!attacker.HasValue && requiredOwner.IsValid && requiredOwner != before.DefenderSideId)
                    attacker = requiredOwner;
                if (!attacker.HasValue || !requiredOwner.IsValid || requiredOwner != attacker.Value)
                {
                    states[settlementId] = Next(before, attacker, barracks?.Id, 0, calendarTick,
                        SettlementCaptureClockStatus.Idle, null);
                    continue;
                }
                var contested = defenderContest != null && defenderContest(settlementId, before.DefenderSideId);
                if (contested)
                {
                    states[settlementId] = Next(before, attacker, barracks?.Id, before.ProgressTicks,
                        calendarTick, SettlementCaptureClockStatus.Frozen, null);
                    continue;
                }
                var delta = Math.Max(0, calendarTick - before.LastProcessedTick);
                var progress = Math.Min(before.RequiredTicks, checked(before.ProgressTicks + delta));
                if (progress < before.RequiredTicks)
                {
                    states[settlementId] = Next(before, attacker, barracks?.Id, progress, calendarTick,
                        SettlementCaptureClockStatus.Advancing, null);
                    continue;
                }
                var outcome = Complete(before, settlement, attacker.Value, calendarTick, biologicalTick);
                states[settlementId] = Next(before, attacker, barracks?.Id, before.RequiredTicks,
                    calendarTick, SettlementCaptureClockStatus.Completed, outcome);
                completed++;
            }
            return completed;
        }

        public void RestoreStates(IEnumerable<SettlementCaptureState> saved)
        {
            var values = (saved ?? Enumerable.Empty<SettlementCaptureState>()).ToArray();
            if (values.Any(x => x == null || map.Settlements.All(s => s.Id != x.SettlementId) ||
                !buildings.TryGet(x.MainBuildingId, out _)) ||
                values.Select(x => x.SettlementId).Distinct().Count() != values.Length)
                throw new ArgumentException("Saved settlement capture state is invalid.", nameof(saved));
            states.Clear(); foreach (var state in values) states.Add(state.SettlementId, state);
        }

        public ulong ComputeStateHash()
        {
            var hash = 14695981039346656037UL;
            foreach (var state in States)
            {
                Add(ref hash, state.SettlementId.ToString()); Add(ref hash, state.DefenderSideId.Value);
                Add(ref hash, state.AttackerSideId?.Value ?? string.Empty); Add(ref hash, state.MainBuildingId.ToString());
                Add(ref hash, state.ActiveBarracksId?.ToString() ?? string.Empty); Add(ref hash, state.ProgressTicks.ToString());
                Add(ref hash, ((int)state.Status).ToString()); Add(ref hash, state.Seed.ToString());
                Add(ref hash, state.Outcome?.ResolutionId.ToString() ?? string.Empty);
            }
            return hash;
        }

        private SettlementCaptureOutcomeState Complete(SettlementCaptureState state, SettlementState settlement,
            CombatSideId winner, long calendarTick, long biologicalTick)
        {
            if (state.Outcome != null) return state.Outcome;
            var transfer = settlement.BuildingIds.Where(id => buildings.TryGet(id, out var building) &&
                !building.IsDestroyed && building.OwnerSideId != winner).ToArray();
            if (transfer.Length > 0)
            {
                var result = buildingDamage.CaptureBuildingsAfterOccupation(transfer, winner,
                    DeterministicStableId.Create(state.Seed, "p107_capture_resolution"), calendarTick);
                if (!result.Succeeded) throw new InvalidOperationException("Settlement ownership transfer failed: " + result.Failure);
            }

            var defenderUnits = units.Units.Where(x => x.OwnerSideId.Value == state.DefenderSideId.Value &&
                x.Status != MilitaryUnitStatus.Disbanded).OrderBy(x => x.Id).ToArray();
            var candidates = defenderUnits.SelectMany(x => x.ActiveMemberIds).Distinct().OrderBy(x => x).ToArray();
            var ordered = candidates.Select(x => new { Id = x, Score = SoldierScore(state.Seed, x) })
                .OrderBy(x => x.Score).ThenBy(x => x.Id).ToArray();
            var joinCount = ordered.Length == 0 ? 0 : ordered.Length == 1 ? (ordered[0].Score & 1UL) == 0 ? 1 : 0
                : Math.Max(1, Math.Min(ordered.Length - 1, ordered.Length / 3));
            var joined = new HashSet<StableEntityId>(ordered.Take(joinCount).Select(x => x.Id));
            var executed = new List<StableEntityId>();
            foreach (var unit in defenderUnits)
            {
                foreach (var id in unit.ActiveMemberIds.Where(x => !joined.Contains(x)).ToArray())
                {
                    units.RemoveMember(unit.Id, id);
                    if (characters.TryGet(id, out var person) && person.LifeState == CharacterLifeState.Alive)
                        person.MarkDead(Math.Max(biologicalTick, person.BiologicalBirthTick));
                    executed.Add(id);
                }
                if (units.TryGet(unit.Id, out var remaining) && remaining.ActiveMemberIds.Any(joined.Contains))
                    units.TransferOwnership(unit.Id, new MilitarySideId(winner.Value));
            }
            map.SynchronizeSettlement(settlement.Id, settlement.LocationId, winner,
                settlement.BuildingIds, settlement.ResidentCharacterIds);
            return new SettlementCaptureOutcomeState(winner, state.Seed, joined, executed, transfer,
                settlement.ResidentCharacterIds, calendarTick,
                DeterministicStableId.Create(state.Seed, "p107_capture_outcome"));
        }

        private Building ActiveBarracks(SettlementState settlement) => settlement.BuildingIds
            .Select(id => buildings.TryGet(id, out var value) ? value : null)
            .Where(x => x != null && x.DefinitionId == MilitaryTrainingService.BarracksDefinitionId &&
                x.IsOperational && !x.IsDestroyed).OrderBy(x => x.Id).FirstOrDefault();

        private long CaptureSeed(SettlementId id)
        {
            var value = DeterministicStableId.Create(worldSeed, "p107_capture:" + id).ToString();
            unchecked { long hash = 1469598103934665603L; foreach (var c in value) hash = (hash ^ c) * 1099511628211L; return hash; }
        }

        private static ulong SoldierScore(long seed, StableEntityId id)
        {
            unchecked
            {
                ulong hash = (ulong)seed ^ 14695981039346656037UL;
                foreach (var c in id.ToString()) hash = (hash ^ c) * 1099511628211UL;
                hash ^= hash >> 33; hash *= 0xff51afd7ed558ccdUL; hash ^= hash >> 33; return hash;
            }
        }

        private static SettlementCaptureState Next(SettlementCaptureState prior, CombatSideId? attacker,
            StableEntityId? barracks, long progress, long tick, SettlementCaptureClockStatus status,
            SettlementCaptureOutcomeState outcome) => new SettlementCaptureState(prior.SettlementId,
            prior.DefenderSideId, attacker, prior.MainBuildingId, barracks, progress, prior.RequiredTicks,
            tick, status, prior.Seed, outcome);

        private static void Add(ref ulong hash, string value)
        {
            for (var i = 0; i < value.Length; i++) hash = (hash ^ value[i]) * 1099511628211UL;
            hash = (hash ^ 255UL) * 1099511628211UL;
        }
    }
}
