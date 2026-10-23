using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    public sealed class WorldMapRoadSample
    {
        public WorldMapRoadSample(WorldMapPoint designPosition, WorldPosition position, float widthMeters,
            float slopeDegrees, bool water, string crossingId)
        {
            DesignPosition = designPosition;
            Position = position;
            WidthMeters = widthMeters;
            SlopeDegrees = slopeDegrees;
            IsWater = water;
            CrossingId = crossingId ?? string.Empty;
        }

        public WorldMapPoint DesignPosition { get; }
        public WorldPosition Position { get; }
        public float WidthMeters { get; }
        public float SlopeDegrees { get; }
        public bool IsWater { get; }
        public string CrossingId { get; }
        public bool UsesAuthoredCrossing => CrossingId.Length > 0;
    }

    public sealed class WorldMapRoadRoutePlan
    {
        public WorldMapRoadRoutePlan(string id, string kind, float maximumSlopeDegrees,
            IEnumerable<WorldMapRoadSample> samples)
        {
            Id = id;
            Kind = kind;
            MaximumSlopeDegrees = maximumSlopeDegrees;
            Samples = Array.AsReadOnly((samples ?? throw new ArgumentNullException(nameof(samples))).ToArray());
            if (Samples.Count < 2) throw new ArgumentException(nameof(samples));
        }

        public string Id { get; }
        public string Kind { get; }
        public float MaximumSlopeDegrees { get; }
        public IReadOnlyList<WorldMapRoadSample> Samples { get; }
        public float MaximumDrySlopeDegrees => Samples.Where(value => !value.IsWater && !value.UsesAuthoredCrossing)
            .Select(value => value.SlopeDegrees).DefaultIfEmpty(0f).Max();
        public bool MeetsSlopeBudget => MaximumDrySlopeDegrees <= MaximumSlopeDegrees + 0.001f;
        public bool CrossesWaterOnlyAtAuthoredCrossings => Samples.All(value => !value.IsWater ||
            value.UsesAuthoredCrossing);
    }

    public sealed class WorldMapNavigationLinkPlan
    {
        public WorldMapNavigationLinkPlan(string id, string kind, string routeId, WorldPosition start,
            WorldPosition end, float widthMeters)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(kind) ||
                string.IsNullOrWhiteSpace(routeId) || widthMeters <= 0f)
                throw new ArgumentException(nameof(id));
            Id = id;
            Kind = kind;
            RouteId = routeId;
            Start = start;
            End = end;
            WidthMeters = widthMeters;
        }

        public string Id { get; }
        public string Kind { get; }
        public string RouteId { get; }
        public WorldPosition Start { get; }
        public WorldPosition End { get; }
        public float WidthMeters { get; }
    }

    public sealed class WorldMapCrossingPlan
    {
        public WorldMapCrossingPlan(WorldMapCrossing definition, WorldMapNavigationLinkPlan link)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Link = link ?? throw new ArgumentNullException(nameof(link));
        }

        public WorldMapCrossing Definition { get; }
        public WorldMapNavigationLinkPlan Link { get; }
    }

    public sealed class WorldMapRoadNetworkPlan
    {
        private readonly HashSet<string> reachableRegions;

        public WorldMapRoadNetworkPlan(IEnumerable<WorldMapRoadRoutePlan> routes,
            IEnumerable<WorldMapCrossingPlan> crossings, IEnumerable<WorldMapNavigationLinkPlan> links,
            IEnumerable<string> reachableRegionIds)
        {
            Routes = Array.AsReadOnly((routes ?? throw new ArgumentNullException(nameof(routes))).ToArray());
            Crossings = Array.AsReadOnly((crossings ?? throw new ArgumentNullException(nameof(crossings))).ToArray());
            Links = Array.AsReadOnly((links ?? throw new ArgumentNullException(nameof(links))).ToArray());
            reachableRegions = new HashSet<string>(reachableRegionIds ??
                throw new ArgumentNullException(nameof(reachableRegionIds)), StringComparer.Ordinal);
        }

        public IReadOnlyList<WorldMapRoadRoutePlan> Routes { get; }
        public IReadOnlyList<WorldMapCrossingPlan> Crossings { get; }
        public IReadOnlyList<WorldMapNavigationLinkPlan> Links { get; }
        public IReadOnlyCollection<string> ReachableRegionIds => reachableRegions;
        public bool AllRoutesMeetSlopeBudget => Routes.All(value => value.MeetsSlopeBudget);
        public bool WaterTraversalIsAuthored => Routes.All(value => value.CrossesWaterOnlyAtAuthoredCrossings);
        public bool IsRegionReachable(string regionId) => reachableRegions.Contains(regionId);
    }

    /// <summary>
    /// Pure projection of authored road splines onto the shared WM02/WM03 surface. The result is consumed by
    /// disposable route renderers and the existing NavMesh adapter; it owns no simulation or save state.
    /// </summary>
    public sealed class WorldMapRoadNetworkPlanner
    {
        private const float SampleSpacingMeters = 3f;
        private readonly TiledWorldDefinition definition;
        private readonly TiledWorldHeightSampler heights;
        private readonly WorldMapRoadDefinition roads;

        public WorldMapRoadNetworkPlanner(TiledWorldDefinition world, TiledWorldHeightSampler heightSampler)
        {
            definition = world ?? throw new ArgumentNullException(nameof(world));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(world, heightSampler.Definition) || world.MapData == null)
                throw new ArgumentException("World Map V2 definition and shared height sampler are required.");
            roads = world.MapData.Roads;
        }

        public WorldMapRoadNetworkPlan Create()
        {
            var routePlans = roads.Routes.OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(CreateRoute).ToArray();
            var crossingPlans = roads.Crossings.OrderBy(value => value.Id, StringComparer.Ordinal)
                .Select(value => CreateCrossing(value, routePlans)).ToArray();
            var links = crossingPlans.Select(value => value.Link).ToList();
            AddChokepointLinks(routePlans, crossingPlans, links);
            var unauthorisedWaterRoutes = routePlans.Where(value => !value.CrossesWaterOnlyAtAuthoredCrossings)
                .Select(value => value.Id + "=" + value.Samples.Count(sample => sample.IsWater &&
                    !sample.UsesAuthoredCrossing) + " [" + string.Join(";", value.Samples
                    .Where(sample => sample.IsWater && !sample.UsesAuthoredCrossing).Take(5)
                    .Select(sample => sample.DesignPosition.X.ToString("0.0") + ":" +
                        sample.DesignPosition.Z.ToString("0.0"))) + "]").ToArray();
            if (unauthorisedWaterRoutes.Length > 0)
                throw new InvalidOperationException("Authored routes cross water outside a crossing ID: " +
                    string.Join(", ", unauthorisedWaterRoutes));
            return new WorldMapRoadNetworkPlan(routePlans, crossingPlans,
                links.OrderBy(value => value.Id, StringComparer.Ordinal), ReachableRegions(routePlans));
        }

        private WorldMapRoadRoutePlan CreateRoute(WorldMapSpline route)
        {
            var controls = InsertCrossings(route);
            var raw = new List<MutableSample>();
            for (var segment = 1; segment < controls.Count; segment++)
            {
                var a = controls[segment - 1];
                var b = controls[segment];
                var length = Distance(a.Position, b.Position);
                var steps = Math.Max(1, (int)Math.Ceiling(length / SampleSpacingMeters));
                for (var step = segment == 1 ? 0 : 1; step <= steps; step++)
                {
                    var t = step / (float)steps;
                    var point = Lerp(a.Position, b.Position, t);
                    var width = a.WidthMeters + (b.WidthMeters - a.WidthMeters) * t;
                    var unity = definition.MapData.CoordinateTransform.ToUnity(point);
                    raw.Add(new MutableSample(point, unity, width, heights.IsWater(unity.X, unity.Z)));
                }
            }

            foreach (var crossing in roads.Crossings.Where(value => value.RouteIds.Contains(route.Id)))
                MarkCrossing(raw, crossing);
            ProjectUnauthorisedWaterToBank(raw);

            var maximumSlope = route.Kind == "main_primitive_trail"
                ? roads.Rules.MainTrailMaximumSlopeDegrees
                : roads.Rules.FootpathMaximumSlopeDegrees;
            var positions = raw.Select(value =>
            {
                var ground = heights.SampleHeight(value.Unity.X, value.Unity.Z);
                var y = ground + 0.08f;
                if (value.Water && value.CrossingId.Length > 0 &&
                    heights.TrySampleWaterSurface(value.Unity.X, value.Unity.Z, out var waterY))
                    y = waterY + 0.12f;
                return new WorldPosition(value.Unity.X, y, value.Unity.Z);
            }).ToArray();
            GradeRoadProfile(positions, maximumSlope);
            var samples = raw.Select((value, index) => new WorldMapRoadSample(value.Design, positions[index],
                value.Width, RoadGradeDegrees(positions, index), value.Water, value.CrossingId)).ToArray();
            return new WorldMapRoadRoutePlan(route.Id, route.Kind, maximumSlope, samples);
        }

        private static void GradeRoadProfile(WorldPosition[] positions, float maximumSlopeDegrees)
        {
            var maximumRise = Math.Tan((maximumSlopeDegrees - 0.2f) * Math.PI / 180d);
            for (var index = 1; index < positions.Length; index++)
                RaiseToGrade(positions, index, index - 1, maximumRise);
            for (var index = positions.Length - 2; index >= 0; index--)
                RaiseToGrade(positions, index, index + 1, maximumRise);
        }

        private static void RaiseToGrade(WorldPosition[] positions, int target, int neighbour, double maximumRise)
        {
            var x = positions[target].X - positions[neighbour].X;
            var z = positions[target].Z - positions[neighbour].Z;
            var allowedDifference = Math.Sqrt(x * x + z * z) * maximumRise;
            var minimumY = positions[neighbour].Y - allowedDifference;
            if (positions[target].Y >= minimumY) return;
            positions[target] = new WorldPosition(positions[target].X, (float)minimumY, positions[target].Z);
        }

        private static float RoadGradeDegrees(IReadOnlyList<WorldPosition> positions, int index)
        {
            var maximum = 0f;
            if (index > 0) maximum = SegmentGradeDegrees(positions[index - 1], positions[index]);
            if (index + 1 < positions.Count)
                maximum = Math.Max(maximum, SegmentGradeDegrees(positions[index], positions[index + 1]));
            return maximum;
        }

        private static float SegmentGradeDegrees(WorldPosition a, WorldPosition b)
        {
            var x = b.X - a.X;
            var z = b.Z - a.Z;
            var horizontal = Math.Sqrt(x * x + z * z);
            return horizontal <= 0.0001 ? 90f :
                (float)(Math.Atan(Math.Abs(b.Y - a.Y) / horizontal) * 180d / Math.PI);
        }

        private IReadOnlyList<ControlPoint> InsertCrossings(WorldMapSpline route)
        {
            var inserts = new Dictionary<int, List<Tuple<float, WorldMapCrossing>>>();
            foreach (var crossing in roads.Crossings.Where(value => value.RouteIds.Contains(route.Id)))
            {
                var bestSegment = -1;
                var bestT = 0f;
                var bestDistance = float.MaxValue;
                for (var segment = 1; segment < route.Points.Count; segment++)
                {
                    var distance = DistanceToSegment(crossing.Position, route.Points[segment - 1].Position,
                        route.Points[segment].Position, out var t);
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestSegment = segment;
                    bestT = t;
                }
                if (bestSegment < 1 || bestDistance > 55f)
                    throw new InvalidOperationException("Crossing " + crossing.Id + " is outside its route corridor.");
                if (!inserts.TryGetValue(bestSegment, out var list))
                    inserts.Add(bestSegment, list = new List<Tuple<float, WorldMapCrossing>>());
                list.Add(Tuple.Create(bestT, crossing));
            }

            var result = new List<ControlPoint> { new ControlPoint(route.Points[0].Position,
                route.Points[0].WidthMeters) };
            for (var segment = 1; segment < route.Points.Count; segment++)
            {
                var a = route.Points[segment - 1];
                var b = route.Points[segment];
                if (inserts.TryGetValue(segment, out var list))
                foreach (var insert in list.OrderBy(value => value.Item1))
                    result.Add(new ControlPoint(insert.Item2.Position,
                        a.WidthMeters + (b.WidthMeters - a.WidthMeters) * insert.Item1));
                result.Add(new ControlPoint(b.Position, b.WidthMeters));
            }
            return result;
        }

        private void MarkCrossing(IList<MutableSample> samples, WorldMapCrossing crossing)
        {
            var center = Enumerable.Range(0, samples.Count).OrderBy(index =>
                Distance(samples[index].Design, crossing.Position)).First();
            var waterCenter = samples[center].Water ? center : Enumerable.Range(0, samples.Count)
                .Where(index => samples[index].Water && Distance(samples[index].Design, crossing.Position) <= 40f)
                .OrderBy(index => Distance(samples[index].Design, crossing.Position)).DefaultIfEmpty(-1).First();
            if (waterCenter < 0 && TryFindNearbyWater(crossing.Position, out var waterDesign, out var waterUnity))
            {
                waterCenter = center + 1;
                samples.Insert(waterCenter, new MutableSample(waterDesign, waterUnity, samples[center].Width, true));
            }
            if (waterCenter < 0)
                throw new InvalidOperationException("Crossing " + crossing.Id + " does not meet canonical water.");
            var first = waterCenter;
            var last = waterCenter;
            while (first > 0 && samples[first - 1].Water) first--;
            while (last + 1 < samples.Count && samples[last + 1].Water) last++;
            var associationRadius = crossing.Kind == "footbridge" ? 95f : crossing.Kind == "ford" ? 45f : 40f;
            var associatedWater = Enumerable.Range(0, samples.Count).Where(index => samples[index].Water &&
                Distance(samples[index].Design, crossing.Position) <= associationRadius).ToArray();
            if (associatedWater.Length > 0)
            {
                first = Math.Min(first, associatedWater.First());
                last = Math.Max(last, associatedWater.Last());
            }
            first = Math.Max(0, first - 1);
            last = Math.Min(samples.Count - 1, last + 1);
            for (var index = first; index <= last; index++) samples[index].CrossingId = crossing.Id;
        }

        private bool TryFindNearbyWater(WorldMapPoint crossing, out WorldMapPoint design,
            out WorldPosition unity)
        {
            const int searchRadiusMeters = 40;
            var found = false;
            var bestDistanceSquared = float.MaxValue;
            design = default;
            unity = default;
            for (var z = -searchRadiusMeters; z <= searchRadiusMeters; z++)
            for (var x = -searchRadiusMeters; x <= searchRadiusMeters; x++)
            {
                var distanceSquared = x * x + z * z;
                if (distanceSquared > searchRadiusMeters * searchRadiusMeters ||
                    distanceSquared >= bestDistanceSquared) continue;
                var candidate = new WorldMapPoint(crossing.X + x, crossing.Z + z);
                var runtime = definition.MapData.CoordinateTransform.ToUnity(candidate);
                if (!heights.IsWater(runtime.X, runtime.Z)) continue;
                found = true;
                bestDistanceSquared = distanceSquared;
                design = candidate;
                unity = runtime;
            }
            return found;
        }

        private void ProjectUnauthorisedWaterToBank(IList<MutableSample> samples)
        {
            for (var index = 0; index < samples.Count; index++)
            {
                var sample = samples[index];
                if (!sample.Water || sample.CrossingId.Length > 0) continue;
                var previous = index > 0 ? samples[index - 1].Design : sample.Design;
                if (!TryFindNearbyDry(sample.Design, previous, out var dryDesign, out var dryUnity))
                    throw new InvalidOperationException("Road corridor cannot reach a dry bank near " +
                        sample.Design.X + ":" + sample.Design.Z + ".");
                samples[index] = new MutableSample(dryDesign, dryUnity, sample.Width, false);
            }
        }

        private bool TryFindNearbyDry(WorldMapPoint source, WorldMapPoint previous, out WorldMapPoint design,
            out WorldPosition unity)
        {
            const int searchRadiusMeters = 48;
            design = default;
            unity = default;
            for (var radius = 0; radius <= searchRadiusMeters; radius++)
            {
                var foundAtRadius = false;
                var bestContinuity = float.MaxValue;
                for (var z = -radius; z <= radius; z++)
                for (var x = -radius; x <= radius; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(z)) != radius) continue;
                    var candidate = new WorldMapPoint(source.X + x, source.Z + z);
                    var runtime = definition.MapData.CoordinateTransform.ToUnity(candidate);
                    if (!HasDryClearance(runtime, 4f)) continue;
                    var continuityX = candidate.X - previous.X;
                    var continuityZ = candidate.Z - previous.Z;
                    var continuity = continuityX * continuityX + continuityZ * continuityZ;
                    if (continuity >= bestContinuity) continue;
                    foundAtRadius = true;
                    bestContinuity = continuity;
                    design = candidate;
                    unity = runtime;
                }
                if (foundAtRadius) return true;
            }
            return false;
        }

        private bool HasDryClearance(WorldPosition center, float clearance)
        {
            if (!definition.ContainsClosed(center.X, center.Z) || heights.IsWater(center.X, center.Z)) return false;
            for (var z = -1; z <= 1; z++)
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && z == 0) continue;
                var sampleX = center.X + x * clearance;
                var sampleZ = center.Z + z * clearance;
                if (!definition.ContainsClosed(sampleX, sampleZ) || heights.IsWater(sampleX, sampleZ)) return false;
            }
            return true;
        }

        private WorldMapCrossingPlan CreateCrossing(WorldMapCrossing crossing,
            IReadOnlyList<WorldMapRoadRoutePlan> routePlans)
        {
            var route = routePlans.Single(value => crossing.RouteIds.Contains(value.Id));
            var indexes = Enumerable.Range(0, route.Samples.Count)
                .Where(index => route.Samples[index].CrossingId == crossing.Id).ToArray();
            if (indexes.Length < 3) throw new InvalidOperationException("Crossing span is incomplete: " + crossing.Id);
            var first = route.Samples[indexes.First()];
            var last = route.Samples[indexes.Last()];
            if (first.IsWater || last.IsWater)
                throw new InvalidOperationException("Crossing endpoints must be dry: " + crossing.Id);
            var width = route.Samples.Skip(indexes.First()).Take(indexes.Last() - indexes.First() + 1)
                .Average(value => value.WidthMeters);
            var link = new WorldMapNavigationLinkPlan("crossing:" + crossing.Id, crossing.Kind, route.Id,
                Lift(first.Position), Lift(last.Position), width);
            return new WorldMapCrossingPlan(crossing, link);
        }

        private void AddChokepointLinks(IReadOnlyList<WorldMapRoadRoutePlan> routes,
            IReadOnlyList<WorldMapCrossingPlan> crossings, ICollection<WorldMapNavigationLinkPlan> links)
        {
            foreach (var chokepoint in roads.Chokepoints.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                var crossing = crossings.FirstOrDefault(value =>
                    Distance(value.Definition.Position, chokepoint.Position) <= chokepoint.RadiusMeters * 0.25f);
                if (crossing != null) continue;
                var nearest = routes.SelectMany(route => route.Samples.Select((sample, index) =>
                        new { Route = route, Sample = sample, Index = index }))
                    .OrderBy(value => Distance(value.Sample.DesignPosition, chokepoint.Position)).First();
                var halfSpan = Math.Max(1, (int)Math.Ceiling(chokepoint.RadiusMeters / SampleSpacingMeters));
                var start = Math.Max(0, nearest.Index - halfSpan);
                var end = Math.Min(nearest.Route.Samples.Count - 1, nearest.Index + halfSpan);
                while (start > 0 && nearest.Route.Samples[start].IsWater) start--;
                while (end + 1 < nearest.Route.Samples.Count && nearest.Route.Samples[end].IsWater) end++;
                if (start == end || nearest.Route.Samples[start].IsWater || nearest.Route.Samples[end].IsWater)
                    throw new InvalidOperationException("Chokepoint link has no dry endpoints: " + chokepoint.Id);
                var kind = chokepoint.Id.IndexOf("marsh", StringComparison.Ordinal) >= 0
                    ? "causeway" : "mountain_pass";
                links.Add(new WorldMapNavigationLinkPlan("chokepoint:" + chokepoint.Id, kind,
                    nearest.Route.Id, Lift(nearest.Route.Samples[start].Position),
                    Lift(nearest.Route.Samples[end].Position), nearest.Route.Samples[nearest.Index].WidthMeters));
            }
        }

        private IEnumerable<string> ReachableRegions(IEnumerable<WorldMapRoadRoutePlan> routes)
        {
            var adjacency = definition.MapData.Regions.ToDictionary(value => value.Id,
                value => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var route in routes)
            {
                var sequence = route.Samples.Select(sample => definition.MapData.Regions.FirstOrDefault(region =>
                        region.Contains(sample.DesignPosition))?.Id).Where(value => value != null)
                    .Distinct().ToArray();
                for (var a = 0; a < sequence.Length; a++)
                for (var b = a + 1; b < sequence.Length; b++)
                {
                    adjacency[sequence[a]].Add(sequence[b]);
                    adjacency[sequence[b]].Add(sequence[a]);
                }
            }
            var startDesign = definition.MapData.CoordinateTransform.ToDesign(definition.PlayerStart);
            var start = definition.MapData.Regions.Single(value => value.Contains(startDesign)).Id;
            var reachable = new HashSet<string>(StringComparer.Ordinal) { start };
            var pending = new Queue<string>();
            pending.Enqueue(start);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var next in adjacency[current]) if (reachable.Add(next)) pending.Enqueue(next);
            }
            return reachable.OrderBy(value => value, StringComparer.Ordinal);
        }

        private static WorldPosition Lift(WorldPosition value) =>
            new WorldPosition(value.X, value.Y + 0.12f, value.Z);

        private static WorldMapPoint Lerp(WorldMapPoint a, WorldMapPoint b, float t) =>
            new WorldMapPoint(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);

        private static float Distance(WorldMapPoint a, WorldMapPoint b)
        {
            var x = b.X - a.X;
            var z = b.Z - a.Z;
            return (float)Math.Sqrt(x * x + z * z);
        }

        private static float DistanceToSegment(WorldMapPoint point, WorldMapPoint a, WorldMapPoint b,
            out float t)
        {
            var x = b.X - a.X;
            var z = b.Z - a.Z;
            var squared = x * x + z * z;
            t = squared <= 0f ? 0f : ((point.X - a.X) * x + (point.Z - a.Z) * z) / squared;
            t = Math.Max(0f, Math.Min(1f, t));
            return Distance(point, Lerp(a, b, t));
        }

        private sealed class ControlPoint
        {
            public ControlPoint(WorldMapPoint position, float widthMeters)
            { Position = position; WidthMeters = widthMeters; }
            public WorldMapPoint Position { get; }
            public float WidthMeters { get; }
        }

        private sealed class MutableSample
        {
            public MutableSample(WorldMapPoint design, WorldPosition unity, float width, bool water)
            { Design = design; Unity = unity; Width = width; Water = water; CrossingId = string.Empty; }
            public WorldMapPoint Design { get; }
            public WorldPosition Unity { get; }
            public float Width { get; }
            public bool Water { get; }
            public string CrossingId { get; set; }
        }

    }
}
