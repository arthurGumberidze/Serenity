using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Campaign;
using Game.Domain.Characters;

namespace Game.Domain.World
{
    public enum WildlifeHabitatFunction { Food, Rest, Water, Shelter }
    public enum WildlifeSex { Female, Male }

    public sealed class WildlifeHabitatNode
    {
        public WildlifeHabitatNode(string id, string regionId, WildlifeHabitatFunction function,
            WorldTileId tile, WorldPosition position)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(regionId))
                throw new ArgumentException(nameof(id));
            Id = id;
            RegionId = regionId;
            Function = function;
            Tile = tile;
            Position = position;
        }

        public string Id { get; }
        public string RegionId { get; }
        public WildlifeHabitatFunction Function { get; }
        public WorldTileId Tile { get; }
        public WorldPosition Position { get; }
    }

    public readonly struct WildlifeHabitatEdge
    {
        public WildlifeHabitatEdge(string fromNodeId, string toNodeId)
        {
            if (string.IsNullOrWhiteSpace(fromNodeId) || string.IsNullOrWhiteSpace(toNodeId) ||
                fromNodeId == toNodeId) throw new ArgumentException(nameof(fromNodeId));
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
        }

        public string FromNodeId { get; }
        public string ToNodeId { get; }
    }

    public sealed class WildlifeHabitatGraph
    {
        private readonly Dictionary<string, WildlifeHabitatNode> byId;
        private readonly Dictionary<string, string[]> regionNeighbors;

        public WildlifeHabitatGraph(IEnumerable<WildlifeHabitatNode> nodes, IEnumerable<WildlifeHabitatEdge> edges)
        {
            var nodeValues = (nodes ?? throw new ArgumentNullException(nameof(nodes))).ToArray();
            var edgeValues = (edges ?? throw new ArgumentNullException(nameof(edges))).ToArray();
            if (nodeValues.Length == 0 || nodeValues.Select(value => value.Id)
                    .Distinct(StringComparer.Ordinal).Count() != nodeValues.Length)
                throw new ArgumentException("Habitat nodes require unique IDs.", nameof(nodes));
            byId = nodeValues.ToDictionary(value => value.Id, StringComparer.Ordinal);
            if (edgeValues.Any(value => !byId.ContainsKey(value.FromNodeId) || !byId.ContainsKey(value.ToNodeId)))
                throw new ArgumentException("Habitat edge references an unknown node.", nameof(edges));
            Nodes = Array.AsReadOnly(nodeValues.OrderBy(value => value.Id, StringComparer.Ordinal).ToArray());
            Edges = Array.AsReadOnly(edgeValues.OrderBy(value => value.FromNodeId, StringComparer.Ordinal)
                .ThenBy(value => value.ToNodeId, StringComparer.Ordinal).ToArray());
            regionNeighbors = BuildRegionNeighbors(nodeValues, edgeValues);
        }

        public IReadOnlyList<WildlifeHabitatNode> Nodes { get; }
        public IReadOnlyList<WildlifeHabitatEdge> Edges { get; }
        public WildlifeHabitatNode Node(string id) => byId.TryGetValue(id, out var value) ? value :
            throw new KeyNotFoundException("Unknown habitat node: " + id);
        public IReadOnlyList<string> RegionNeighbors(string regionId) =>
            regionNeighbors.TryGetValue(regionId, out var values) ? values : Array.Empty<string>();

        private static Dictionary<string, string[]> BuildRegionNeighbors(IEnumerable<WildlifeHabitatNode> nodes,
            IEnumerable<WildlifeHabitatEdge> edges)
        {
            var regions = nodes.ToDictionary(value => value.Id, value => value.RegionId, StringComparer.Ordinal);
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var region in regions.Values.Distinct(StringComparer.Ordinal))
                result.Add(region, new HashSet<string>(StringComparer.Ordinal));
            foreach (var edge in edges)
            {
                var from = regions[edge.FromNodeId];
                var to = regions[edge.ToNodeId];
                if (from == to) continue;
                result[from].Add(to);
                result[to].Add(from);
            }
            return result.ToDictionary(pair => pair.Key,
                pair => pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        }
    }

    public sealed class WildlifeHabitatCapacity
    {
        public WildlifeHabitatCapacity(string speciesId, string regionId, int carryingCapacity,
            float densityMultiplier)
        {
            if (string.IsNullOrWhiteSpace(speciesId) || string.IsNullOrWhiteSpace(regionId) ||
                carryingCapacity <= 0 || densityMultiplier < 1f || !WorldMapPoint.IsFinite(densityMultiplier))
                throw new ArgumentException(nameof(speciesId));
            SpeciesId = speciesId;
            RegionId = regionId;
            CarryingCapacity = carryingCapacity;
            DensityMultiplier = densityMultiplier;
        }

        public string SpeciesId { get; }
        public string RegionId { get; }
        public int CarryingCapacity { get; }
        public float DensityMultiplier { get; }
    }

    public sealed class WildlifeIndividualState
    {
        public WildlifeIndividualState(StableEntityId id, string speciesId, WildlifeSex sex, long birthDay,
            string habitatNodeId, WorldPosition position, CharacterSimulationTier tier, long fleeUntilTick = 0,
            WorldPosition threatPosition = default, int fleeSequence = 0)
        {
            if (!id.IsValid || string.IsNullOrWhiteSpace(speciesId) || string.IsNullOrWhiteSpace(habitatNodeId) ||
                birthDay < 0 || fleeUntilTick < 0 || fleeSequence < 0 || !Enum.IsDefined(typeof(CharacterSimulationTier), tier))
                throw new ArgumentException(nameof(id));
            Id = id;
            SpeciesId = speciesId;
            Sex = sex;
            BirthDay = birthDay;
            HabitatNodeId = habitatNodeId;
            Position = position;
            Tier = tier; FleeUntilTick = fleeUntilTick; ThreatPosition = threatPosition; FleeSequence = fleeSequence;
        }

        public StableEntityId Id { get; }
        public string SpeciesId { get; }
        public WildlifeSex Sex { get; }
        public long BirthDay { get; }
        public string HabitatNodeId { get; }
        public WorldPosition Position { get; }
        public CharacterSimulationTier Tier { get; }
        public long FleeUntilTick { get; }
        public WorldPosition ThreatPosition { get; }
        public int FleeSequence { get; }
        public bool IsFleeing(long tick) => FleeUntilTick > tick;
        public WildlifeIndividualState Escape(WorldPosition threat, long until) =>
            new WildlifeIndividualState(Id, SpeciesId, Sex, BirthDay, HabitatNodeId, Position, Tier, until, threat, checked(FleeSequence+1));
        public WildlifeIndividualState Calm() =>
            new WildlifeIndividualState(Id, SpeciesId, Sex, BirthDay, HabitatNodeId, Position, Tier, 0, ThreatPosition, FleeSequence);
        public WildlifeIndividualState At(string habitatNodeId, WorldPosition position) =>
            new WildlifeIndividualState(Id, SpeciesId, Sex, BirthDay, habitatNodeId, position, Tier, FleeUntilTick, ThreatPosition, FleeSequence);
        public WildlifeIndividualState InTier(CharacterSimulationTier tier) =>
            new WildlifeIndividualState(Id, SpeciesId, Sex, BirthDay, HabitatNodeId, Position, tier, FleeUntilTick, ThreatPosition, FleeSequence);
    }

    public sealed class WildlifePopulationPlan
    {
        public WildlifePopulationPlan(WorldMapWildlifeDefinition definition, WildlifeHabitatGraph habitatGraph,
            IEnumerable<WildlifeHabitatCapacity> capacities, IEnumerable<WildlifeIndividualState> individuals)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            HabitatGraph = habitatGraph ?? throw new ArgumentNullException(nameof(habitatGraph));
            var capacityValues = (capacities ?? throw new ArgumentNullException(nameof(capacities))).ToArray();
            var animalValues = (individuals ?? throw new ArgumentNullException(nameof(individuals)))
                .OrderBy(value => value.Id).ToArray();
            if (capacityValues.Select(value => value.SpeciesId + ":" + value.RegionId)
                    .Distinct(StringComparer.Ordinal).Count() != capacityValues.Length ||
                animalValues.Select(value => value.Id).Distinct().Count() != animalValues.Length)
                throw new ArgumentException("Wildlife plan identities must be unique.");
            if (!definition.CanonicalPopulationTarget.Contains(animalValues.Length))
                throw new ArgumentException("Initial wildlife population is outside its authored target.");
            Capacities = Array.AsReadOnly(capacityValues.OrderBy(value => value.SpeciesId, StringComparer.Ordinal)
                .ThenBy(value => value.RegionId, StringComparer.Ordinal).ToArray());
            Individuals = Array.AsReadOnly(animalValues);
        }

        public WorldMapWildlifeDefinition Definition { get; }
        public WildlifeHabitatGraph HabitatGraph { get; }
        public IReadOnlyList<WildlifeHabitatCapacity> Capacities { get; }
        public IReadOnlyList<WildlifeIndividualState> Individuals { get; }
    }

    /// <summary>Pure seeded World Map V2 habitat/population authoring. It creates no runtime views.</summary>
    public sealed class WorldMapWildlifePlanner
    {
        private static readonly IReadOnlyDictionary<string, string[]> BiomeAliases =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "mixed_open_forest", new[] { "open_woodland", "mixed_forest_edge" } },
                { "meadow_clearings", new[] { "open_meadow" } },
                { "ravines", new[] { "rocky_highland" } },
                { "alpine_scrub", new[] { "shrubland" } },
                { "small_clearings", new[] { "open_meadow" } },
                { "wooded_islets", new[] { "open_woodland" } },
                { "lake_forest", new[] { "mixed_forest_edge", "remote_mixed_forest" } },
                { "reed_shore", new[] { "lake_shore", "marsh" } },
                { "wet_meadow", new[] { "open_meadow", "marsh" } },
                { "rolling_mixed_forest", new[] { "mixed_forest", "mixed_forest_edge", "remote_mixed_forest" } },
                { "rock_passes", new[] { "rocky_highland" } },
                { "wet_scrub", new[] { "shrubland", "marsh" } },
                { "reed_channels", new[] { "riparian", "marsh" } }
            };

        private readonly TiledWorldDefinition world;
        private readonly TiledWorldHeightSampler heights;
        private readonly WorldMapTerrainPaintSampler paint;
        private readonly long seed;

        public WorldMapWildlifePlanner(TiledWorldDefinition definition, TiledWorldHeightSampler heightSampler,
            long worldSeed)
        {
            world = definition ?? throw new ArgumentNullException(nameof(definition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(world, heights.Definition) || world.MapData?.Wildlife == null)
                throw new ArgumentException("World Map V2 wildlife requires the canonical shared definition.");
            paint = new WorldMapTerrainPaintSampler(world, heights);
            seed = worldSeed;
        }

        public WildlifePopulationPlan Create()
        {
            var graph = CreateGraph();
            var capacities = new List<WildlifeHabitatCapacity>();
            var individuals = new List<WildlifeIndividualState>();
            foreach (var species in world.MapData.Wildlife.Species.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                var regions = world.MapData.Biomes.Regions.Where(region => IsSuitable(species, region.Biomes))
                    .OrderBy(region => region.Id, StringComparer.Ordinal).ToArray();
                if (regions.Length == 0) throw new InvalidOperationException("No habitat region for " + species.Id);
                var weighted = regions.Select(region => new
                {
                    Region = region,
                    Multiplier = DensityMultiplier(species.Id, region,
                        graph.Node(region.Id + ":food").Position)
                }).ToArray();
                var allocatedCapacity = Allocate(species.TargetPopulation.Maximum,
                    weighted.Select(value => value.Multiplier).ToArray());
                var initialPopulation = Math.Max(species.TargetPopulation.Midpoint, weighted.Length);
                var allocatedPopulation = Allocate(initialPopulation,
                    weighted.Select(value => value.Multiplier).ToArray(), allocatedCapacity, 0);
                var ordinal = 0;
                for (var i = 0; i < weighted.Length; i++)
                {
                    var region = weighted[i].Region;
                    capacities.Add(new WildlifeHabitatCapacity(species.Id, region.Id,
                        allocatedCapacity[i], weighted[i].Multiplier));
                    var node = graph.Node(region.Id + ":rest");
                    for (var local = 0; local < allocatedPopulation[i]; local++, ordinal++)
                    {
                        var id = DeterministicStableId.Create(seed,
                            "world_wildlife:" + world.MapData.MapVersion + ":" + species.Id, ordinal);
                        individuals.Add(new WildlifeIndividualState(id, species.Id,
                            (ordinal & 1) == 0 ? WildlifeSex.Female : WildlifeSex.Male, 0, node.Id,
                            Jitter(node.Position, species.Id, ordinal, individuals), CharacterSimulationTier.Tier3));
                    }
                }
            }
            return new WildlifePopulationPlan(world.MapData.Wildlife, graph, capacities, individuals);
        }

        private WildlifeHabitatGraph CreateGraph()
        {
            var nodes = new List<WildlifeHabitatNode>();
            var edges = new List<WildlifeHabitatEdge>();
            var regions = world.MapData.Biomes.Regions.OrderBy(value => value.Code, StringComparer.Ordinal).ToArray();
            foreach (var region in regions)
            {
                foreach (WildlifeHabitatFunction function in Enum.GetValues(typeof(WildlifeHabitatFunction)))
                {
                    var position = FindHabitatPoint(region, function);
                    if (!world.TryGetTile(position.X, position.Z, out var tile))
                        throw new InvalidOperationException("Habitat escaped the tiled world.");
                    nodes.Add(new WildlifeHabitatNode(region.Id + ":" + function.ToString().ToLowerInvariant(),
                        region.Id, function, tile, position));
                }
                AddEdge(edges, region.Id, WildlifeHabitatFunction.Food, WildlifeHabitatFunction.Rest);
                AddEdge(edges, region.Id, WildlifeHabitatFunction.Rest, WildlifeHabitatFunction.Water);
                AddEdge(edges, region.Id, WildlifeHabitatFunction.Water, WildlifeHabitatFunction.Shelter);
                AddEdge(edges, region.Id, WildlifeHabitatFunction.Shelter, WildlifeHabitatFunction.Food);
            }
            for (var i = 1; i < regions.Length; i++)
                edges.Add(new WildlifeHabitatEdge(regions[i - 1].Id + ":rest", regions[i].Id + ":rest"));
            return new WildlifeHabitatGraph(nodes, edges);
        }

        private static void AddEdge(ICollection<WildlifeHabitatEdge> edges, string region,
            WildlifeHabitatFunction from, WildlifeHabitatFunction to) => edges.Add(new WildlifeHabitatEdge(
            region + ":" + from.ToString().ToLowerInvariant(), region + ":" + to.ToString().ToLowerInvariant()));

        private WorldPosition FindHabitatPoint(WorldMapBiomeRegion region, WildlifeHabitatFunction function)
        {
            var points = region.Polygon.Points;
            var xMin = points.Min(value => value.X); var xMax = points.Max(value => value.X);
            var zMin = points.Min(value => value.Z); var zMax = points.Max(value => value.Z);
            var salt = StableHash(region.Id + ":" + function);
            WorldPosition best = default;
            var bestScore = float.MinValue;
            for (var attempt = 0; attempt < 1024; attempt++)
            {
                var design = new WorldMapPoint(xMin + Unit(salt, attempt, 1) * (xMax - xMin),
                    zMin + Unit(salt, attempt, 2) * (zMax - zMin));
                if (!region.Polygon.Contains(design)) continue;
                var runtime = world.MapData.CoordinateTransform.ToUnity(design);
                var startDx = runtime.X - world.PlayerStart.X;
                var startDz = runtime.Z - world.PlayerStart.Z;
                if (startDx * startDx + startDz * startDz < 100f * 100f) continue;
                if (heights.IsWater(runtime.X, runtime.Z) || !heights.IsBuildable(runtime.X, runtime.Z, 34f)) continue;
                var weights = paint.SampleWeights(runtime.X, runtime.Z);
                var waterDistance = heights.DistanceToWater(runtime.X, runtime.Z);
                var score = function == WildlifeHabitatFunction.Water ? -waterDistance :
                    function == WildlifeHabitatFunction.Shelter
                        ? weights[(int)WorldMapTerrainPaintLayer.ForestMoss] * 3f +
                          weights[(int)WorldMapTerrainPaintLayer.Scrub]
                        : function == WildlifeHabitatFunction.Food
                            ? weights[(int)WorldMapTerrainPaintLayer.Grass] * 2f +
                              weights[(int)WorldMapTerrainPaintLayer.ForestMoss] +
                              weights[(int)WorldMapTerrainPaintLayer.Wetland]
                            : -heights.SampleSlopeDegrees(runtime.X, runtime.Z);
                if (score <= bestScore) continue;
                bestScore = score;
                best = new WorldPosition(runtime.X, heights.SampleHeight(runtime.X, runtime.Z), runtime.Z);
            }
            if (bestScore == float.MinValue) throw new InvalidOperationException("No habitat point in " + region.Id);
            return best;
        }

        private float DensityMultiplier(string speciesId, WorldMapBiomeRegion region, WorldPosition position)
        {
            var design = world.MapData.CoordinateTransform.ToDesign(position);
            var multiplier = 1f;
            foreach (var zone in world.MapData.Wildlife.ConcentrationZones)
                if (zone.References.Contains(speciesId) && (zone.Shape.Contains(design) ||
                    zone.Shape.Kind != WorldMapShapeKind.Polygon && region.Polygon.Contains(zone.Shape.Center)))
                    multiplier = Math.Max(multiplier, zone.Multiplier);
            return multiplier;
        }

        public static bool IsSuitable(WorldMapWildlifeSpeciesDefinition species, IEnumerable<string> biomeIds)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            var available = new HashSet<string>(StringComparer.Ordinal);
            foreach (var biomeId in biomeIds ?? Array.Empty<string>())
            {
                available.Add(biomeId);
                if (BiomeAliases.TryGetValue(biomeId, out var aliases))
                    foreach (var alias in aliases) available.Add(alias);
            }
            return species.SuitableBiomes.Any(available.Contains);
        }

        private static int[] Allocate(int total, IReadOnlyList<float> weights, IReadOnlyList<int> ceilings = null,
            int minimumPerBucket = 1)
        {
            if (weights.Count == 0 || minimumPerBucket < 0 || total < weights.Count * minimumPerBucket)
                throw new ArgumentOutOfRangeException(nameof(total));
            var result = Enumerable.Repeat(minimumPerBucket, weights.Count).ToArray();
            for (var remaining = total - result.Sum(); remaining > 0; remaining--)
            {
                var best = -1;
                var bestScore = double.MinValue;
                for (var i = 0; i < result.Length; i++)
                {
                    if (ceilings != null && result[i] >= ceilings[i]) continue;
                    var score = weights[i] / (result[i] + 1d);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = i;
                }
                if (best < 0) throw new InvalidOperationException("Wildlife allocation exceeds carrying capacity.");
                result[best]++;
            }
            return result;
        }

        private WorldPosition Jitter(WorldPosition origin, string speciesId, int ordinal,
            IReadOnlyList<WildlifeIndividualState> placed)
        {
            var salt = StableHash(speciesId);
            for (var attempt = 0; attempt < 512; attempt++)
            {
                var angle = Unit(salt, ordinal, 7 + attempt * 2) * Math.PI * 2d;
                var radius = 3f + Unit(salt, ordinal, 8 + attempt * 2) * 32f;
                var x = origin.X + (float)Math.Cos(angle) * radius;
                var z = origin.Z + (float)Math.Sin(angle) * radius;
                if (!world.ContainsClosed(x, z) || !heights.IsBuildable(x, z, 34f)) continue;
                if (placed.Any(value => (value.Position.X - x) * (value.Position.X - x) +
                    (value.Position.Z - z) * (value.Position.Z - z) < 9f)) continue;
                return new WorldPosition(x, heights.SampleHeight(x, z), z);
            }
            throw new InvalidOperationException("No separated wildlife spawn near " + origin);
        }

        private float Unit(int a, int b, int c)
        {
            unchecked
            {
                ulong value = (ulong)seed ^ (uint)a * 0x9E3779B185EBCA87UL;
                value ^= (uint)b * 0xC2B2AE3D27D4EB4FUL;
                value ^= (uint)c * 0x165667B19E3779F9UL;
                value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27; value *= 0x94D049BB133111EBUL;
                return ((value ^ (value >> 31)) & 0xFFFFFFUL) / 16777215f;
            }
        }

        private static int StableHash(string value)
        {
            unchecked { var hash = 17; foreach (var character in value) hash = hash * 31 + character; return hash; }
        }
    }

    public sealed class WildlifeWorldState
    {
        public WildlifeWorldState(long lastDay, int nextBirthSequence, IEnumerable<WildlifeIndividualState> animals,
            IEnumerable<WildlifeHuntingPressureState> huntingPressures = null)
        {
            if (lastDay < 0 || nextBirthSequence < 0) throw new ArgumentOutOfRangeException(nameof(lastDay));
            LastDay = lastDay;
            NextBirthSequence = nextBirthSequence;
            Animals = Array.AsReadOnly((animals ?? throw new ArgumentNullException(nameof(animals)))
                .OrderBy(value => value.Id).ToArray());
            var pressureValues = (huntingPressures ?? Array.Empty<WildlifeHuntingPressureState>())
                .OrderBy(value => value.SpeciesId, StringComparer.Ordinal)
                .ThenBy(value => value.RegionId, StringComparer.Ordinal).ToArray();
            if (pressureValues.Select(value => value.SpeciesId + ":" + value.RegionId)
                .Distinct(StringComparer.Ordinal).Count() != pressureValues.Length)
                throw new ArgumentException("Wildlife hunting-pressure identities must be unique.",
                    nameof(huntingPressures));
            HuntingPressures = Array.AsReadOnly(pressureValues);
        }
        public long LastDay { get; }
        public int NextBirthSequence { get; }
        public IReadOnlyList<WildlifeIndividualState> Animals { get; }
        public IReadOnlyList<WildlifeHuntingPressureState> HuntingPressures { get; }
    }

    public sealed class WildlifeHuntingPressureState
    {
        public WildlifeHuntingPressureState(string speciesId, string regionId, int permille)
        {
            if (string.IsNullOrWhiteSpace(speciesId) || string.IsNullOrWhiteSpace(regionId))
                throw new ArgumentException("Wildlife pressure requires species and region IDs.");
            if (permille < 0 || permille > 1000) throw new ArgumentOutOfRangeException(nameof(permille));
            SpeciesId = speciesId;
            RegionId = regionId;
            Permille = permille;
        }

        public string SpeciesId { get; }
        public string RegionId { get; }
        public int Permille { get; }
    }

    /// <summary>Single canonical mutable wildlife authority for Tier1, Tier2 and Tier3 projections.</summary>
    public sealed class WildlifePopulationService
    {
        private readonly WildlifePopulationPlan plan;
        private readonly long seed;
        private readonly Dictionary<StableEntityId, WildlifeIndividualState> animals;
        private readonly Dictionary<string, int> pressure = new Dictionary<string, int>(StringComparer.Ordinal);
        private int nextBirthSequence;

        public WildlifePopulationService(WildlifePopulationPlan populationPlan, long worldSeed)
        {
            plan = populationPlan ?? throw new ArgumentNullException(nameof(populationPlan));
            seed = worldSeed;
            animals = plan.Individuals.ToDictionary(value => value.Id);
        }

        public WildlifePopulationPlan Plan => plan;
        public Func<StableEntityId, bool> IsAlive { get; set; } = _ => true;
        public long LastDay { get; private set; }
        public IReadOnlyList<WildlifeIndividualState> Animals => animals.Values.OrderBy(value => value.Id).ToArray();
        public int Count => animals.Count;

        public WildlifeIndividualState Get(StableEntityId id) => animals.TryGetValue(id, out var value) ? value :
            throw new KeyNotFoundException("Unknown wildlife identity: " + id);

        public void Transition(StableEntityId id, CharacterSimulationTier tier)
        {
            animals[id] = Get(id).InTier(tier);
        }

        public void RebalanceTiers(WorldPosition focus)
        {
            var ordered = animals.Values.OrderBy(value => value.Position.DistanceSquared(focus))
                .ThenBy(value => value.Id).ToArray();
            var tier1 = Math.Min(plan.Definition.Tier1VisibleCap.Maximum, ordered.Length);
            var tier2 = Math.Min(plan.Definition.Tier2ActiveCap.Maximum, ordered.Length - tier1);
            for (var i = 0; i < ordered.Length; i++)
                animals[ordered[i].Id] = ordered[i].InTier(i < tier1 ? CharacterSimulationTier.Tier1 :
                    i < tier1 + tier2 ? CharacterSimulationTier.Tier2 : CharacterSimulationTier.Tier3);
        }

        public void ApplyHuntingPressure(string speciesId, string regionId, int permille)
        {
            plan.Definition.SpeciesById(speciesId);
            if (permille < 0 || permille > 1000) throw new ArgumentOutOfRangeException(nameof(permille));
            pressure[speciesId + ":" + regionId] = permille;
        }

        public bool Remove(StableEntityId id) => animals.Remove(id);
        public bool TryGet(StableEntityId id, out WildlifeIndividualState animal) => animals.TryGetValue(id,out animal);
        public bool BeginFlee(StableEntityId id, WorldPosition threat, long tick, WildlifeEscapePolicy policy)
        {
            if (!animals.TryGetValue(id,out var animal) || !IsAlive(id)) return false;
            animals[id] = animal.Escape(threat,checked(tick+policy.DurationTicks)); return true;
        }
        public void CalmExpired(long tick)
        {
            foreach (var animal in animals.Values.Where(x=>x.FleeUntilTick>0 && (!IsAlive(x.Id) || !x.IsFleeing(tick))).ToArray())
                animals[animal.Id] = animal.Calm();
        }


        public bool TryMove(StableEntityId id, WorldPosition destination, float minimumSeparation = 3f)
        {
            var animal = Get(id);
            if (!IsAlive(id)) return false;
            if (minimumSeparation <= 0f) throw new ArgumentOutOfRangeException(nameof(minimumSeparation));
            foreach (var other in animals.Values)
            {
                if (other.Id == id) continue;
                var dx = other.Position.X - destination.X; var dz = other.Position.Z - destination.Z;
                if (dx * dx + dz * dz < minimumSeparation * minimumSeparation) return false;
            }
            animals[id] = animal.At(animal.HabitatNodeId, destination);
            return true;
        }

        public void AdvanceTo(long calendarTicks)
        {
            if (calendarTicks < 0) throw new ArgumentOutOfRangeException(nameof(calendarTicks));
            CalmExpired(calendarTicks);
            var targetDay = calendarTicks / TimeSpan.TicksPerDay;
            if (targetDay < LastDay) throw new InvalidOperationException("Wildlife time cannot move backwards.");
            while (LastDay < targetDay)
            {
                LastDay++;
                Migrate();
                Reproduce();
                foreach (var key in pressure.Keys.ToArray()) pressure[key] = pressure[key] * 3 / 4;
            }
        }

        public WildlifeWorldState CaptureState() => new WildlifeWorldState(LastDay, nextBirthSequence, Animals,
            pressure.Select(pair =>
            {
                var separator = pair.Key.IndexOf(':');
                return new WildlifeHuntingPressureState(pair.Key.Substring(0, separator),
                    pair.Key.Substring(separator + 1), pair.Value);
            }));

        public void RestoreState(WildlifeWorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // Explicit content retirement only; other unknown species remain a corrupt-save error.
            bool Retired(string id) => (id == "fox" || id == "wolf") &&
                !plan.Definition.Species.Any(species => species.Id == id);
            var restored = state.Animals.Where(value => !Retired(value.SpeciesId)).ToDictionary(value => value.Id);
            foreach (var animal in restored.Values)
            {
                plan.Definition.SpeciesById(animal.SpeciesId);
                plan.HabitatGraph.Node(animal.HabitatNodeId);
            }
            foreach (var capacity in plan.Capacities)
                if (restored.Values.Count(value => value.SpeciesId == capacity.SpeciesId &&
                    plan.HabitatGraph.Node(value.HabitatNodeId).RegionId == capacity.RegionId) >
                    capacity.CarryingCapacity)
                    throw new InvalidOperationException("Restored wildlife exceeds carrying capacity.");
            var restoredPressure = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var value in state.HuntingPressures)
            {
                if (Retired(value.SpeciesId)) continue;
                plan.Definition.SpeciesById(value.SpeciesId);
                if (!plan.Capacities.Any(capacity => capacity.SpeciesId == value.SpeciesId &&
                    capacity.RegionId == value.RegionId))
                    throw new InvalidOperationException("Restored wildlife pressure references an unknown habitat.");
                restoredPressure.Add(value.SpeciesId + ":" + value.RegionId, value.Permille);
            }
            animals.Clear();
            foreach (var pair in restored) animals.Add(pair.Key, pair.Value);
            LastDay = state.LastDay;
            nextBirthSequence = state.NextBirthSequence;
            pressure.Clear();
            foreach (var pair in restoredPressure) pressure.Add(pair.Key, pair.Value);
        }

        private void Migrate()
        {
            foreach (var capacity in plan.Capacities)
            {
                var local = Local(capacity.SpeciesId, capacity.RegionId);
                pressure.TryGetValue(capacity.SpeciesId + ":" + capacity.RegionId, out var localPressure);
                if (local.Count == 0) continue;
                if (local.Count <= capacity.CarryingCapacity && localPressure < 250) continue;
                var destination = plan.HabitatGraph.RegionNeighbors(capacity.RegionId)
                    .Select(region => Capacity(capacity.SpeciesId, region))
                    .Where(value => value != null && animals.Values.Count(animal => animal.SpeciesId == value.SpeciesId &&
                        plan.HabitatGraph.Node(animal.HabitatNodeId).RegionId == value.RegionId) < value.CarryingCapacity)
                    .OrderBy(value => Local(value.SpeciesId, value.RegionId).Count / (double)value.CarryingCapacity)
                    .ThenBy(value => value.RegionId, StringComparer.Ordinal).FirstOrDefault();
                if (destination == null) continue;
                var migrant = local.OrderBy(value => value.Id).First();
                var target = plan.HabitatGraph.Node(destination.RegionId + ":rest");
                animals[migrant.Id] = migrant.At(target.Id, target.Position);
            }
        }

        private void Reproduce()
        {
            foreach (var capacity in plan.Capacities)
            {
                var local = Local(capacity.SpeciesId, capacity.RegionId);
                if (animals.Values.Count(value => value.SpeciesId == capacity.SpeciesId &&
                    plan.HabitatGraph.Node(value.HabitatNodeId).RegionId == capacity.RegionId) >= capacity.CarryingCapacity ||
                    !local.Any(value => value.Sex == WildlifeSex.Female) ||
                    !local.Any(value => value.Sex == WildlifeSex.Male)) continue;
                var interval = 3 + Math.Abs(StableHash(capacity.SpeciesId)) % 4;
                if ((LastDay + Math.Abs(StableHash(capacity.RegionId))) % interval != 0) continue;
                var id = DeterministicStableId.Create(seed,
                    "world_wildlife_birth:" + capacity.SpeciesId, nextBirthSequence++);
                var node = plan.HabitatGraph.Node(capacity.RegionId + ":rest");
                var sex = (nextBirthSequence & 1) == 0 ? WildlifeSex.Female : WildlifeSex.Male;
                animals.Add(id, new WildlifeIndividualState(id, capacity.SpeciesId, sex, LastDay,
                    node.Id, node.Position, CharacterSimulationTier.Tier3));
            }
        }

        private List<WildlifeIndividualState> Local(string speciesId, string regionId) => animals.Values
            .Where(value => IsAlive(value.Id) && value.SpeciesId == speciesId &&
                            plan.HabitatGraph.Node(value.HabitatNodeId).RegionId == regionId).ToList();

        private WildlifeHabitatCapacity Capacity(string speciesId, string regionId) => plan.Capacities
            .FirstOrDefault(value => value.SpeciesId == speciesId && value.RegionId == regionId);

        private static int StableHash(string value)
        {
            unchecked { var hash = 17; foreach (var character in value) hash = hash * 31 + character; return hash; }
        }
    }
}
