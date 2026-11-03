using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Campaign;
using Game.Domain.Production;

namespace Game.Domain.World
{
    public sealed class TiledWorldResourceNode
    {
        public TiledWorldResourceNode(int stableIndex, WorldTileId tile, string recipeId,
            NaturalResourceBiome biome, WorldPosition position, long capacity, string sourceZoneId,
            WorldPosition? visualPosition = null, string resourceTypeId = "", bool hidden = false,
            string unlockTechnologyId = "", string depletionPolicy = "", string renewalPolicy = "",
            string stableKey = "", int carryingCapacityGrowthPermillePerDay = 0,
            int minimumRecoveryUnits = 0)
        {
            if (stableIndex < 0) throw new ArgumentOutOfRangeException(nameof(stableIndex));
            if (string.IsNullOrWhiteSpace(recipeId)) throw new ArgumentException(nameof(recipeId));
            // Fish represents a whole water body; other nodes retain their small local deposits.
            if (capacity < 20 || capacity > 50 && recipeId != "fish_water")
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (hidden && string.IsNullOrWhiteSpace(unlockTechnologyId))
                throw new ArgumentException("Hidden deposits require an unlock technology.", nameof(unlockTechnologyId));
            if (carryingCapacityGrowthPermillePerDay < 0 || carryingCapacityGrowthPermillePerDay > 1000 ||
                minimumRecoveryUnits < 0 || carryingCapacityGrowthPermillePerDay == 0 && minimumRecoveryUnits != 0 ||
                carryingCapacityGrowthPermillePerDay > 0 && minimumRecoveryUnits == 0)
                throw new ArgumentOutOfRangeException(nameof(carryingCapacityGrowthPermillePerDay));
            StableIndex = stableIndex;
            Tile = tile;
            RecipeId = recipeId;
            Biome = biome;
            Position = position;
            VisualPosition = visualPosition ?? position;
            Capacity = capacity;
            SourceZoneId = sourceZoneId ?? string.Empty;
            ResourceTypeId = resourceTypeId ?? string.Empty;
            Hidden = hidden;
            UnlockTechnologyId = unlockTechnologyId ?? string.Empty;
            DepletionPolicy = depletionPolicy ?? string.Empty;
            RenewalPolicy = renewalPolicy ?? string.Empty;
            StableKey = string.IsNullOrWhiteSpace(stableKey) ? "legacy:" + stableIndex : stableKey;
            CarryingCapacityGrowthPermillePerDay = carryingCapacityGrowthPermillePerDay;
            MinimumRecoveryUnits = minimumRecoveryUnits;
        }

        public int StableIndex { get; }
        public WorldTileId Tile { get; }
        public string RecipeId { get; }
        public NaturalResourceBiome Biome { get; }
        public WorldPosition Position { get; }
        public WorldPosition VisualPosition { get; }
        public long Capacity { get; }
        public string SourceZoneId { get; }
        public string ResourceTypeId { get; }
        public bool Hidden { get; }
        public string UnlockTechnologyId { get; }
        public string DepletionPolicy { get; }
        public string RenewalPolicy { get; }
        public string StableKey { get; }
        public int CarryingCapacityGrowthPermillePerDay { get; }
        public int MinimumRecoveryUnits { get; }

        public StableEntityId CreateStableId(long worldSeed, string generationId) =>
            StableKey == "legacy:" + StableIndex
                ? DeterministicStableId.Create(worldSeed, "p109_world_source:" + generationId, StableIndex)
                : DeterministicStableId.Create(worldSeed, "world_resource:" + generationId + ":" + StableKey);
    }

    public sealed class TiledWorldIndustrialNode
    {
        public TiledWorldIndustrialNode(int stableIndex, WorldTileId tile, string definitionId,
            string resourceId, WorldPosition position, string sourceZoneId)
        {
            if (stableIndex < 0) throw new ArgumentOutOfRangeException(nameof(stableIndex));
            if (string.IsNullOrWhiteSpace(definitionId) || string.IsNullOrWhiteSpace(resourceId) ||
                string.IsNullOrWhiteSpace(sourceZoneId)) throw new ArgumentException("Industrial identity is required.");
            StableIndex = stableIndex;
            Tile = tile;
            DefinitionId = definitionId;
            ResourceId = resourceId;
            Position = position;
            SourceZoneId = sourceZoneId;
        }

        public int StableIndex { get; }
        public WorldTileId Tile { get; }
        public string DefinitionId { get; }
        public string ResourceId { get; }
        public WorldPosition Position { get; }
        public string SourceZoneId { get; }
    }

    public sealed class TiledWorldPopulationPlan
    {
        public TiledWorldPopulationPlan(IEnumerable<TiledWorldResourceNode> resources,
            IEnumerable<TiledWorldIndustrialNode> industry, WorldMapStartTerritoryPlan startTerritory = null)
        {
            Resources = Array.AsReadOnly((resources ?? throw new ArgumentNullException(nameof(resources)))
                .OrderBy(value => value.StableIndex).ToArray());
            Industry = Array.AsReadOnly((industry ?? throw new ArgumentNullException(nameof(industry)))
                .OrderBy(value => value.StableIndex).ToArray());
            if (Resources.Select(value => value.StableIndex).Distinct().Count() != Resources.Count ||
                Industry.Select(value => value.StableIndex).Distinct().Count() != Industry.Count)
                throw new ArgumentException("Population indices must be unique.");
            StartTerritory = startTerritory;
        }

        public IReadOnlyList<TiledWorldResourceNode> Resources { get; }
        public IReadOnlyList<TiledWorldIndustrialNode> Industry { get; }
        public IReadOnlyList<TiledWorldResourceNode> VisibleResources => Resources.Where(value => !value.Hidden).ToArray();
        public IReadOnlyList<TiledWorldResourceNode> HiddenDeposits => Resources.Where(value => value.Hidden).ToArray();
        public WorldMapStartTerritoryPlan StartTerritory { get; }
    }

    /// <summary>
    /// Deterministic canonical population layout. Loaded GameObjects are projections of this plan, never the authority.
    /// </summary>
    public sealed class TiledWorldPopulationPlanner
    {
        private readonly TiledWorldDefinition definition;
        private readonly TiledWorldHeightSampler heights;
        private readonly TiledWorldHeightSampler projectedHeights;
        private readonly long seed;
        private readonly List<WorldPosition> occupied = new List<WorldPosition>();
        private readonly WorldMapTerrainPaintSampler paint;

        private static readonly IReadOnlyDictionary<string, int> StartMinimums =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["wood"] = 8,
                ["branches"] = 6,
                ["berries"] = 10,
                ["stone"] = 5,
                ["medicinal_plants"] = 4,
                ["mushrooms"] = 5,
                ["plant_fiber"] = 5
            };

        public TiledWorldPopulationPlanner(TiledWorldDefinition worldDefinition,
            TiledWorldHeightSampler heightSampler, long worldSeed)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            projectedHeights = heightSampler;
            if (heights.Landscape != null) heights = new TiledWorldHeightSampler(worldDefinition, worldSeed, false);
            if (!ReferenceEquals(definition, heights.Definition))
                throw new ArgumentException("Population and height samplers must share one world definition.");
            seed = worldSeed;
            if (definition.MapData?.Resources != null)
                paint = new WorldMapTerrainPaintSampler(definition, heights);
        }

        public static int StartGuarantee(string resourceTypeId) =>
            StartMinimums.TryGetValue(resourceTypeId, out var value) ? value : 0;

        public TiledWorldPopulationPlan Create()
        {
            var plan=definition.MapData?.Resources==null?CreateLegacy():CreateWorldMapV2();
            var sample=projectedHeights.Landscape;
            if(sample==null)return plan;
            WorldPosition Ground(WorldPosition p)=>sample.ProjectGround(projectedHeights,p);
            return new TiledWorldPopulationPlan(plan.Resources.Select(n=>new TiledWorldResourceNode(
                n.StableIndex,n.Tile,n.RecipeId,n.Biome,Ground(n.Position),n.Capacity,n.SourceZoneId,
                Ground(n.VisualPosition),n.ResourceTypeId,n.Hidden,n.UnlockTechnologyId,n.DepletionPolicy,
                n.RenewalPolicy,n.StableKey,n.CarryingCapacityGrowthPermillePerDay,n.MinimumRecoveryUnits)),
                plan.Industry.Select(n=>new TiledWorldIndustrialNode(n.StableIndex,n.Tile,n.DefinitionId,
                    n.ResourceId,Ground(n.Position),n.SourceZoneId)),plan.StartTerritory);
        }

        private TiledWorldPopulationPlan CreateLegacy()
        {
            occupied.Clear();
            var resources = new List<TiledWorldResourceNode>();
            var nextResource = 0;
            foreach (var tile in definition.Tiles.OrderBy(value => value))
            {
                AddTileResource(resources, ref nextResource, tile, "chop_wood", NaturalResourceBiome.Forest, 0);
                AddTileResource(resources, ref nextResource, tile, "chop_wood", NaturalResourceBiome.Forest, 1);
                AddTileResource(resources, ref nextResource, tile, "gather_stone", NaturalResourceBiome.Rocky, 2);
                AddTileResource(resources, ref nextResource, tile, "gather_berries", NaturalResourceBiome.Meadow, 3);
                AddTileResource(resources, ref nextResource, tile, "gather_fiber", NaturalResourceBiome.Wetland, 4);
            }

            foreach (var zone in definition.Zones.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                if (zone.Uses.HasFlag(WorldZoneUse.Forest))
                    AddZoneResources(resources, ref nextResource, zone, "chop_wood", NaturalResourceBiome.Forest, 70);
                if (zone.Uses.HasFlag(WorldZoneUse.Berries))
                    AddZoneResources(resources, ref nextResource, zone, "gather_berries", NaturalResourceBiome.Meadow, 10);
                if (zone.Uses.HasFlag(WorldZoneUse.Hunting))
                    AddZoneResources(resources, ref nextResource, zone, "hunt_animal", NaturalResourceBiome.AnimalRange, 6);
                if (zone.Uses.HasFlag(WorldZoneUse.Ore))
                {
                    AddZoneResources(resources, ref nextResource, zone, "gather_stone", NaturalResourceBiome.Rocky, 6);
                    AddZoneResources(resources, ref nextResource, zone, "gather_iron", NaturalResourceBiome.Mountain, 4);
                    AddZoneResources(resources, ref nextResource, zone, "gather_gold", NaturalResourceBiome.Mountain, 2);
                }
                if (zone.Uses.HasFlag(WorldZoneUse.Fishing))
                    AddFishingSchools(resources, ref nextResource, zone);
            }

            var industry = new List<TiledWorldIndustrialNode>();
            var nextIndustry = 0;
            foreach (var zone in definition.Zones.OrderBy(value => value.Id, StringComparer.Ordinal))
            {
                if (zone.Uses.HasFlag(WorldZoneUse.Forest))
                    AddIndustry(industry, ref nextIndustry, zone, "forest_workshop", "wood_log", 0);
                if (zone.Uses.HasFlag(WorldZoneUse.Ore))
                {
                    AddIndustry(industry, ref nextIndustry, zone, "stone_quarry", "stone", 0);
                    AddIndustry(industry, ref nextIndustry, zone, "iron_mine", "iron_ore", 1);
                    AddIndustry(industry, ref nextIndustry, zone, "gold_mine", "gold_ore", 2);
                }
                if (zone.Uses.HasFlag(WorldZoneUse.Fishing))
                    AddIndustry(industry, ref nextIndustry, zone, "lake_fishing", "fish", 0, true);
                if (zone.Uses.HasFlag(WorldZoneUse.Hunting))
                    AddIndustry(industry, ref nextIndustry, zone, "animal_hunting", "meat", 0);
            }
            return new TiledWorldPopulationPlan(resources, industry);
        }

        private TiledWorldPopulationPlan CreateWorldMapV2()
        {
            occupied.Clear();
            var result = new List<TiledWorldResourceNode>();
            var stableIndex = 0;
            foreach (var zone in definition.MapData.Resources.Zones.OrderBy(value => value.Id, StringComparer.Ordinal))
            foreach (var resourceId in zone.ResourceIds.OrderBy(value => value, StringComparer.Ordinal))
            {
                // WM07 owns the authored lake/river population sources and carrying-capacity recovery.
                if (resourceId == "fish") continue;
                var type = definition.MapData.Resources.Type(resourceId);
                var count = zone.Id == "res_start_basics"
                    ? StartGuarantee(resourceId)
                    : CountForDensity(zone.Density) * (resourceId == "berries" || resourceId == "mushrooms" ? 2 : 1);
                for (var ordinal = 0; ordinal < count; ordinal++)
                {
                    if (!TryFindWorldMapPoint(zone, type, ordinal, out var design, out var position))
                        throw new InvalidOperationException($"No valid {resourceId} placement in {zone.Id}/{ordinal}.");
                    if (!definition.TryGetTile(position.X, position.Z, out var tile))
                        throw new InvalidOperationException("Resource placement escaped the tiled world.");
                    var quantizedX = (int)Math.Floor((design.X - definition.MapData.CoordinateTransform.DesignBounds.XMin) / 4f);
                    var quantizedZ = (int)Math.Floor((design.Z - definition.MapData.CoordinateTransform.DesignBounds.ZMin) / 4f);
                    var stableKey = definition.MapData.MapVersion + ":resource:" + resourceId + ":" + zone.Id +
                                    ":" + quantizedX + ":" + quantizedZ + ":" + ordinal;
                    var capacity = CapacityFor(type, zone, ordinal);
                    result.Add(new TiledWorldResourceNode(stableIndex++, tile, RecipeFor(resourceId),
                        BiomeFor(resourceId), position, capacity, zone.Id, null, resourceId, type.IsHidden,
                        type.UnlockTechnologyId, type.DepletionPolicy, type.RenewalPolicy, stableKey));
                    occupied.Add(position);
                }
            }

            foreach (var pair in StartMinimums)
                if (result.Count(value => value.SourceZoneId == "res_start_basics" &&
                                          value.ResourceTypeId == pair.Key) < pair.Value)
                    throw new InvalidOperationException("The authored start resource guarantee was not met: " + pair.Key);
            AddWorldMapFishPopulations(result, ref stableIndex);
            var startTerritory = new WorldMapStartTerritoryPlanner(definition, heights, seed).Create(result);
            var quarry = result.Where(value => value.SourceZoneId == "res_ridge_stone" && value.ResourceTypeId == "stone")
                .OrderBy(value => heights.SampleSlopeDegrees(value.Position.X, value.Position.Z))
                .ThenBy(value => value.StableIndex).First();
            return new TiledWorldPopulationPlan(result, new[]
            {
                new TiledWorldIndustrialNode(0, quarry.Tile, "stone_quarry", "stone", quarry.Position, "res_ridge_stone")
            }, startTerritory);
        }

        private void AddWorldMapFishPopulations(ICollection<TiledWorldResourceNode> target, ref int stableIndex)
        {
            var fishery = definition.MapData.Resources.Fishery;
            var fishType = definition.MapData.Resources.Type("fish");
            foreach (var waterGroup in fishery.Populations.GroupBy(value => value.WaterBodyId).OrderBy(value => value.Key,StringComparer.Ordinal))
            {
                var population = waterGroup.OrderBy(value=>value.Id,StringComparer.Ordinal).First();
                WorldPosition bank;
                WorldPosition water;
                float habitatCapacityIndex;
                if (population.IsRiver)
                {
                    ResolveRiverFishPosition(population, fishery, out bank, out water);
                    habitatCapacityIndex = 1f;
                }
                else
                {
                    var lake = definition.MapData.Hydrology.Lakes.Single(value =>
                        value.Id == population.WaterBodyId);
                    ResolveLakeFishPosition(population, lake, out bank, out water);
                    habitatCapacityIndex = lake.FishCapacityIndex;
                }
                if (!definition.TryGetTile(bank.X, bank.Z, out var tile))
                    throw new InvalidOperationException("Fish landing escaped the tiled world: " + population.Id);
                var dailyGrowth = Math.Max(1, (int)Math.Round(fishery.DailyGrowthPermille *
                    habitatCapacityIndex, MidpointRounding.AwayFromZero));
                target.Add(new TiledWorldResourceNode(stableIndex++, tile, "fish_water",
                    NaturalResourceBiome.Lake, bank, Math.Max(fishery.MinimumSharedStockUnits,waterGroup.Sum(value=>value.CarryingCapacityUnits)*fishery.SharedStockMultiplier),
                    population.WaterBodyId, water, "fish", false, string.Empty,
                    fishType.DepletionPolicy, "none",
                    definition.MapData.MapVersion + ":fish:" + population.Id,
                    0, 0));
                occupied.Add(bank);
            }
        }

        private void ResolveLakeFishPosition(WorldMapFishPopulationDefinition population,
            WorldMapWaterBody lake, out WorldPosition bank, out WorldPosition water)
        {
            var landing = lake.FishingLandings[population.SchoolOrdinal % lake.FishingLandings.Count];
            if (!TryResolveDryBank(landing, population.SchoolOrdinal, out bank))
                throw new InvalidOperationException("No dry reachable fishing landing for " + population.Id);
            var dx = landing.X - lake.Center.X;
            var dz = landing.Z - lake.Center.Z;
            WorldMapPoint visual = lake.Center;
            for (var factor = 0.72f; factor >= 0.2f; factor -= 0.08f)
            {
                var candidate = new WorldMapPoint(lake.Center.X + dx * factor,
                    lake.Center.Z + dz * factor);
                if (!lake.Shoreline.Contains(candidate)) continue;
                visual = candidate;
                break;
            }
            var runtime = definition.MapData.CoordinateTransform.ToUnity(visual);
            water = new WorldPosition(runtime.X, lake.SurfaceY + 0.12f, runtime.Z);
        }

        private void ResolveRiverFishPosition(WorldMapFishPopulationDefinition population,
            WorldMapFisheryDefinition fishery, out WorldPosition bank, out WorldPosition water)
        {
            var river = definition.MapData.Hydrology.MainRiver;
            var progress = (population.SchoolOrdinal + 1f) /
                           (fishery.Populations.Count(value => value.IsRiver) + 1f);
            SampleSpline(river.Spline, progress, out var center, out var width, out var tangentX, out var tangentZ);
            var centerRuntime = definition.MapData.CoordinateTransform.ToUnity(center);
            water = new WorldPosition(centerRuntime.X,
                river.SurfaceLevelPolicy.Sample(progress) + 0.12f, centerRuntime.Z);
            var length = (float)Math.Sqrt(tangentX * tangentX + tangentZ * tangentZ);
            var normalX = -tangentZ / Math.Max(0.001f, length);
            var normalZ = tangentX / Math.Max(0.001f, length);
            for (var attempt = 0; attempt < 48; attempt++)
            {
                var side = ((population.SchoolOrdinal + attempt) & 1) == 0 ? 1f : -1f;
                var along = (attempt / 2 % 5 - 2) * 2.5f;
                var offset = width * 0.5f + fishery.MinimumBankWorkPointOffsetMeters + attempt / 10 * 2f;
                var design = new WorldMapPoint(center.X + normalX * offset * side + tangentX / length * along,
                    center.Z + normalZ * offset * side + tangentZ / length * along);
                if (!definition.MapData.CoordinateTransform.DesignBounds.Contains(design) ||
                    !TryResolveDryBank(design, population.SchoolOrdinal + attempt, out bank)) continue;
                return;
            }
            throw new InvalidOperationException("No dry reachable river landing for " + population.Id);
        }

        private bool TryResolveDryBank(WorldMapPoint authored, int salt, out WorldPosition bank)
        {
            for (var ring = 0; ring <= 20; ring++)
            for (var direction = 0; direction < (ring == 0 ? 1 : 16); direction++)
            {
                var radius = ring * 1.5f;
                var angle = (salt * 17f + direction * 22.5f) * Math.PI / 180d;
                var candidate = new WorldMapPoint(authored.X + (float)Math.Cos(angle) * radius,
                    authored.Z + (float)Math.Sin(angle) * radius);
                if (!definition.MapData.CoordinateTransform.DesignBounds.Contains(candidate)) continue;
                var runtime = definition.MapData.CoordinateTransform.ToUnity(candidate);
                if (!heights.IsBuildable(runtime.X, runtime.Z, 30f) || !HasDryApproach(runtime.X, runtime.Z))
                    continue;
                bank = new WorldPosition(runtime.X, heights.SampleHeight(runtime.X, runtime.Z), runtime.Z);
                return true;
            }
            bank = default;
            return false;
        }

        private bool HasDryApproach(float worldX, float worldZ)
        {
            for (var i = 0; i < 8; i++)
            {
                var angle = i * Math.PI * 0.25d;
                var x = worldX + (float)Math.Cos(angle) * 3f;
                var z = worldZ + (float)Math.Sin(angle) * 3f;
                if (heights.IsBuildable(x, z, 30f) &&
                    Math.Abs(heights.SampleHeight(x, z) - heights.SampleHeight(worldX, worldZ)) <= 1.5f)
                    return true;
            }
            return false;
        }

        private static void SampleSpline(WorldMapSpline spline, float progress, out WorldMapPoint point,
            out float width, out float tangentX, out float tangentZ)
        {
            progress = Math.Max(0f, Math.Min(1f, progress));
            var lengths = new float[spline.Points.Count - 1];
            var total = 0f;
            for (var i = 0; i < lengths.Length; i++)
            {
                var a = spline.Points[i].Position;
                var b = spline.Points[i + 1].Position;
                var dx = b.X - a.X;
                var dz = b.Z - a.Z;
                lengths[i] = (float)Math.Sqrt(dx * dx + dz * dz);
                total += lengths[i];
            }
            var target = progress * total;
            var traversed = 0f;
            for (var i = 0; i < lengths.Length; i++)
            {
                if (target > traversed + lengths[i] && i + 1 < lengths.Length)
                {
                    traversed += lengths[i];
                    continue;
                }
                var local = lengths[i] <= 0f ? 0f : (target - traversed) / lengths[i];
                var a = spline.Points[i];
                var b = spline.Points[i + 1];
                point = new WorldMapPoint(a.Position.X + (b.Position.X - a.Position.X) * local,
                    a.Position.Z + (b.Position.Z - a.Position.Z) * local);
                width = a.WidthMeters + (b.WidthMeters - a.WidthMeters) * local;
                tangentX = b.Position.X - a.Position.X;
                tangentZ = b.Position.Z - a.Position.Z;
                return;
            }
            var last = spline.Points[spline.Points.Count - 1];
            var previous = spline.Points[spline.Points.Count - 2];
            point = last.Position;
            width = last.WidthMeters;
            tangentX = last.Position.X - previous.Position.X;
            tangentZ = last.Position.Z - previous.Position.Z;
        }

        private bool TryFindWorldMapPoint(WorldMapResourceZone zone, WorldMapResourceType type, int ordinal,
            out WorldMapPoint design, out WorldPosition position)
        {
            ShapeBounds(zone.Shape, out var xMin, out var xMax, out var zMin, out var zMax);
            var salt = StableHash(zone.Id + ":" + type.Id);
            var spacing = type.IsHidden ? 18f : IsLargeNode(type.Id) ? 9f : 5f;
            for (var attempt = 0; attempt < 2048; attempt++)
            {
                var x = xMin + Unit(salt ^ ordinal * 733, attempt, 41) * (xMax - xMin);
                var z = zMin + Unit(salt ^ ordinal * 977, attempt, 43) * (zMax - zMin);
                design = new WorldMapPoint(x, z);
                if (!zone.Shape.Contains(design)) continue;
                var projected = definition.MapData.CoordinateTransform.ToUnity(design);
                if (!AcceptWorldMapResource(zone, type.Id, design, projected.X, projected.Z) ||
                    TooClose(projected.X, projected.Z, spacing)) continue;
                position = new WorldPosition(projected.X, heights.SampleHeight(projected.X, projected.Z), projected.Z);
                return true;
            }
            design = default;
            position = default;
            return false;
        }

        private bool AcceptWorldMapResource(WorldMapResourceZone zone, string resourceId, WorldMapPoint design,
            float worldX, float worldZ)
        {
            if (!definition.ContainsClosed(worldX, worldZ) || heights.IsWater(worldX, worldZ) ||
                !heights.IsBuildable(worldX, worldZ, MaximumSlope(resourceId))) return false;
            if (definition.MapData.StartTerritory.IsPrepared(design,
                    WorldMapStartTerritoryDefinition.ResourceCorridorClearanceMeters)) return false;
            foreach (var route in definition.MapData.Routes)
                if (DistanceToSpline(design, route) < route.MaximumWidthMeters * 0.5f + 2f) return false;
            if (zone.Id == "res_start_basics" && IsLargeNode(resourceId))
            {
                var start = definition.MapData.CoordinateTransform.ToDesign(definition.PlayerStart);
                var dx = design.X - start.X;
                var dz = design.Z - start.Z;
                if (dx * dx + dz * dz < 58f * 58f) return false;
            }
            foreach (var poi in definition.MapData.PointsOfInterest)
            {
                var dx = design.X - poi.Position.X;
                var dz = design.Z - poi.Position.Z;
                var radius = poi.ProtectionRadiusMeters + 2f;
                if (dx * dx + dz * dz < radius * radius) return false;
            }
            // Resource zones are authored semantic placement contracts. Terrain paint is a disposable visual
            // projection and used to be sampled for every random candidate, turning population planning into
            // minutes of repeated height/slope/biome work. Once shape, water, slope, routes, POIs and prepared
            // corridors accept a point, the zone itself is sufficient authority for the resource type.
            return true;
        }

        private static float PaintAffinity(string resourceId, IReadOnlyList<float> weights)
        {
            var grass = weights[(int)WorldMapTerrainPaintLayer.Grass];
            var forest = weights[(int)WorldMapTerrainPaintLayer.ForestMoss];
            var rock = weights[(int)WorldMapTerrainPaintLayer.Rock];
            var shore = weights[(int)WorldMapTerrainPaintLayer.Shore];
            var soil = weights[(int)WorldMapTerrainPaintLayer.Soil];
            var scrub = weights[(int)WorldMapTerrainPaintLayer.Scrub];
            var wetland = weights[(int)WorldMapTerrainPaintLayer.Wetland];
            switch (resourceId)
            {
                case "wood": case "branches": case "mushrooms": return forest + grass * 0.25f;
                case "berries": return grass + forest * 0.65f + scrub * 0.35f;
                case "medicinal_plants": return grass + scrub + wetland;
                case "plant_fiber": return wetland + scrub + grass * 0.45f;
                case "clay": return soil + shore + wetland * 0.55f;
                case "stone": case "flint": case "copper_ore": case "iron_ore": case "gold_ore":
                    return rock + scrub * 0.35f;
                case "oil": return wetland + soil * 0.25f;
                default: return 1f;
            }
        }

        private static int CountForDensity(string density)
        {
            switch (density)
            {
                case "high": return 18;
                case "medium_high": return 12;
                case "medium": return 10;
                case "hidden_gradient": return 6;
                case "hidden_reservoir": return 1;
                case "low_to_medium": return 5;
                default: throw new InvalidOperationException("Unsupported resource density: " + density);
            }
        }

        private long CapacityFor(WorldMapResourceType type, WorldMapResourceZone zone, int ordinal)
        {
            var minimum = type.IsHidden || type.Id == "clay" ? 40 : 24;
            return minimum + (long)(Hash(StableHash(type.Id), StableHash(zone.Id), ordinal, 71) %
                                     (ulong)(51 - minimum));
        }

        private static string RecipeFor(string resourceId)
        {
            switch (resourceId)
            {
                case "wood": return "chop_wood";
                case "stone": return "gather_stone";
                case "berries": return "gather_berries";
                case "mushrooms": return "gather_mushrooms";
                case "medicinal_plants": return "gather_medicinal_plants";
                case "branches": return "gather_branches";
                case "plant_fiber": return "gather_fiber";
                case "clay": return "gather_clay";
                case "flint": return "gather_flint";
                case "copper_ore": return "gather_copper";
                case "iron_ore": return "gather_iron";
                case "gold_ore": return "gather_gold";
                case "oil": return "extract_oil";
                default: throw new InvalidOperationException("Unsupported WM06 resource: " + resourceId);
            }
        }

        private static NaturalResourceBiome BiomeFor(string resourceId)
        {
            switch (resourceId)
            {
                case "wood": case "branches": case "mushrooms": return NaturalResourceBiome.Forest;
                case "stone": case "flint": return NaturalResourceBiome.Rocky;
                case "copper_ore": case "iron_ore": case "gold_ore": return NaturalResourceBiome.Mountain;
                case "plant_fiber": case "clay": case "oil": return NaturalResourceBiome.Wetland;
                default: return NaturalResourceBiome.Meadow;
            }
        }

        private static float MaximumSlope(string resourceId)
        {
            switch (resourceId)
            {
                case "stone": case "flint": case "copper_ore": case "iron_ore": case "gold_ore": return 38f;
                case "wood": case "branches": return 30f;
                case "clay": case "oil": return 24f;
                default: return 22f;
            }
        }

        private static bool IsLargeNode(string resourceId) => resourceId == "wood" || resourceId == "stone" ||
            resourceId == "flint" || resourceId.EndsWith("_ore", StringComparison.Ordinal) || resourceId == "oil";

        private static void ShapeBounds(WorldMapShape shape, out float xMin, out float xMax,
            out float zMin, out float zMax)
        {
            if (shape.Kind == WorldMapShapeKind.Polygon)
            {
                xMin = shape.Polygon.Points.Min(value => value.X);
                xMax = shape.Polygon.Points.Max(value => value.X);
                zMin = shape.Polygon.Points.Min(value => value.Z);
                zMax = shape.Polygon.Points.Max(value => value.Z);
                return;
            }
            xMin = shape.Center.X - shape.RadiusX;
            xMax = shape.Center.X + shape.RadiusX;
            zMin = shape.Center.Z - shape.RadiusZ;
            zMax = shape.Center.Z + shape.RadiusZ;
        }

        private static float DistanceToSpline(WorldMapPoint point, WorldMapSpline spline)
        {
            var result = float.MaxValue;
            for (var index = 1; index < spline.Points.Count; index++)
                result = Math.Min(result, DistanceToSegment(point, spline.Points[index - 1].Position,
                    spline.Points[index].Position));
            return result;
        }

        private static float DistanceToSegment(WorldMapPoint point, WorldMapPoint start, WorldMapPoint end)
        {
            var dx = end.X - start.X;
            var dz = end.Z - start.Z;
            var lengthSquared = dx * dx + dz * dz;
            var progress = lengthSquared <= 0f ? 0f :
                ((point.X - start.X) * dx + (point.Z - start.Z) * dz) / lengthSquared;
            progress = Math.Max(0f, Math.Min(1f, progress));
            var x = start.X + dx * progress;
            var z = start.Z + dz * progress;
            dx = point.X - x;
            dz = point.Z - z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        private void AddTileResource(ICollection<TiledWorldResourceNode> target, ref int stableIndex,
            WorldTileId tile, string recipe, NaturalResourceBiome biome, int ordinal)
        {
            var position = FindTilePoint(tile, stableIndex * 11 + ordinal, recipe);
            var capacity = 20 + (long)(Hash(stableIndex, tile.X, tile.Z, 17) % 31UL);
            target.Add(new TiledWorldResourceNode(stableIndex++, tile, recipe, biome, position, capacity, string.Empty));
            occupied.Add(position);
        }

        private void AddFishingSchools(ICollection<TiledWorldResourceNode> target, ref int stableIndex,
            WorldZoneDefinition lake)
        {
            for (var school = 0; school < 4; school++)
            {
                // The source's work/output point is a dry bank, while its selectable fish
                // projection remains in the water. Neither requires an actor to swim.
                WorldPosition bank = default;
                double angle = 0;
                var found = false;
                for (var attempt = 0; attempt < 48 && !found; attempt++)
                {
                    angle = (225 + school * 90 + (attempt % 9 - 4) * 4) * Math.PI / 180d;
                    // Work beyond the ten-metre bank transition; its inner crest can
                    // be too steep for the runtime NavMesh despite a dry height sample.
                    var distance = 12f + attempt / 9 * 2f;
                    var x = lake.Center.X + (float)Math.Cos(angle) * (lake.RadiusX + distance);
                    var z = lake.Center.Z + (float)Math.Sin(angle) * (lake.RadiusZ + distance);
                    if (!heights.IsBuildable(x, z, 30f)) continue;
                    bank = new WorldPosition(x, heights.SampleHeight(x, z), z);
                    found = true;
                }
                if (!found) throw new InvalidOperationException("No dry fishing bank for " + lake.Id + "/" + school);
                var water = new WorldPosition(lake.Center.X + (float)Math.Cos(angle) * lake.RadiusX * 0.55f,
                    TiledWorldHeightSampler.WaterLevelMeters + 0.12f,
                    lake.Center.Z + (float)Math.Sin(angle) * lake.RadiusZ * 0.55f);
                definition.TryGetTile(bank.X, bank.Z, out var tile);
                target.Add(new TiledWorldResourceNode(stableIndex++, tile, "fish_water",
                    NaturalResourceBiome.Lake, bank, 48, lake.Id, water));
                occupied.Add(bank);
            }
        }

        private void AddZoneResources(ICollection<TiledWorldResourceNode> target, ref int stableIndex,
            WorldZoneDefinition zone, string recipe, NaturalResourceBiome biome, int count)
        {
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (!TryFindZonePoint(zone, recipe, stableIndex * 13 + ordinal, out var position)) continue;
                definition.TryGetTile(position.X, position.Z, out var tile);
                var capacity = 20 + (long)(Hash(stableIndex, ordinal, StableHash(zone.Id), 29) % 31UL);
                target.Add(new TiledWorldResourceNode(stableIndex++, tile, recipe, biome, position, capacity, zone.Id));
                occupied.Add(position);
            }
        }

        private void AddIndustry(ICollection<TiledWorldIndustrialNode> target, ref int stableIndex,
            WorldZoneDefinition zone, string definitionId, string resourceId, int ordinal, bool shore = false)
        {
            if (!TryFindIndustryPoint(zone, stableIndex * 17 + ordinal, shore, out var position)) return;
            definition.TryGetTile(position.X, position.Z, out var tile);
            target.Add(new TiledWorldIndustrialNode(stableIndex++, tile, definitionId, resourceId,
                position, zone.Id));
            occupied.Add(position);
        }

        private WorldPosition FindTilePoint(WorldTileId tile, int salt, string recipe)
        {
            var origin = definition.TileOrigin(tile);
            for (var attempt = 0; attempt < 160; attempt++)
            {
                var x = origin.X + 8f + Unit(salt, attempt, 1) * (definition.TileSizeMeters - 16f);
                var z = origin.Z + 8f + Unit(salt, attempt, 2) * (definition.TileSizeMeters - 16f);
                if (!AcceptLand(recipe, x, z, attempt >= 96) || TooClose(x, z, 8f)) continue;
                return new WorldPosition(x, heights.SampleHeight(x, z), z);
            }
            throw new InvalidOperationException("No dry resource placement in tile " + tile);
        }

        private bool TryFindZonePoint(WorldZoneDefinition zone, string recipe, int salt, out WorldPosition position)
        {
            for (var attempt = 0; attempt < 256; attempt++)
            {
                var dx = (Unit(salt, attempt, 3) * 2f - 1f) * zone.RadiusX * 0.88f;
                var dz = (Unit(salt, attempt, 4) * 2f - 1f) * zone.RadiusZ * 0.88f;
                var x = zone.Center.X + dx;
                var z = zone.Center.Z + dz;
                if (!zone.Contains(x, z) || !definition.TryGetTile(x, z, out _) || TooClose(x, z, 7f)) continue;
                if (recipe == "fish_water")
                {
                    if (!heights.IsWater(x, z)) continue;
                    position = new WorldPosition(x, TiledWorldHeightSampler.WaterLevelMeters + 0.12f, z);
                    return true;
                }
                if (!AcceptLand(recipe, x, z, true)) continue;
                position = new WorldPosition(x, heights.SampleHeight(x, z), z);
                return true;
            }
            position = default;
            return false;
        }

        private bool TryFindIndustryPoint(WorldZoneDefinition zone, int salt, bool shore, out WorldPosition position)
        {
            for (var attempt = 0; attempt < 256; attempt++)
            {
                var angle = Unit(salt, attempt, 5) * Math.PI * 2d;
                var radius = shore ? 1f + (10f + Unit(salt, attempt, 6) * 8f) /
                    Math.Min(zone.RadiusX, zone.RadiusZ) : Unit(salt, attempt, 6) * 0.72f;
                var x = zone.Center.X + (float)Math.Cos(angle) * zone.RadiusX * radius;
                var z = zone.Center.Z + (float)Math.Sin(angle) * zone.RadiusZ * radius;
                if (!definition.TryGetTile(x, z, out _) || heights.IsWater(x, z) ||
                    !heights.IsBuildable(x, z, 34f) || TooClose(x, z, 9f)) continue;
                position = new WorldPosition(x, heights.SampleHeight(x, z), z);
                return true;
            }
            position = default;
            return false;
        }

        private bool AcceptLand(string recipe, float x, float z, bool relaxedAgriculture)
        {
            if (!definition.TryGetTile(x, z, out _) || heights.IsWater(x, z) ||
                !heights.IsBuildable(x, z, recipe == "gather_gold" || recipe == "gather_iron" ? 38f : 30f))
                return false;
            var uses = definition.UsesAt(x, z);
            if (uses.HasFlag(WorldZoneUse.Settlement)) return false;
            if (!relaxedAgriculture && uses.HasFlag(WorldZoneUse.Agriculture) &&
                (recipe == "chop_wood" || recipe == "gather_stone")) return false;
            return true;
        }

        private bool TooClose(float x, float z, float radius)
        {
            var square = radius * radius;
            return occupied.Any(value =>
            {
                var dx = value.X - x;
                var dz = value.Z - z;
                return dx * dx + dz * dz < square;
            });
        }

        private float Unit(int a, int b, int c) => (Hash(a, b, c, 0) & 0xFFFFFFUL) / 16777215f;

        private ulong Hash(int a, int b, int c, int d)
        {
            unchecked
            {
                ulong value = (ulong)seed ^ (uint)a * 0x9E3779B185EBCA87UL;
                value ^= (uint)b * 0xC2B2AE3D27D4EB4FUL;
                value ^= (uint)c * 0x165667B19E3779F9UL;
                value ^= (uint)d * 0xD6E8FEB86659FD93UL;
                value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27; value *= 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value ?? string.Empty) hash = hash * 31 + character;
                return hash;
            }
        }
    }
}
