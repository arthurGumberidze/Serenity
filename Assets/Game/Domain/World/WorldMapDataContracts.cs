using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    public readonly struct WorldMapPoint : IEquatable<WorldMapPoint>
    {
        public WorldMapPoint(float x, float z)
        {
            if (!IsFinite(x) || !IsFinite(z)) throw new ArgumentOutOfRangeException(nameof(x));
            X = x;
            Z = z;
        }

        public float X { get; }
        public float Z { get; }
        public bool Equals(WorldMapPoint other) => X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is WorldMapPoint other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Z.GetHashCode());
        public override string ToString() => $"({X:0.###}, {Z:0.###})";
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct WorldMapBounds
    {
        public WorldMapBounds(float xMin, float xMax, float zMin, float zMax)
        {
            if (!WorldMapPoint.IsFinite(xMin) || !WorldMapPoint.IsFinite(xMax) ||
                !WorldMapPoint.IsFinite(zMin) || !WorldMapPoint.IsFinite(zMax) || xMax <= xMin || zMax <= zMin)
                throw new ArgumentOutOfRangeException(nameof(xMax));
            XMin = xMin;
            XMax = xMax;
            ZMin = zMin;
            ZMax = zMax;
        }

        public float XMin { get; }
        public float XMax { get; }
        public float ZMin { get; }
        public float ZMax { get; }
        public float Width => XMax - XMin;
        public float Depth => ZMax - ZMin;
        public bool Contains(WorldMapPoint point) => point.X >= XMin && point.X <= XMax &&
                                                     point.Z >= ZMin && point.Z <= ZMax;
    }

    /// <summary>Pure design-space to Unity-space translation. It never mutates authored coordinates.</summary>
    public sealed class WorldMapCoordinateTransform
    {
        public WorldMapCoordinateTransform(WorldMapBounds designBounds, float unityOffsetX, float unityOffsetZ)
        {
            if (!WorldMapPoint.IsFinite(unityOffsetX) || !WorldMapPoint.IsFinite(unityOffsetZ))
                throw new ArgumentOutOfRangeException(nameof(unityOffsetX));
            DesignBounds = designBounds;
            UnityOffsetX = unityOffsetX;
            UnityOffsetZ = unityOffsetZ;
        }

        public WorldMapBounds DesignBounds { get; }
        public float UnityOffsetX { get; }
        public float UnityOffsetZ { get; }

        public WorldPosition ToUnity(WorldMapPoint designPoint, float y = 0f)
        {
            if (!DesignBounds.Contains(designPoint) || !WorldMapPoint.IsFinite(y))
                throw new ArgumentOutOfRangeException(nameof(designPoint));
            return new WorldPosition(designPoint.X + UnityOffsetX, y, designPoint.Z + UnityOffsetZ);
        }

        public WorldMapPoint ToDesign(WorldPosition unityPoint)
        {
            var result = new WorldMapPoint(unityPoint.X - UnityOffsetX, unityPoint.Z - UnityOffsetZ);
            if (!DesignBounds.Contains(result)) throw new ArgumentOutOfRangeException(nameof(unityPoint));
            return result;
        }
    }

    public sealed class WorldMapPolygon
    {
        private const float Epsilon = 0.0001f;

        public WorldMapPolygon(string id, IEnumerable<WorldMapPoint> points, WorldMapBounds bounds)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            var values = (points ?? throw new ArgumentNullException(nameof(points))).ToArray();
            if (values.Length < 3 || values.Any(point => !bounds.Contains(point)))
                throw new ArgumentException("A polygon needs at least three in-bounds points.", nameof(points));
            for (var i = 0; i < values.Length; i++)
                if (values[i].Equals(values[(i + 1) % values.Length]))
                    throw new ArgumentException("Polygon edges must have non-zero length.", nameof(points));
            if (Math.Abs(SignedArea(values)) <= Epsilon)
                throw new ArgumentException("Polygon area must be non-zero.", nameof(points));
            ValidateSimple(values);
            Id = id;
            Points = Array.AsReadOnly(values);
        }

        public string Id { get; }
        public IReadOnlyList<WorldMapPoint> Points { get; }

        public bool Contains(WorldMapPoint point)
        {
            var inside = false;
            for (var i = 0; i < Points.Count; i++)
            {
                var a = Points[i];
                var b = Points[(i + Points.Count - 1) % Points.Count];
                if (OnSegment(a, b, point)) return true;
                if ((a.Z > point.Z) != (b.Z > point.Z) &&
                    point.X < (b.X - a.X) * (point.Z - a.Z) / (b.Z - a.Z) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        private static double SignedArea(IReadOnlyList<WorldMapPoint> points)
        {
            double area = 0;
            for (var i = 0; i < points.Count; i++)
            {
                var next = points[(i + 1) % points.Count];
                area += (double)points[i].X * next.Z - (double)next.X * points[i].Z;
            }
            return area * 0.5;
        }

        private static void ValidateSimple(IReadOnlyList<WorldMapPoint> points)
        {
            for (var i = 0; i < points.Count; i++)
            for (var j = i + 1; j < points.Count; j++)
            {
                var iNext = (i + 1) % points.Count;
                var jNext = (j + 1) % points.Count;
                if (i == j || iNext == j || jNext == i) continue;
                if (SegmentsIntersect(points[i], points[iNext], points[j], points[jNext]))
                    throw new ArgumentException("Polygon must not self-intersect.", nameof(points));
            }
        }

        private static bool SegmentsIntersect(WorldMapPoint a, WorldMapPoint b, WorldMapPoint c, WorldMapPoint d)
        {
            var abC = Cross(a, b, c);
            var abD = Cross(a, b, d);
            var cdA = Cross(c, d, a);
            var cdB = Cross(c, d, b);
            if (((abC > Epsilon && abD < -Epsilon) || (abC < -Epsilon && abD > Epsilon)) &&
                ((cdA > Epsilon && cdB < -Epsilon) || (cdA < -Epsilon && cdB > Epsilon))) return true;
            return Math.Abs(abC) <= Epsilon && OnSegment(a, b, c) ||
                   Math.Abs(abD) <= Epsilon && OnSegment(a, b, d) ||
                   Math.Abs(cdA) <= Epsilon && OnSegment(c, d, a) ||
                   Math.Abs(cdB) <= Epsilon && OnSegment(c, d, b);
        }

        private static float Cross(WorldMapPoint a, WorldMapPoint b, WorldMapPoint c) =>
            (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);

        private static bool OnSegment(WorldMapPoint a, WorldMapPoint b, WorldMapPoint point) =>
            Math.Abs(Cross(a, b, point)) <= Epsilon && point.X >= Math.Min(a.X, b.X) - Epsilon &&
            point.X <= Math.Max(a.X, b.X) + Epsilon && point.Z >= Math.Min(a.Z, b.Z) - Epsilon &&
            point.Z <= Math.Max(a.Z, b.Z) + Epsilon;
    }

    public enum WorldMapShapeKind { Polygon, Ellipse, Ring }

    public sealed class WorldMapShape
    {
        private WorldMapShape(WorldMapShapeKind kind, WorldMapPolygon polygon, WorldMapPoint center,
            float radiusX, float radiusZ, float innerRadius)
        {
            Kind = kind;
            Polygon = polygon;
            Center = center;
            RadiusX = radiusX;
            RadiusZ = radiusZ;
            InnerRadius = innerRadius;
        }

        public WorldMapShapeKind Kind { get; }
        public WorldMapPolygon Polygon { get; }
        public WorldMapPoint Center { get; }
        public float RadiusX { get; }
        public float RadiusZ { get; }
        public float InnerRadius { get; }

        public static WorldMapShape FromPolygon(WorldMapPolygon polygon) =>
            new WorldMapShape(WorldMapShapeKind.Polygon, polygon ?? throw new ArgumentNullException(nameof(polygon)),
                default, 0f, 0f, 0f);

        public static WorldMapShape Ellipse(WorldMapPoint center, float radiusX, float radiusZ,
            WorldMapBounds bounds)
        {
            ValidateEllipse(center, radiusX, radiusZ, bounds);
            return new WorldMapShape(WorldMapShapeKind.Ellipse, null, center, radiusX, radiusZ, 0f);
        }

        public static WorldMapShape Ring(WorldMapPoint center, float innerRadius, float outerRadius,
            WorldMapBounds bounds)
        {
            ValidateEllipse(center, outerRadius, outerRadius, bounds);
            if (!WorldMapPoint.IsFinite(innerRadius) || innerRadius < 0f || innerRadius >= outerRadius)
                throw new ArgumentOutOfRangeException(nameof(innerRadius));
            return new WorldMapShape(WorldMapShapeKind.Ring, null, center, outerRadius, outerRadius, innerRadius);
        }

        public bool Contains(WorldMapPoint point)
        {
            if (Kind == WorldMapShapeKind.Polygon) return Polygon.Contains(point);
            var dx = (point.X - Center.X) / RadiusX;
            var dz = (point.Z - Center.Z) / RadiusZ;
            var distance = dx * dx + dz * dz;
            if (Kind == WorldMapShapeKind.Ellipse) return distance <= 1f;
            var inner = InnerRadius / RadiusX;
            return distance <= 1f && distance >= inner * inner;
        }

        private static void ValidateEllipse(WorldMapPoint center, float radiusX, float radiusZ,
            WorldMapBounds bounds)
        {
            if (!WorldMapPoint.IsFinite(radiusX) || !WorldMapPoint.IsFinite(radiusZ) ||
                radiusX <= 0f || radiusZ <= 0f || !bounds.Contains(center))
                throw new ArgumentOutOfRangeException(nameof(radiusX));
        }
    }

    public readonly struct WorldMapSplinePoint
    {
        public WorldMapSplinePoint(WorldMapPoint position, float widthMeters)
        {
            if (!WorldMapPoint.IsFinite(widthMeters) || widthMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(widthMeters));
            Position = position;
            WidthMeters = widthMeters;
        }
        public WorldMapPoint Position { get; }
        public float WidthMeters { get; }
    }

    public sealed class WorldMapSpline
    {
        public WorldMapSpline(string id, IEnumerable<WorldMapSplinePoint> points, WorldMapBounds bounds,
            float minimumWidthMeters = 0f, float maximumWidthMeters = 0f)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            var values = (points ?? throw new ArgumentNullException(nameof(points))).ToArray();
            if (values.Length < 2 || values.Any(point => !bounds.Contains(point.Position)))
                throw new ArgumentException("A spline needs at least two in-bounds points.", nameof(points));
            for (var i = 1; i < values.Length; i++)
                if (values[i - 1].Position.Equals(values[i].Position))
                    throw new ArgumentException("Spline segments must have non-zero length.", nameof(points));
            var pointMinimum = values.Min(point => point.WidthMeters);
            var pointMaximum = values.Max(point => point.WidthMeters);
            minimumWidthMeters = minimumWidthMeters > 0f ? minimumWidthMeters : pointMinimum;
            maximumWidthMeters = maximumWidthMeters > 0f ? maximumWidthMeters : pointMaximum;
            if (!WorldMapPoint.IsFinite(minimumWidthMeters) || !WorldMapPoint.IsFinite(maximumWidthMeters) ||
                minimumWidthMeters <= 0f || maximumWidthMeters < minimumWidthMeters)
                throw new ArgumentOutOfRangeException(nameof(minimumWidthMeters));
            Id = id;
            Points = Array.AsReadOnly(values);
            MinimumWidthMeters = minimumWidthMeters;
            MaximumWidthMeters = maximumWidthMeters;
        }
        public string Id { get; }
        public IReadOnlyList<WorldMapSplinePoint> Points { get; }
        public float MinimumWidthMeters { get; }
        public float MaximumWidthMeters { get; }
    }

    public enum WorldMapWaterLevelPolicyKind
    {
        Reference,
        Fixed,
        LinearDrainage
    }

    /// <summary>Immutable vertical policy used by terrain carving and every water projection.</summary>
    public sealed class WorldMapWaterLevelPolicy
    {
        public WorldMapWaterLevelPolicy(WorldMapWaterLevelPolicyKind kind, float upstreamMeters,
            float downstreamMeters)
        {
            if (!WorldMapPoint.IsFinite(upstreamMeters) || !WorldMapPoint.IsFinite(downstreamMeters) ||
                kind != WorldMapWaterLevelPolicyKind.LinearDrainage &&
                Math.Abs(upstreamMeters - downstreamMeters) > 0.0001f || downstreamMeters > upstreamMeters)
                throw new ArgumentOutOfRangeException(nameof(downstreamMeters));
            Kind = kind;
            UpstreamMeters = upstreamMeters;
            DownstreamMeters = downstreamMeters;
        }

        public WorldMapWaterLevelPolicyKind Kind { get; }
        public float UpstreamMeters { get; }
        public float DownstreamMeters { get; }
        public float Sample(float normalizedDistance)
        {
            normalizedDistance = Math.Max(0f, Math.Min(1f, normalizedDistance));
            return UpstreamMeters + (DownstreamMeters - UpstreamMeters) * normalizedDistance;
        }
    }

    public sealed class WorldMapWaterCourse
    {
        public WorldMapWaterCourse(WorldMapSpline spline, float minimumDepthMeters, float maximumDepthMeters,
            float minimumBankBlendMeters, float maximumBankBlendMeters, IEnumerable<string> connections,
            WorldMapWaterLevelPolicy surfaceLevelPolicy)
        {
            if (!WorldMapPoint.IsFinite(minimumDepthMeters) || !WorldMapPoint.IsFinite(maximumDepthMeters) ||
                minimumDepthMeters <= 0f || maximumDepthMeters < minimumDepthMeters ||
                !WorldMapPoint.IsFinite(minimumBankBlendMeters) ||
                !WorldMapPoint.IsFinite(maximumBankBlendMeters) || minimumBankBlendMeters <= 0f ||
                maximumBankBlendMeters < minimumBankBlendMeters)
                throw new ArgumentOutOfRangeException(nameof(maximumDepthMeters));
            var connectionIds = (connections ?? throw new ArgumentNullException(nameof(connections))).ToArray();
            if (connectionIds.Any(string.IsNullOrWhiteSpace) || connectionIds.Distinct(StringComparer.Ordinal).Count() !=
                connectionIds.Length)
                throw new ArgumentException("Water-course connections require unique stable IDs.", nameof(connections));
            Spline = spline ?? throw new ArgumentNullException(nameof(spline));
            MinimumDepthMeters = minimumDepthMeters;
            MaximumDepthMeters = maximumDepthMeters;
            MinimumBankBlendMeters = minimumBankBlendMeters;
            MaximumBankBlendMeters = maximumBankBlendMeters;
            Connections = Array.AsReadOnly(connectionIds);
            SurfaceLevelPolicy = surfaceLevelPolicy ?? throw new ArgumentNullException(nameof(surfaceLevelPolicy));
        }

        public string Id => Spline.Id;
        public WorldMapSpline Spline { get; }
        public float MinimumDepthMeters { get; }
        public float MaximumDepthMeters { get; }
        public float MinimumBankBlendMeters { get; }
        public float MaximumBankBlendMeters { get; }
        public IReadOnlyList<string> Connections { get; }
        public WorldMapWaterLevelPolicy SurfaceLevelPolicy { get; }
    }

    public sealed class WorldMapWaterBody
    {
        public WorldMapWaterBody(string id, WorldMapPoint center, WorldMapPolygon shoreline, float surfaceY,
            float maximumDepthMeters, float minimumBankBlendMeters, float maximumBankBlendMeters,
            string shoreKind, IEnumerable<WorldMapPoint> fishingLandings)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            if (!WorldMapPoint.IsFinite(surfaceY) || !WorldMapPoint.IsFinite(maximumDepthMeters) ||
                maximumDepthMeters <= 0f || !WorldMapPoint.IsFinite(minimumBankBlendMeters) ||
                !WorldMapPoint.IsFinite(maximumBankBlendMeters) || minimumBankBlendMeters <= 0f ||
                maximumBankBlendMeters < minimumBankBlendMeters || string.IsNullOrWhiteSpace(shoreKind))
                throw new ArgumentOutOfRangeException(nameof(surfaceY));
            Id = id;
            Center = center;
            Shoreline = shoreline ?? throw new ArgumentNullException(nameof(shoreline));
            SurfaceY = surfaceY;
            MaximumDepthMeters = maximumDepthMeters;
            MinimumBankBlendMeters = minimumBankBlendMeters;
            MaximumBankBlendMeters = maximumBankBlendMeters;
            ShoreKind = shoreKind;
            SurfaceLevelPolicy = new WorldMapWaterLevelPolicy(WorldMapWaterLevelPolicyKind.Fixed,
                surfaceY, surfaceY);
            FishingLandings = Array.AsReadOnly((fishingLandings ??
                throw new ArgumentNullException(nameof(fishingLandings))).ToArray());
        }
        public string Id { get; }
        public WorldMapPoint Center { get; }
        public WorldMapPolygon Shoreline { get; }
        public float SurfaceY { get; }
        public float MaximumDepthMeters { get; }
        public float MinimumBankBlendMeters { get; }
        public float MaximumBankBlendMeters { get; }
        public string ShoreKind { get; }
        public WorldMapWaterLevelPolicy SurfaceLevelPolicy { get; }
        public IReadOnlyList<WorldMapPoint> FishingLandings { get; }
    }

    /// <summary>Canonical immutable hydrology graph; runtime services only evaluate this data.</summary>
    public sealed class WorldMapHydrologyDefinition
    {
        public WorldMapHydrologyDefinition(float referenceWaterLevelMeters, WorldMapWaterCourse mainRiver,
            IEnumerable<WorldMapWaterCourse> tributaries, IEnumerable<WorldMapWaterBody> lakes)
        {
            if (!WorldMapPoint.IsFinite(referenceWaterLevelMeters))
                throw new ArgumentOutOfRangeException(nameof(referenceWaterLevelMeters));
            ReferenceWaterLevelMeters = referenceWaterLevelMeters;
            MainRiver = mainRiver ?? throw new ArgumentNullException(nameof(mainRiver));
            Tributaries = ReadOnly(tributaries, nameof(tributaries), value => value.Id);
            Lakes = ReadOnly(lakes, nameof(lakes), value => value.Id);
            if (Lakes.Count != 4) throw new ArgumentException("World Map V2 requires exactly four lakes.", nameof(lakes));
            ValidateDrainage();
        }

        public float ReferenceWaterLevelMeters { get; }
        public WorldMapWaterCourse MainRiver { get; }
        public IReadOnlyList<WorldMapWaterCourse> Tributaries { get; }
        public IReadOnlyList<WorldMapWaterBody> Lakes { get; }

        private void ValidateDrainage()
        {
            var waterIds = new HashSet<string>(Lakes.Select(value => value.Id), StringComparer.Ordinal)
                { MainRiver.Id };
            var adjacency = waterIds.ToDictionary(value => value,
                _ => new HashSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
            foreach (var tributary in Tributaries)
            {
                if (tributary.Connections.Count != 2 || tributary.Connections.Any(value => !waterIds.Contains(value)))
                    throw new ArgumentException($"Tributary {tributary.Id} must connect two canonical water bodies.");
                var from = tributary.Connections[0];
                var to = tributary.Connections[1];
                adjacency[from].Add(to);
            }
            foreach (var lake in Lakes)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal) { lake.Id };
                var pending = new Queue<string>();
                pending.Enqueue(lake.Id);
                while (pending.Count > 0)
                    foreach (var next in adjacency[pending.Dequeue()])
                        if (visited.Add(next)) pending.Enqueue(next);
                if (!visited.Contains(MainRiver.Id))
                    throw new ArgumentException($"Lake {lake.Id} has no surface drainage path to {MainRiver.Id}.");
            }
        }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values, string parameter,
            Func<T, string> identity) where T : class
        {
            var array = (values ?? throw new ArgumentNullException(parameter)).ToArray();
            if (array.Any(value => value == null) || array.Select(identity).Distinct(StringComparer.Ordinal).Count() !=
                array.Length)
                throw new ArgumentException("Hydrology definitions require unique stable IDs.", parameter);
            return Array.AsReadOnly(array);
        }
    }

    public sealed class WorldMapPointOfInterest
    {
        public WorldMapPointOfInterest(string id, WorldMapPoint position, string regionId,
            float protectionRadiusMeters, IEnumerable<string> purposes)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(regionId))
                throw new ArgumentException(nameof(id));
            if (!WorldMapPoint.IsFinite(protectionRadiusMeters) || protectionRadiusMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(protectionRadiusMeters));
            Id = id;
            Position = position;
            RegionId = regionId;
            ProtectionRadiusMeters = protectionRadiusMeters;
            Purposes = Array.AsReadOnly((purposes ?? throw new ArgumentNullException(nameof(purposes))).ToArray());
        }
        public string Id { get; }
        public WorldMapPoint Position { get; }
        public string RegionId { get; }
        public float ProtectionRadiusMeters { get; }
        public IReadOnlyList<string> Purposes { get; }
    }

    public sealed class WorldMapCrossing
    {
        public WorldMapCrossing(string id, WorldMapPoint position, string kind, IEnumerable<string> routeIds)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(kind))
                throw new ArgumentException(nameof(id));
            var routes = (routeIds ?? throw new ArgumentNullException(nameof(routeIds))).ToArray();
            if (routes.Length == 0 || routes.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException(nameof(routeIds));
            Id = id;
            Position = position;
            Kind = kind;
            RouteIds = Array.AsReadOnly(routes);
        }
        public string Id { get; }
        public WorldMapPoint Position { get; }
        public string Kind { get; }
        public IReadOnlyList<string> RouteIds { get; }
    }

    public sealed class WorldMapMask
    {
        public WorldMapMask(string id, string layer, WorldMapShape shape, IEnumerable<string> references,
            string category, float multiplier = 1f, float slopeMaxDegrees = 0f, bool requiresClearing = false)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(layer) ||
                string.IsNullOrWhiteSpace(category) || !WorldMapPoint.IsFinite(multiplier) || multiplier <= 0f ||
                !WorldMapPoint.IsFinite(slopeMaxDegrees) || slopeMaxDegrees < 0f || slopeMaxDegrees > 90f)
                throw new ArgumentException(nameof(id));
            var values = (references ?? throw new ArgumentNullException(nameof(references))).ToArray();
            if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException(nameof(references));
            Id = id;
            Layer = layer;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            References = Array.AsReadOnly(values);
            Category = category;
            Multiplier = multiplier;
            SlopeMaxDegrees = slopeMaxDegrees;
            RequiresClearing = requiresClearing;
        }
        public string Id { get; }
        public string Layer { get; }
        public WorldMapShape Shape { get; }
        public IReadOnlyList<string> References { get; }
        public string Category { get; }
        public float Multiplier { get; }
        public float SlopeMaxDegrees { get; }
        public bool RequiresClearing { get; }
    }

    public sealed class WorldMapHeightMask
    {
        public WorldMapHeightMask(string id, string operation, WorldMapShape shape, WorldMapSpline spline,
            string source, float minimumValue, float maximumValue)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(operation) ||
                !WorldMapPoint.IsFinite(minimumValue) || !WorldMapPoint.IsFinite(maximumValue) ||
                maximumValue < minimumValue || shape == null && spline == null && string.IsNullOrWhiteSpace(source))
                throw new ArgumentException(nameof(id));
            Id = id;
            Operation = operation;
            Shape = shape;
            Spline = spline;
            Source = source ?? string.Empty;
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
        }
        public string Id { get; }
        public string Operation { get; }
        public WorldMapShape Shape { get; }
        public WorldMapSpline Spline { get; }
        public string Source { get; }
        public float MinimumValue { get; }
        public float MaximumValue { get; }
    }

    public sealed class WorldMapHeightAnchor
    {
        public WorldMapHeightAnchor(string id, WorldMapPoint position, float heightMeters,
            float radiusMeters, string mode)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(mode) ||
                !WorldMapPoint.IsFinite(heightMeters) || !WorldMapPoint.IsFinite(radiusMeters) ||
                radiusMeters <= 0f)
                throw new ArgumentException(nameof(id));
            Id = id;
            Position = position;
            HeightMeters = heightMeters;
            RadiusMeters = radiusMeters;
            Mode = mode;
        }

        public string Id { get; }
        public WorldMapPoint Position { get; }
        public float HeightMeters { get; }
        public float RadiusMeters { get; }
        public string Mode { get; }
    }

    public sealed class WorldMapNoiseOctave
    {
        public WorldMapNoiseOctave(string id, float minimumWavelengthMeters, float maximumWavelengthMeters,
            float minimumAmplitudeMeters, float maximumAmplitudeMeters, float weight)
        {
            if (string.IsNullOrWhiteSpace(id) || !WorldMapPoint.IsFinite(minimumWavelengthMeters) ||
                !WorldMapPoint.IsFinite(maximumWavelengthMeters) || minimumWavelengthMeters <= 0f ||
                maximumWavelengthMeters < minimumWavelengthMeters ||
                !WorldMapPoint.IsFinite(minimumAmplitudeMeters) ||
                !WorldMapPoint.IsFinite(maximumAmplitudeMeters) || minimumAmplitudeMeters < 0f ||
                maximumAmplitudeMeters < minimumAmplitudeMeters || !WorldMapPoint.IsFinite(weight) || weight <= 0f)
                throw new ArgumentException(nameof(id));
            Id = id;
            MinimumWavelengthMeters = minimumWavelengthMeters;
            MaximumWavelengthMeters = maximumWavelengthMeters;
            MinimumAmplitudeMeters = minimumAmplitudeMeters;
            MaximumAmplitudeMeters = maximumAmplitudeMeters;
            Weight = weight;
        }

        public string Id { get; }
        public float MinimumWavelengthMeters { get; }
        public float MaximumWavelengthMeters { get; }
        public float MinimumAmplitudeMeters { get; }
        public float MaximumAmplitudeMeters { get; }
        public float Weight { get; }
    }

    public sealed class WorldMapSlopeTargets
    {
        public WorldMapSlopeTargets(float buildablePercentMinimum, float slopeUnder12PercentMinimum,
            float slopeUnder12PercentMaximum, float slopeOver32PercentMaximum)
        {
            if (!WorldMapPoint.IsFinite(buildablePercentMinimum) || buildablePercentMinimum < 0f ||
                buildablePercentMinimum > 100f || !WorldMapPoint.IsFinite(slopeUnder12PercentMinimum) ||
                !WorldMapPoint.IsFinite(slopeUnder12PercentMaximum) || slopeUnder12PercentMinimum < 0f ||
                slopeUnder12PercentMaximum < slopeUnder12PercentMinimum || slopeUnder12PercentMaximum > 100f ||
                !WorldMapPoint.IsFinite(slopeOver32PercentMaximum) || slopeOver32PercentMaximum < 0f ||
                slopeOver32PercentMaximum > 100f)
                throw new ArgumentOutOfRangeException(nameof(buildablePercentMinimum));
            BuildablePercentMinimum = buildablePercentMinimum;
            SlopeUnder12PercentMinimum = slopeUnder12PercentMinimum;
            SlopeUnder12PercentMaximum = slopeUnder12PercentMaximum;
            SlopeOver32PercentMaximum = slopeOver32PercentMaximum;
        }

        public float BuildablePercentMinimum { get; }
        public float SlopeUnder12PercentMinimum { get; }
        public float SlopeUnder12PercentMaximum { get; }
        public float SlopeOver32PercentMaximum { get; }
    }

    public sealed class WorldMapTerrainDefinition
    {
        public WorldMapTerrainDefinition(float minimumHeightMeters, float maximumHeightMeters,
            float waterLevelMeters, float minimumTypicalLandMeters, float maximumTypicalLandMeters,
            float ridgePeakMeters, int worldResolution, int tileResolution, bool sharedBorderSamples,
            IEnumerable<WorldMapHeightAnchor> anchors, IEnumerable<WorldMapNoiseOctave> noiseOctaves,
            IEnumerable<WorldMapHeightMask> masks, WorldMapSlopeTargets slopeTargets)
        {
            if (!WorldMapPoint.IsFinite(minimumHeightMeters) || !WorldMapPoint.IsFinite(maximumHeightMeters) ||
                maximumHeightMeters <= minimumHeightMeters || !WorldMapPoint.IsFinite(waterLevelMeters) ||
                waterLevelMeters < minimumHeightMeters || waterLevelMeters > maximumHeightMeters ||
                !WorldMapPoint.IsFinite(minimumTypicalLandMeters) ||
                !WorldMapPoint.IsFinite(maximumTypicalLandMeters) ||
                maximumTypicalLandMeters < minimumTypicalLandMeters ||
                !WorldMapPoint.IsFinite(ridgePeakMeters) || ridgePeakMeters > maximumHeightMeters ||
                worldResolution < 2 || tileResolution < 2)
                throw new ArgumentOutOfRangeException(nameof(maximumHeightMeters));
            MinimumHeightMeters = minimumHeightMeters;
            MaximumHeightMeters = maximumHeightMeters;
            WaterLevelMeters = waterLevelMeters;
            MinimumTypicalLandMeters = minimumTypicalLandMeters;
            MaximumTypicalLandMeters = maximumTypicalLandMeters;
            RidgePeakMeters = ridgePeakMeters;
            WorldResolution = worldResolution;
            TileResolution = tileResolution;
            SharedBorderSamples = sharedBorderSamples;
            Anchors = ReadOnly(anchors, nameof(anchors), value => value.Id);
            NoiseOctaves = ReadOnly(noiseOctaves, nameof(noiseOctaves), value => value.Id);
            HeightMasks = ReadOnly(masks, nameof(masks), value => value.Id);
            SlopeTargets = slopeTargets ?? throw new ArgumentNullException(nameof(slopeTargets));
            if (Math.Abs(NoiseOctaves.Sum(value => value.Weight) - 1f) > 0.0001f)
                throw new ArgumentException("Terrain noise weights must total one.", nameof(noiseOctaves));
        }

        public float MinimumHeightMeters { get; }
        public float MaximumHeightMeters { get; }
        public float WaterLevelMeters { get; }
        public float MinimumTypicalLandMeters { get; }
        public float MaximumTypicalLandMeters { get; }
        public float RidgePeakMeters { get; }
        public int WorldResolution { get; }
        public int TileResolution { get; }
        public bool SharedBorderSamples { get; }
        public IReadOnlyList<WorldMapHeightAnchor> Anchors { get; }
        public IReadOnlyList<WorldMapNoiseOctave> NoiseOctaves { get; }
        public IReadOnlyList<WorldMapHeightMask> HeightMasks { get; }
        public WorldMapSlopeTargets SlopeTargets { get; }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values, string parameter,
            Func<T, string> identity) where T : class
        {
            var array = (values ?? throw new ArgumentNullException(parameter)).ToArray();
            if (array.Any(value => value == null) || array.Select(identity)
                    .Distinct(StringComparer.Ordinal).Count() != array.Length)
                throw new ArgumentException("Terrain definitions require unique stable IDs.", parameter);
            return Array.AsReadOnly(array);
        }
    }

    /// <summary>Immutable WM01 package. Later stages consume these contracts; none is mutable save-state.</summary>
    public sealed class WorldMapDataDefinition
    {
        public WorldMapDataDefinition(int schemaVersion, string mapVersion, string generationId,
            WorldMapCoordinateTransform coordinateTransform, WorldMapTerrainDefinition terrain,
            WorldMapHydrologyDefinition hydrology,
            IEnumerable<WorldMapPolygon> regions,
            WorldMapSpline river, IEnumerable<WorldMapSpline> tributaries, IEnumerable<WorldMapWaterBody> lakes,
            IEnumerable<WorldMapSpline> routes, IEnumerable<WorldMapPointOfInterest> pointsOfInterest,
            IEnumerable<WorldMapCrossing> crossings, IEnumerable<WorldMapMask> resourceMasks,
            IEnumerable<WorldMapMask> wildlifeMasks, IEnumerable<WorldMapMask> buildabilityMasks,
            IEnumerable<WorldMapMask> vegetationMasks, IEnumerable<WorldMapHeightMask> heightMasks)
        {
            if (schemaVersion <= 0 || string.IsNullOrWhiteSpace(mapVersion) || string.IsNullOrWhiteSpace(generationId))
                throw new ArgumentException(nameof(schemaVersion));
            SchemaVersion = schemaVersion;
            MapVersion = mapVersion;
            GenerationId = generationId;
            CoordinateTransform = coordinateTransform ?? throw new ArgumentNullException(nameof(coordinateTransform));
            Terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            Hydrology = hydrology ?? throw new ArgumentNullException(nameof(hydrology));
            Regions = ReadOnly(regions, nameof(regions));
            River = river ?? throw new ArgumentNullException(nameof(river));
            Tributaries = ReadOnly(tributaries, nameof(tributaries));
            Lakes = ReadOnly(lakes, nameof(lakes));
            Routes = ReadOnly(routes, nameof(routes));
            PointsOfInterest = ReadOnly(pointsOfInterest, nameof(pointsOfInterest));
            Crossings = ReadOnly(crossings, nameof(crossings));
            ResourceMasks = ReadOnly(resourceMasks, nameof(resourceMasks));
            WildlifeMasks = ReadOnly(wildlifeMasks, nameof(wildlifeMasks));
            BuildabilityMasks = ReadOnly(buildabilityMasks, nameof(buildabilityMasks));
            VegetationMasks = ReadOnly(vegetationMasks, nameof(vegetationMasks));
            HeightMasks = ReadOnly(heightMasks, nameof(heightMasks));
        }

        public int SchemaVersion { get; }
        public string MapVersion { get; }
        public string GenerationId { get; }
        public WorldMapCoordinateTransform CoordinateTransform { get; }
        public WorldMapTerrainDefinition Terrain { get; }
        public WorldMapHydrologyDefinition Hydrology { get; }
        public IReadOnlyList<WorldMapPolygon> Regions { get; }
        public WorldMapSpline River { get; }
        public IReadOnlyList<WorldMapSpline> Tributaries { get; }
        public IReadOnlyList<WorldMapWaterBody> Lakes { get; }
        public IReadOnlyList<WorldMapSpline> Routes { get; }
        public IReadOnlyList<WorldMapPointOfInterest> PointsOfInterest { get; }
        public IReadOnlyList<WorldMapCrossing> Crossings { get; }
        public IReadOnlyList<WorldMapMask> ResourceMasks { get; }
        public IReadOnlyList<WorldMapMask> WildlifeMasks { get; }
        public IReadOnlyList<WorldMapMask> BuildabilityMasks { get; }
        public IReadOnlyList<WorldMapMask> VegetationMasks { get; }
        public IReadOnlyList<WorldMapHeightMask> HeightMasks { get; }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values, string parameter) where T : class
        {
            var array = (values ?? throw new ArgumentNullException(parameter)).ToArray();
            if (array.Any(value => value == null) || array.Select(Identity).Distinct(StringComparer.Ordinal).Count() != array.Length)
                throw new ArgumentException("Definitions require unique stable IDs.", parameter);
            return Array.AsReadOnly(array);
        }

        private static string Identity<T>(T value)
        {
            if (value is WorldMapPolygon polygon) return polygon.Id;
            if (value is WorldMapSpline spline) return spline.Id;
            if (value is WorldMapWaterBody water) return water.Id;
            if (value is WorldMapPointOfInterest poi) return poi.Id;
            if (value is WorldMapCrossing crossing) return crossing.Id;
            if (value is WorldMapMask mask) return mask.Id;
            if (value is WorldMapHeightMask heightMask) return heightMask.Id;
            throw new InvalidOperationException(typeof(T).FullName);
        }
    }
}
