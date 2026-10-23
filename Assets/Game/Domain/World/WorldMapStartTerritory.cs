using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Campaign;

namespace Game.Domain.World
{
    public sealed class WorldMapStartObjectPlacement
    {
        public WorldMapStartObjectPlacement(StableEntityId id, string definitionId, WorldPosition position,
            float widthMeters, float depthMeters)
        {
            if (!id.IsValid || string.IsNullOrWhiteSpace(definitionId) || widthMeters <= 0f || depthMeters <= 0f)
                throw new ArgumentException(nameof(definitionId));
            Id = id;
            DefinitionId = definitionId;
            Position = position;
            WidthMeters = widthMeters;
            DepthMeters = depthMeters;
        }

        public StableEntityId Id { get; }
        public string DefinitionId { get; }
        public WorldPosition Position { get; }
        public float WidthMeters { get; }
        public float DepthMeters { get; }
    }

    public sealed class WorldMapFirstConstructionSite
    {
        public WorldMapFirstConstructionSite(string definitionId, WorldPosition position,
            float widthMeters, float depthMeters)
        {
            if (string.IsNullOrWhiteSpace(definitionId) || widthMeters <= 0f || depthMeters <= 0f)
                throw new ArgumentException(nameof(definitionId));
            DefinitionId = definitionId;
            Position = position;
            WidthMeters = widthMeters;
            DepthMeters = depthMeters;
        }

        public string DefinitionId { get; }
        public WorldPosition Position { get; }
        public float WidthMeters { get; }
        public float DepthMeters { get; }
    }

    public sealed class WorldMapExpansionCorridorPlan
    {
        public WorldMapExpansionCorridorPlan(string id, int priority, WorldPosition start,
            WorldPosition end, float widthMeters)
        {
            if (string.IsNullOrWhiteSpace(id) || priority <= 0 || widthMeters <= 0f)
                throw new ArgumentException(nameof(id));
            Id = id;
            Priority = priority;
            Start = start;
            End = end;
            WidthMeters = widthMeters;
        }

        public string Id { get; }
        public int Priority { get; }
        public WorldPosition Start { get; }
        public WorldPosition End { get; }
        public float WidthMeters { get; }
    }

    /// <summary>
    /// Immutable result of WM10 planning. It extends the existing population/New Game flow and owns no
    /// mutable settlement, building, inventory, navigation or presentation state.
    /// </summary>
    public sealed class WorldMapStartTerritoryPlan
    {
        public WorldMapStartTerritoryPlan(long worldSeed, StableEntityId id, WorldPosition center,
            float clearingRadiusMeters, float initialRevealRadiusMeters,
            IEnumerable<WorldMapStartObjectPlacement> startingObjects,
            IEnumerable<WorldMapFirstConstructionSite> firstConstructionSites,
            IEnumerable<WorldMapExpansionCorridorPlan> expansionCorridors,
            IEnumerable<TiledWorldResourceNode> startingResources,
            IEnumerable<WorldPosition> residentSpawns, string riverRouteId, string riverCrossingId,
            WorldPosition riverAccessPoint, IEnumerable<WorldPosition> riverRoute)
        {
            if (!id.IsValid || clearingRadiusMeters <= 0f || initialRevealRadiusMeters <= 0f ||
                string.IsNullOrWhiteSpace(riverRouteId) || string.IsNullOrWhiteSpace(riverCrossingId))
                throw new ArgumentException(nameof(id));
            WorldSeed = worldSeed;
            Id = id;
            Center = center;
            ClearingRadiusMeters = clearingRadiusMeters;
            InitialRevealRadiusMeters = initialRevealRadiusMeters;
            StartingObjects = ReadOnly(startingObjects, nameof(startingObjects));
            FirstConstructionSites = ReadOnly(firstConstructionSites, nameof(firstConstructionSites));
            ExpansionCorridors = ReadOnly(expansionCorridors, nameof(expansionCorridors));
            StartingResources = ReadOnly(startingResources, nameof(startingResources));
            ResidentSpawns = Array.AsReadOnly((residentSpawns ?? throw new ArgumentNullException(nameof(residentSpawns))).ToArray());
            RiverRouteId = riverRouteId;
            RiverCrossingId = riverCrossingId;
            RiverAccessPoint = riverAccessPoint;
            RiverRoute = Array.AsReadOnly((riverRoute ?? throw new ArgumentNullException(nameof(riverRoute))).ToArray());
            if (StartingObjects.Count < 2 || FirstConstructionSites.Count < 3 ||
                ExpansionCorridors.Count != 3 || StartingResources.Count == 0 ||
                ResidentSpawns.Count != 10 || RiverRoute.Count < 2)
                throw new ArgumentException("The WM10 start plan is incomplete.");
        }

        public long WorldSeed { get; }
        public StableEntityId Id { get; }
        public WorldPosition Center { get; }
        public float ClearingRadiusMeters { get; }
        public float InitialRevealRadiusMeters { get; }
        public IReadOnlyList<WorldMapStartObjectPlacement> StartingObjects { get; }
        public IReadOnlyList<WorldMapFirstConstructionSite> FirstConstructionSites { get; }
        public IReadOnlyList<WorldMapExpansionCorridorPlan> ExpansionCorridors { get; }
        public IReadOnlyList<TiledWorldResourceNode> StartingResources { get; }
        public IReadOnlyList<WorldPosition> ResidentSpawns { get; }
        public string RiverRouteId { get; }
        public string RiverCrossingId { get; }
        public WorldPosition RiverAccessPoint { get; }
        public IReadOnlyList<WorldPosition> RiverRoute { get; }

        private static IReadOnlyList<T> ReadOnly<T>(IEnumerable<T> values, string parameter) where T : class
        {
            var result = (values ?? throw new ArgumentNullException(parameter)).ToArray();
            if (result.Any(value => value == null)) throw new ArgumentException(parameter);
            return Array.AsReadOnly(result);
        }
    }

    public sealed class WorldMapStartTerritoryPlanner
    {
        private const float MaximumStartSlopeDegrees = 8f;
        private const float MaximumCorridorSlopeDegrees = 30f;
        private const float MaximumFoundationHeightDeltaMeters = 0.75f;
        private const float CorridorWidthMeters = WorldMapStartTerritoryDefinition.PreparedCorridorHalfWidthMeters * 2f;
        private readonly TiledWorldDefinition world;
        private readonly TiledWorldHeightSampler heights;
        private readonly long seed;

        public WorldMapStartTerritoryPlanner(TiledWorldDefinition definition,
            TiledWorldHeightSampler heightSampler, long worldSeed)
        {
            world = definition ?? throw new ArgumentNullException(nameof(definition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(world, heights.Definition) || world.MapData?.StartTerritory == null)
                throw new ArgumentException("WM10 requires the proposed world-map definition and its shared height sampler.");
            seed = worldSeed;
        }

        public WorldMapStartTerritoryPlan Create(IEnumerable<TiledWorldResourceNode> populationResources)
        {
            var allResources = (populationResources ?? throw new ArgumentNullException(nameof(populationResources)))
                .Where(value => value != null && !value.Hidden).ToArray();
            var definition = world.MapData.StartTerritory;
            var center = Ground(world.MapData.CoordinateTransform.ToUnity(definition.DesignPosition));
            if (!SameXZ(center, world.PlayerStart))
                throw new InvalidOperationException("The WM10 plan must use the canonical player start.");

            var startingObjects = new[]
            {
                StartObject("primitive_shelter", -14f, 10f, 4f, 3f, 0),
                StartObject("storage_basket", -8f, -5f, 2f, 1f, 1)
            };
            var firstConstruction = new[]
            {
                Construction("campfire", 6f, 7f, 2f, 2f),
                Construction("stone_age_hut", -17f, -10f, 4f, 4f),
                Construction("stone_tool_workshop", 16f, -7f, 4f, 3f),
                Construction("primitive_garden", 8f, -18f, 4f, 4f)
            };
            ValidateSites(startingObjects, firstConstruction, allResources, definition);

            var corridors = definition.ExpansionDirections.Select(value =>
            {
                var start = Ground(world.MapData.CoordinateTransform.ToUnity(value.From));
                var end = Ground(world.MapData.CoordinateTransform.ToUnity(value.To));
                ValidateCorridor(value, allResources);
                return new WorldMapExpansionCorridorPlan(value.Id, value.Priority, start, end,
                    CorridorWidthMeters);
            }).ToArray();

            var startingResources = allResources.Where(value => value.SourceZoneId == "res_start_basics")
                .OrderBy(value => value.ResourceTypeId, StringComparer.Ordinal)
                .ThenBy(value => value.StableIndex).ToArray();
            foreach (var resourceType in new[] { "wood", "branches", "berries", "stone", "medicinal_plants" })
                if (startingResources.Count(value => value.ResourceTypeId == resourceType) <
                    TiledWorldPopulationPlanner.StartGuarantee(resourceType))
                    throw new InvalidOperationException("Missing WM10 starting resource: " + resourceType);

            var residents = Enumerable.Range(0, 10).Select(index =>
            {
                var angle = index * Math.PI * 2d / 10d;
                var radius = index % 2 == 0 ? 4.5f : 7.5f;
                return Ground(new WorldPosition(center.X + (float)Math.Cos(angle) * radius, 0f,
                    center.Z + (float)Math.Sin(angle) * radius));
            }).ToArray();
            if (residents.Any(value => heights.IsWater(value.X, value.Z) ||
                                       heights.SampleSlopeDegrees(value.X, value.Z) > MaximumStartSlopeDegrees))
                throw new InvalidOperationException("A WM10 resident spawn is not on safe start ground.");

            var roads = new WorldMapRoadNetworkPlanner(world, heights).Create();
            var route = roads.Routes.Single(value => value.Id == "path_start_west_lake");
            var crossing = roads.Crossings.Single(value => value.Definition.Id == "crossing_start_ford");
            var access = DistanceSquared(center, crossing.Link.Start) <= DistanceSquared(center, crossing.Link.End)
                ? crossing.Link.Start : crossing.Link.End;
            if (heights.IsWater(access.X, access.Z) || !SameXZ(route.Samples[0].Position, center, 1f))
                throw new InvalidOperationException("The WM09 river route is not connected to the WM10 start.");

            return new WorldMapStartTerritoryPlan(seed,
                DeterministicStableId.Create(seed, "world_start_territory:" + world.GenerationId), center,
                definition.ClearingRadiusMeters, definition.InitialRevealRadiusMeters,
                startingObjects, firstConstruction, corridors, startingResources, residents,
                route.Id, crossing.Definition.Id, access, route.Samples.Select(value => value.Position));
        }

        private WorldMapStartObjectPlacement StartObject(string definitionId, float dx, float dz,
            float width, float depth, int ordinal)
        {
            var position = Ground(new WorldPosition(world.PlayerStart.X + dx, 0f, world.PlayerStart.Z + dz));
            return new WorldMapStartObjectPlacement(DeterministicStableId.Create(seed,
                "world_start_object:" + world.GenerationId + ":" + definitionId, ordinal),
                definitionId, position, width, depth);
        }

        private WorldMapFirstConstructionSite Construction(string definitionId, float dx, float dz,
            float width, float depth) => new WorldMapFirstConstructionSite(definitionId,
            Ground(new WorldPosition(world.PlayerStart.X + dx, 0f, world.PlayerStart.Z + dz)), width, depth);

        private void ValidateSites(IReadOnlyList<WorldMapStartObjectPlacement> objects,
            IReadOnlyList<WorldMapFirstConstructionSite> construction,
            IReadOnlyList<TiledWorldResourceNode> resources, WorldMapStartTerritoryDefinition definition)
        {
            var sites = objects.Select(value => Tuple.Create(value.DefinitionId, value.Position,
                    value.WidthMeters, value.DepthMeters))
                .Concat(construction.Select(value => Tuple.Create(value.DefinitionId, value.Position,
                    value.WidthMeters, value.DepthMeters))).ToArray();
            foreach (var site in sites)
            {
                var minimum = float.MaxValue;
                var maximum = float.MinValue;
                for (var z = -site.Item4 * 0.5f - 1f; z <= site.Item4 * 0.5f + 1.001f; z += 1f)
                for (var x = -site.Item3 * 0.5f - 1f; x <= site.Item3 * 0.5f + 1.001f; x += 1f)
                {
                    var worldX = site.Item2.X + x;
                    var worldZ = site.Item2.Z + z;
                    var design = world.MapData.CoordinateTransform.ToDesign(new WorldPosition(worldX, 0f, worldZ));
                    if (!definition.CoreBuildability.Shape.Contains(design) || heights.IsWater(worldX, worldZ) ||
                        !heights.IsBuildable(worldX, worldZ, MaximumStartSlopeDegrees))
                        throw new InvalidOperationException(site.Item1 + " is outside the prepared start core.");
                    var height = heights.SampleHeight(worldX, worldZ);
                    minimum = Math.Min(minimum, height);
                    maximum = Math.Max(maximum, height);
                }
                if (maximum - minimum > MaximumFoundationHeightDeltaMeters)
                    throw new InvalidOperationException(site.Item1 + " exceeds the first-building height tolerance.");
                var radius = (float)Math.Sqrt(site.Item3 * site.Item3 + site.Item4 * site.Item4) * 0.5f + 1.5f;
                if (resources.Any(value => DistanceSquared(site.Item2, value.Position) < radius * radius))
                    throw new InvalidOperationException(site.Item1 + " is blocked by a canonical resource.");
            }
            for (var left = 0; left < sites.Length; left++)
            for (var right = left + 1; right < sites.Length; right++)
            {
                var firstRadius = Math.Max(sites[left].Item3, sites[left].Item4) * 0.5f + 1f;
                var secondRadius = Math.Max(sites[right].Item3, sites[right].Item4) * 0.5f + 1f;
                if (DistanceSquared(sites[left].Item2, sites[right].Item2) <
                    (firstRadius + secondRadius) * (firstRadius + secondRadius))
                    throw new InvalidOperationException("WM10 starter building sites overlap.");
            }
        }

        private void ValidateCorridor(WorldMapExpansionDirection direction,
            IReadOnlyList<TiledWorldResourceNode> resources)
        {
            var dx = direction.To.X - direction.From.X;
            var dz = direction.To.Z - direction.From.Z;
            var length = (float)Math.Sqrt(dx * dx + dz * dz);
            var normalX = -dz / length;
            var normalZ = dx / length;
            var steps = Math.Max(1, (int)Math.Ceiling(length / 3f));
            for (var step = 0; step <= steps; step++)
            {
                var progress = step / (float)steps;
                if (progress * length < 24f) continue; // The shared central clearing owns this overlap.
                var centerX = direction.From.X + dx * progress;
                var centerZ = direction.From.Z + dz * progress;
                for (var side = -1; side <= 1; side++)
                {
                    var design = new WorldMapPoint(centerX + normalX * side *
                        WorldMapStartTerritoryDefinition.PreparedCorridorHalfWidthMeters,
                        centerZ + normalZ * side * WorldMapStartTerritoryDefinition.PreparedCorridorHalfWidthMeters);
                    var runtime = world.MapData.CoordinateTransform.ToUnity(design);
                    if (heights.IsWater(runtime.X, runtime.Z) ||
                        !heights.IsBuildable(runtime.X, runtime.Z, MaximumCorridorSlopeDegrees))
                        throw new InvalidOperationException(direction.Id + " is not a dry buildable expansion corridor at " +
                            design + "; water=" + heights.IsWater(runtime.X, runtime.Z) + "; slope=" +
                            heights.SampleSlopeDegrees(runtime.X, runtime.Z).ToString("0.00") + ".");
                }
            }
            foreach (var resource in resources)
            {
                var design = world.MapData.CoordinateTransform.ToDesign(resource.Position);
                if (direction.DistanceTo(design) <=
                    WorldMapStartTerritoryDefinition.PreparedCorridorHalfWidthMeters +
                    WorldMapStartTerritoryDefinition.ResourceCorridorClearanceMeters)
                    throw new InvalidOperationException(direction.Id + " is blocked by " + resource.ResourceTypeId + ".");
            }
        }

        private WorldPosition Ground(WorldPosition position) =>
            new WorldPosition(position.X, heights.SampleHeight(position.X, position.Z), position.Z);

        private static bool SameXZ(WorldPosition left, WorldPosition right, float tolerance = 0.01f) =>
            Math.Abs(left.X - right.X) <= tolerance && Math.Abs(left.Z - right.Z) <= tolerance;

        private static float DistanceSquared(WorldPosition left, WorldPosition right)
        {
            var dx = left.X - right.X;
            var dz = left.Z - right.Z;
            return dx * dx + dz * dz;
        }
    }
}
