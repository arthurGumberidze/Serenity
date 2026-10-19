using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Domain.World
{
    public static class ConnectedWorldContract
    {
        public const string GenerationId = "p10-connected-world-v1";
        public const int SizeMeters = 512;
        public const int OriginMeters = -256;
        public const int DiscoveryCellMeters = 16;
        public const int AuditSampleMeters = 2;
        public const int MinimumAccessibleAreaSquareMeters = 163840;

        public static readonly WorldLocationId StarterSettlement = new WorldLocationId("starter_settlement");
        public static readonly string[] PreservedLocationIds =
        {
            "starter_settlement", "enemy_settlement", "north_forest", "river_ford", "east_hills", "south_field"
        };

        public static ConnectedWorldLayout LegacyLayout => new ConnectedWorldLayout(GenerationId,
            OriginMeters, OriginMeters, SizeMeters, SizeMeters, DiscoveryCellMeters, StarterSettlement);
    }

    public sealed class ConnectedWorldLayout
    {
        public ConnectedWorldLayout(string generationId, float originX, float originZ, int widthMeters,
            int depthMeters, int discoveryCellMeters, WorldLocationId starterSettlement)
        {
            if (string.IsNullOrWhiteSpace(generationId)) throw new ArgumentException(nameof(generationId));
            if (float.IsNaN(originX) || float.IsInfinity(originX) || float.IsNaN(originZ) || float.IsInfinity(originZ))
                throw new ArgumentOutOfRangeException(nameof(originX));
            if (widthMeters <= 0 || depthMeters <= 0 || discoveryCellMeters <= 0 ||
                widthMeters % discoveryCellMeters != 0 || depthMeters % discoveryCellMeters != 0)
                throw new ArgumentOutOfRangeException(nameof(discoveryCellMeters));
            if (!starterSettlement.IsValid) throw new ArgumentException(nameof(starterSettlement));
            GenerationId = generationId;
            OriginX = originX;
            OriginZ = originZ;
            WidthMeters = widthMeters;
            DepthMeters = depthMeters;
            DiscoveryCellMeters = discoveryCellMeters;
            StarterSettlement = starterSettlement;
        }

        public string GenerationId { get; }
        public float OriginX { get; }
        public float OriginZ { get; }
        public int WidthMeters { get; }
        public int DepthMeters { get; }
        public int DiscoveryCellMeters { get; }
        public int Columns => WidthMeters / DiscoveryCellMeters;
        public int Rows => DepthMeters / DiscoveryCellMeters;
        public WorldLocationId StarterSettlement { get; }

        public bool TryGetCell(float worldX, float worldZ, out WorldDiscoveryCell cell)
        {
            cell = default;
            if (float.IsNaN(worldX) || float.IsInfinity(worldX) || float.IsNaN(worldZ) || float.IsInfinity(worldZ) ||
                worldX < OriginX || worldZ < OriginZ || worldX >= OriginX + WidthMeters ||
                worldZ >= OriginZ + DepthMeters) return false;
            cell = new WorldDiscoveryCell((int)Math.Floor((worldX - OriginX) / DiscoveryCellMeters),
                (int)Math.Floor((worldZ - OriginZ) / DiscoveryCellMeters));
            return true;
        }

        public bool IsValid(WorldDiscoveryCell cell) => cell.X >= 0 && cell.X < Columns &&
            cell.Z >= 0 && cell.Z < Rows;

        public static ConnectedWorldLayout For(TiledWorldDefinition definition, int discoveryCellMeters = 250)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return new ConnectedWorldLayout(definition.GenerationId, definition.Origin.X, definition.Origin.Z,
                definition.WidthMeters, definition.DepthMeters, discoveryCellMeters,
                ConnectedWorldContract.StarterSettlement);
        }
    }

    public readonly struct WorldDiscoveryCell : IEquatable<WorldDiscoveryCell>, IComparable<WorldDiscoveryCell>
    {
        public WorldDiscoveryCell(int x, int z) { X = x; Z = z; }
        public int X { get; }
        public int Z { get; }
        public int CompareTo(WorldDiscoveryCell other) { var x = X.CompareTo(other.X); return x != 0 ? x : Z.CompareTo(other.Z); }
        public bool Equals(WorldDiscoveryCell other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldDiscoveryCell other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Z);
        public override string ToString() => X + "," + Z;
    }

    public sealed class ScoutingAssignmentState
    {
        public ScoutingAssignmentState(StableEntityId scoutId, WorldLocationId destinationId,
            long assignedTick, long completionTick, bool completed)
        {
            if (!scoutId.IsValid || !destinationId.IsValid) throw new ArgumentException("Scouting identity is required.");
            if (assignedTick < 0 || completionTick <= assignedTick) throw new ArgumentOutOfRangeException(nameof(completionTick));
            ScoutId = scoutId;
            DestinationId = destinationId;
            AssignedTick = assignedTick;
            CompletionTick = completionTick;
            Completed = completed;
        }
        public StableEntityId ScoutId { get; }
        public WorldLocationId DestinationId { get; }
        public long AssignedTick { get; }
        public long CompletionTick { get; }
        public bool Completed { get; }
    }

    public sealed class ConnectedWorldSaveState
    {
        public ConnectedWorldSaveState(string generationId, IEnumerable<WorldDiscoveryCell> revealedCells,
            IEnumerable<ScoutingAssignmentState> scoutingAssignments, long lastProcessedTick)
        {
            if (!string.Equals(generationId, ConnectedWorldContract.GenerationId, StringComparison.Ordinal) &&
                !string.Equals(generationId, TiledWorldDefinition.ReferenceGenerationId, StringComparison.Ordinal))
                throw new ArgumentException("Unknown connected-world generation.", nameof(generationId));
            if (lastProcessedTick < 0) throw new ArgumentOutOfRangeException(nameof(lastProcessedTick));
            var cells = (revealedCells ?? throw new ArgumentNullException(nameof(revealedCells))).Distinct().OrderBy(x => x).ToArray();
            var scouts = (scoutingAssignments ?? Enumerable.Empty<ScoutingAssignmentState>()).ToArray();
            if (scouts.Any(x => x == null || x.CompletionTick > lastProcessedTick && x.Completed) ||
                scouts.Select(x => x.ScoutId).Distinct().Count() != scouts.Length)
                throw new ArgumentException("Scouting assignments are invalid.", nameof(scoutingAssignments));
            GenerationId = generationId;
            RevealedCells = Array.AsReadOnly(cells);
            ScoutingAssignments = Array.AsReadOnly(scouts.OrderBy(x => x.ScoutId).ToArray());
            LastProcessedTick = lastProcessedTick;
        }
        public string GenerationId { get; }
        public IReadOnlyList<WorldDiscoveryCell> RevealedCells { get; }
        public IReadOnlyList<ScoutingAssignmentState> ScoutingAssignments { get; }
        public long LastProcessedTick { get; }
    }
}
