using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    public enum WorldMapVegetationKind
    {
        InteractiveTree,
        DecorativeTreeOrShrub,
        GroundCover
    }

    /// <summary>Immutable seeded placement. Presentation may stream it; later ecology may bind interactive IDs.</summary>
    public sealed class WorldMapVegetationInstance
    {
        public WorldMapVegetationInstance(StableEntityId stableId, WorldMapVegetationKind kind,
            string zoneId, string classId, WorldTileId tile, WorldPosition position,
            float rotationDegrees, float uniformScale)
        {
            if (!stableId.IsValid || string.IsNullOrWhiteSpace(zoneId) || string.IsNullOrWhiteSpace(classId) ||
                !WorldMapPoint.IsFinite(rotationDegrees) || !WorldMapPoint.IsFinite(uniformScale) ||
                uniformScale <= 0f)
                throw new ArgumentException(nameof(stableId));
            StableId = stableId;
            Kind = kind;
            ZoneId = zoneId;
            ClassId = classId;
            Tile = tile;
            Position = position;
            RotationDegrees = rotationDegrees;
            UniformScale = uniformScale;
        }

        public StableEntityId StableId { get; }
        public WorldMapVegetationKind Kind { get; }
        public string ZoneId { get; }
        public string ClassId { get; }
        public WorldTileId Tile { get; }
        public WorldPosition Position { get; }
        public float RotationDegrees { get; }
        public float UniformScale { get; }
    }

    public sealed class WorldMapVegetationPlan
    {
        private readonly IReadOnlyDictionary<WorldTileId, IReadOnlyList<WorldMapVegetationInstance>> byTile;

        public WorldMapVegetationPlan(IEnumerable<WorldMapVegetationInstance> instances)
        {
            var values = (instances ?? throw new ArgumentNullException(nameof(instances)))
                .OrderBy(value => value.StableId).ToArray();
            if (values.Any(value => value == null) || values.Select(value => value.StableId).Distinct().Count() !=
                values.Length)
                throw new ArgumentException("Vegetation instances require unique stable IDs.", nameof(instances));
            Instances = Array.AsReadOnly(values);
            byTile = values.GroupBy(value => value.Tile).ToDictionary(group => group.Key,
                group => (IReadOnlyList<WorldMapVegetationInstance>)Array.AsReadOnly(group.ToArray()));
        }

        public IReadOnlyList<WorldMapVegetationInstance> Instances { get; }
        public int InteractiveTreeCount => Instances.Count(value => value.Kind == WorldMapVegetationKind.InteractiveTree);
        public int DecorativeTreeAndShrubCount => Instances.Count(value =>
            value.Kind == WorldMapVegetationKind.DecorativeTreeOrShrub);
        public int GroundCoverCount => Instances.Count(value => value.Kind == WorldMapVegetationKind.GroundCover);
        public IReadOnlyList<WorldMapVegetationInstance> ForTile(WorldTileId tile) =>
            byTile.TryGetValue(tile, out var values) ? values : Array.Empty<WorldMapVegetationInstance>();

        public IReadOnlyList<WorldMapVegetationInstance> SelectInteractiveColliders(
            IEnumerable<WorldTileId> visualTiles, WorldPosition focus, float radiusMeters, int maximum)
        {
            if (visualTiles == null) throw new ArgumentNullException(nameof(visualTiles));
            if (!WorldMapPoint.IsFinite(radiusMeters) || radiusMeters <= 0f || maximum < 0)
                throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            var tiles = new HashSet<WorldTileId>(visualTiles);
            var radiusSquared = radiusMeters * radiusMeters;
            return Array.AsReadOnly(Instances.Where(value =>
                    value.Kind == WorldMapVegetationKind.InteractiveTree && tiles.Contains(value.Tile) &&
                    SquaredDistance(value.Position, focus) <= radiusSquared)
                .OrderBy(value => SquaredDistance(value.Position, focus)).ThenBy(value => value.StableId)
                .Take(maximum).ToArray());
        }

        public ulong CalculateHash()
        {
            ulong hash = 14695981039346656037UL;
            foreach (var value in Instances)
            {
                Add(ref hash, value.StableId.ToString());
                Add(ref hash, value.ZoneId);
                Add(ref hash, value.ClassId);
                Add(ref hash, ((int)value.Kind).ToString());
                Add(ref hash, BitConverter.SingleToInt32Bits(value.Position.X).ToString());
                Add(ref hash, BitConverter.SingleToInt32Bits(value.Position.Z).ToString());
            }
            return hash;
        }

        private static void Add(ref ulong hash, string value)
        {
            foreach (var character in value) hash = (hash ^ character) * 1099511628211UL;
            hash = (hash ^ 255UL) * 1099511628211UL;
        }

        private static float SquaredDistance(WorldPosition a, WorldPosition b)
        {
            var x = a.X - b.X;
            var z = a.Z - b.Z;
            return x * x + z * z;
        }
    }

    /// <summary>
    /// Pure global scatter. It evaluates the WM05 masks once, so tile load order cannot move an instance.
    /// </summary>
    public sealed class WorldMapVegetationPlanner
    {
        private const float DensityChoice = 0.86f;
        private readonly TiledWorldDefinition world;
        private readonly TiledWorldHeightSampler heights;
        private readonly WorldMapTerrainPaintSampler paint;
        private readonly WorldMapVegetationDefinition definition;
        private readonly long seed;
        private readonly Dictionary<long, List<WorldMapPoint>> interactiveGrid =
            new Dictionary<long, List<WorldMapPoint>>();

        public WorldMapVegetationPlanner(TiledWorldDefinition worldDefinition,
            TiledWorldHeightSampler heightSampler, long worldSeed)
        {
            world = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(world, heights.Definition) || world.MapData?.Vegetation == null)
                throw new ArgumentException("World Map V2 vegetation requires its matching height sampler.");
            definition = world.MapData.Vegetation;
            seed = worldSeed;
            paint = new WorldMapTerrainPaintSampler(world, heights);
        }

        public WorldMapVegetationPlan Create()
        {
            interactiveGrid.Clear();
            var result = new List<WorldMapVegetationInstance>();
            var areas = definition.Zones.ToDictionary(value => value, EstimateUsableArea);
            var interactiveDensity = definition.Zones.ToDictionary(value => value, value =>
                definition.Class(value.ClassId).InteractiveTreesPerHectare.Lerp(DensityChoice) *
                value.DensityMultiplier);
            var decorativeDensity = definition.Zones.ToDictionary(value => value, value =>
                definition.Class(value.ClassId).DecorativeInstancesPerHectare.Lerp(DensityChoice) *
                value.DensityMultiplier);
            var interactiveTargets = AllocateTargets(areas, interactiveDensity,
                definition.Budget.InteractiveTrees);
            var decorativeTargets = AllocateTargets(areas, decorativeDensity,
                definition.Budget.DecorativeTreesAndShrubs);

            foreach (var zone in definition.Zones.OrderByDescending(value => interactiveDensity[value])
                         .ThenBy(value => value.Id, StringComparer.Ordinal))
            {
                var vegetationClass = definition.Class(zone.ClassId);
                var normalizedDensity = vegetationClass.InteractiveTreesPerHectare.Maximum <= 0f ? 0f :
                    interactiveTargets[zone] * 10000f / Math.Max(1f, areas[zone]) /
                    vegetationClass.InteractiveTreesPerHectare.Maximum;
                var spacing = definition.Distribution.InteractiveTreeSpacingMeters.Lerp(1f - normalizedDensity);
                AddZone(result, zone, WorldMapVegetationKind.InteractiveTree, interactiveTargets[zone], spacing);
            }

            foreach (var zone in definition.Zones.OrderBy(value => value.Id, StringComparer.Ordinal))
                AddZone(result, zone, WorldMapVegetationKind.DecorativeTreeOrShrub,
                    decorativeTargets[zone], 0f);

            AddGroundCover(result, areas);
            return new WorldMapVegetationPlan(result);
        }

        private Dictionary<WorldMapVegetationZone, int> AllocateTargets(
            IReadOnlyDictionary<WorldMapVegetationZone, float> areas,
            IReadOnlyDictionary<WorldMapVegetationZone, float> densities, WorldMapRange budget)
        {
            var raw = definition.Zones.ToDictionary(value => value,
                value => areas[value] * densities[value] / 10000f);
            var rawTotal = raw.Values.Sum();
            // Preserve relative per-zone densities, then normalize the authored masks into the canonical
            // population budget. The resulting spacing is recalculated below and never drops under 4.5 m.
            var desiredTotal = (int)Math.Round(Math.Max(budget.Minimum,
                Math.Min(budget.Maximum, rawTotal)));
            var scale = rawTotal <= 0f ? 0f : desiredTotal / rawTotal;
            var exact = definition.Zones.ToDictionary(value => value, value => raw[value] * scale);
            var targets = exact.ToDictionary(value => value.Key, value => (int)Math.Floor(value.Value));
            var remaining = desiredTotal - targets.Values.Sum();
            foreach (var zone in definition.Zones.OrderByDescending(value => exact[value] - targets[value])
                         .ThenBy(value => value.Id, StringComparer.Ordinal).Take(remaining))
                targets[zone]++;
            return targets;
        }

        private void AddGroundCover(ICollection<WorldMapVegetationInstance> result,
            IReadOnlyDictionary<WorldMapVegetationZone, float> areas)
        {
            var target = (int)Math.Round(definition.Budget.DecorativeGroundInstances.Lerp(0.5f));
            var weights = definition.Zones.ToDictionary(value => value,
                value => Math.Max(1f, areas[value] * value.DensityMultiplier));
            var total = weights.Values.Sum();
            var assigned = 0;
            var ordered = definition.Zones.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
            for (var index = 0; index < ordered.Length; index++)
            {
                var count = index == ordered.Length - 1 ? target - assigned :
                    (int)Math.Round(target * weights[ordered[index]] / total);
                assigned += count;
                AddZone(result, ordered[index], WorldMapVegetationKind.GroundCover, count, 0f);
            }
        }

        private void AddZone(ICollection<WorldMapVegetationInstance> result, WorldMapVegetationZone zone,
            WorldMapVegetationKind kind, int target, float spacing)
        {
            if (target <= 0) return;
            var bounds = Bounds(zone.Shape);
            var accepted = 0;
            var maximumAttempts = Math.Max(512, target * 12);
            for (var attempt = 0; attempt < maximumAttempts && accepted < target; attempt++)
            {
                var x = bounds.XMin + bounds.Width * Unit(zone.Id, kind, attempt, 11);
                var z = bounds.ZMin + bounds.Depth * Unit(zone.Id, kind, attempt, 23);
                var design = new WorldMapPoint(x, z);
                if (!zone.Shape.Contains(design) || !OwnsPoint(zone, design) || IsExcluded(zone, design, kind))
                    continue;
                if (kind == WorldMapVegetationKind.InteractiveTree && !HasSpacing(design, spacing)) continue;
                var projected = world.MapData.CoordinateTransform.ToUnity(design);
                if (!world.TryGetTile(projected.X, projected.Z, out var tile)) continue;
                // Terrain details derive elevation from TerrainData at draw time; storing a redundant sampled Y
                // for tens of thousands of ground-cover records only adds startup cost.
                var y = kind == WorldMapVegetationKind.GroundCover
                    ? 0f
                    : heights.SampleHeight(projected.X, projected.Z);
                var id = CreateStableId(zone.Id, kind, design, attempt, spacing);
                var suitability = kind == WorldMapVegetationKind.GroundCover
                    ? 0.5f
                    : BiomeSuitability(zone.ClassId, design);
                result.Add(new WorldMapVegetationInstance(id, kind, zone.Id, zone.ClassId, tile,
                    new WorldPosition(projected.X, y, projected.Z),
                    Unit(zone.Id, kind, attempt, 53) * 360f,
                    0.78f + suitability * 0.12f + Unit(zone.Id, kind, attempt, 71) * 0.24f));
                accepted++;
            }
            if (accepted < target)
                throw new InvalidOperationException($"Vegetation zone {zone.Id} accepted {accepted}/{target} {kind} instances.");
        }

        private float EstimateUsableArea(WorldMapVegetationZone zone)
        {
            var bounds = Bounds(zone.Shape);
            const int samples = 512;
            var accepted = 0;
            for (var index = 0; index < samples; index++)
            {
                var point = new WorldMapPoint(bounds.XMin + bounds.Width * Unit(zone.Id,
                        WorldMapVegetationKind.GroundCover, index, 101),
                    bounds.ZMin + bounds.Depth * Unit(zone.Id,
                        WorldMapVegetationKind.GroundCover, index, 103));
                if (zone.Shape.Contains(point) && OwnsPoint(zone, point) &&
                    !IsExcluded(zone, point, WorldMapVegetationKind.GroundCover))
                    accepted++;
            }
            return bounds.Width * bounds.Depth * accepted / samples;
        }

        private bool OwnsPoint(WorldMapVegetationZone zone, WorldMapPoint point)
        {
            var priority = Priority(zone);
            foreach (var other in definition.Zones)
            {
                if (ReferenceEquals(other, zone) || !other.Shape.Contains(point)) continue;
                var otherPriority = Priority(other);
                if (otherPriority > priority || Math.Abs(otherPriority - priority) < 0.0001f &&
                    string.CompareOrdinal(other.Id, zone.Id) < 0) return false;
            }
            return true;
        }

        private float Priority(WorldMapVegetationZone zone) =>
            definition.Class(zone.ClassId).InteractiveTreesPerHectare.Lerp(DensityChoice) *
            zone.DensityMultiplier;

        public bool IsExcluded(WorldMapVegetationZone zone, WorldMapPoint design,
            WorldMapVegetationKind kind = WorldMapVegetationKind.InteractiveTree)
        {
            foreach (var clearing in zone.Clearings)
                if (Distance(design, clearing.Center) < clearing.RadiusMeters) return true;
            var start = world.MapData.CoordinateTransform.ToDesign(world.PlayerStart);
            if (Distance(design, start) < definition.Distribution.StartClearingExclusionRadiusMeters) return true;
            if (world.MapData.StartTerritory.IsPrepared(design)) return true;
            foreach (var route in world.MapData.Routes)
            {
                var clearance = route.Kind == "main_primitive_trail"
                    ? definition.Distribution.MainTrailClearanceMeters
                    : definition.Distribution.FootpathClearanceMeters;
                if (DistanceToSpline(design, route) < route.MaximumWidthMeters * 0.5f + clearance) return true;
            }
            foreach (var poi in world.MapData.PointsOfInterest)
                if (Distance(design, poi.Position) < poi.ProtectionRadiusMeters) return true;
            foreach (var build in world.MapData.BuildabilityMasks)
                if (build.Category != "restricted" && !build.RequiresClearing &&
                    build.Shape.Contains(design)) return true;

            var unity = world.MapData.CoordinateTransform.ToUnity(design);
            if (heights.IsWater(unity.X, unity.Z)) return true;
            // Ground-cover detail meshes follow the Terrain surface and are safe on steep land; trees and
            // shrubs retain the stricter slope gate required by their upright silhouette/collider contract.
            return kind != WorldMapVegetationKind.GroundCover &&
                   heights.SampleSlopeDegrees(unity.X, unity.Z) > 32f;
        }

        private float BiomeSuitability(string classId, WorldMapPoint design)
        {
            var unity = world.MapData.CoordinateTransform.ToUnity(design);
            var weights = paint.SampleWeights(unity.X, unity.Z);
            var grass = weights[(int)WorldMapTerrainPaintLayer.Grass];
            var forest = weights[(int)WorldMapTerrainPaintLayer.ForestMoss];
            var rock = weights[(int)WorldMapTerrainPaintLayer.Rock];
            var shore = weights[(int)WorldMapTerrainPaintLayer.Shore];
            var scrub = weights[(int)WorldMapTerrainPaintLayer.Scrub];
            var wetland = weights[(int)WorldMapTerrainPaintLayer.Wetland];
            if (classId == "dense_conifer") return Clamp01(0.3f + forest * 1.5f + rock * 0.15f);
            if (classId == "mixed_forest") return Clamp01(0.35f + forest + grass * 0.45f);
            if (classId == "deciduous_grove") return Clamp01(0.4f + forest * 0.85f + grass * 0.4f);
            if (classId == "open_woodland") return Clamp01(0.5f + grass * 0.45f + scrub * 0.35f);
            if (classId == "meadow") return Clamp01(0.52f + grass * 0.7f + scrub * 0.2f);
            if (classId == "marsh_vegetation") return Clamp01(0.42f + wetland + shore * 0.45f);
            return 0f;
        }

        private bool HasSpacing(WorldMapPoint point, float spacing)
        {
            var cellSize = definition.Distribution.InteractiveTreeSpacingMeters.Maximum;
            var cellX = (int)Math.Floor(point.X / cellSize);
            var cellZ = (int)Math.Floor(point.Z / cellSize);
            var squared = spacing * spacing;
            for (var z = cellZ - 1; z <= cellZ + 1; z++)
            for (var x = cellX - 1; x <= cellX + 1; x++)
                if (interactiveGrid.TryGetValue(CellKey(x, z), out var neighbours) &&
                    neighbours.Any(other => SquaredDistance(point, other) < squared)) return false;
            var key = CellKey(cellX, cellZ);
            if (!interactiveGrid.TryGetValue(key, out var values))
                interactiveGrid.Add(key, values = new List<WorldMapPoint>());
            values.Add(point);
            return true;
        }

        private StableEntityId CreateStableId(string zoneId, WorldMapVegetationKind kind,
            WorldMapPoint point, int ordinal, float spacing)
        {
            var quantization = kind == WorldMapVegetationKind.InteractiveTree ? Math.Max(1f, spacing) : 1f;
            var cellX = (int)Math.Floor(point.X / quantization);
            var cellZ = (int)Math.Floor(point.Z / quantization);
            var purpose = world.MapData.MapVersion + ":vegetation:" + kind + ":" + zoneId + ":" +
                          cellX + ":" + cellZ + ":" + ordinal;
            return StableEntityId.FromDeterministicHash(Hash(purpose, 149), Hash(purpose, 197));
        }

        private float Unit(string zoneId, WorldMapVegetationKind kind, int ordinal, int salt)
        {
            var hash = Hash(zoneId + ":" + kind + ":" + ordinal, salt);
            return (hash & 0xFFFFFFUL) / 16777216f;
        }

        private ulong Hash(string value, int salt)
        {
            ulong hash = 14695981039346656037UL;
            var seeded = seed.ToString() + ":" + salt + ":" + value;
            foreach (var character in seeded) hash = (hash ^ character) * 1099511628211UL;
            // FNV is stable but adjacent salted prefixes retain visible correlation in two-dimensional
            // samples. Final avalanche makes the independently salted X/Z streams spatially uniform.
            hash ^= hash >> 33;
            hash *= 0xff51afd7ed558ccdUL;
            hash ^= hash >> 33;
            hash *= 0xc4ceb9fe1a85ec53UL;
            hash ^= hash >> 33;
            return hash;
        }

        private static WorldMapBounds Bounds(WorldMapShape shape)
        {
            if (shape.Kind == WorldMapShapeKind.Polygon)
                return new WorldMapBounds(shape.Polygon.Points.Min(value => value.X),
                    shape.Polygon.Points.Max(value => value.X), shape.Polygon.Points.Min(value => value.Z),
                    shape.Polygon.Points.Max(value => value.Z));
            return new WorldMapBounds(shape.Center.X - shape.RadiusX, shape.Center.X + shape.RadiusX,
                shape.Center.Z - shape.RadiusZ, shape.Center.Z + shape.RadiusZ);
        }

        private static float DistanceToSpline(WorldMapPoint point, WorldMapSpline spline)
        {
            var best = float.MaxValue;
            for (var index = 1; index < spline.Points.Count; index++)
                best = Math.Min(best, DistanceToSegment(point, spline.Points[index - 1].Position,
                    spline.Points[index].Position));
            return best;
        }

        private static float DistanceToSegment(WorldMapPoint point, WorldMapPoint a, WorldMapPoint b)
        {
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            var length = dx * dx + dz * dz;
            var t = length <= 0f ? 0f : ((point.X - a.X) * dx + (point.Z - a.Z) * dz) / length;
            t = Math.Max(0f, Math.Min(1f, t));
            var ox = point.X - (a.X + dx * t);
            var oz = point.Z - (a.Z + dz * t);
            return (float)Math.Sqrt(ox * ox + oz * oz);
        }

        private static float Distance(WorldMapPoint a, WorldMapPoint b) =>
            (float)Math.Sqrt(SquaredDistance(a, b));
        private static float SquaredDistance(WorldMapPoint a, WorldMapPoint b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }
        private static long CellKey(int x, int z) => ((long)x << 32) ^ (uint)z;
        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
