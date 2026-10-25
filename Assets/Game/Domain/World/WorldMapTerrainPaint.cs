using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    /// <summary>Semantic ordering shared with the seven admitted P9.3 Terrain layers.</summary>
    public enum WorldMapTerrainPaintLayer
    {
        Grass = 0,
        ForestMoss = 1,
        Rock = 2,
        Shore = 3,
        Soil = 4,
        Scrub = 5,
        Wetland = 6
    }

    /// <summary>
    /// Pure deterministic WM04 paint authority. Unity Terrain and the strategic map consume the same weights.
    /// </summary>
    public sealed class WorldMapTerrainPaintSampler
    {
        public const int LayerCount = 7;
        private readonly TiledWorldDefinition definition;
        private readonly TiledWorldHeightSampler heights;
        private readonly WorldMapBiomeDefinition biomes;
        private readonly Dictionary<string, float[]> regionalProfiles;
        private readonly long seed;

        public WorldMapTerrainPaintSampler(TiledWorldDefinition worldDefinition,
            TiledWorldHeightSampler heightSampler)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            heights = heightSampler ?? throw new ArgumentNullException(nameof(heightSampler));
            if (!ReferenceEquals(definition, heights.Definition))
                throw new ArgumentException("Paint and height samplers must use the same world definition.",
                    nameof(heightSampler));
            biomes = definition.MapData?.Biomes ??
                     throw new ArgumentException("World Map V2 biome contracts are required.", nameof(worldDefinition));
            seed = heights.WorldSeed;
            regionalProfiles = biomes.Regions.ToDictionary(value => value.Id, BuildRegionalProfile,
                StringComparer.Ordinal);
        }

        public TiledWorldDefinition Definition => definition;

        public float[] SampleWeights(float worldX, float worldZ)
        {
            if (!definition.ContainsClosed(worldX, worldZ))
                throw new ArgumentOutOfRangeException(nameof(worldX));
            var point = definition.MapData.CoordinateTransform.ToDesign(new WorldPosition(worldX, 0f, worldZ));
            var result = new float[LayerCount];
            var transition = biomes.Transitions;
            var widthNoise = (ValueNoise(point.X / 180f, point.Z / 180f, 101) + 1f) * 0.5f;
            var blendWidth = Math.Max(transition.MinimumBlendWidthMeters,
                Lerp(transition.PreferredMinimumBlendWidthMeters,
                    transition.PreferredMaximumBlendWidthMeters, widthNoise));
            var membershipTotal = 0f;
            WorldMapBiomeRegion nearest = null;
            var nearestDistance = float.MaxValue;

            foreach (var region in biomes.Regions)
            {
                var distance = DistanceToPolygon(point, region.Polygon);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = region;
                }
                var signedDistance = region.Polygon.Contains(point) ? distance : -distance;
                var membership = Smooth01((signedDistance + blendWidth) / (2f * blendWidth));
                if (membership <= 0f) continue;
                AddScaled(result, regionalProfiles[region.Id], membership);
                membershipTotal += membership;
            }

            if (membershipTotal <= 0.0001f)
                AddScaled(result, regionalProfiles[nearest.Id], 1f);
            else
                Scale(result, 1f / membershipTotal);

            ApplyTerrainModifiers(result, point, worldX, worldZ);
            Normalize(result);
            return result;
        }

        /// <summary>Edge-aligned alphamap samples; adjacent tiles evaluate identical global coordinates.</summary>
        public float[,,] CreateTileWeights(WorldTileId tile, int resolution)
        {
            if (!definition.IsValid(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
            if (resolution < 2 || resolution > 2049) throw new ArgumentOutOfRangeException(nameof(resolution));
            var result = new float[resolution, resolution, LayerCount];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var position = definition.SamplePosition(tile, x, z, resolution);
                var weights = SampleWeights(position.X, position.Z);
                for (var layer = 0; layer < LayerCount; layer++) result[z, x, layer] = weights[layer];
            }
            return result;
        }

        public ulong CalculateMaskHash(int samplesPerAxis)
        {
            if (samplesPerAxis < 2 || samplesPerAxis > 1025)
                throw new ArgumentOutOfRangeException(nameof(samplesPerAxis));
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            var hash = offset;
            for (var z = 0; z < samplesPerAxis; z++)
            for (var x = 0; x < samplesPerAxis; x++)
            {
                var worldX = definition.Origin.X + definition.WidthMeters * x / (samplesPerAxis - 1f);
                var worldZ = definition.Origin.Z + definition.DepthMeters * z / (samplesPerAxis - 1f);
                foreach (var weight in SampleWeights(worldX, worldZ))
                {
                    var quantized = (uint)Math.Round(Math.Max(0f, Math.Min(1f, weight)) * 65535f);
                    hash = (hash ^ (quantized & 0xffU)) * prime;
                    hash = (hash ^ (quantized >> 8)) * prime;
                }
            }
            return hash;
        }

        private void ApplyTerrainModifiers(float[] weights, WorldMapPoint point, float worldX, float worldZ)
        {
            var height = heights.SampleHeight(worldX, worldZ);
            // Paint reads a broader slope footprint than construction rules so isolated heightmap facets do not
            // create speckled cliff paint or abrupt one-cell changes.
            var slope = heights.SampleSlopeDegrees(worldX, worldZ, 12f);
            var distanceToWater = heights.DistanceToWater(worldX, worldZ);
            var rock = SmoothRange(slope, 16f, 34f);
            var highland = SmoothRange(height, 46f, 78f);
            var shore = 1f - SmoothRange(distanceToWater, 2f, 18f);
            var moisture = 1f - SmoothRange(distanceToWater, 0f, 42f);

            weights[(int)WorldMapTerrainPaintLayer.Rock] += rock * 3.2f + highland * 1.15f;
            weights[(int)WorldMapTerrainPaintLayer.Scrub] += highland * (1f - rock * 0.55f) * 0.8f;
            weights[(int)WorldMapTerrainPaintLayer.Shore] += shore * 1.45f;
            weights[(int)WorldMapTerrainPaintLayer.Wetland] += moisture * 2.8f;

            var micro = (ValueNoise(point.X / 34f, point.Z / 34f, 109) + 1f) * 0.5f;
            weights[(int)WorldMapTerrainPaintLayer.Grass] *= 0.86f + micro * 0.28f;
            weights[(int)WorldMapTerrainPaintLayer.ForestMoss] *= 0.9f + micro * 0.2f;
            weights[(int)WorldMapTerrainPaintLayer.Soil] *= 1.08f - micro * 0.16f;
            weights[(int)WorldMapTerrainPaintLayer.Scrub] *= 0.9f + (1f - micro) * 0.2f;
        }

        private static float[] BuildRegionalProfile(WorldMapBiomeRegion region)
        {
            var weights = new[] { 0.16f, 0.08f, 0.04f, 0.03f, 0.12f, 0.05f, 0.03f };
            foreach (var token in region.Biomes.Concat(region.Soils))
            {
                var value = token.ToLowerInvariant();
                if (Has(value, "forest", "conifer", "grove", "woodland", "podzol"))
                {
                    weights[(int)WorldMapTerrainPaintLayer.ForestMoss] += 1.45f;
                    weights[(int)WorldMapTerrainPaintLayer.Grass] += 0.2f;
                }
                if (Has(value, "meadow", "clearing", "loam", "alluvial"))
                {
                    weights[(int)WorldMapTerrainPaintLayer.Grass] += 0.72f;
                    weights[(int)WorldMapTerrainPaintLayer.Soil] += 0.88f;
                }
                if (Has(value, "rock", "highland", "gravel", "stony", "pass", "alpine"))
                {
                    weights[(int)WorldMapTerrainPaintLayer.Rock] += 1.42f;
                    weights[(int)WorldMapTerrainPaintLayer.Scrub] += 0.34f;
                }
                if (Has(value, "marsh", "wet", "riparian", "shore", "reed", "peat", "waterlogged"))
                {
                    weights[(int)WorldMapTerrainPaintLayer.Wetland] += 1.46f;
                    weights[(int)WorldMapTerrainPaintLayer.Shore] += 0.66f;
                }
                if (Has(value, "clay", "soil"))
                    weights[(int)WorldMapTerrainPaintLayer.Soil] += 0.96f;
                if (Has(value, "scrub", "shrub"))
                    weights[(int)WorldMapTerrainPaintLayer.Scrub] += 1.28f;
                if (Has(value, "sand"))
                    weights[(int)WorldMapTerrainPaintLayer.Shore] += 1f;
            }
            // Regional soils retain their existing smooth spatial membership, but grass must dominate
            // meadows: the former loam rule made every open area look like the same bare dirt field.
            if (region.Id == "region_a_start_valley" || region.Id == "region_e_south_meadows")
            {
                weights[(int)WorldMapTerrainPaintLayer.Grass] *= 3.3f;
                weights[(int)WorldMapTerrainPaintLayer.Soil] *= .35f;
                weights[(int)WorldMapTerrainPaintLayer.Wetland] *= .25f;
            }
            if (region.Id == "region_d_west_grove")
            {
                weights[(int)WorldMapTerrainPaintLayer.Grass] *= 2.2f;
                weights[(int)WorldMapTerrainPaintLayer.ForestMoss] *= .65f;
            }
            Normalize(weights);
            return weights;
        }

        private static bool Has(string value, params string[] fragments) =>
            fragments.Any(fragment => value.IndexOf(fragment, StringComparison.Ordinal) >= 0);

        private static float DistanceToPolygon(WorldMapPoint point, WorldMapPolygon polygon)
        {
            var best = float.MaxValue;
            for (var index = 0; index < polygon.Points.Count; index++)
            {
                var a = polygon.Points[index];
                var b = polygon.Points[(index + 1) % polygon.Points.Count];
                var abX = b.X - a.X;
                var abZ = b.Z - a.Z;
                var lengthSquared = abX * abX + abZ * abZ;
                var t = lengthSquared <= 0f ? 0f :
                    ((point.X - a.X) * abX + (point.Z - a.Z) * abZ) / lengthSquared;
                t = Math.Max(0f, Math.Min(1f, t));
                var dx = point.X - (a.X + abX * t);
                var dz = point.Z - (a.Z + abZ * t);
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        private float ValueNoise(float x, float z, int octave)
        {
            var ix = (int)Math.Floor(x);
            var iz = (int)Math.Floor(z);
            var fx = Smooth01(x - ix);
            var fz = Smooth01(z - iz);
            var a = HashSigned(ix, iz, octave);
            var b = HashSigned(ix + 1, iz, octave);
            var c = HashSigned(ix, iz + 1, octave);
            var d = HashSigned(ix + 1, iz + 1, octave);
            return Lerp(Lerp(a, b, fx), Lerp(c, d, fx), fz);
        }

        private float HashSigned(int x, int z, int octave)
        {
            unchecked
            {
                ulong value = (ulong)seed;
                value ^= (ulong)(uint)x * 0x9E3779B185EBCA87UL;
                value ^= (ulong)(uint)z * 0xC2B2AE3D27D4EB4FUL;
                value ^= (ulong)(uint)octave * 0x165667B19E3779F9UL;
                value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL;
                value ^= value >> 27; value *= 0x94D049BB133111EBUL;
                value ^= value >> 31;
                return (float)((value & 0xFFFFFFUL) / 8388607.5d - 1d);
            }
        }

        private static void AddScaled(float[] target, float[] source, float scale)
        {
            for (var index = 0; index < target.Length; index++) target[index] += source[index] * scale;
        }

        private static void Scale(float[] values, float scale)
        {
            for (var index = 0; index < values.Length; index++) values[index] *= scale;
        }

        private static void Normalize(float[] values)
        {
            var total = values.Sum();
            if (total <= 0f) throw new InvalidOperationException("Terrain paint weights must have positive mass.");
            Scale(values, 1f / total);
        }

        private static float SmoothRange(float value, float minimum, float maximum) =>
            Smooth01((value - minimum) / Math.Max(0.0001f, maximum - minimum));

        private static float Smooth01(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - 2f * value);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
