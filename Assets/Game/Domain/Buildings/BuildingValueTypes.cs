using System;
using System.Collections.Generic;

namespace Game.Domain.Buildings
{
    public readonly struct BuildingDefinitionId : IEquatable<BuildingDefinitionId>
    {
        private readonly string value;

        public BuildingDefinitionId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 64)
                throw new ArgumentException("Building definition ID must contain 1-64 characters.", nameof(value));
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') &&
                    character != '_' && character != '-')
                    throw new ArgumentException("Building definition ID must use lowercase ASCII, digits, '_' or '-'.", nameof(value));
            }
            this.value = value;
        }

        public bool IsValid => !string.IsNullOrEmpty(value);
        // Authored level variants retain distinct save/catalog IDs but share their facility role.
        public BuildingDefinitionId FamilyId
        {
            get
            {
                var marker = value?.LastIndexOf("_level", StringComparison.Ordinal) ?? -1;
                return marker > 0 && int.TryParse(value.Substring(marker + 6), out var level) && level >= 2
                    ? new BuildingDefinitionId(value.Substring(0, marker)) : this;
            }
        }
        public bool Equals(BuildingDefinitionId other) => string.Equals(value, other.value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BuildingDefinitionId other && Equals(other);
        public override int GetHashCode() => value == null ? 0 : StringComparer.Ordinal.GetHashCode(value);
        public override string ToString() => value ?? string.Empty;
        public static bool operator ==(BuildingDefinitionId left, BuildingDefinitionId right) => left.Equals(right);
        public static bool operator !=(BuildingDefinitionId left, BuildingDefinitionId right) => !left.Equals(right);
    }

    public enum BuildingCategory
    {
        Shelter = 0,
        Storage = 1,
        Structure = 2,
        Furniture = 3,
        Production = 4,
        Food = 5,
        Community = 6,
        Medical = 7,
        Military = 8,
        Infrastructure = 9
    }

    public enum BuildingPlacementMode
    {
        Grid = 0,
        Free = 1,
        Edge = 2
    }

    public enum BuildingEra
    {
        StoneAge = 0,
        BronzeAge = 1,
        IronAge = 2,
        Medieval = 3,
        Industrial = 4,
        Modern = 5,
        Future = 6
    }

    public enum BuildingSpatialForm
    {
        Structure = 0,
        Zone = 1,
        Segment = 2
    }

    [Flags]
    public enum BuildingPlacementConstraint
    {
        None = 0,
        OwnedTerritory = 1 << 0,
        ReachableApproach = 1 << 1,
        Shoreline = 1 << 2,
        Groundwater = 1 << 3
    }

    public enum BuildingOrientation
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public enum ConstructionState
    {
        Planned = 0,
        Completed = 1
    }

    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public GridCoordinate(int x, int z, int level = 0)
        {
            X = x;
            Z = z;
            Level = level;
        }

        public int X { get; }
        public int Z { get; }
        public int Level { get; }
        public bool Equals(GridCoordinate other) => X == other.X && Z == other.Z && Level == other.Level;
        public override bool Equals(object obj) => obj is GridCoordinate other && Equals(other);
        public override int GetHashCode()
        {
            unchecked { return ((X * 397) ^ Z) * 397 ^ Level; }
        }
        public override string ToString() => $"({X},{Z},L{Level})";
        public static bool operator ==(GridCoordinate left, GridCoordinate right) => left.Equals(right);
        public static bool operator !=(GridCoordinate left, GridCoordinate right) => !left.Equals(right);
    }

    public readonly struct BuildingFootprint : IEquatable<BuildingFootprint>
    {
        public BuildingFootprint(int width, int depth)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (depth <= 0) throw new ArgumentOutOfRangeException(nameof(depth));
            Width = width;
            Depth = depth;
        }

        public int Width { get; }
        public int Depth { get; }

        public BuildingFootprint Rotate(BuildingOrientation orientation) =>
            orientation == BuildingOrientation.East || orientation == BuildingOrientation.West
                ? new BuildingFootprint(Depth, Width)
                : this;

        public IEnumerable<GridCoordinate> EnumerateCells(GridCoordinate anchor, BuildingOrientation orientation)
        {
            var rotated = Rotate(orientation);
            for (var x = 0; x < rotated.Width; x++)
            for (var z = 0; z < rotated.Depth; z++)
                yield return new GridCoordinate(anchor.X + x, anchor.Z + z, anchor.Level);
        }

        public bool Equals(BuildingFootprint other) => Width == other.Width && Depth == other.Depth;
        public override bool Equals(object obj) => obj is BuildingFootprint other && Equals(other);
        public override int GetHashCode() => (Width * 397) ^ Depth;
    }

    public readonly struct BuildingGridBounds
    {
        public BuildingGridBounds(int minX, int minZ, int maxX, int maxZ, int minLevel = 0, int maxLevel = 0)
        {
            if (maxX < minX) throw new ArgumentOutOfRangeException(nameof(maxX));
            if (maxZ < minZ) throw new ArgumentOutOfRangeException(nameof(maxZ));
            if (maxLevel < minLevel) throw new ArgumentOutOfRangeException(nameof(maxLevel));
            MinX = minX;
            MinZ = minZ;
            MaxX = maxX;
            MaxZ = maxZ;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
        }

        public int MinX { get; }
        public int MinZ { get; }
        public int MaxX { get; }
        public int MaxZ { get; }
        public int MinLevel { get; }
        public int MaxLevel { get; }
        public bool Contains(GridCoordinate coordinate) => coordinate.X >= MinX && coordinate.X <= MaxX &&
            coordinate.Z >= MinZ && coordinate.Z <= MaxZ && coordinate.Level >= MinLevel && coordinate.Level <= MaxLevel;
    }

    public static class BuildingOrientationExtensions
    {
        public static BuildingOrientation RotateClockwise(this BuildingOrientation orientation) =>
            (BuildingOrientation)(((int)orientation + 1) % 4);

        public static int Degrees(this BuildingOrientation orientation) => (int)orientation * 90;
    }
}
