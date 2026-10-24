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
        private readonly WorldMapHydrologyDefinition authoredHydrology;

        public TiledWorldHeightSampler(TiledWorldDefinition worldDefinition, long worldSeed)
        {
            definition = worldDefinition ?? throw new ArgumentNullException(nameof(worldDefinition));
            seed = worldSeed;
            authoredTerrain = definition.MapData?.Terrain;
            authoredHydrology = definition.MapData?.Hydrology;
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

            height = ApplyAuthoredHydrology(height, design);

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

            height = ApplyAuthoredHydrology(height, design);
            height = ApplyPreparedExpansionCorridors(height, design, plateau);

            // Prepared dry construction corridors remain authoritative; water cannot be filled by them.
            height = ReapplyAuthoredWaterInterior(height, design);

            return Math.Max(authoredTerrain.MinimumHeightMeters,
                Math.Min(authoredTerrain.MaximumHeightMeters, height));
        }

        private float ApplyPreparedExpansionCorridors(float height, WorldMapPoint point,
            WorldMapHeightAnchor plateau)
        {
            var start = definition.MapData.StartTerritory;
            if (start == null || plateau == null) return height;

            // The authored corridors are construction-ready ground. Give the four-metre playable strip
            // a wider terrain shoulder so height samples at its edge remain gentle for every world seed.
            const float fullInfluenceRadiusMeters = 7f;
            const float shoulderRadiusMeters = 24f;
            var influence = 0f;
            foreach (var direction in start.ExpansionDirections)
            {
                var distance = direction.DistanceTo(point);
                if (distance >= shoulderRadiusMeters) continue;
                var corridorInfluence = distance <= fullInfluenceRadiusMeters
                    ? 1f
                    : Smooth01(1f - (distance - fullInfluenceRadiusMeters) /
                        (shoulderRadiusMeters - fullInfluenceRadiusMeters));
                influence = Math.Max(influence, corridorInfluence);
            }
            return Lerp(height, plateau.HeightMeters, influence);
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
            if (authoredHydrology != null) return TrySampleWaterSurface(worldX, worldZ, out _);
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

        public bool TrySampleWaterSurface(float worldX, float worldZ, out float surfaceY)
        {
            surfaceY = 0f;
            if (!definition.ContainsClosed(worldX, worldZ)) return false;
            if (authoredHydrology == null)
            {
                if (!IsLegacyWater(worldX, worldZ)) return false;
                surfaceY = WaterLevelMeters;
                return true;
            }

            var point = definition.MapData.CoordinateTransform.ToDesign(new WorldPosition(worldX, 0f, worldZ));
            foreach (var lake in authoredHydrology.Lakes)
            {
                if (!lake.Shoreline.Contains(point)) continue;
                surfaceY = lake.SurfaceLevelPolicy.Sample(0f);
                return true;
            }

            foreach (var course in EnumerateWaterCourses())
            {
                var sample = SampleSpline(point, course.Spline);
                if (sample.Distance > sample.WidthMeters * 0.5f) continue;
                surfaceY = course.SurfaceLevelPolicy.Sample(sample.Progress);
                return true;
            }
            return false;
        }

        public float WaterSurfaceLevel(string waterId, float normalizedDistance = 0f)
        {
            if (authoredHydrology == null) return WaterLevelMeters;
            if (waterId == authoredHydrology.MainRiver.Id)
                return authoredHydrology.MainRiver.SurfaceLevelPolicy.Sample(normalizedDistance);
            var tributary = authoredHydrology.Tributaries.FirstOrDefault(value => value.Id == waterId);
            if (tributary != null) return tributary.SurfaceLevelPolicy.Sample(normalizedDistance);
            var lake = authoredHydrology.Lakes.FirstOrDefault(value => value.Id == waterId);
            if (lake != null) return lake.SurfaceLevelPolicy.Sample(normalizedDistance);
            throw new ArgumentException("Unknown canonical water ID.", nameof(waterId));
        }

        public float DistanceToRiver(float worldX, float worldZ)
        {
            if (authoredHydrology != null)
            {
                var design = definition.MapData.CoordinateTransform.ToDesign(new WorldPosition(worldX, 0f, worldZ));
                return SampleSpline(design, authoredHydrology.MainRiver.Spline).Distance;
            }
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

        /// <summary>Distance from dry ground to the nearest canonical water edge; zero inside water.</summary>
        public float DistanceToWater(float worldX, float worldZ)
        {
            if (!definition.ContainsClosed(worldX, worldZ))
                throw new ArgumentOutOfRangeException(nameof(worldX));
            if (authoredHydrology == null)
            {
                var best = Math.Max(0f, DistanceToRiver(worldX, worldZ) - RiverHalfWidthMeters);
                foreach (var lake in definition.Zones.Where(value => value.Uses.HasFlag(WorldZoneUse.Fishing)))
                {
                    var dx = (worldX - lake.Center.X) / lake.RadiusX;
                    var dz = (worldZ - lake.Center.Z) / lake.RadiusZ;
                    var normalized = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (normalized <= 1f) return 0f;
                    best = Math.Min(best, (normalized - 1f) * Math.Min(lake.RadiusX, lake.RadiusZ));
                }
                return best;
            }

            var point = definition.MapData.CoordinateTransform.ToDesign(new WorldPosition(worldX, 0f, worldZ));
            var distance = float.MaxValue;
            foreach (var lake in authoredHydrology.Lakes)
            {
                if (lake.Shoreline.Contains(point)) return 0f;
                distance = Math.Min(distance, DistanceToPolygon(point, lake.Shoreline));
            }
            foreach (var course in EnumerateWaterCourses())
            {
                var sample = SampleSpline(point, course.Spline);
                distance = Math.Min(distance, Math.Max(0f, sample.Distance - sample.WidthMeters * 0.5f));
            }
            return distance;
        }

        private bool IsLegacyWater(float worldX, float worldZ)
        {
            if (DistanceToRiver(worldX, worldZ) <= RiverHalfWidthMeters) return true;
            return definition.Zones.Any(zone => zone.Uses.HasFlag(WorldZoneUse.Fishing) &&
                                                NormalizedDistance(zone, worldX, worldZ) <= 1f);
        }

        private float ApplyAuthoredHydrology(float height, WorldMapPoint point)
        {
            foreach (var lake in authoredHydrology.Lakes)
            {
                var edgeDistance = DistanceToPolygon(point, lake.Shoreline);
                if (lake.Shoreline.Contains(point))
                {
                    var depthFactor = Smooth01(Math.Min(1f, edgeDistance / Math.Max(8f,
                        lake.MaximumDepthMeters * 4f)));
                    var depth = Lerp(0.35f, lake.MaximumDepthMeters, depthFactor);
                    height = Math.Min(height, lake.SurfaceY - depth);
                }
                else if (edgeDistance < lake.MaximumBankBlendMeters)
                    height = RaiseDryBank(height, lake.SurfaceY, edgeDistance,
                        lake.MinimumBankBlendMeters, lake.MaximumBankBlendMeters);
            }

            foreach (var course in EnumerateWaterCourses())
            {
                var sample = SampleSpline(point, course.Spline);
                var halfWidth = sample.WidthMeters * 0.5f;
                var surface = course.SurfaceLevelPolicy.Sample(sample.Progress);
                if (sample.Distance <= halfWidth)
                {
                    var channel = 1f - sample.Distance / Math.Max(0.001f, halfWidth);
                    var depth = Lerp(course.MinimumDepthMeters, course.MaximumDepthMeters, Smooth01(channel));
                    height = Math.Min(height, surface - depth);
                }
                else
                {
                    var bankDistance = sample.Distance - halfWidth;
                    if (bankDistance < course.MaximumBankBlendMeters)
                        height = RaiseDryBank(height, surface, bankDistance,
                            course.MinimumBankBlendMeters, course.MaximumBankBlendMeters);
                }
            }
            return height;
        }

        private float ReapplyAuthoredWaterInterior(float height, WorldMapPoint point)
        {
            foreach (var lake in authoredHydrology.Lakes)
            {
                if (!lake.Shoreline.Contains(point)) continue;
                var edgeDistance = DistanceToPolygon(point, lake.Shoreline);
                var depthFactor = Smooth01(Math.Min(1f, edgeDistance /
                    Math.Max(8f, lake.MaximumDepthMeters * 4f)));
                height = Math.Min(height, lake.SurfaceY -
                    Lerp(0.35f, lake.MaximumDepthMeters, depthFactor));
            }
            foreach (var course in EnumerateWaterCourses())
            {
                var sample = SampleSpline(point, course.Spline);
                var halfWidth = sample.WidthMeters * 0.5f;
                if (sample.Distance > halfWidth) continue;
                var channel = 1f - sample.Distance / Math.Max(0.001f, halfWidth);
                var surface = course.SurfaceLevelPolicy.Sample(sample.Progress);
                height = Math.Min(height, surface - Lerp(course.MinimumDepthMeters,
                    course.MaximumDepthMeters, Smooth01(channel)));
            }
            return height;
        }

        private IEnumerable<WorldMapWaterCourse> EnumerateWaterCourses()
        {
            yield return authoredHydrology.MainRiver;
            foreach (var tributary in authoredHydrology.Tributaries) yield return tributary;
        }

        private static float RaiseDryBank(float height, float surface, float distance,
            float minimumBlend, float maximumBlend)
        {
            var dryTarget = surface + 1.1f + Math.Min(2.4f, distance * 0.12f);
            var inner = distance <= minimumBlend ? 1f :
                Smooth01(1f - (distance - minimumBlend) /
                    Math.Max(0.001f, maximumBlend - minimumBlend));
            return Lerp(height, dryTarget, inner);
        }

        private readonly struct SplineSample
        {
            public SplineSample(float distance, float progress, float widthMeters)
            {
                Distance = distance;
                Progress = progress;
                WidthMeters = widthMeters;
            }
            public float Distance { get; }
            public float Progress { get; }
            public float WidthMeters { get; }
        }

        private static SplineSample SampleSpline(WorldMapPoint point, WorldMapSpline spline)
        {
            var totalLength = 0f;
            for (var index = 1; index < spline.Points.Count; index++)
                totalLength += Distance(spline.Points[index - 1].Position, spline.Points[index].Position);
            var bestSquared = float.MaxValue;
            var bestProgress = 0f;
            var bestWidth = spline.Points[0].WidthMeters;
            var traversed = 0f;
            for (var index = 1; index < spline.Points.Count; index++)
            {
                var a = spline.Points[index - 1];
                var b = spline.Points[index];
                var abX = b.Position.X - a.Position.X;
                var abZ = b.Position.Z - a.Position.Z;
                var lengthSquared = abX * abX + abZ * abZ;
                var length = (float)Math.Sqrt(lengthSquared);
                var t = lengthSquared <= 0f ? 0f :
                    ((point.X - a.Position.X) * abX + (point.Z - a.Position.Z) * abZ) / lengthSquared;
                t = Math.Max(0f, Math.Min(1f, t));
                var dx = point.X - (a.Position.X + abX * t);
                var dz = point.Z - (a.Position.Z + abZ * t);
                var squared = dx * dx + dz * dz;
                if (squared < bestSquared)
                {
                    bestSquared = squared;
                    bestProgress = totalLength <= 0f ? 0f : (traversed + length * t) / totalLength;
                    bestWidth = Lerp(a.WidthMeters, b.WidthMeters, t);
                }
                traversed += length;
            }
            return new SplineSample((float)Math.Sqrt(bestSquared), bestProgress, bestWidth);
        }

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

        private static float Distance(WorldMapPoint a, WorldMapPoint b)
        {
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
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
