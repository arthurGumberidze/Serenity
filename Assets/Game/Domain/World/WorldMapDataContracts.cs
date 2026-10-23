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

    /// <summary>Immutable authored biome metadata attached to one canonical region polygon.</summary>
    public sealed class WorldMapBiomeRegion
    {
        public WorldMapBiomeRegion(WorldMapPolygon polygon, string code, string name, string englishName,
            float minimumElevationMeters, float maximumElevationMeters, IEnumerable<string> biomes,
            IEnumerable<string> soils, IEnumerable<string> gameplayTags)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(englishName) || !WorldMapPoint.IsFinite(minimumElevationMeters) ||
                !WorldMapPoint.IsFinite(maximumElevationMeters) || maximumElevationMeters < minimumElevationMeters)
                throw new ArgumentException(nameof(code));
            Polygon = polygon;
            Code = code;
            Name = name;
            EnglishName = englishName;
            MinimumElevationMeters = minimumElevationMeters;
            MaximumElevationMeters = maximumElevationMeters;
            Biomes = Strings(biomes, nameof(biomes));
            Soils = Strings(soils, nameof(soils));
            GameplayTags = Strings(gameplayTags, nameof(gameplayTags));
        }

        public string Id => Polygon.Id;
        public WorldMapPolygon Polygon { get; }
        public string Code { get; }
        public string Name { get; }
        public string EnglishName { get; }
        public float MinimumElevationMeters { get; }
        public float MaximumElevationMeters { get; }
        public IReadOnlyList<string> Biomes { get; }
        public IReadOnlyList<string> Soils { get; }
        public IReadOnlyList<string> GameplayTags { get; }

        private static IReadOnlyList<string> Strings(IEnumerable<string> values, string parameter)
        {
            var result = (values ?? throw new ArgumentNullException(parameter)).ToArray();
            if (result.Length == 0 || result.Any(string.IsNullOrWhiteSpace) ||
                result.Distinct(StringComparer.Ordinal).Count() != result.Length)
                throw new ArgumentException("Biome tags must be non-empty and unique.", parameter);
            return Array.AsReadOnly(result);
        }
    }

    public sealed class WorldMapBiomeTransitionDefinition
    {
        public WorldMapBiomeTransitionDefinition(float minimumBlendWidthMeters,
            float preferredMinimumBlendWidthMeters, float preferredMaximumBlendWidthMeters,
            bool noRectangularVisualEdges, IEnumerable<string> ecotoneLayers)
        {
            if (!WorldMapPoint.IsFinite(minimumBlendWidthMeters) || minimumBlendWidthMeters <= 0f ||
                !WorldMapPoint.IsFinite(preferredMinimumBlendWidthMeters) ||
                preferredMinimumBlendWidthMeters < minimumBlendWidthMeters ||
                !WorldMapPoint.IsFinite(preferredMaximumBlendWidthMeters) ||
                preferredMaximumBlendWidthMeters < preferredMinimumBlendWidthMeters ||
                !noRectangularVisualEdges)
                throw new ArgumentOutOfRangeException(nameof(minimumBlendWidthMeters));
            var layers = (ecotoneLayers ?? throw new ArgumentNullException(nameof(ecotoneLayers))).ToArray();
            if (layers.Length == 0 || layers.Any(string.IsNullOrWhiteSpace) ||
                layers.Distinct(StringComparer.Ordinal).Count() != layers.Length)
                throw new ArgumentException(nameof(ecotoneLayers));
            MinimumBlendWidthMeters = minimumBlendWidthMeters;
            PreferredMinimumBlendWidthMeters = preferredMinimumBlendWidthMeters;
            PreferredMaximumBlendWidthMeters = preferredMaximumBlendWidthMeters;
            NoRectangularVisualEdges = noRectangularVisualEdges;
            EcotoneLayers = Array.AsReadOnly(layers);
        }

        public float MinimumBlendWidthMeters { get; }
        public float PreferredMinimumBlendWidthMeters { get; }
        public float PreferredMaximumBlendWidthMeters { get; }
        public bool NoRectangularVisualEdges { get; }
        public IReadOnlyList<string> EcotoneLayers { get; }
    }

    /// <summary>One canonical biome authority consumed by paint, vegetation and later ecology stages.</summary>
    public sealed class WorldMapBiomeDefinition
    {
        public WorldMapBiomeDefinition(IEnumerable<WorldMapBiomeRegion> regions,
            WorldMapBiomeTransitionDefinition transitions)
        {
            var values = (regions ?? throw new ArgumentNullException(nameof(regions))).ToArray();
            if (values.Length == 0 || values.Any(value => value == null) ||
                values.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != values.Length ||
                values.Select(value => value.Code).Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("Biome regions require unique IDs and codes.", nameof(regions));
            Regions = Array.AsReadOnly(values);
            Transitions = transitions ?? throw new ArgumentNullException(nameof(transitions));
        }

        public IReadOnlyList<WorldMapBiomeRegion> Regions { get; }
        public WorldMapBiomeTransitionDefinition Transitions { get; }
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

    public readonly struct WorldMapRange
    {
        public WorldMapRange(float minimum, float maximum)
        {
            if (!WorldMapPoint.IsFinite(minimum) || !WorldMapPoint.IsFinite(maximum) ||
                minimum < 0f || maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            Minimum = minimum;
            Maximum = maximum;
        }

        public float Minimum { get; }
        public float Maximum { get; }
        public float Lerp(float value) => Minimum + (Maximum - Minimum) * Math.Max(0f, Math.Min(1f, value));
        public bool Contains(float value) => value >= Minimum && value <= Maximum;
    }

    public sealed class WorldMapVegetationClass
    {
        public WorldMapVegetationClass(string id, WorldMapRange interactiveTreesPerHectare,
            WorldMapRange decorativeInstancesPerHectare, WorldMapRange canopy)
        {
            if (string.IsNullOrWhiteSpace(id) || canopy.Maximum > 1f)
                throw new ArgumentException(nameof(id));
            Id = id;
            InteractiveTreesPerHectare = interactiveTreesPerHectare;
            DecorativeInstancesPerHectare = decorativeInstancesPerHectare;
            Canopy = canopy;
        }

        public string Id { get; }
        public WorldMapRange InteractiveTreesPerHectare { get; }
        public WorldMapRange DecorativeInstancesPerHectare { get; }
        public WorldMapRange Canopy { get; }
    }

    public readonly struct WorldMapVegetationClearing
    {
        public WorldMapVegetationClearing(WorldMapPoint center, float radiusMeters)
        {
            if (!WorldMapPoint.IsFinite(radiusMeters) || radiusMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            Center = center;
            RadiusMeters = radiusMeters;
        }

        public WorldMapPoint Center { get; }
        public float RadiusMeters { get; }
    }

    public sealed class WorldMapVegetationZone
    {
        public WorldMapVegetationZone(string id, string classId, WorldMapShape shape,
            float densityMultiplier, IEnumerable<WorldMapVegetationClearing> clearings)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(classId) ||
                !WorldMapPoint.IsFinite(densityMultiplier) || densityMultiplier <= 0f)
                throw new ArgumentException(nameof(id));
            Id = id;
            ClassId = classId;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            DensityMultiplier = densityMultiplier;
            Clearings = Array.AsReadOnly((clearings ??
                throw new ArgumentNullException(nameof(clearings))).ToArray());
        }

        public string Id { get; }
        public string ClassId { get; }
        public WorldMapShape Shape { get; }
        public float DensityMultiplier { get; }
        public IReadOnlyList<WorldMapVegetationClearing> Clearings { get; }
    }

    public sealed class WorldMapVegetationDistribution
    {
        public WorldMapVegetationDistribution(string method, WorldMapRange interactiveTreeSpacingMeters,
            float startClearingExclusionRadiusMeters, float mainTrailClearanceMeters,
            float footpathClearanceMeters, WorldMapRange shoreReedBandMeters)
        {
            if (method != "seeded_poisson_disc_plus_density_masks" ||
                interactiveTreeSpacingMeters.Minimum <= 0f || startClearingExclusionRadiusMeters <= 0f ||
                mainTrailClearanceMeters <= 0f || footpathClearanceMeters <= 0f)
                throw new ArgumentException(nameof(method));
            Method = method;
            InteractiveTreeSpacingMeters = interactiveTreeSpacingMeters;
            StartClearingExclusionRadiusMeters = startClearingExclusionRadiusMeters;
            MainTrailClearanceMeters = mainTrailClearanceMeters;
            FootpathClearanceMeters = footpathClearanceMeters;
            ShoreReedBandMeters = shoreReedBandMeters;
        }

        public string Method { get; }
        public WorldMapRange InteractiveTreeSpacingMeters { get; }
        public float StartClearingExclusionRadiusMeters { get; }
        public float MainTrailClearanceMeters { get; }
        public float FootpathClearanceMeters { get; }
        public WorldMapRange ShoreReedBandMeters { get; }
    }

    public sealed class WorldMapVegetationBudget
    {
        public WorldMapVegetationBudget(WorldMapRange interactiveTrees, WorldMapRange decorativeTreesAndShrubs,
            WorldMapRange activeTreeColliders, WorldMapRange interactiveBushesAndHerbs,
            WorldMapRange decorativeGroundInstances)
        {
            InteractiveTrees = interactiveTrees;
            DecorativeTreesAndShrubs = decorativeTreesAndShrubs;
            ActiveTreeColliders = activeTreeColliders;
            InteractiveBushesAndHerbs = interactiveBushesAndHerbs;
            DecorativeGroundInstances = decorativeGroundInstances;
        }

        public WorldMapRange InteractiveTrees { get; }
        public WorldMapRange DecorativeTreesAndShrubs { get; }
        public WorldMapRange ActiveTreeColliders { get; }
        public WorldMapRange InteractiveBushesAndHerbs { get; }
        public WorldMapRange DecorativeGroundInstances { get; }
    }

    /// <summary>Immutable, data-driven vegetation intent. Mutable depletion remains a later resource concern.</summary>
    public sealed class WorldMapVegetationDefinition
    {
        public WorldMapVegetationDefinition(IEnumerable<WorldMapVegetationClass> classes,
            IEnumerable<WorldMapVegetationZone> zones, WorldMapVegetationDistribution distribution,
            WorldMapVegetationBudget budget)
        {
            var classArray = (classes ?? throw new ArgumentNullException(nameof(classes))).ToArray();
            var zoneArray = (zones ?? throw new ArgumentNullException(nameof(zones))).ToArray();
            if (classArray.Length == 0 || classArray.Any(value => value == null) ||
                classArray.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != classArray.Length)
                throw new ArgumentException("Vegetation classes require unique IDs.", nameof(classes));
            if (zoneArray.Length == 0 || zoneArray.Any(value => value == null) ||
                zoneArray.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != zoneArray.Length ||
                zoneArray.Any(value => classArray.All(candidate => candidate.Id != value.ClassId)))
                throw new ArgumentException("Vegetation zones require unique IDs and known classes.", nameof(zones));
            Classes = Array.AsReadOnly(classArray);
            Zones = Array.AsReadOnly(zoneArray);
            Distribution = distribution ?? throw new ArgumentNullException(nameof(distribution));
            Budget = budget ?? throw new ArgumentNullException(nameof(budget));
        }

        public IReadOnlyList<WorldMapVegetationClass> Classes { get; }
        public IReadOnlyList<WorldMapVegetationZone> Zones { get; }
        public WorldMapVegetationDistribution Distribution { get; }
        public WorldMapVegetationBudget Budget { get; }
        public WorldMapVegetationClass Class(string id) => Classes.Single(value => value.Id == id);
    }

    /// <summary>Immutable authored resource metadata. Mutable stock remains in ProductionService.</summary>
    public sealed class WorldMapResourceType
    {
        public WorldMapResourceType(string id, string era, bool visible, string depletionPolicy,
            string renewalPolicy, string unlockTechnologyId)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(era))
                throw new ArgumentException(nameof(id));
            if (visible && string.IsNullOrWhiteSpace(depletionPolicy))
                throw new ArgumentException("Visible resources require a depletion policy.", nameof(depletionPolicy));
            if (!visible && string.IsNullOrWhiteSpace(unlockTechnologyId))
                throw new ArgumentException("Hidden deposits require an unlock technology.", nameof(unlockTechnologyId));
            Id = id;
            Era = era;
            Visible = visible;
            DepletionPolicy = depletionPolicy ?? string.Empty;
            RenewalPolicy = renewalPolicy ?? string.Empty;
            UnlockTechnologyId = unlockTechnologyId ?? string.Empty;
        }

        public string Id { get; }
        public string Era { get; }
        public bool Visible { get; }
        public bool IsHidden => !Visible;
        public string DepletionPolicy { get; }
        public string RenewalPolicy { get; }
        public string UnlockTechnologyId { get; }
    }

    public sealed class WorldMapResourceZone
    {
        public WorldMapResourceZone(string id, WorldMapShape shape, IEnumerable<string> resourceIds,
            string density, string access)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(density) ||
                string.IsNullOrWhiteSpace(access)) throw new ArgumentException(nameof(id));
            var values = (resourceIds ?? throw new ArgumentNullException(nameof(resourceIds))).ToArray();
            if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("Resource-zone references must be unique.", nameof(resourceIds));
            Id = id;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            ResourceIds = Array.AsReadOnly(values);
            Density = density;
            Access = access;
        }

        public string Id { get; }
        public WorldMapShape Shape { get; }
        public IReadOnlyList<string> ResourceIds { get; }
        public string Density { get; }
        public string Access { get; }
    }

    public sealed class WorldMapFishPopulationDefinition
    {
        public WorldMapFishPopulationDefinition(string id, string waterBodyId, bool river,
            int schoolOrdinal, long carryingCapacityUnits)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(waterBodyId))
                throw new ArgumentException(nameof(id));
            if (schoolOrdinal < 0 || carryingCapacityUnits < 20 || carryingCapacityUnits > 50)
                throw new ArgumentOutOfRangeException(nameof(carryingCapacityUnits));
            Id = id;
            WaterBodyId = waterBodyId;
            IsRiver = river;
            SchoolOrdinal = schoolOrdinal;
            CarryingCapacityUnits = carryingCapacityUnits;
        }

        public string Id { get; }
        public string WaterBodyId { get; }
        public bool IsRiver { get; }
        public int SchoolOrdinal { get; }
        public long CarryingCapacityUnits { get; }
    }

    /// <summary>Immutable WM07 population and recovery policy; mutable stock remains in ProductionService.</summary>
    public sealed class WorldMapFisheryDefinition
    {
        public WorldMapFisheryDefinition(IEnumerable<WorldMapFishPopulationDefinition> populations,
            string growthModel, float minimumBankWorkPointOffsetMeters, int dailyGrowthPermille,
            int minimumRecoveryUnits)
        {
            var values = (populations ?? throw new ArgumentNullException(nameof(populations))).ToArray();
            if (values.Length != 24 || values.Any(value => value == null) ||
                values.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("World Map V2 requires exactly 24 unique fish populations.",
                    nameof(populations));
            if (growthModel != "logistic_growth_with_harvest_pressure" ||
                !WorldMapPoint.IsFinite(minimumBankWorkPointOffsetMeters) ||
                minimumBankWorkPointOffsetMeters <= 0f || dailyGrowthPermille <= 0 ||
                dailyGrowthPermille > 1000 || minimumRecoveryUnits <= 0)
                throw new ArgumentOutOfRangeException(nameof(dailyGrowthPermille));
            Populations = Array.AsReadOnly(values.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray());
            GrowthModel = growthModel;
            MinimumBankWorkPointOffsetMeters = minimumBankWorkPointOffsetMeters;
            DailyGrowthPermille = dailyGrowthPermille;
            MinimumRecoveryUnits = minimumRecoveryUnits;
        }

        public IReadOnlyList<WorldMapFishPopulationDefinition> Populations { get; }
        public string GrowthModel { get; }
        public float MinimumBankWorkPointOffsetMeters { get; }
        public int DailyGrowthPermille { get; }
        public int MinimumRecoveryUnits { get; }
    }

    /// <summary>Canonical WM06 masks and visibility policy; it contains no mutable depletion state.</summary>
    public sealed class WorldMapResourceDefinition
    {
        public WorldMapResourceDefinition(IEnumerable<WorldMapResourceType> types,
            IEnumerable<WorldMapResourceZone> zones, WorldMapFisheryDefinition fishery)
        {
            var typeValues = (types ?? throw new ArgumentNullException(nameof(types))).ToArray();
            var zoneValues = (zones ?? throw new ArgumentNullException(nameof(zones))).ToArray();
            if (typeValues.Length == 0 || typeValues.Any(value => value == null) ||
                typeValues.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != typeValues.Length)
                throw new ArgumentException("Resource types require unique IDs.", nameof(types));
            var known = new HashSet<string>(typeValues.Select(value => value.Id), StringComparer.Ordinal);
            if (zoneValues.Length == 0 || zoneValues.Any(value => value == null) ||
                zoneValues.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != zoneValues.Length ||
                zoneValues.SelectMany(value => value.ResourceIds).Any(value => !known.Contains(value)))
                throw new ArgumentException("Resource zones require unique IDs and known resources.", nameof(zones));
            Types = Array.AsReadOnly(typeValues);
            Zones = Array.AsReadOnly(zoneValues);
            Fishery = fishery ?? throw new ArgumentNullException(nameof(fishery));
            if (!known.Contains("fish"))
                throw new ArgumentException("Fishery requires the canonical fish resource type.", nameof(types));
        }

        public IReadOnlyList<WorldMapResourceType> Types { get; }
        public IReadOnlyList<WorldMapResourceZone> Zones { get; }
        public WorldMapFisheryDefinition Fishery { get; }
        public WorldMapResourceType Type(string id) => Types.Single(value => value.Id == id);
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
            float minimumWidthMeters = 0f, float maximumWidthMeters = 0f, string kind = "")
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
            Kind = kind ?? string.Empty;
        }
        public string Id { get; }
        public IReadOnlyList<WorldMapSplinePoint> Points { get; }
        public float MinimumWidthMeters { get; }
        public float MaximumWidthMeters { get; }
        public string Kind { get; }
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
            string shoreKind, float fishCapacityIndex, IEnumerable<WorldMapPoint> fishingLandings)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            if (!WorldMapPoint.IsFinite(surfaceY) || !WorldMapPoint.IsFinite(maximumDepthMeters) ||
                maximumDepthMeters <= 0f || !WorldMapPoint.IsFinite(minimumBankBlendMeters) ||
                !WorldMapPoint.IsFinite(maximumBankBlendMeters) || minimumBankBlendMeters <= 0f ||
                maximumBankBlendMeters < minimumBankBlendMeters || string.IsNullOrWhiteSpace(shoreKind) ||
                !WorldMapPoint.IsFinite(fishCapacityIndex) || fishCapacityIndex <= 0f)
                throw new ArgumentOutOfRangeException(nameof(surfaceY));
            Id = id;
            Center = center;
            Shoreline = shoreline ?? throw new ArgumentNullException(nameof(shoreline));
            SurfaceY = surfaceY;
            MaximumDepthMeters = maximumDepthMeters;
            MinimumBankBlendMeters = minimumBankBlendMeters;
            MaximumBankBlendMeters = maximumBankBlendMeters;
            ShoreKind = shoreKind;
            FishCapacityIndex = fishCapacityIndex;
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
        public float FishCapacityIndex { get; }
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

    public sealed class WorldMapRoadChokepoint
    {
        public WorldMapRoadChokepoint(string id, WorldMapPoint position, float radiusMeters)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException(nameof(id));
            if (!WorldMapPoint.IsFinite(radiusMeters) || radiusMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radiusMeters));
            Id = id;
            Position = position;
            RadiusMeters = radiusMeters;
        }

        public string Id { get; }
        public WorldMapPoint Position { get; }
        public float RadiusMeters { get; }
    }

    public sealed class WorldMapRoadRules
    {
        public WorldMapRoadRules(string mainTrailSurface, float mainTrailMaximumSlopeDegrees,
            float footpathMaximumSlopeDegrees, bool crossingRequiresAuthoredId,
            bool bridgeSocketsUseStableIds)
        {
            if (string.IsNullOrWhiteSpace(mainTrailSurface) ||
                !WorldMapPoint.IsFinite(mainTrailMaximumSlopeDegrees) || mainTrailMaximumSlopeDegrees <= 0f ||
                !WorldMapPoint.IsFinite(footpathMaximumSlopeDegrees) ||
                footpathMaximumSlopeDegrees < mainTrailMaximumSlopeDegrees ||
                !crossingRequiresAuthoredId || !bridgeSocketsUseStableIds)
                throw new ArgumentException(nameof(mainTrailSurface));
            MainTrailSurface = mainTrailSurface;
            MainTrailMaximumSlopeDegrees = mainTrailMaximumSlopeDegrees;
            FootpathMaximumSlopeDegrees = footpathMaximumSlopeDegrees;
            CrossingRequiresAuthoredId = crossingRequiresAuthoredId;
            BridgeSocketsUseStableIds = bridgeSocketsUseStableIds;
        }

        public string MainTrailSurface { get; }
        public float MainTrailMaximumSlopeDegrees { get; }
        public float FootpathMaximumSlopeDegrees { get; }
        public bool CrossingRequiresAuthoredId { get; }
        public bool BridgeSocketsUseStableIds { get; }
    }

    /// <summary>Immutable authored road/navigation authority from WORLD_MAP_ROADS.json.</summary>
    public sealed class WorldMapRoadDefinition
    {
        public WorldMapRoadDefinition(IEnumerable<WorldMapSpline> routes,
            IEnumerable<WorldMapCrossing> crossings, IEnumerable<WorldMapRoadChokepoint> chokepoints,
            WorldMapRoadRules rules)
        {
            Routes = ReadOnly(routes, nameof(routes), value => value.Id);
            Crossings = ReadOnly(crossings, nameof(crossings), value => value.Id);
            Chokepoints = ReadOnly(chokepoints, nameof(chokepoints), value => value.Id);
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (Routes.Count != 7 || Crossings.Count != 3)
                throw new ArgumentException("The authored road network requires seven routes and three crossings.");
            var routeIds = new HashSet<string>(Routes.Select(value => value.Id), StringComparer.Ordinal);
            if (Crossings.SelectMany(value => value.RouteIds).Any(value => !routeIds.Contains(value)))
                throw new ArgumentException("A crossing references an unknown route.", nameof(crossings));
        }

        public IReadOnlyList<WorldMapSpline> Routes { get; }
        public IReadOnlyList<WorldMapCrossing> Crossings { get; }
        public IReadOnlyList<WorldMapRoadChokepoint> Chokepoints { get; }
        public WorldMapRoadRules Rules { get; }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values, string parameter,
            Func<T, string> identity) where T : class
        {
            var array = (values ?? throw new ArgumentNullException(parameter)).ToArray();
            if (array.Length == 0 || array.Any(value => value == null) ||
                array.Select(identity).Distinct(StringComparer.Ordinal).Count() != array.Length)
                throw new ArgumentException("Road definitions require unique stable IDs.", parameter);
            return Array.AsReadOnly(array);
        }
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
            WorldMapHydrologyDefinition hydrology, WorldMapBiomeDefinition biomes,
            WorldMapVegetationDefinition vegetation, WorldMapResourceDefinition resources,
            WorldMapWildlifeDefinition wildlife,
            WorldMapSpline river, IEnumerable<WorldMapSpline> tributaries, IEnumerable<WorldMapWaterBody> lakes,
            WorldMapRoadDefinition roads, IEnumerable<WorldMapPointOfInterest> pointsOfInterest,
            IEnumerable<WorldMapMask> resourceMasks,
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
            Biomes = biomes ?? throw new ArgumentNullException(nameof(biomes));
            Vegetation = vegetation ?? throw new ArgumentNullException(nameof(vegetation));
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
            Regions = Array.AsReadOnly(Biomes.Regions.Select(value => value.Polygon).ToArray());
            River = river ?? throw new ArgumentNullException(nameof(river));
            Tributaries = ReadOnly(tributaries, nameof(tributaries));
            Lakes = ReadOnly(lakes, nameof(lakes));
            Roads = roads ?? throw new ArgumentNullException(nameof(roads));
            Routes = Roads.Routes;
            PointsOfInterest = ReadOnly(pointsOfInterest, nameof(pointsOfInterest));
            Crossings = Roads.Crossings;
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
        public WorldMapBiomeDefinition Biomes { get; }
        public WorldMapVegetationDefinition Vegetation { get; }
        public WorldMapResourceDefinition Resources { get; }
        public WorldMapWildlifeDefinition Wildlife { get; }
        public WorldMapRoadDefinition Roads { get; }
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
