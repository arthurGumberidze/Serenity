using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain;
using Game.Domain.World;

namespace Game.Simulation.World
{
    /// <summary>Canonical fog and remote-scouting state for the single physical P10 world.</summary>
    public sealed class ConnectedWorldService
    {
        private readonly Dictionary<WorldLocationId, WorldLocationState> locations;
        private readonly HashSet<WorldDiscoveryCell> revealed = new HashSet<WorldDiscoveryCell>();
        private readonly Dictionary<StableEntityId, ScoutingAssignmentState> assignments =
            new Dictionary<StableEntityId, ScoutingAssignmentState>();

        public ConnectedWorldService(IEnumerable<WorldLocationState> worldLocations, long initialTick = 0)
        {
            locations = (worldLocations ?? throw new ArgumentNullException(nameof(worldLocations)))
                .ToDictionary(x => x.Id);
            if (!locations.ContainsKey(ConnectedWorldContract.StarterSettlement))
                throw new ArgumentException("Starter settlement is required.", nameof(worldLocations));
            LastProcessedTick = initialTick;
            RevealAround(locations[ConnectedWorldContract.StarterSettlement].MapX,
                locations[ConnectedWorldContract.StarterSettlement].MapY, 80f);
        }

        public long LastProcessedTick { get; private set; }
        public int RevealedCellCount => revealed.Count;
        public IReadOnlyCollection<WorldDiscoveryCell> RevealedCells => revealed;
        public bool IsRevealed(float worldX, float worldZ) => revealed.Contains(ToCell(worldX, worldZ));
        public bool IsLocationRevealed(WorldLocationId id) => locations.TryGetValue(id, out var location) &&
            IsRevealed(location.MapX, location.MapY);

        public int RevealAround(float worldX, float worldZ, float radiusMeters)
        {
            if (radiusMeters < 0f) throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            var minX = Math.Max(0, (int)Math.Floor((worldX - radiusMeters - ConnectedWorldContract.OriginMeters) /
                ConnectedWorldContract.DiscoveryCellMeters));
            var maxX = Math.Min(31, (int)Math.Floor((worldX + radiusMeters - ConnectedWorldContract.OriginMeters) /
                ConnectedWorldContract.DiscoveryCellMeters));
            var minZ = Math.Max(0, (int)Math.Floor((worldZ - radiusMeters - ConnectedWorldContract.OriginMeters) /
                ConnectedWorldContract.DiscoveryCellMeters));
            var maxZ = Math.Min(31, (int)Math.Floor((worldZ + radiusMeters - ConnectedWorldContract.OriginMeters) /
                ConnectedWorldContract.DiscoveryCellMeters));
            var changed = 0;
            var radiusSquared = radiusMeters * radiusMeters;
            for (var x = minX; x <= maxX; x++) for (var z = minZ; z <= maxZ; z++)
            {
                var cx = ConnectedWorldContract.OriginMeters + (x + 0.5f) * ConnectedWorldContract.DiscoveryCellMeters;
                var cz = ConnectedWorldContract.OriginMeters + (z + 0.5f) * ConnectedWorldContract.DiscoveryCellMeters;
                var dx = cx - worldX; var dz = cz - worldZ;
                if (dx * dx + dz * dz <= radiusSquared && revealed.Add(new WorldDiscoveryCell(x, z))) changed++;
            }
            return changed;
        }

        public void ReportPhysicalPresence(float worldX, float worldZ) => RevealAround(worldX, worldZ, 32f);

        public ScoutingAssignmentState AssignScout(StableEntityId scoutId, WorldLocationId destinationId,
            long assignedTick, long durationTicks)
        {
            if (!scoutId.IsValid || !locations.ContainsKey(destinationId)) throw new ArgumentException("Known scout and destination required.");
            if (assignedTick < LastProcessedTick || durationTicks <= 0) throw new ArgumentOutOfRangeException(nameof(durationTicks));
            var state = new ScoutingAssignmentState(scoutId, destinationId, assignedTick,
                checked(assignedTick + durationTicks), false);
            assignments[scoutId] = state;
            return state;
        }

        public void AdvanceTo(long calendarTick)
        {
            if (calendarTick < LastProcessedTick) throw new ArgumentOutOfRangeException(nameof(calendarTick));
            foreach (var pair in assignments.ToArray())
            {
                var state = pair.Value;
                if (state.Completed || state.CompletionTick > calendarTick) continue;
                var destination = locations[state.DestinationId];
                RevealAround(destination.MapX, destination.MapY, 56f);
                assignments[pair.Key] = new ScoutingAssignmentState(state.ScoutId, state.DestinationId,
                    state.AssignedTick, state.CompletionTick, true);
            }
            LastProcessedTick = calendarTick;
        }

        public ConnectedWorldSaveState CaptureState() => new ConnectedWorldSaveState(
            ConnectedWorldContract.GenerationId, revealed, assignments.Values, LastProcessedTick);

        public void RestoreState(ConnectedWorldSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.ScoutingAssignments.Any(x => !locations.ContainsKey(x.DestinationId)))
                throw new InvalidOperationException("Scouting save references an unknown connected-world location.");
            revealed.Clear(); foreach (var cell in state.RevealedCells) revealed.Add(cell);
            assignments.Clear(); foreach (var assignment in state.ScoutingAssignments) assignments.Add(assignment.ScoutId, assignment);
            LastProcessedTick = state.LastProcessedTick;
        }

        private static WorldDiscoveryCell ToCell(float worldX, float worldZ) => new WorldDiscoveryCell(
            Math.Max(0, Math.Min(31, (int)Math.Floor((worldX - ConnectedWorldContract.OriginMeters) /
                ConnectedWorldContract.DiscoveryCellMeters))),
            Math.Max(0, Math.Min(31, (int)Math.Floor((worldZ - ConnectedWorldContract.OriginMeters) /
                ConnectedWorldContract.DiscoveryCellMeters))));
    }
}
