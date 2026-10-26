using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Campaign;

namespace Game.Domain.World
{
    public sealed class WorldMapPoiPlanEntry
    {
        public WorldMapPoiPlanEntry(WorldMapPointOfInterest definition, StableEntityId stableId,
            WorldPosition position, WorldTileId tile, string accessRouteId,
            IEnumerable<WorldPosition> approachRoute)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (!stableId.IsValid || string.IsNullOrWhiteSpace(accessRouteId))
                throw new ArgumentException("POI identity and access route are required.");
            var approach = (approachRoute ?? throw new ArgumentNullException(nameof(approachRoute))).ToArray();
            if (approach.Length < 2) throw new ArgumentException("A POI needs a reachable approach.", nameof(approachRoute));
            StableId = stableId;
            Position = position;
            Tile = tile;
            AccessRouteId = accessRouteId;
            ApproachRoute = Array.AsReadOnly(approach);
        }

        public WorldMapPointOfInterest Definition { get; }
        public string Id => Definition.Id;
        public string DisplayName => Definition.DisplayName;
        public string RegionId => Definition.RegionId;
        public float ProtectionRadiusMeters => Definition.ProtectionRadiusMeters;
        public IReadOnlyList<string> Purposes => Definition.Purposes;
        public StableEntityId StableId { get; }
        public WorldPosition Position { get; }
        public WorldTileId Tile { get; }
        public string AccessRouteId { get; }
        public IReadOnlyList<WorldPosition> ApproachRoute { get; }
        public WorldPosition ArrivalPosition => ApproachRoute[ApproachRoute.Count - 1];
        public bool AllowsAutomaticClearance => false;
    }

    public sealed class WorldMapPoiPlan
    {
        private readonly Dictionary<string, WorldMapPoiPlanEntry> byId;

        public WorldMapPoiPlan(IEnumerable<WorldMapPoiPlanEntry> points)
        {
            var values = (points ?? throw new ArgumentNullException(nameof(points))).ToArray();
            if (values.Length != 9 || values.Any(value => value == null) ||
                values.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != values.Length ||
                values.Select(value => value.StableId).Distinct().Count() != values.Length)
                throw new ArgumentException("The canonical World Map V2 POI plan requires nine unique points.", nameof(points));
            Points = Array.AsReadOnly(values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray());
            byId = Points.ToDictionary(value => value.Id, StringComparer.Ordinal);
        }

        public IReadOnlyList<WorldMapPoiPlanEntry> Points { get; }
        public WorldMapPoiPlanEntry Get(string id) => byId.TryGetValue(id, out var value)
            ? value : throw new KeyNotFoundException("Unknown World Map POI: " + id);

        public bool IsProtected(float worldX, float worldZ, float extraClearanceMeters = 0f)
        {
            if (extraClearanceMeters < 0f) throw new ArgumentOutOfRangeException(nameof(extraClearanceMeters));
            foreach (var point in Points)
            {
                var dx = worldX - point.Position.X;
                var dz = worldZ - point.Position.Z;
                var radius = point.ProtectionRadiusMeters + extraClearanceMeters;
                if (dx * dx + dz * dz <= radius * radius) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Pure WM11 projection of the nine authored POIs. Stable identity is established before presentation;
    /// last-mile approaches connect each protected footprint to the already-authored WM09 road network.
    /// </summary>
    public sealed class WorldMapPoiPlanner
    {
        public const float MaximumApproachSlopeDegrees = 30f;
        public const float ApproachGridMeters = 8f;
        private readonly TiledWorldDefinition world;
        private readonly TiledWorldHeightSampler heights;
        private readonly WorldMapRoadNetworkPlan roads;
        private readonly long seed;

        public WorldMapPoiPlanner(TiledWorldDefinition definition, TiledWorldHeightSampler heightSampler,
            WorldMapRoadNetworkPlan roadPlan, long worldSeed)
        {
            world = definition ?? throw new ArgumentNullException(nameof(definition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            roads = roadPlan ?? throw new ArgumentNullException(nameof(roadPlan));
            if (!ReferenceEquals(world, heights.Definition) || world.MapData == null)
                throw new ArgumentException("World Map V2 definition and shared height sampler are required.");
            seed = worldSeed;
        }

        public WorldMapPoiPlan Create()
        {
            if (world.MapData.PointsOfInterest.Count != 9)
                throw new InvalidOperationException("World Map V2 must define exactly nine POIs.");
            var entries = new List<WorldMapPoiPlanEntry>(9);
            foreach (var definition in world.MapData.PointsOfInterest.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                if (!roads.IsRegionReachable(definition.RegionId))
                    throw new InvalidOperationException("POI region is not reachable through WM09: " + definition.Id);
                var flat = world.MapData.CoordinateTransform.ToUnity(definition.Position);
                if (!world.TryGetTile(flat.X, flat.Z, out var tile) || heights.IsWater(flat.X, flat.Z))
                    throw new InvalidOperationException("POI must occupy dry in-bounds terrain: " + definition.Id);
                var position = new WorldPosition(flat.X, heights.SampleHeight(flat.X, flat.Z), flat.Z);
                var approach = FindApproach(definition, position);
                entries.Add(new WorldMapPoiPlanEntry(definition,
                    DeterministicStableId.Create(seed, "world_poi:" + world.GenerationId + ":" + definition.Id),
                    position, tile, approach.RouteId, approach.Points));
            }
            return new WorldMapPoiPlan(entries);
        }

        private ApproachResult FindApproach(WorldMapPointOfInterest definition, WorldPosition target, float grid = ApproachGridMeters)
        {
            var columns = (int)Math.Floor((world.WidthMeters - 0.001f) / grid) + 1;
            var rows = (int)Math.Floor((world.DepthMeters - 0.001f) / grid) + 1;
            var sourceRoutes = new Dictionary<GridNode, string>();
            foreach (var route in roads.Routes.OrderBy(value => value.Id, StringComparer.Ordinal))
            foreach (var sample in route.Samples.Where(value => !value.IsWater))
            {
                var node = ToNode(sample.Position, columns, rows, grid);
                if (!IsTraversable(node, columns, rows, grid)) continue;
                if (!sourceRoutes.TryGetValue(node, out var previous) ||
                    string.Compare(route.Id, previous, StringComparison.Ordinal) < 0) sourceRoutes[node] = route.Id;
            }
            if (sourceRoutes.Count == 0) throw new InvalidOperationException("WM09 has no dry road samples.");

            var arrivalRadius = Math.Max(6f, definition.ProtectionRadiusMeters);
            var open = new SortedSet<OpenNode>(OpenNodeComparer.Instance);
            var cost = new Dictionary<GridNode, float>();
            var parent = new Dictionary<GridNode, GridNode>();
            var rootRoute = new Dictionary<GridNode, string>();
            var closed = new HashSet<GridNode>();
            var sequence = 0;
            foreach (var source in sourceRoutes.OrderBy(value => value.Value, StringComparer.Ordinal)
                         .ThenBy(value => value.Key.X).ThenBy(value => value.Key.Z))
            {
                cost[source.Key] = 0f;
                rootRoute[source.Key] = source.Value;
                open.Add(new OpenNode(source.Key, Heuristic(source.Key, target, grid), sequence++));
            }

            GridNode? reached = null;
            while (open.Count > 0)
            {
                var currentOpen = open.Min;
                open.Remove(currentOpen);
                var current = currentOpen.Node;
                if (!cost.TryGetValue(current, out var currentCost)) continue;
                if (!closed.Add(current)) continue;
                var currentPosition = Position(current, grid);
                if (Distance2D(currentPosition, target) <= arrivalRadius)
                {
                    reached = current;
                    break;
                }
                foreach (var next in Neighbours(current, columns, rows))
                {
                    if (!IsTraversable(next, columns, rows, grid) || !SegmentTraversable(current, next, grid)) continue;
                    var step = current.X != next.X && current.Z != next.Z
                        ? grid * 1.41421356f : grid;
                    var nextCost = currentCost + step;
                    if (cost.TryGetValue(next, out var existing) && nextCost >= existing - 0.001f) continue;
                    cost[next] = nextCost;
                    parent[next] = current;
                    rootRoute[next] = rootRoute[current];
                    open.Add(new OpenNode(next, nextCost + Heuristic(next, target, grid), sequence++));
                }
            }
            if (!reached.HasValue && grid > 2f)
                return FindApproach(definition, target, grid * .5f); // Resolve narrow dry passages missed by the coarse planning lattice.
            if (!reached.HasValue)
                throw new InvalidOperationException("No dry WM09 approach could be found for POI " + definition.Id);

            var nodes = new List<GridNode>();
            var cursor = reached.Value;
            nodes.Add(cursor);
            while (parent.TryGetValue(cursor, out var previous))
            {
                cursor = previous;
                nodes.Add(cursor);
            }
            nodes.Reverse();
            var points = nodes.Select(node => Position(node, grid)).ToArray();
            if (points.Length == 1)
            {
                var source = points[0];
                var directionX = target.X - source.X;
                var directionZ = target.Z - source.Z;
                var length = Math.Sqrt(directionX * directionX + directionZ * directionZ);
                if (length > 0.001)
                {
                    var travel = Math.Max(1f, length - arrivalRadius);
                    points = new[] { source, new WorldPosition(
                        source.X + (float)(directionX / length * travel),
                        heights.SampleHeight(source.X + (float)(directionX / length * travel),
                            source.Z + (float)(directionZ / length * travel)) + 0.1f,
                        source.Z + (float)(directionZ / length * travel)) };
                }
            }
            if (points.Length < 2)
                throw new InvalidOperationException("POI approach collapsed to one point: " + definition.Id);
            return new ApproachResult(rootRoute[reached.Value], points);
        }

        private GridNode ToNode(WorldPosition value, int columns, int rows, float grid) => new GridNode(
            Math.Max(0, Math.Min(columns - 1, (int)Math.Round((value.X - world.Origin.X) / grid))),
            Math.Max(0, Math.Min(rows - 1, (int)Math.Round((value.Z - world.Origin.Z) / grid))));

        private WorldPosition Position(GridNode node, float grid)
        {
            var x = world.Origin.X + node.X * grid;
            var z = world.Origin.Z + node.Z * grid;
            return new WorldPosition(x, heights.SampleHeight(x, z) + 0.1f, z);
        }

        private bool IsTraversable(GridNode node, int columns, int rows, float grid)
        {
            if (node.X < 0 || node.Z < 0 || node.X >= columns || node.Z >= rows) return false;
            var position = Position(node, grid);
            return !heights.IsWater(position.X, position.Z) &&
                   heights.SampleSlopeDegrees(position.X, position.Z) <= MaximumApproachSlopeDegrees + 0.001f;
        }

        private bool SegmentTraversable(GridNode first, GridNode second, float grid)
        {
            var a = Position(first, grid);
            var b = Position(second, grid);
            var mx = (a.X + b.X) * 0.5f;
            var mz = (a.Z + b.Z) * 0.5f;
            if (heights.IsWater(mx, mz) || heights.SampleSlopeDegrees(mx, mz) > MaximumApproachSlopeDegrees + 0.001f)
                return false;
            var horizontal = Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Z - b.Z) * (a.Z - b.Z));
            var grade = Math.Atan(Math.Abs(a.Y - b.Y) / Math.Max(0.001, horizontal)) * 180d / Math.PI;
            return grade <= MaximumApproachSlopeDegrees + 0.001f;
        }

        private static IEnumerable<GridNode> Neighbours(GridNode value, int columns, int rows)
        {
            for (var dz = -1; dz <= 1; dz++)
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0) continue;
                var next = new GridNode(value.X + dx, value.Z + dz);
                if (next.X >= 0 && next.Z >= 0 && next.X < columns && next.Z < rows) yield return next;
            }
        }

        private WorldPosition FlatPosition(GridNode node, float grid) => new WorldPosition(
            world.Origin.X + node.X * grid, 0f,
            world.Origin.Z + node.Z * grid);
        private float Heuristic(GridNode node, WorldPosition target, float grid) => Distance2D(FlatPosition(node, grid), target);
        private static float Distance2D(WorldPosition a, WorldPosition b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private readonly struct GridNode : IEquatable<GridNode>
        {
            public GridNode(int x, int z) { X = x; Z = z; }
            public int X { get; }
            public int Z { get; }
            public bool Equals(GridNode other) => X == other.X && Z == other.Z;
            public override bool Equals(object obj) => obj is GridNode other && Equals(other);
            public override int GetHashCode() => unchecked(X * 397 ^ Z);
        }

        private readonly struct OpenNode
        {
            public OpenNode(GridNode node, float priority, int sequence)
            { Node = node; Priority = priority; Sequence = sequence; }
            public GridNode Node { get; }
            public float Priority { get; }
            public int Sequence { get; }
        }

        private sealed class OpenNodeComparer : IComparer<OpenNode>
        {
            public static readonly OpenNodeComparer Instance = new OpenNodeComparer();
            public int Compare(OpenNode a, OpenNode b)
            {
                var priority = a.Priority.CompareTo(b.Priority);
                if (priority != 0) return priority;
                var x = a.Node.X.CompareTo(b.Node.X);
                if (x != 0) return x;
                var z = a.Node.Z.CompareTo(b.Node.Z);
                return z != 0 ? z : a.Sequence.CompareTo(b.Sequence);
            }
        }

        private readonly struct ApproachResult
        {
            public ApproachResult(string routeId, IReadOnlyList<WorldPosition> points)
            { RouteId = routeId; Points = points; }
            public string RouteId { get; }
            public IReadOnlyList<WorldPosition> Points { get; }
        }
    }
}
