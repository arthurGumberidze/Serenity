using System;
using System.Collections.Generic;
using System.Linq;
using Game.Domain.AI;

namespace Game.Domain.World
{
    /// <summary>
    /// Pure deterministic height, water and buildability authority for the P10.9 tiled world.
    /// Terrain objects, colliders and strategic meshes are disposable projections of this data.
    /// </summary>
    public sealed class TiledWorldHeightSampler
    {
        public const float TerrainHeightMeters = 96f;
        public const float WaterLevelMeters = 8f;
        public const float RiverHalfWidthMeters = 52f / 12f;
        private const float SettlementHeightMeters = 12f;
        private const float ProposedNoiseReliefScale = 0.5f;
        private readonly TiledWorldDefinition definition;
        private readonly long seed;
        private readonly WorldMapTerrainDefinition authoredTerrain;

        public TiledWorldHeightSampler(TiledWorldDefinition worldDefinition, long worldSeed)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            seed = worldSeed;
            authoredTerrain = definition.MapData?.Terrain;
        }

        public TiledWorldDefinition Definition => definition;
        public long WorldSeed => seed;
        public int HeightmapResolution => authoredTerrain?.TileResolution ?? 257;
        public float MaximumTerrainHeightMeters => authoredTerrain?.MaximumHeightMeters ?? TerrainHeightMeters;

        public float SampleHeight(float worldX, float worldZ)
        {
            if (!definition.ContainsClosed(worldX, worldZ))
                throw new ArgumentOutOfRangeException(nameof(worldX), "World height samples must remain inside the tiled world.");

            return authoredTerrain == null
                ? SampleLegacyHeight(worldX, worldZ)
                : SampleAuthoredHeight(worldX, worldZ);
        }

        private float SampleLegacyHeight(float worldX, float worldZ)
        {

            var macro = ValueNoise(worldX / 150f, worldZ / 150f, 0) * 3f;
            var medium = ValueNoise(worldX / 52f, worldZ / 52f, 1) * 1.2f;
            var detail = ValueNoise(worldX / 16f, worldZ / 16f, 2) * 0.3f;
            var height = SettlementHeightMeters + macro + medium + detail;

            // Cultivated regions remain naturally rolling, but are deliberately gentle enough for plots and roads.
            foreach (var zone in definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Agriculture)))
            {
                var normalized = NormalizedDistance(zone, worldX, worldZ);
                if (normalized >= 1.12f) continue;
                var blend = Smooth01((1.12f - normalized) / 0.3f) * 0.72f;
                var agriculturalHeight = SettlementHeightMeters + ValueNoise(worldX / 80f, worldZ / 80f, 7) * 0.5f;
                height = Lerp(height, agriculturalHeight, blend);
            }

            foreach (var zone in definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Settlement)))
            {
                var normalized = NormalizedDistance(zone, worldX, worldZ);
                if (normalized >= 1.2f) continue;
                var blend = Smooth01((1.2f - normalized) / 0.45f);
                height = Lerp(height, SettlementHeightMeters, blend);
            }

            // Dry lowlands must not turn into unintended water patches outside the plan.
            height = Math.Max(WaterLevelMeters + 2.5f, height);
            var riverDistance = DistanceToRiver(worldX, worldZ);
            if (riverDistance < RiverHalfWidthMeters + 10f)
            {
                var blend = Smooth01((RiverHalfWidthMeters + 10f - riverDistance) / 10f);
                height = Lerp(height, WaterLevelMeters - 2.4f, blend);
            }

            foreach (var lake in definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Fishing)))
            {
                var normalized = NormalizedDistance(lake, worldX, worldZ);
                // Keep banks walkable at human scale, rather than scaling a cliff to 1/12.
                var bank = 10f / Math.Min(lake.RadiusX, lake.RadiusZ);
                if (normalized >= 1f + bank) continue;
                var blend = Smooth01((1f + bank - normalized) / (bank + 0.1f));
                height = Lerp(height, WaterLevelMeters - 2.1f, blend);
            }

            return Math.Max(0.5f, Math.Min(TerrainHeightMeters - 0.5f, height));
        }

        private float SampleAuthoredHeight(float worldX, float worldZ)
        {
            var design = definition.MapData.CoordinateTransform.ToDesign(new WorldPosition(worldX, 0f, worldZ));
            var plateau = authoredTerrain.Anchors.FirstOrDefault(value => value.Mode == "flatten_blend");
            var height = plateau?.HeightMeters ?? SettlementHeightMeters;
            for (var index = 0; index < authoredTerrain.NoiseOctaves.Count; index++)
            {
                var octave = authoredTerrain.NoiseOctaves[index];
                var choice = HashUnit(index, 31);
                var wavelength = Lerp(octave.MinimumWavelengthMeters, octave.MaximumWavelengthMeters, choice);
                var amplitude = Lerp(octave.MinimumAmplitudeMeters, octave.MaximumAmplitudeMeters,
                    HashUnit(index, 47));
                var frequencyRatio = authoredTerrain.NoiseOctaves[0].MinimumWavelengthMeters /
                                     octave.MinimumWavelengthMeters;
                var frequencyGain = frequencyRatio * frequencyRatio;
                height += ValueNoise(design.X / wavelength, design.Z / wavelength, 20 + index) *
                          amplitude * octave.Weight * ProposedNoiseReliefScale * frequencyGain;
            }

            foreach (var anchor in authoredTerrain.Anchors)
            {
                if (anchor.Mode == "flatten_blend" || anchor.Mode == "lower_pass") continue;
                var influence = RadialInfluence(design, anchor.Position, anchor.RadiusMeters, 0.15f, 1.15f);
                if (influence <= 0f) continue;
                switch (anchor.Mode)
                {
                    case "raise":
                        height = Math.Max(height, Lerp(height, anchor.HeightMeters, influence));
                        break;
                    case "rolling":
                        height = Lerp(height, anchor.HeightMeters +
                            ValueNoise(design.X / 65f, design.Z / 65f, 40) * 1.4f, influence * 0.72f);
                        break;
                    case "gentle":
                        height = Lerp(height, anchor.HeightMeters +
                            ValueNoise(design.X / 105f, design.Z / 105f, 41) * 0.45f, influence * 0.9f);
                        break;
                    case "lower":
                        height = Lerp(height, anchor.HeightMeters, influence * 0.92f);
                        break;
                }
            }

            foreach (var mask in authoredTerrain.HeightMasks)
            {
                if (mask.Operation == "ridge_spline_plus_erosion" && mask.Spline != null)
                    height = ApplyRidge(height, design, mask);
                else if (mask.Operation == "flatten_low" && mask.Shape != null)
                {
                    var influence = ShapeInfluence(mask.Shape, design, 0.45f);
                    var target = (mask.MinimumValue + mask.MaximumValue) * 0.5f +
                                 ValueNoise(design.X / 95f, design.Z / 95f, 42) *
                                 (mask.MaximumValue - mask.MinimumValue) * 0.18f;
                    height = Lerp(height, target, influence * 0.94f);
                }
            }

            // Passes and plateaus are last so the authored traversable surfaces cannot be overwritten by a ridge.
            foreach (var anchor in authoredTerrain.Anchors)
            {
                if (anchor.Mode != "lower_pass" && anchor.Mode != "flatten_blend") continue;
                var outerScale = anchor.Mode == "lower_pass" ? 2f : 1.35f;
                var innerScale = anchor.Mode == "lower_pass" ? 0.35f : 0.52f;
                var influence = RadialInfluence(design, anchor.Position, anchor.RadiusMeters, innerScale, outerScale);
                height = Lerp(height, anchor.HeightMeters, influence);
            }
            foreach (var mask in authoredTerrain.HeightMasks)
            {
                if (mask.Operation != "flatten_blend" || mask.Shape == null) continue;
                var influence = ShapeInfluence(mask.Shape, design, 0.55f);
                height = Lerp(height, mask.MinimumValue, influence);
            }

            return Math.Max(authoredTerrain.MinimumHeightMeters,
                Math.Min(authoredTerrain.MaximumHeightMeters, height));
        }

        private float ApplyRidge(float height, WorldMapPoint point, WorldMapHeightMask mask)
        {
            var distance = DistanceToSpline(point, mask.Spline, out var closest);
            var peak = authoredTerrain.Anchors.FirstOrDefault(value => value.Mode == "raise" &&
                value.Id.IndexOf("ridge_peak", StringComparison.Ordinal) >= 0);
            var ridgeHalfWidthMeters = peak == null ? 112f : peak.RadiusMeters * 0.66f;
            if (distance >= ridgeHalfWidthMeters) return height;
            var crossSection = Smooth01(1f - distance / ridgeHalfWidthMeters);
            var peakInfluence = peak == null ? 0f : RadialInfluence(closest, peak.Position,
                peak.RadiusMeters * 1.15f, 0f, 1f);
            var ridgeBase = authoredTerrain.Anchors.Where(value => value.Mode == "raise")
                .Select(value => value.HeightMeters).DefaultIfEmpty(43f).Min();
            var target = Lerp(ridgeBase, authoredTerrain.RidgePeakMeters, peakInfluence);
            var erosion = ValueNoise(point.X / 42f, point.Z / 42f, 43) * 2.4f * crossSection;
            return Math.Max(height, Lerp(height, target + erosion, crossSection * 0.88f));
        }

        public float SampleSlopeDegrees(float worldX, float worldZ, float sampleStepMeters = 4f)
        {
            if (!definition.ContainsClosed(worldX, worldZ) || !WorldMapPoint.IsFinite(sampleStepMeters) ||
                sampleStepMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(worldX));
            var minX = Math.Max(definition.Origin.X, worldX - sampleStepMeters);
            var maxX = Math.Min(definition.Origin.X + definition.WidthMeters, worldX + sampleStepMeters);
            var minZ = Math.Max(definition.Origin.Z, worldZ - sampleStepMeters);
            var maxZ = Math.Min(definition.Origin.Z + definition.DepthMeters, worldZ + sampleStepMeters);
            var dx = (SampleHeight(maxX, worldZ) - SampleHeight(minX, worldZ)) / Math.Max(0.001f, maxX - minX);
            var dz = (SampleHeight(worldX, maxZ) - SampleHeight(worldX, minZ)) / Math.Max(0.001f, maxZ - minZ);
            return (float)(Math.Atan(Math.Sqrt(dx * dx + dz * dz)) * 180d / Math.PI);
        }

        public WorldMapSlopeHistogram AnalyzeSlopeBudget(int samplesPerAxis = 129)
        {
            if (authoredTerrain == null)
                throw new InvalidOperationException("Slope-budget analysis requires authored terrain contracts.");
            if (samplesPerAxis < 3 || samplesPerAxis > 1025) throw new ArgumentOutOfRangeException(nameof(samplesPerAxis));
            var under12 = 0;
            var under18 = 0;
            var over32 = 0;
            var total = samplesPerAxis * samplesPerAxis;
            for (var z = 0; z < samplesPerAxis; z++)
            for (var x = 0; x < samplesPerAxis; x++)
            {
                var worldX = definition.Origin.X + definition.WidthMeters * x / (float)(samplesPerAxis - 1);
                var worldZ = definition.Origin.Z + definition.DepthMeters * z / (float)(samplesPerAxis - 1);
                var slope = SampleSlopeDegrees(worldX, worldZ);
                if (slope <= 12f) under12++;
                if (slope <= 18f) under18++;
                if (slope > 32f) over32++;
            }
            return new WorldMapSlopeHistogram(total, under12, under18, over32, authoredTerrain.SlopeTargets);
        }

        public bool IsWater(float worldX, float worldZ)
        {
            if (!definition.ContainsClosed(worldX, worldZ)) return false;
            if (DistanceToRiver(worldX, worldZ) <= RiverHalfWidthMeters) return true;
            return definition.Zones.Any(zone => zone.Uses.HasFlag(WorldZoneUse.Fishing) &&
                                                NormalizedDistance(zone, worldX, worldZ) <= 1f);
        }

        public bool IsBuildable(float worldX, float worldZ, float maximumSlopeDegrees = 32f)
        {
            if (!definition.TryGetTile(worldX, worldZ, out _) || maximumSlopeDegrees < 0f ||
                float.IsNaN(maximumSlopeDegrees) || float.IsInfinity(maximumSlopeDegrees) ||
                IsWater(worldX, worldZ)) return false;
            var waterLevel = authoredTerrain?.WaterLevelMeters ?? WaterLevelMeters;
            if (SampleHeight(worldX, worldZ) <= waterLevel + 0.75f) return false;
            return SampleSlopeDegrees(worldX, worldZ) <= maximumSlopeDegrees;
        }

        public float DistanceToRiver(float worldX, float worldZ)
        {
            var best = float.MaxValue;
            for (var i = 1; i < definition.River.Count; i++)
            {
                var a = definition.River[i - 1];
                var b = definition.River[i];
                var abX = b.X - a.X;
                var abZ = b.Z - a.Z;
                var lengthSquared = abX * abX + abZ * abZ;
                var t = lengthSquared <= 0f ? 0f : ((worldX - a.X) * abX + (worldZ - a.Z) * abZ) / lengthSquared;
                t = Math.Max(0f, Math.Min(1f, t));
                var dx = worldX - (a.X + abX * t);
                var dz = worldZ - (a.Z + abZ * t);
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        public float[,] CreateNormalizedHeights(WorldTileId tile, int resolution)
        {
            if (!definition.IsValid(tile)) throw new ArgumentOutOfRangeException(nameof(tile));
            if (resolution < 2 || resolution > 4097) throw new ArgumentOutOfRangeException(nameof(resolution));
            var result = new float[resolution, resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
            {
                var point = definition.SamplePosition(tile, x, z, resolution);
                result[z, x] = SampleHeight(point.X, point.Z) / MaximumTerrainHeightMeters;
            }
            return result;
        }

        private float ValueNoise(float x, float z, int octave)
        {
            var ix = (int)Math.Floor(x);
            var iz = (int)Math.Floor(z);
            var fx = x - ix;
            var fz = z - iz;
            var sx = Smooth01(fx);
            var sz = Smooth01(fz);
            var a = HashSigned(ix, iz, octave);
            var b = HashSigned(ix + 1, iz, octave);
            var c = HashSigned(ix, iz + 1, octave);
            var d = HashSigned(ix + 1, iz + 1, octave);
            return Lerp(Lerp(a, b, sx), Lerp(c, d, sx), sz);
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

        private float HashUnit(int x, int octave) => (HashSigned(x, octave, 91) + 1f) * 0.5f;

        private static float RadialInfluence(WorldMapPoint point, WorldMapPoint center, float radius,
            float innerScale, float outerScale)
        {
            var dx = point.X - center.X;
            var dz = point.Z - center.Z;
            var normalized = (float)Math.Sqrt(dx * dx + dz * dz) / radius;
            if (normalized <= innerScale) return 1f;
            if (normalized >= outerScale) return 0f;
            return Smooth01(1f - (normalized - innerScale) / Math.Max(0.0001f, outerScale - innerScale));
        }

        private static float ShapeInfluence(WorldMapShape shape, WorldMapPoint point, float innerScale)
        {
            if (shape.Kind != WorldMapShapeKind.Ellipse) return shape.Contains(point) ? 1f : 0f;
            var dx = (point.X - shape.Center.X) / shape.RadiusX;
            var dz = (point.Z - shape.Center.Z) / shape.RadiusZ;
            var normalized = (float)Math.Sqrt(dx * dx + dz * dz);
            if (normalized <= innerScale) return 1f;
            if (normalized >= 1f) return 0f;
            return Smooth01(1f - (normalized - innerScale) / (1f - innerScale));
        }

        private static float DistanceToSpline(WorldMapPoint point, WorldMapSpline spline,
            out WorldMapPoint closest)
        {
            var bestSquared = float.MaxValue;
            closest = spline.Points[0].Position;
            for (var index = 1; index < spline.Points.Count; index++)
            {
                var a = spline.Points[index - 1].Position;
                var b = spline.Points[index].Position;
                var abX = b.X - a.X;
                var abZ = b.Z - a.Z;
                var lengthSquared = abX * abX + abZ * abZ;
                var t = lengthSquared <= 0f ? 0f :
                    ((point.X - a.X) * abX + (point.Z - a.Z) * abZ) / lengthSquared;
                t = Math.Max(0f, Math.Min(1f, t));
                var candidate = new WorldMapPoint(a.X + abX * t, a.Z + abZ * t);
                var dx = point.X - candidate.X;
                var dz = point.Z - candidate.Z;
                var squared = dx * dx + dz * dz;
                if (squared >= bestSquared) continue;
                bestSquared = squared;
                closest = candidate;
            }
            return (float)Math.Sqrt(bestSquared);
        }

        private static float NormalizedDistance(WorldZoneDefinition zone, float x, float z)
        {
            var dx = (x - zone.Center.X) / zone.RadiusX;
            var dz = (z - zone.Center.Z) / zone.RadiusZ;
            return zone.IsEllipse ? (float)Math.Sqrt(dx * dx + dz * dz) : Math.Max(Math.Abs(dx), Math.Abs(dz));
        }

        private static float Smooth01(float value)
        {
            value = Math.Max(0f, Math.Min(1f, value));
            return value * value * (3f - 2f * value);
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public sealed class WorldMapSlopeHistogram
    {
        public WorldMapSlopeHistogram(int sampleCount, int slopeUnder12Count, int buildableSlopeCount,
            int slopeOver32Count, WorldMapSlopeTargets targets)
        {
            if (sampleCount <= 0 || slopeUnder12Count < 0 || buildableSlopeCount < 0 || slopeOver32Count < 0 ||
                slopeUnder12Count > sampleCount || buildableSlopeCount > sampleCount || slopeOver32Count > sampleCount)
                throw new ArgumentOutOfRangeException(nameof(sampleCount));
            SampleCount = sampleCount;
            SlopeUnder12Percent = 100f * slopeUnder12Count / sampleCount;
            BuildableSlopePercent = 100f * buildableSlopeCount / sampleCount;
            SlopeOver32Percent = 100f * slopeOver32Count / sampleCount;
            Targets = targets ?? throw new ArgumentNullException(nameof(targets));
        }

        public int SampleCount { get; }
        public float SlopeUnder12Percent { get; }
        public float BuildableSlopePercent { get; }
        public float SlopeOver32Percent { get; }
        public WorldMapSlopeTargets Targets { get; }
        public bool MeetsBudget => BuildableSlopePercent >= Targets.BuildablePercentMinimum &&
                                   SlopeUnder12Percent >= Targets.SlopeUnder12PercentMinimum &&
                                   SlopeUnder12Percent <= Targets.SlopeUnder12PercentMaximum &&
                                   SlopeOver32Percent <= Targets.SlopeOver32PercentMaximum;
    }

    public sealed class TiledWorldStreamingPlan
    {
        public TiledWorldStreamingPlan(IEnumerable<WorldTileId> visualTiles,
            IEnumerable<WorldTileId> collisionTiles)
        {
            VisualTiles = Array.AsReadOnly((visualTiles ?? throw new ArgumentNullException(nameof(visualTiles)))
                .Distinct().OrderBy(value => value).ToArray());
            CollisionTiles = Array.AsReadOnly((collisionTiles ?? throw new ArgumentNullException(nameof(collisionTiles)))
                .Distinct().OrderBy(value => value).ToArray());
            if (VisualTiles.Any(value => !CollisionTiles.Contains(value)))
                throw new ArgumentException("Every visual tile must retain collision.", nameof(collisionTiles));
        }

        public IReadOnlyList<WorldTileId> VisualTiles { get; }
        public IReadOnlyList<WorldTileId> CollisionTiles { get; }
    }

    /// <summary>Bounded visual set plus actor and command-route collision retention.</summary>
    public sealed class TiledWorldStreamingPlanner
    {
        private readonly TiledWorldDefinition definition;

        public TiledWorldStreamingPlanner(TiledWorldDefinition worldDefinition)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
        }

        public TiledWorldStreamingPlan CreatePlan(WorldPosition cameraFocus,
            IEnumerable<WorldPosition> physicalActors,
            IEnumerable<Tuple<WorldPosition, WorldPosition>> navigationRoutes,
            int visualRadius = 1, int actorRadius = 1, int routePadding = 0)
        {
            var visual = Around(cameraFocus, visualRadius);
            var collision = new SortedSet<WorldTileId>(visual);
            foreach (var actor in physicalActors ?? throw new ArgumentNullException(nameof(physicalActors)))
                foreach (var tile in Around(actor, actorRadius)) collision.Add(tile);
            foreach (var route in navigationRoutes ?? throw new ArgumentNullException(nameof(navigationRoutes)))
            {
                if (route == null) continue;
                foreach (var tile in NavigationCorridor(route.Item1, route.Item2, routePadding)) collision.Add(tile);
            }
            return new TiledWorldStreamingPlan(visual, collision);
        }

        public IReadOnlyList<WorldTileId> NavigationCorridor(WorldPosition start, WorldPosition destination,
            int padding = 0)
        {
            if (padding < 0 || padding > 2) throw new ArgumentOutOfRangeException(nameof(padding));
            if (!definition.TryGetTile(start.X, start.Z, out var from) ||
                !definition.TryGetTile(destination.X, destination.Z, out var to))
                return Array.Empty<WorldTileId>();
            var result = new SortedSet<WorldTileId>();
            var x = from.X;
            var z = from.Z;
            var dx = Math.Abs(to.X - from.X);
            var dz = Math.Abs(to.Z - from.Z);
            var sx = from.X < to.X ? 1 : -1;
            var sz = from.Z < to.Z ? 1 : -1;
            var error = dx - dz;
            while (true)
            {
                AddWithPadding(result, new WorldTileId(x, z), padding);
                if (x == to.X && z == to.Z) break;
                var twice = error * 2;
                if (twice > -dz) { error -= dz; x += sx; }
                if (twice < dx) { error += dx; z += sz; }
            }
            return result.ToArray();
        }

        private IReadOnlyList<WorldTileId> Around(WorldPosition position, int radius)
        {
            if (radius < 0 || radius > 2) throw new ArgumentOutOfRangeException(nameof(radius));
            if (!definition.TryGetTile(position.X, position.Z, out var center)) return Array.Empty<WorldTileId>();
            var result = new SortedSet<WorldTileId>();
            AddWithPadding(result, center, radius);
            return result.ToArray();
        }

        private void AddWithPadding(ISet<WorldTileId> result, WorldTileId center, int padding)
        {
            for (var z = center.Z - padding; z <= center.Z + padding; z++)
            for (var x = center.X - padding; x <= center.X + padding; x++)
            {
                var tile = new WorldTileId(x, z);
                if (definition.IsValid(tile)) result.Add(tile);
            }
        }
    }
}
