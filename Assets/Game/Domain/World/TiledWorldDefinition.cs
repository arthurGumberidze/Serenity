using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    public readonly struct WorldTileId : IEquatable<WorldTileId>, IComparable<WorldTileId>
    {
        public WorldTileId(int x, int z) { X = x; Z = z; }
        public int X { get; }
        public int Z { get; }
        public bool Equals(WorldTileId other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldTileId other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Z);
        public int CompareTo(WorldTileId other) => Z != other.Z ? Z.CompareTo(other.Z) : X.CompareTo(other.X);
        public override string ToString() => $"{X:D2}-{Z:D2}";
    }

    [Flags]
    public enum WorldZoneUse
    {
        None = 0, Agriculture = 1, Hunting = 2, Berries = 4, Settlement = 8,
        Forest = 16, Ore = 32, Fishing = 64
    }

    public sealed class WorldZoneDefinition
    {
        public WorldZoneDefinition(string id, WorldZoneUse uses, float x, float z,
            float radiusX, float radiusZ, bool ellipse)
        {
            if (string.IsNullOrWhiteSpace(id) || uses == WorldZoneUse.None || ((int)uses & ~127) != 0)
                throw new ArgumentException("A stable zone ID and known uses are required.");
            Center = new WorldPosition(x, 0f, z);
            if (float.IsNaN(radiusX) || float.IsInfinity(radiusX) || radiusX <= 0f ||
                float.IsNaN(radiusZ) || float.IsInfinity(radiusZ) || radiusZ <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radiusX));
            Id = id; Uses = uses; RadiusX = radiusX; RadiusZ = radiusZ; IsEllipse = ellipse;
        }
        public string Id { get; }
        public WorldZoneUse Uses { get; }
        public WorldPosition Center { get; }
        public float RadiusX { get; }
        public float RadiusZ { get; }
        public bool IsEllipse { get; }
        public bool Contains(float x, float z)
        {
            var dx = (x - Center.X) / RadiusX;
            var dz = (z - Center.Z) / RadiusZ;
            return IsEllipse ? dx * dx + dz * dz <= 1f : Math.Abs(dx) <= 1f && Math.Abs(dz) <= 1f;
        }
    }

    /// <summary>Immutable geography configuration. No loaded Terrain, entity or mutable save-state.</summary>
    public sealed class TiledWorldDefinition
    {
        public const string ReferenceGenerationId = "p109-worksite-v2";
        public TiledWorldDefinition(string generationId, int columns, int rows, int tileSizeMeters,
            float originX, float originZ, IEnumerable<WorldZoneDefinition> zones,
            IEnumerable<WorldPosition> river, WorldPosition playerStart)
        {
            if (string.IsNullOrWhiteSpace(generationId)) throw new ArgumentException(nameof(generationId));
            if (columns <= 0 || rows <= 0 || tileSizeMeters <= 0 || columns > 128 || rows > 128)
                throw new ArgumentOutOfRangeException(nameof(columns));
            GenerationId = generationId; Columns = columns; Rows = rows; TileSizeMeters = tileSizeMeters;
            Origin = new WorldPosition(originX, 0f, originZ);
            var zoneArray = (zones ?? throw new ArgumentNullException(nameof(zones))).ToArray();
            if (zoneArray.Any(x => x == null) || zoneArray.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != zoneArray.Length)
                throw new ArgumentException("Zone identities must be non-null and unique.", nameof(zones));
            Zones = Array.AsReadOnly(zoneArray);
            var points = (river ?? throw new ArgumentNullException(nameof(river))).ToArray();
            if (points.Length < 2 || points.Any(x => !ContainsClosed(x.X, x.Z)))
                throw new ArgumentException("The river needs at least two in-bounds points.", nameof(river));
            River = Array.AsReadOnly(points);
            if (!TryGetTile(playerStart.X, playerStart.Z, out _)) throw new ArgumentOutOfRangeException(nameof(playerStart));
            PlayerStart = playerStart;
            Tiles = Array.AsReadOnly(Enumerable.Range(0, rows).SelectMany(z =>
                Enumerable.Range(0, columns).Select(x => new WorldTileId(x, z))).ToArray());
        }
        public string GenerationId { get; }
        public int Columns { get; }
        public int Rows { get; }
        public int TileSizeMeters { get; }
        public int WidthMeters => checked(Columns * TileSizeMeters);
        public int DepthMeters => checked(Rows * TileSizeMeters);
        public long AreaSquareMeters => (long)WidthMeters * DepthMeters;
        public WorldPosition Origin { get; }
        public WorldPosition PlayerStart { get; }
        public IReadOnlyList<WorldTileId> Tiles { get; }
        public IReadOnlyList<WorldZoneDefinition> Zones { get; }
        public IReadOnlyList<WorldPosition> River { get; }

        public bool IsValid(WorldTileId tile) => tile.X >= 0 && tile.X < Columns && tile.Z >= 0 && tile.Z < Rows;
        public bool ContainsClosed(float x, float z) => !float.IsNaN(x) && !float.IsInfinity(x) &&
            !float.IsNaN(z) && !float.IsInfinity(z) && x >= Origin.X && z >= Origin.Z &&
            x <= Origin.X + WidthMeters && z <= Origin.Z + DepthMeters;
        public bool TryGetTile(float x, float z, out WorldTileId tile)
        {
            tile = default;
            // Half-open bounds assign an internal border to exactly one tile, never silently clamp outside land.
            if (float.IsNaN(x) || float.IsNaN(z) || x < Origin.X || z < Origin.Z ||
                x >= Origin.X + WidthMeters || z >= Origin.Z + DepthMeters) return false;
            tile = new WorldTileId((int)Math.Floor((x - Origin.X) / TileSizeMeters),
                (int)Math.Floor((z - Origin.Z) / TileSizeMeters));
            return IsValid(tile);
        }
        public WorldPosition TileOrigin(WorldTileId tile)
        {
            if (!IsValid(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
            return new WorldPosition(Origin.X + tile.X * TileSizeMeters, 0f, Origin.Z + tile.Z * TileSizeMeters);
        }
        public WorldPosition SamplePosition(WorldTileId tile, int x, int z, int resolution)
        {
            if (resolution < 2 || x < 0 || z < 0 || x >= resolution || z >= resolution)
                throw new ArgumentOutOfRangeException(nameof(resolution));
            TileOrigin(tile); // Validate before deriving the global integer lattice.
            var intervals = resolution - 1;
            return new WorldPosition((float)(Origin.X + ((long)tile.X * intervals + x) * (double)TileSizeMeters / intervals),
                0f, (float)(Origin.Z + ((long)tile.Z * intervals + z) * (double)TileSizeMeters / intervals));
        }
        public WorldZoneUse UsesAt(float x, float z)
        {
            if (!TryGetTile(x, z, out _)) return WorldZoneUse.None;
            return Zones.Where(zone => zone.Contains(x, z)).Aggregate(WorldZoneUse.None, (value, zone) => value | zone.Uses);
        }
        public IReadOnlyList<WorldTileId> RequiredTiles(IEnumerable<WorldPosition> physicalPositions,
            WorldPosition cameraFocus, int radius = 1)
        {
            if (radius < 0 || radius > Math.Max(Columns, Rows)) throw new ArgumentOutOfRangeException(nameof(radius));
            var required = new SortedSet<WorldTileId>();
            foreach (var position in (physicalPositions ?? throw new ArgumentNullException(nameof(physicalPositions))).Append(cameraFocus))
            {
                if (!TryGetTile(position.X, position.Z, out var center)) continue;
                for (var z = center.Z - radius; z <= center.Z + radius; z++)
                for (var x = center.X - radius; x <= center.X + radius; x++)
                {
                    var tile = new WorldTileId(x, z);
                    if (IsValid(tile)) required.Add(tile);
                }
            }
            return required.ToArray();
        }
    }
}
