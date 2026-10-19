using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.Production;

namespace Game.Domain.World
{
    public sealed class TiledWorldResourceNode
    {
        public TiledWorldResourceNode(int stableIndex, WorldTileId tile, string recipeId,
            NaturalResourceBiome biome, WorldPosition position, long capacity, string sourceZoneId)
        {
            if (stableIndex < 0) throw new ArgumentOutOfRangeException(nameof(stableIndex));
            if (string.IsNullOrWhiteSpace(recipeId)) throw new ArgumentException(nameof(recipeId));
            if (capacity < 20 || capacity > 50) throw new ArgumentOutOfRangeException(nameof(capacity));
            StableIndex = stableIndex;
            Tile = tile;
            RecipeId = recipeId;
            Biome = biome;
            Position = position;
            Capacity = capacity;
            SourceZoneId = sourceZoneId ?? string.Empty;
        }

        public int StableIndex { get; }
        public WorldTileId Tile { get; }
        public string RecipeId { get; }
        public NaturalResourceBiome Biome { get; }
        public WorldPosition Position { get; }
        public long Capacity { get; }
        public string SourceZoneId { get; }
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
            IEnumerable<TiledWorldIndustrialNode> industry)
        {
            Resources = Array.AsReadOnly((resources ?? throw new ArgumentNullException(nameof(resources)))
                .OrderBy(value => value.StableIndex).ToArray());
            Industry = Array.AsReadOnly((industry ?? throw new ArgumentNullException(nameof(industry)))
                .OrderBy(value => value.StableIndex).ToArray());
            if (Resources.Select(value => value.StableIndex).Distinct().Count() != Resources.Count ||
                Industry.Select(value => value.StableIndex).Distinct().Count() != Industry.Count)
                throw new ArgumentException("Population indices must be unique.");
        }

        public IReadOnlyList<TiledWorldResourceNode> Resources { get; }
        public IReadOnlyList<TiledWorldIndustrialNode> Industry { get; }
    }

    /// <summary>
    /// Deterministic canonical population layout. Loaded GameObjects are projections of this plan, never the authority.
    /// </summary>
    public sealed class TiledWorldPopulationPlanner
    {
        private readonly TiledWorldDefinition definition;
        private readonly TiledWorldHeightSampler heights;
        private readonly long seed;
        private readonly List<WorldPosition> occupied = new List<WorldPosition>();

        public TiledWorldPopulationPlanner(TiledWorldDefinition worldDefinition,
            TiledWorldHeightSampler heightSampler, long worldSeed)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(definition, heights.Definition))
                throw new ArgumentException("Population and height samplers must share one world definition.");
            seed = worldSeed;
        }

        public TiledWorldPopulationPlan Create()
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
                    AddZoneResources(resources, ref nextResource, zone, "chop_wood", NaturalResourceBiome.Forest, 14);
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
                    AddZoneResources(resources, ref nextResource, zone, "fish_water", NaturalResourceBiome.Lake, 4);
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

        private void AddTileResource(ICollection<TiledWorldResourceNode> target, ref int stableIndex,
            WorldTileId tile, string recipe, NaturalResourceBiome biome, int ordinal)
        {
            var position = FindTilePoint(tile, stableIndex * 11 + ordinal, recipe);
            var capacity = 20 + (long)(Hash(stableIndex, tile.X, tile.Z, 17) % 31UL);
            target.Add(new TiledWorldResourceNode(stableIndex++, tile, recipe, biome, position, capacity, string.Empty));
            occupied.Add(position);
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
                var x = origin.X + 42f + Unit(salt, attempt, 1) * (definition.TileSizeMeters - 84f);
                var z = origin.Z + 42f + Unit(salt, attempt, 2) * (definition.TileSizeMeters - 84f);
                if (!AcceptLand(recipe, x, z, attempt >= 96) || TooClose(x, z, 26f)) continue;
                return new WorldPosition(x, heights.SampleHeight(x, z), z);
            }
            var fallbackX = origin.X + 100f + (salt % 7) * 110f;
            var fallbackZ = origin.Z + 100f + (salt % 5) * 150f;
            return new WorldPosition(fallbackX, heights.SampleHeight(fallbackX, fallbackZ), fallbackZ);
        }

        private bool TryFindZonePoint(WorldZoneDefinition zone, string recipe, int salt, out WorldPosition position)
        {
            for (var attempt = 0; attempt < 256; attempt++)
            {
                var dx = (Unit(salt, attempt, 3) * 2f - 1f) * zone.RadiusX * 0.88f;
                var dz = (Unit(salt, attempt, 4) * 2f - 1f) * zone.RadiusZ * 0.88f;
                var x = zone.Center.X + dx;
                var z = zone.Center.Z + dz;
                if (!zone.Contains(x, z) || !definition.TryGetTile(x, z, out _) || TooClose(x, z, 24f)) continue;
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
                var radius = shore ? 1.08f + Unit(salt, attempt, 6) * 0.18f : Unit(salt, attempt, 6) * 0.72f;
                var x = zone.Center.X + (float)Math.Cos(angle) * zone.RadiusX * radius;
                var z = zone.Center.Z + (float)Math.Sin(angle) * zone.RadiusZ * radius;
                if (!definition.TryGetTile(x, z, out _) || heights.IsWater(x, z) ||
                    !heights.IsBuildable(x, z, 34f) || TooClose(x, z, 38f)) continue;
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
