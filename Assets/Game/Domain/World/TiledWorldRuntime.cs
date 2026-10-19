using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    /// <summary>
    /// Pure deterministic height, water and buildability authority for the P10.9 tiled world.
    /// Terrain objects, colliders and strategic meshes are disposable projections of this data.
    /// </summary>
    public sealed class TiledWorldHeightSampler
    {
        public const float TerrainHeightMeters = 96f;
        public const float WaterLevelMeters = 8f;
        public const float RiverHalfWidthMeters = 52f;
        private const float SettlementHeightMeters = 18f;
        private readonly TiledWorldDefinition definition;
        private readonly long seed;

        public TiledWorldHeightSampler(TiledWorldDefinition worldDefinition, long worldSeed)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            seed = worldSeed;
        }

        public TiledWorldDefinition Definition => definition;
        public long WorldSeed => seed;

        public float SampleHeight(float worldX, float worldZ)
        {
            if (!definition.ContainsClosed(worldX, worldZ))
                throw new ArgumentOutOfRangeException(nameof(worldX), "World height samples must remain inside the tiled world.");

            var macro = ValueNoise(worldX / 1800f, worldZ / 1800f, 0) * 9f;
            var medium = ValueNoise(worldX / 620f, worldZ / 620f, 1) * 4f;
            var detail = ValueNoise(worldX / 190f, worldZ / 190f, 2) * 1.4f;
            var height = 18f + macro + medium + detail;

            // Cultivated regions remain naturally rolling, but are deliberately gentle enough for plots and roads.
            foreach (var zone in definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Agriculture)))
            {
                var normalized = NormalizedDistance(zone, worldX, worldZ);
                if (normalized >= 1.12f) continue;
                var blend = Smooth01((1.12f - normalized) / 0.3f) * 0.72f;
                var agriculturalHeight = 18f + ValueNoise(worldX / 950f, worldZ / 950f, 7) * 1.4f;
                height = Lerp(height, agriculturalHeight, blend);
            }

            foreach (var zone in definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Settlement)))
            {
                var normalized = NormalizedDistance(zone, worldX, worldZ);
                if (normalized >= 1.2f) continue;
                var blend = Smooth01((1.2f - normalized) / 0.45f);
                height = Lerp(height, SettlementHeightMeters, blend);
            }

            var riverDistance = DistanceToRiver(worldX, worldZ);
            if (riverDistance < RiverHalfWidthMeters + 38f)
            {
                var blend = Smooth01((RiverHalfWidthMeters + 38f - riverDistance) / 38f);
                height = Lerp(height, WaterLevelMeters - 2.4f, blend);
            }

            foreach (var lake in definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Fishing)))
            {
                var normalized = NormalizedDistance(lake, worldX, worldZ);
                if (normalized >= 1.18f) continue;
                var blend = Smooth01((1.18f - normalized) / 0.28f);
                height = Lerp(height, WaterLevelMeters - 2.1f, blend);
            }

            return Math.Max(0.5f, Math.Min(TerrainHeightMeters - 0.5f, height));
        }

        public bool IsWater(float worldX, float worldZ)
        {
            if (!definition.ContainsClosed(worldX, worldZ)) return false;
            if (DistanceToRiver(worldX, worldZ) <= RiverHalfWidthMeters) return true;
            return definition.Zones.Any(zone => zone.Uses.HasFlag(WorldZoneUse.Fishing) &&
                                                NormalizedDistance(zone, worldX, worldZ) <= 1f);
        }

        public bool IsBuildable(float worldX, float worldZ, float maximumSlopeDegrees = 32f)
        {
            if (!definition.TryGetTile(worldX, worldZ, out _) || maximumSlopeDegrees < 0f ||
                float.IsNaN(maximumSlopeDegrees) || float.IsInfinity(maximumSlopeDegrees) ||
                IsWater(worldX, worldZ)) return false;
            var height = SampleHeight(worldX, worldZ);
            if (height <= WaterLevelMeters + 0.75f) return false;
            const float step = 4f;
            var minX = Math.Max(definition.Origin.X, worldX - step);
            var maxX = Math.Min(definition.Origin.X + definition.WidthMeters, worldX + step);
            var minZ = Math.Max(definition.Origin.Z, worldZ - step);
            var maxZ = Math.Min(definition.Origin.Z + definition.DepthMeters, worldZ + step);
            var dx = (SampleHeight(maxX, worldZ) - SampleHeight(minX, worldZ)) / Math.Max(0.001f, maxX - minX);
            var dz = (SampleHeight(worldX, maxZ) - SampleHeight(worldX, minZ)) / Math.Max(0.001f, maxZ - minZ);
            var slope = Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180d / Math.PI;
            return slope <= maximumSlopeDegrees;
        }

        public float DistanceToRiver(float worldX, float worldZ)
        {
            var best = float.MaxValue;
            for (var i = 1; i < definition.River.Count; i++)
            {
                var a = definition.River[i - 1];
                var b = definition.River[i];
                var abX = b.X - a.X;
                var abZ = b.Z - a.Z;
                var lengthSquared = abX * abX + abZ * abZ;
                var t = lengthSquared <= 0f ? 0f : ((worldX - a.X) * abX + (worldZ - a.Z) * abZ) / lengthSquared;
                t = Math.Max(0f, Math.Min(1f, t));
                var dx = worldX - (a.X + abX * t);
                var dz = worldZ - (a.Z + abZ * t);
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        public float[,] CreateNormalizedHeights(WorldTileId tile, int resolution)
        {
            if (!definition.IsValid(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
            if (resolution < 2 || resolution > 4097) throw new ArgumentOutOfRangeException(nameof(resolution));
            var result = new float[resolution, resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var point = definition.SamplePosition(tile, x, z, resolution);
                result[z, x] = SampleHeight(point.X, point.Z) / TerrainHeightMeters;
            }
            return result;
        }

        private float ValueNoise(float x, float z, int octave)
        {
            var ix = (int)Math.Floor(x);
            var iz = (int)Math.Floor(z);
            var fx = x - ix;
            var fz = z - iz;
            var sx = Smooth01(fx);
            var sz = Smooth01(fz);
            var a = HashSigned(ix, iz, octave);
            var b = HashSigned(ix + 1, iz, octave);
            var c = HashSigned(ix, iz + 1, octave);
            var d = HashSigned(ix + 1, iz + 1, octave);
            return Lerp(Lerp(a, b, sx), Lerp(c, d, sx), sz);
        }

        private float HashSigned(int x, int z, int octave)
        {
            unchecked
            {
                ulong value = (ulong)seed;
                value ^= (ulong)(uint)x * 0x9E3779B185EBCA87UL;
                value ^= (ulong)(uint)z * 0xC2B2AE3D27D4EB4FUL;
                value ^= (ulong)(uint)octave * 0x165667B19E3779F9UL;
                value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27; value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (float)((value & 0xFFFFFFUL) / 8388607.5d - 1d);
            }
        }

        private static float NormalizedDistance(WorldZoneDefinition zone, float x, float z)
        {
            var dx = (x - zone.Center.X) / zone.RadiusX;
            var dz = (z - zone.Center.Z) / zone.RadiusZ;
            return zone.IsEllipse ? (float)Math.Sqrt(dx * dx + dz * dz) : Math.Max(Math.Abs(dx), Math.Abs(dz));
        }

        private static float Smooth01(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - 2f * value);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public sealed class TiledWorldStreamingPlan
    {
        public TiledWorldStreamingPlan(IEnumerable<WorldTileId> visualTiles,
            IEnumerable<WorldTileId> collisionTiles)
        {
            VisualTiles = Array.AsReadOnly((visualTiles ?? throw new ArgumentNullException(nameof(visualTiles)))
                .Distinct().OrderBy(value => value).ToArray());
            CollisionTiles = Array.AsReadOnly((collisionTiles ?? throw new ArgumentNullException(nameof(collisionTiles)))
                .Distinct().OrderBy(value => value).ToArray());
            if (VisualTiles.Any(value => !CollisionTiles.Contains(value)))
                throw new ArgumentException("Every visual tile must retain collision.", nameof(collisionTiles));
        }

        public IReadOnlyList<WorldTileId> VisualTiles { get; }
        public IReadOnlyList<WorldTileId> CollisionTiles { get; }
    }

    /// <summary>Bounded visual set plus actor and command-route collision retention.</summary>
    public sealed class TiledWorldStreamingPlanner
    {
        private readonly TiledWorldDefinition definition;

        public TiledWorldStreamingPlanner(TiledWorldDefinition worldDefinition)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
        }

        public TiledWorldStreamingPlan CreatePlan(WorldPosition cameraFocus,
            IEnumerable<WorldPosition> physicalActors,
            IEnumerable<Tuple<WorldPosition, WorldPosition>> navigationRoutes,
            int visualRadius = 1, int actorRadius = 1, int routePadding = 0)
        {
            var visual = Around(cameraFocus, visualRadius);
            var collision = new SortedSet<WorldTileId>(visual);
            foreach (var actor in physicalActors ?? throw new ArgumentNullException(nameof(physicalActors)))
                foreach (var tile in Around(actor, actorRadius)) collision.Add(tile);
            foreach (var route in navigationRoutes ?? throw new ArgumentNullException(nameof(navigationRoutes)))
            {
                if (route == null) continue;
                foreach (var tile in NavigationCorridor(route.Item1, route.Item2, routePadding)) collision.Add(tile);
            }
            return new TiledWorldStreamingPlan(visual, collision);
        }

        public IReadOnlyList<WorldTileId> NavigationCorridor(WorldPosition start, WorldPosition destination,
            int padding = 0)
        {
            if (padding < 0 || padding > 2) throw new ArgumentOutOfRangeException(nameof(padding));
            if (!definition.TryGetTile(start.X, start.Z, out var from) ||
                !definition.TryGetTile(destination.X, destination.Z, out var to))
                return Array.Empty<WorldTileId>();
            var result = new SortedSet<WorldTileId>();
            var x = from.X;
            var z = from.Z;
            var dx = Math.Abs(to.X - from.X);
            var dz = Math.Abs(to.Z - from.Z);
            var sx = from.X < to.X ? 1 : -1;
            var sz = from.Z < to.Z ? 1 : -1;
            var error = dx - dz;
            while (true)
            {
                AddWithPadding(result, new WorldTileId(x, z), padding);
                if (x == to.X && z == to.Z) break;
                var twice = error * 2;
                if (twice > -dz) { error -= dz; x += sx; }
                if (twice < dx) { error += dx; z += sz; }
            }
            return result.ToArray();
        }

        private IReadOnlyList<WorldTileId> Around(WorldPosition position, int radius)
        {
            if (radius < 0 || radius > 2) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!definition.TryGetTile(position.X, position.Z, out var center)) return Array.Empty<WorldTileId>();
            var result = new SortedSet<WorldTileId>();
            AddWithPadding(result, center, radius);
            return result.ToArray();
        }

        private void AddWithPadding(ISet<WorldTileId> result, WorldTileId center, int padding)
        {
            for (var z = center.Z - padding; z <= center.Z + padding; z++)
            for (var x = center.X - padding; x <= center.X + padding; x++)
            {
                var tile = new WorldTileId(x, z);
                if (definition.IsValid(tile)) result.Add(tile);
            }
        }
    }
}
