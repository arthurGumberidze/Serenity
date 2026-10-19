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
        private readonly ConnectedWorldLayout layout;

        public ConnectedWorldService(IEnumerable<WorldLocationState> worldLocations, long initialTick = 0)
            : this(worldLocations, ConnectedWorldContract.LegacyLayout, initialTick)
        {
        }

        public ConnectedWorldService(IEnumerable<WorldLocationState> worldLocations, ConnectedWorldLayout worldLayout,
            long initialTick = 0)
        {
            locations = (worldLocations ?? throw new ArgumentNullException(nameof(worldLocations)))
                .ToDictionary(x => x.Id);
            layout = worldLayout ?? throw new ArgumentNullException(nameof(worldLayout));
            if (!locations.ContainsKey(layout.StarterSettlement))
                throw new ArgumentException("Starter settlement is required.", nameof(worldLocations));
            LastProcessedTick = initialTick;
            RevealAround(locations[layout.StarterSettlement].MapX,
                locations[layout.StarterSettlement].MapY, 80f);
        }

        public ConnectedWorldLayout Layout => layout;
        public long LastProcessedTick { get; private set; }
        public int RevealedCellCount => revealed.Count;
        public IReadOnlyCollection<WorldDiscoveryCell> RevealedCells => revealed;
        public bool IsRevealed(float worldX, float worldZ) => layout.TryGetCell(worldX, worldZ, out var cell) &&
            revealed.Contains(cell);
        public bool IsLocationRevealed(WorldLocationId id) => locations.TryGetValue(id, out var location) &&
            IsRevealed(location.MapX, location.MapY);

        public int RevealAround(float worldX, float worldZ, float radiusMeters)
        {
            if (radiusMeters < 0f) throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            var changed = 0;
            // Presence must reveal its containing cell even when a coarse cell is wider than the reveal radius.
            if (layout.TryGetCell(worldX, worldZ, out var containing) && revealed.Add(containing)) changed++;
            var minX = Math.Max(0, (int)Math.Floor((worldX - radiusMeters - layout.OriginX) /
                layout.DiscoveryCellMeters));
            var maxX = Math.Min(layout.Columns - 1, (int)Math.Floor((worldX + radiusMeters - layout.OriginX) /
                layout.DiscoveryCellMeters));
            var minZ = Math.Max(0, (int)Math.Floor((worldZ - radiusMeters - layout.OriginZ) /
                layout.DiscoveryCellMeters));
            var maxZ = Math.Min(layout.Rows - 1, (int)Math.Floor((worldZ + radiusMeters - layout.OriginZ) /
                layout.DiscoveryCellMeters));
            var radiusSquared = radiusMeters * radiusMeters;
            for (var x = minX; x <= maxX; x++) for (var z = minZ; z <= maxZ; z++)
            {
                var cx = layout.OriginX + (x + 0.5f) * layout.DiscoveryCellMeters;
                var cz = layout.OriginZ + (z + 0.5f) * layout.DiscoveryCellMeters;
                var dx = cx - worldX; var dz = cz - worldZ;
                if (dx * dx + dz * dz <= radiusSquared && revealed.Add(new WorldDiscoveryCell(x, z))) changed++;
            }
            return changed;
        }

        public int RevealAll()
        {
            var changed = 0;
            for (var x = 0; x < layout.Columns; x++)
            for (var z = 0; z < layout.Rows; z++)
                if (revealed.Add(new WorldDiscoveryCell(x, z))) changed++;
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
            layout.GenerationId, revealed, assignments.Values, LastProcessedTick);

        public void RestoreState(ConnectedWorldSaveState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (!string.Equals(state.GenerationId, layout.GenerationId, StringComparison.Ordinal))
                throw new NotSupportedException("Connected-world save generation does not match the active layout.");
            if (state.RevealedCells.Any(cell => !layout.IsValid(cell)))
                throw new InvalidOperationException("Connected-world save contains an out-of-layout discovery cell.");
            if (state.ScoutingAssignments.Any(x => !locations.ContainsKey(x.DestinationId)))
                throw new InvalidOperationException("Scouting save references an unknown connected-world location.");
            revealed.Clear(); foreach (var cell in state.RevealedCells) revealed.Add(cell);
            assignments.Clear(); foreach (var assignment in state.ScoutingAssignments) assignments.Add(assignment.ScoutId, assignment);
            LastProcessedTick = state.LastProcessedTick;
        }
    }
}
