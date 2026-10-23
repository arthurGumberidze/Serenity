using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    public enum WorldMapPlacementVerdict
    {
        Green = 0,
        Amber = 1,
        Red = 2
    }

    public readonly struct WorldMapFootprintCell
    {
        public WorldMapFootprintCell(float worldX, float worldZ, float halfExtentMeters)
        {
            if (!WorldMapPoint.IsFinite(worldX) || !WorldMapPoint.IsFinite(worldZ) ||
                !WorldMapPoint.IsFinite(halfExtentMeters) || halfExtentMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(worldX));
            WorldX = worldX;
            WorldZ = worldZ;
            HalfExtentMeters = halfExtentMeters;
        }

        public float WorldX { get; }
        public float WorldZ { get; }
        public float HalfExtentMeters { get; }
    }

    public readonly struct WorldMapBuildabilityResult
    {
        public WorldMapBuildabilityResult(WorldMapPlacementVerdict verdict, string reason,
            IEnumerable<string> zoneIds, float maximumSlopeDegrees)
        {
            if (!Enum.IsDefined(typeof(WorldMapPlacementVerdict), verdict))
                throw new ArgumentOutOfRangeException(nameof(verdict));
            Verdict = verdict;
            Reason = reason ?? string.Empty;
            ZoneIds = Array.AsReadOnly((zoneIds ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray());
            MaximumSlopeDegrees = maximumSlopeDegrees;
        }

        public WorldMapPlacementVerdict Verdict { get; }
        public string Reason { get; }
        public IReadOnlyList<string> ZoneIds { get; }
        public float MaximumSlopeDegrees { get; }
        public bool CanPlace => Verdict != WorldMapPlacementVerdict.Red;
    }

    /// <summary>
    /// Pure WM12 world-surface policy. It consumes authored masks, hydrology, height and POI definitions;
    /// it neither reserves the building grid nor clears a physical blocker.
    /// </summary>
    public sealed class WorldMapBuildabilityEvaluator
    {
        public const string OutsideWorld = "outside_world";
        public const string Water = "water";
        public const string RestrictedMask = "restricted_mask";
        public const string ExcessiveSlope = "excessive_slope";
        public const string ProtectedPoi = "protected_poi";
        public const string ClearableBlockers = "clearable_blockers";
        public const string HardBlocker = "hard_blocker";

        private readonly TiledWorldDefinition world;
        private readonly TiledWorldHeightSampler heights;
        private readonly WorldMapDataDefinition map;
        private readonly WorldMapBuildabilityDefinition definition;

        public WorldMapBuildabilityEvaluator(TiledWorldDefinition worldDefinition,
            TiledWorldHeightSampler heightSampler)
        {
            world = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(world, heights.Definition))
                throw new ArgumentException("Height sampler must use the evaluated world.", nameof(heightSampler));
            map = world.MapData ?? throw new ArgumentException(
                "World-map buildability requires authored map data.", nameof(worldDefinition));
            definition = map.Buildability ?? throw new ArgumentException(
                "World-map buildability policy is missing.", nameof(worldDefinition));
        }

        public WorldMapBuildabilityDefinition Definition => definition;

        public WorldMapBuildabilityResult EvaluateFootprint(IEnumerable<WorldMapFootprintCell> footprint)
        {
            var cells = (footprint ?? throw new ArgumentNullException(nameof(footprint)))
                .OrderBy(value => value.WorldZ).ThenBy(value => value.WorldX).ToArray();
            if (cells.Length == 0) throw new ArgumentException("A footprint needs at least one cell.", nameof(footprint));

            var zones = new HashSet<string>(StringComparer.Ordinal);
            var maximumSlope = WorldMapBuildabilityDefinition.OrdinaryBuildingMaximumSlopeDegrees;
            var failures = new SortedSet<Failure>(FailureComparer.Instance);
            foreach (var cell in cells)
            {
                if (!world.TryGetTile(cell.WorldX, cell.WorldZ, out _) ||
                    !world.ContainsClosed(cell.WorldX - cell.HalfExtentMeters, cell.WorldZ - cell.HalfExtentMeters) ||
                    !world.ContainsClosed(cell.WorldX + cell.HalfExtentMeters, cell.WorldZ + cell.HalfExtentMeters))
                {
                    failures.Add(new Failure(0, OutsideWorld));
                    continue;
                }

                foreach (var poi in map.PointsOfInterest)
                    if (OverlapsCircle(cell, map.CoordinateTransform.ToUnity(poi.Position),
                            poi.ProtectionRadiusMeters))
                        failures.Add(new Failure(1, ProtectedPoi + ":" + poi.Id));

                for (var z = -1; z <= 1; z++)
                for (var x = -1; x <= 1; x++)
                {
                    var worldX = cell.WorldX + x * cell.HalfExtentMeters;
                    var worldZ = cell.WorldZ + z * cell.HalfExtentMeters;
                    if (!world.ContainsClosed(worldX, worldZ))
                    {
                        failures.Add(new Failure(0, OutsideWorld));
                        continue;
                    }
                    if (heights.IsWater(worldX, worldZ) ||
                        heights.SampleHeight(worldX, worldZ) <= map.Terrain.WaterLevelMeters + 0.75f)
                        failures.Add(new Failure(2, Water));

                    var design = map.CoordinateTransform.ToDesign(new WorldPosition(worldX, 0f, worldZ));
                    var matching = definition.Zones.Where(value => value.Shape.Contains(design))
                        .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
                    foreach (var mask in matching)
                    {
                        zones.Add(mask.Id);
                        if (string.Equals(mask.Category, "restricted", StringComparison.Ordinal))
                            failures.Add(new Failure(3, RestrictedMask + ":" + mask.Id));
                    }
                    var pointSlope = matching.Where(value => !string.Equals(value.Category, "restricted",
                            StringComparison.Ordinal) && value.SlopeMaxDegrees > 0f)
                        .Select(value => value.SlopeMaxDegrees)
                        .DefaultIfEmpty(WorldMapBuildabilityDefinition.OrdinaryBuildingMaximumSlopeDegrees).Min();
                    maximumSlope = Math.Min(maximumSlope, pointSlope);
                    if (heights.SampleSlopeDegrees(worldX, worldZ) > pointSlope + 0.001f)
                        failures.Add(new Failure(4, ExcessiveSlope));
                }
            }

            var failure = failures.FirstOrDefault();
            return failures.Count > 0
                ? new WorldMapBuildabilityResult(WorldMapPlacementVerdict.Red, failure.Reason, zones, maximumSlope)
                : new WorldMapBuildabilityResult(WorldMapPlacementVerdict.Green, string.Empty, zones, maximumSlope);
        }

        public WorldMapBuildabilityResult ClassifyBlockers(IEnumerable<string> blockerKinds)
        {
            var blockers = (blockerKinds ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (blockers.Length == 0)
                return new WorldMapBuildabilityResult(WorldMapPlacementVerdict.Green, string.Empty,
                    Array.Empty<string>(), WorldMapBuildabilityDefinition.OrdinaryBuildingMaximumSlopeDegrees);
            if (blockers.Any(value => definition.IsHardBlocker(value) || !definition.IsClearableBlocker(value)))
                return new WorldMapBuildabilityResult(WorldMapPlacementVerdict.Red, HardBlocker,
                    Array.Empty<string>(), WorldMapBuildabilityDefinition.OrdinaryBuildingMaximumSlopeDegrees);
            return new WorldMapBuildabilityResult(WorldMapPlacementVerdict.Amber, ClearableBlockers,
                Array.Empty<string>(), WorldMapBuildabilityDefinition.OrdinaryBuildingMaximumSlopeDegrees);
        }

        private static bool OverlapsCircle(WorldMapFootprintCell cell, WorldPosition circle,
            float radiusMeters)
        {
            var dx = Math.Max(Math.Abs(circle.X - cell.WorldX) - cell.HalfExtentMeters, 0f);
            var dz = Math.Max(Math.Abs(circle.Z - cell.WorldZ) - cell.HalfExtentMeters, 0f);
            return dx * dx + dz * dz <= radiusMeters * radiusMeters;
        }

        private readonly struct Failure
        {
            public Failure(int priority, string reason) { Priority = priority; Reason = reason; }
            public int Priority { get; }
            public string Reason { get; }
        }

        private sealed class FailureComparer : IComparer<Failure>
        {
            public static readonly FailureComparer Instance = new FailureComparer();
            public int Compare(Failure left, Failure right)
            {
                var priority = left.Priority.CompareTo(right.Priority);
                return priority != 0 ? priority : string.Compare(left.Reason, right.Reason, StringComparison.Ordinal);
            }
        }
    }
}
