using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.AI;
using Game.Domain.World;
using UnityEngine;

namespace Game.Infrastructure
{
    public static class TiledWorldDefinitionLoader
    {
        private const string MapVersion = "serenity-world-map-v1";
        private const string ProposedResourceRoot = "World/WorldMapV2/";

        private static readonly string[] ProposedFiles =
        {
            "WORLD_MAP_COORDINATES", "WORLD_MAP_BIOMES", "WORLD_MAP_TERRAIN", "WORLD_MAP_WATER",
            "WORLD_MAP_VEGETATION", "WORLD_MAP_RESOURCES", "WORLD_MAP_WILDLIFE", "WORLD_MAP_ROADS",
            "WORLD_MAP_POI", "WORLD_MAP_BUILDABILITY", "WORLD_MAP_MANIFEST"
        };

        [Serializable] private sealed class LayoutData
        {
            public string generationId;
            public int columns, rows, tileSizeMeters;
            public float originX, originZ, playerStartX, playerStartZ;
            public ZoneData[] zones;
            public PointData[] river;
        }
        [Serializable] private sealed class ZoneData
        {
            public string id, uses;
            public float x, z, radiusX, radiusZ;
            public bool ellipse;
        }
        [Serializable] private sealed class PointData { public float x, z; }

        /// <summary>The active compact-world contract. WM01 deliberately leaves this route unchanged.</summary>
        public static TiledWorldDefinition LoadReference()
        {
            var asset = UnityEngine.Resources.Load<TextAsset>("World/P109TiledWorld");
            if (asset == null) throw new InvalidOperationException("The tiled-world definition is missing.");
            return Parse(asset.text);
        }

        /// <summary>Loads and validates the isolated V2 design package without activating it.</summary>
        public static TiledWorldDefinition LoadProposedWorldMap()
        {
            var documents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in ProposedFiles)
            {
                var asset = UnityEngine.Resources.Load<TextAsset>(ProposedResourceRoot + file);
                if (asset == null) throw new InvalidOperationException($"World-map package file {file}.json is missing.");
                documents.Add(file + ".json", asset.text);
            }
            return ParseWorldMapPackage(documents);
        }

        /// <summary>Legacy compact parser retained for p109-compact-world-v1 callers and saves.</summary>
        public static TiledWorldDefinition Parse(string json)
        {
            var data = JsonUtility.FromJson<LayoutData>(json);
            if (data == null || data.generationId != TiledWorldDefinition.ReferenceGenerationId ||
                data.columns != 4 || data.rows != 4 || data.tileSizeMeters != 250 || data.zones == null || data.river == null)
                throw new InvalidOperationException("P10.9 requires the versioned 1000 x 1000 m world (4 x 4 tiles).");
            try
            {
                return new TiledWorldDefinition(data.generationId, data.columns, data.rows, data.tileSizeMeters,
                    data.originX, data.originZ, data.zones.Select(zone => new WorldZoneDefinition(zone.id,
                        (WorldZoneUse)Enum.Parse(typeof(WorldZoneUse), zone.uses, false), zone.x, zone.z,
                        zone.radiusX, zone.radiusZ, zone.ellipse)),
                    data.river.Select(point => new WorldPosition(point.x, 0f, point.z)),
                    new WorldPosition(data.playerStartX, 0f, data.playerStartZ));
            }
            catch (Exception exception) when (!(exception is InvalidOperationException))
            {
                throw new InvalidOperationException("The compact-world definition is invalid.", exception);
            }
        }

        public static TiledWorldDefinition ParseWorldMapPackage(IReadOnlyDictionary<string, string> jsonDocuments)
        {
            if (jsonDocuments == null) throw new ArgumentNullException(nameof(jsonDocuments));
            try
            {
                var roots = new Dictionary<string, Node>(StringComparer.Ordinal);
                foreach (var file in ProposedFiles.Select(name => name + ".json"))
                {
                    if (!jsonDocuments.TryGetValue(file, out var json) || string.IsNullOrWhiteSpace(json))
                        throw new InvalidDataException($"Required world-map file {file} is missing.");
                    roots.Add(file, new Node(StrictJsonReader.ParseObject(json, file), file));
                }

                ValidateHeaders(roots);
                ValidateManifest(roots["WORLD_MAP_MANIFEST.json"]);

                var coordinates = roots["WORLD_MAP_COORDINATES.json"];
                var boundsData = coordinates.Object("designCoordinateSystem").Object("bounds");
                var bounds = new WorldMapBounds(boundsData.Float("xMin"), boundsData.Float("xMax"),
                    boundsData.Float("zMin"), boundsData.Float("zMax"));
                if (Math.Abs(bounds.Width * bounds.Depth - boundsData.Float("areaSquareMeters")) > 0.01f)
                    throw new InvalidDataException("Coordinate area does not match its bounds.");

                var projection = coordinates.Object("unityProjection");
                var runtimeOrigin = projection.Point3("currentCompactRuntimeOrigin");
                var transform = new WorldMapCoordinateTransform(bounds, runtimeOrigin.X, runtimeOrigin.Z);
                ValidateProjectionFormula(projection.Object("formula"));

                var tiling = coordinates.Object("tiling");
                var columns = tiling.Int("columns");
                var rows = tiling.Int("rows");
                var tileSize = tiling.Int("tileSizeMeters");
                if (columns != 4 || rows != 4 || tileSize != 250 ||
                    Math.Abs(columns * tileSize - bounds.Width) > 0.001f ||
                    Math.Abs(rows * tileSize - bounds.Depth) > 0.001f)
                    throw new InvalidDataException("WM01 requires the authored 4 x 4 x 250 m tiling.");
                ValidateTiles(tiling.Array("tiles"), transform, columns, rows, tileSize);
                ValidateCanonicalFileIndex(coordinates.StringArray("canonicalFiles"));

                var biomes = ParseBiomeContracts(roots["WORLD_MAP_BIOMES.json"], bounds);
                var regions = biomes.Regions.Select(value => value.Polygon).ToArray();
                ValidateCoordinateRegionIndex(coordinates.Array("regionPolygons"), regions, bounds);
                var terrainDefinition = ParseTerrainContracts(roots["WORLD_MAP_TERRAIN.json"], bounds);
                var heightMasks = terrainDefinition.HeightMasks;

                var water = roots["WORLD_MAP_WATER.json"];
                var lakeMask = heightMasks.Single(value => value.Operation == "carve_polygons");
                var river = ParseRiver(water.Object("river"), bounds, terrainDefinition.WaterLevelMeters);
                var lakes = ParseLakes(water.Array("lakes"), bounds,
                    lakeMask.MinimumValue, lakeMask.MaximumValue);
                var waterIds = new HashSet<string>(lakes.Select(value => value.Id), StringComparer.Ordinal) { river.Id };
                var tributaries = ParseTributaries(water.Array("tributaries"), bounds, waterIds,
                    river, lakes);
                var hydrology = new WorldMapHydrologyDefinition(terrainDefinition.WaterLevelMeters,
                    river, tributaries, lakes);
                ValidateWaterIndex(coordinates.Object("waterIndex"), river, lakes);
                ValidateWaterRules(water);

                var roads = roots["WORLD_MAP_ROADS.json"];
                var routes = ParseRoutes(roads.Array("routes"), bounds);
                ValidateRouteIndex(coordinates.Array("routeIndex"), routes, bounds);
                var crossings = ParseCrossings(roads.Array("crossings"), bounds, routes);
                ValidateRoadRules(roads, bounds);

                var poiRoot = roots["WORLD_MAP_POI.json"];
                var poi = ParsePointsOfInterest(poiRoot.Array("points"), bounds, regions);
                ValidatePoiIndex(coordinates.Array("poiIndex"), poi);
                ValidatePoiRules(poiRoot.Object("rules"));

                var resources = roots["WORLD_MAP_RESOURCES.json"];
                var resourceDefinition = ParseResourceDefinition(resources, bounds);
                var resourceTypes = UniqueStrings(resourceDefinition.Types.Select(value => value.Id), "resource type");
                var resourceMasks = resourceDefinition.Zones.Select(zone => new WorldMapMask(zone.Id, "resources",
                    zone.Shape, zone.ResourceIds, zone.Density + ":" + zone.Access)).ToArray();
                ValidateFishSources(resources, lakes, river.Spline);
                ValidateRequiredTrueFlags(resources.Object("mutationAndSaveRules"), "resource mutation",
                    "stableEntityIdRequired", "depletedVisibleNodeMustDisappear", "remainingCapacitySaved",
                    "regrowthUsesCanonicalState", "buildingClearanceUsesNormalWorkAndHauling",
                    "noAutomaticDeletionForUniqueOrUnclearableObjects");

                var wildlife = roots["WORLD_MAP_WILDLIFE.json"];
                var species = UniqueStrings(wildlife.Array("species").Select(value => value.Object().String("id")), "species");
                var wildlifeMasks = ParseWildlifeMasks(wildlife.Array("concentrationZones"), bounds, species);
                ValidateWildlifeSimulation(wildlife.Object("simulation"));
                var vegetation = ParseVegetationDefinition(roots["WORLD_MAP_VEGETATION.json"], bounds);
                var vegetationMasks = vegetation.Zones.Select(zone => new WorldMapMask(zone.Id, "vegetation",
                    zone.Shape, new[] { zone.ClassId }, "density", zone.DensityMultiplier)).ToArray();
                var buildability = roots["WORLD_MAP_BUILDABILITY.json"];
                var buildabilityMasks = ParseBuildabilityMasks(buildability.Array("zones"), bounds);
                ValidateBuildabilityRules(buildability, bounds);
                ValidateGlobalIds(regions, river.Spline, tributaries.Select(value => value.Spline).ToArray(),
                    lakes, routes, crossings, poi, resourceMasks,
                    wildlifeMasks, buildabilityMasks, vegetationMasks, heightMasks, resourceTypes, species);

                var playerStartData = coordinates.Object("playerStart");
                var designStart = playerStartData.Point2("designPosition");
                var authoredRuntimeStart = playerStartData.Point3("runtimePosition");
                var runtimeStart = transform.ToUnity(designStart);
                if (!Same(runtimeStart.X, authoredRuntimeStart.X) || !Same(runtimeStart.Z, authoredRuntimeStart.Z))
                    throw new InvalidDataException("Player-start runtime position does not match the coordinate transform.");
                var startRegionId = playerStartData.String("regionId");
                var startRegion = regions.SingleOrDefault(value => value.Id == startRegionId);
                if (startRegion == null || !startRegion.Contains(designStart))
                    throw new InvalidDataException("Player start must be contained by its referenced region.");

                var mapData = new WorldMapDataDefinition(1, MapVersion,
                    TiledWorldDefinition.ProposedWorldMapGenerationId, transform, terrainDefinition, hydrology,
                    biomes, vegetation, resourceDefinition, river.Spline, tributaries.Select(value => value.Spline),
                    lakes, routes, poi, crossings, resourceMasks, wildlifeMasks, buildabilityMasks,
                    vegetationMasks, heightMasks);
                var unityRiver = river.Spline.Points.Select(point => transform.ToUnity(point.Position));
                return new TiledWorldDefinition(mapData.GenerationId, columns, rows, tileSize,
                    runtimeOrigin.X, runtimeOrigin.Z, Array.Empty<WorldZoneDefinition>(), unityRiver,
                    runtimeStart, mapData);
            }
            catch (Exception exception) when (!(exception is InvalidDataException))
            {
                throw new InvalidDataException("World-map package validation failed.", exception);
            }
        }

        private static void ValidateHeaders(IReadOnlyDictionary<string, Node> roots)
        {
            var expectedLayers = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WORLD_MAP_COORDINATES.json"] = "coordinate_index",
                ["WORLD_MAP_BIOMES.json"] = "biomes_and_regions",
                ["WORLD_MAP_TERRAIN.json"] = "terrain",
                ["WORLD_MAP_WATER.json"] = "water",
                ["WORLD_MAP_VEGETATION.json"] = "vegetation",
                ["WORLD_MAP_RESOURCES.json"] = "resources",
                ["WORLD_MAP_WILDLIFE.json"] = "wildlife",
                ["WORLD_MAP_ROADS.json"] = "roads_and_navigation",
                ["WORLD_MAP_POI.json"] = "points_of_interest",
                ["WORLD_MAP_BUILDABILITY.json"] = "buildability",
                ["WORLD_MAP_MANIFEST.json"] = "manifest"
            };
            foreach (var pair in roots)
            {
                var root = pair.Value;
                if (root.Int("schemaVersion") != 1 || root.String("mapVersion") != MapVersion ||
                    root.String("implementationGenerationId") != TiledWorldDefinition.ProposedWorldMapGenerationId ||
                    root.String("status") != "design_only_not_activated" || root.String("units") != "meters" ||
                    root.String("north") != "+Z" || root.String("layer") != expectedLayers[pair.Key])
                    throw new InvalidDataException($"{pair.Key} has an incompatible world-map header.");
            }
        }

        private static void ValidateManifest(Node manifest)
        {
            var entries = manifest.Array("files").Select(value => value.Object()).ToArray();
            var paths = UniqueStrings(entries.Select(value => value.String("path")), "manifest path");
            foreach (var required in ProposedFiles.Where(name => name != "WORLD_MAP_MANIFEST")
                         .Select(name => name + ".json"))
                if (!paths.Contains(required)) throw new InvalidDataException($"Manifest omits {required}.");
            foreach (var entry in entries)
            {
                if (entry.Int("bytes") <= 0) throw new InvalidDataException("Manifest byte counts must be positive.");
                var hash = entry.String("sha256");
                if (hash.Length != 64 || hash.Any(value => !Uri.IsHexDigit(value)))
                    throw new InvalidDataException("Manifest SHA-256 values must be 64 hexadecimal characters.");
            }
        }

        private static void ValidateProjectionFormula(Node formula)
        {
            if (formula.String("unityX") != "designX - 570" || formula.String("unityZ") != "designZ - 120" ||
                formula.String("designX") != "unityX + 570" || formula.String("designZ") != "unityZ + 120")
                throw new InvalidDataException("Unsupported design/Unity coordinate formula.");
        }

        private static void ValidateCanonicalFileIndex(IReadOnlyList<string> files)
        {
            var expected = new[]
            {
                "WORLD_MAP_BIOMES.json", "WORLD_MAP_TERRAIN.json", "WORLD_MAP_WATER.json",
                "WORLD_MAP_VEGETATION.json", "WORLD_MAP_RESOURCES.json", "WORLD_MAP_WILDLIFE.json",
                "WORLD_MAP_ROADS.json", "WORLD_MAP_POI.json", "WORLD_MAP_BUILDABILITY.json"
            };
            if (!SetEquals(files, expected) || files.Count != expected.Length)
                throw new InvalidDataException("Coordinate index must list every canonical layer exactly once.");
        }

        private static void ValidateTiles(IReadOnlyList<Node> tiles, WorldMapCoordinateTransform transform,
            int columns, int rows, int tileSize)
        {
            if (tiles.Count != columns * rows) throw new InvalidDataException("Tile index must cover the complete grid.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in tiles)
            {
                var tile = value.Object();
                var index = tile.NumberArray("index", 2);
                var x = RequireWhole(index[0], "tile X");
                var z = RequireWhole(index[1], "tile Z");
                if (x < 0 || x >= columns || z < 0 || z >= rows || !seen.Add(x + ":" + z))
                    throw new InvalidDataException("Tile indices must be unique and in bounds.");
                var origin = tile.Point3("runtimeOrigin");
                var expected = transform.ToUnity(new WorldMapPoint(x * tileSize, z * tileSize));
                if (!Same(origin.X, expected.X) || !Same(origin.Z, expected.Z))
                    throw new InvalidDataException($"Tile {tile.String("id")} has an invalid runtime origin.");
            }
        }

        private static WorldMapBiomeDefinition ParseBiomeContracts(Node biomes, WorldMapBounds bounds)
        {
            var regions = biomes.Array("regions").Select(value =>
            {
                var region = value.Object();
                var elevation = region.NumberArray("elevationMeters", 2);
                if (elevation[1] < elevation[0] || region.StringArray("biomes").Count == 0 ||
                    region.StringArray("soil").Count == 0 || region.StringArray("gameplay").Count == 0)
                    throw new InvalidDataException($"Region {region.String("id")} has invalid authored ranges.");
                return new WorldMapBiomeRegion(
                    new WorldMapPolygon(region.String("id"), region.Points("polygon"), bounds),
                    region.String("code"), region.String("name"), region.String("nameEn"),
                    elevation[0], elevation[1], region.StringArray("biomes"), region.StringArray("soil"),
                    region.StringArray("gameplay"));
            }).ToArray();
            var transition = biomes.Object("transitionRules");
            var preferred = transition.NumberArray("preferredBlendWidthMeters", 2);
            if (transition.Float("minimumBlendWidthMeters") <= 0f || preferred[0] <= 0f ||
                preferred[1] < preferred[0] || !transition.Bool("noRectangularVisualEdges") ||
                transition.StringArray("ecotoneLayers").Count == 0)
                throw new InvalidDataException("Biome transition rules are invalid.");
            return new WorldMapBiomeDefinition(regions, new WorldMapBiomeTransitionDefinition(
                transition.Float("minimumBlendWidthMeters"), preferred[0], preferred[1],
                transition.Bool("noRectangularVisualEdges"), transition.StringArray("ecotoneLayers")));
        }

        private static WorldMapTerrainDefinition ParseTerrainContracts(Node terrain,
            WorldMapBounds bounds)
        {
            var heightRange = terrain.NumberArray("heightRangeMeters", 2);
            var walkable = terrain.NumberArray("walkableLandTypicalMeters", 2);
            if (heightRange[1] <= heightRange[0] || walkable[1] < walkable[0] ||
                terrain.Float("waterLevelReferenceMeters") < heightRange[0] ||
                terrain.Float("ridgePeakMeters") > heightRange[1])
                throw new InvalidDataException("Terrain height ranges are invalid.");
            var heightmap = terrain.Object("heightmap");
            var tileCounts = heightmap.NumberArray("tiles", 2);
            if (heightmap.Int("worldResolution") != 1025 || heightmap.Int("tileResolution") != 257 ||
                tileCounts[0] != 4f || tileCounts[1] != 4f || !heightmap.Bool("sharedBorderSamples"))
                throw new InvalidDataException("Terrain heightmap contract is incompatible.");

            var anchors = terrain.Array("heightAnchors").Select(value =>
            {
                var anchor = value.Object();
                var mode = anchor.String("mode");
                if (mode != "flatten_blend" && mode != "raise" && mode != "lower_pass" &&
                    mode != "rolling" && mode != "gentle" && mode != "lower")
                    throw new InvalidDataException($"Unsupported terrain anchor mode {mode}.");
                if (!bounds.Contains(anchor.Point2("position")) || anchor.Float("radiusMeters") <= 0f ||
                    !WorldMapPoint.IsFinite(anchor.Float("heightMeters")))
                    throw new InvalidDataException("Terrain height anchor is invalid.");
                return new WorldMapHeightAnchor(anchor.String("id"), anchor.Point2("position"),
                    anchor.Float("heightMeters"), anchor.Float("radiusMeters"), mode);
            }).ToArray();
            UniqueStrings(anchors.Select(value => value.Id), "height anchor");

            var noise = terrain.Array("noise").Select(value =>
            {
                var octave = value.Object();
                var wavelength = octave.NumberArray("wavelengthMeters", 2);
                var amplitude = octave.NumberArray("amplitudeMeters", 2);
                var currentWeight = octave.Float("weight");
                if (wavelength[0] <= 0f || wavelength[1] < wavelength[0] || amplitude[0] < 0f ||
                    amplitude[1] < amplitude[0] || currentWeight <= 0f)
                    throw new InvalidDataException("Terrain noise octave is invalid.");
                return new WorldMapNoiseOctave(octave.String("id"), wavelength[0], wavelength[1],
                    amplitude[0], amplitude[1], currentWeight);
            }).ToArray();
            UniqueStrings(noise.Select(value => value.Id), "noise octave");
            var weight = noise.Sum(value => value.Weight);
            if (Math.Abs(weight - 1f) > 0.0001f) throw new InvalidDataException("Terrain noise weights must total one.");

            var result = terrain.Array("manualMasks").Select(value =>
            {
                var mask = value.Object();
                var id = mask.String("id");
                var operation = mask.String("operation");
                if (operation != "flatten_blend" && operation != "carve_spline" &&
                    operation != "carve_polygons" && operation != "ridge_spline_plus_erosion" &&
                    operation != "flatten_low")
                    throw new InvalidDataException($"Unsupported terrain mask operation {operation}.");
                WorldMapShape shape = null;
                WorldMapSpline spline = null;
                var source = string.Empty;
                if (mask.Has("center"))
                {
                    var radius = mask.Float("radiusMeters");
                    shape = WorldMapShape.Ellipse(mask.Point2("center"), radius, radius, bounds);
                }
                if (mask.Has("points"))
                    spline = new WorldMapSpline(id + ":spline", mask.Points("points")
                        .Select(point => new WorldMapSplinePoint(point, 1f)), bounds);
                if (mask.Has("source")) source = mask.String("source");
                var range = mask.Has("targetHeightMeters") ? mask.Range("targetHeightMeters") :
                    mask.Has("bankBlendMeters") ? mask.Range("bankBlendMeters") :
                    mask.Has("talusAngleDegrees") ? mask.Range("talusAngleDegrees") :
                    mask.Has("maximumSlopeDegrees") ? mask.Range("maximumSlopeDegrees") : new[] { 0f, 0f };
                return new WorldMapHeightMask(id, operation, shape, spline, source, range[0], range[1]);
            }).ToArray();
            UniqueStrings(result.Select(value => value.Id), "height mask");

            var slopes = terrain.Object("slopeTargets");
            var under = slopes.NumberArray("slopeUnder12DegreesPercent", 2);
            if (slopes.Float("buildablePercentMinimum") <= 0f || under[0] < 0f || under[1] > 100f ||
                under[1] < under[0] || slopes.Float("slopeOver32DegreesPercentMaximum") < 0f)
                throw new InvalidDataException("Terrain slope targets are invalid.");
            return new WorldMapTerrainDefinition(heightRange[0], heightRange[1],
                terrain.Float("waterLevelReferenceMeters"), walkable[0], walkable[1],
                terrain.Float("ridgePeakMeters"), heightmap.Int("worldResolution"),
                heightmap.Int("tileResolution"), heightmap.Bool("sharedBorderSamples"), anchors, noise, result,
                new WorldMapSlopeTargets(slopes.Float("buildablePercentMinimum"), under[0], under[1],
                    slopes.Float("slopeOver32DegreesPercentMaximum")));
        }

        private static void ValidateCoordinateRegionIndex(IReadOnlyList<Node> index,
            IReadOnlyList<WorldMapPolygon> regions, WorldMapBounds bounds)
        {
            if (index.Count != regions.Count) throw new InvalidDataException("Region index count does not match biomes.");
            foreach (var value in index)
            {
                var item = value.Object();
                var indexed = new WorldMapPolygon(item.String("id"), item.Points("polygon"), bounds);
                var region = regions.SingleOrDefault(candidate => candidate.Id == indexed.Id);
                if (region == null || !SamePoints(region.Points, indexed.Points))
                    throw new InvalidDataException($"Region index differs for {indexed.Id}.");
            }
        }

        private static WorldMapWaterCourse ParseRiver(Node data, WorldMapBounds bounds,
            float referenceWaterLevelMeters)
        {
            var points = data.Array("centerline").Select(value =>
            {
                var point = value.Object();
                return new WorldMapSplinePoint(new WorldMapPoint(point.Float("x"), point.Float("z")),
                    point.Float("widthMeters"));
            }).ToArray();
            var river = new WorldMapSpline(data.String("id"), points, bounds);
            if (!TouchesBoundary(river.Points.First().Position, bounds) || !TouchesBoundary(river.Points.Last().Position, bounds))
                throw new InvalidDataException("The main river must connect two map boundaries.");
            var depth = data.NumberArray("depthMeters", 2);
            var bank = data.NumberArray("bankBlendMeters", 2);
            return new WorldMapWaterCourse(river, depth[0], depth[1], bank[0], bank[1],
                Array.Empty<string>(), new WorldMapWaterLevelPolicy(WorldMapWaterLevelPolicyKind.Reference,
                    referenceWaterLevelMeters, referenceWaterLevelMeters));
        }

        private static IReadOnlyList<WorldMapWaterBody> ParseLakes(IReadOnlyList<Node> values,
            WorldMapBounds bounds, float minimumBankBlendMeters, float maximumBankBlendMeters)
        {
            var result = values.Select(value =>
            {
                var lake = value.Object();
                var id = lake.String("id");
                var polygon = new WorldMapPolygon(id + ":shoreline", lake.Points("polygon"), bounds);
                var landings = lake.Points("fishingLandings").ToArray();
                if (landings.Length < 3 || landings.Any(point => !bounds.Contains(point) || polygon.Contains(point)))
                    throw new InvalidDataException($"Fishing landings for {id} must be dry, bounded authored points.");
                var center = lake.Point2("center");
                if (!polygon.Contains(center)) throw new InvalidDataException($"Lake center for {id} must be inside its shoreline.");
                return new WorldMapWaterBody(id, center, polygon, lake.Float("surfaceY"),
                    lake.Float("maxDepthMeters"), minimumBankBlendMeters, maximumBankBlendMeters,
                    lake.String("shore"), landings);
            }).ToArray();
            if (result.Length != 4) throw new InvalidDataException("The V2 contract requires exactly four lakes.");
            UniqueStrings(result.Select(value => value.Id), "lake");
            return result;
        }

        private static IReadOnlyList<WorldMapWaterCourse> ParseTributaries(IReadOnlyList<Node> values,
            WorldMapBounds bounds, ISet<string> waterIds, WorldMapWaterCourse river,
            IReadOnlyList<WorldMapWaterBody> lakes)
        {
            var result = new List<WorldMapWaterCourse>();
            foreach (var value in values)
            {
                var stream = value.Object();
                var widths = stream.NumberArray("widthMeters", 2);
                if (widths[0] <= 0 || widths[1] < widths[0]) throw new InvalidDataException("Invalid stream width range.");
                var connections = stream.StringArray("connects");
                if (connections.Count != 2) throw new InvalidDataException("A tributary must have two water connections.");
                foreach (var connection in connections)
                    if (!waterIds.Contains(connection)) throw new InvalidDataException($"Unknown water connection {connection}.");
                var positions = stream.Points("points");
                var points = positions.Select((point, index) => new WorldMapSplinePoint(point,
                    widths[0] + (widths[1] - widths[0]) * index / Math.Max(1f, positions.Count - 1f)));
                var spline = new WorldMapSpline(stream.String("id"), points, bounds, widths[0], widths[1]);
                var upstream = WaterLevel(connections[0], river, lakes);
                var downstream = WaterLevel(connections[1], river, lakes);
                if (downstream > upstream + 0.001f)
                    throw new InvalidDataException($"Tributary {spline.Id} flows uphill.");
                ValidateConnectionPoint(spline.Points.First().Position, connections[0], river, lakes,
                    spline.MaximumWidthMeters);
                ValidateConnectionPoint(spline.Points.Last().Position, connections[1], river, lakes,
                    spline.MaximumWidthMeters);
                var maximumDepth = Math.Max(0.45f, spline.MaximumWidthMeters * 0.28f);
                result.Add(new WorldMapWaterCourse(spline, Math.Min(0.35f, maximumDepth), maximumDepth,
                    Math.Max(4f, spline.MinimumWidthMeters), Math.Max(8f, spline.MaximumWidthMeters * 2f),
                    connections, new WorldMapWaterLevelPolicy(WorldMapWaterLevelPolicyKind.LinearDrainage,
                        upstream, downstream)));
            }
            UniqueStrings(result.Select(value => value.Id), "tributary");
            return result;
        }

        private static float WaterLevel(string id, WorldMapWaterCourse river,
            IReadOnlyList<WorldMapWaterBody> lakes) => id == river.Id
            ? river.SurfaceLevelPolicy.Sample(0f)
            : lakes.Single(value => value.Id == id).SurfaceY;

        private static void ValidateConnectionPoint(WorldMapPoint point, string id, WorldMapWaterCourse river,
            IReadOnlyList<WorldMapWaterBody> lakes, float toleranceMeters)
        {
            if (id == river.Id)
            {
                if (DistanceToSpline(point, river.Spline) <= river.Spline.MaximumWidthMeters * 0.5f + toleranceMeters)
                    return;
            }
            else
            {
                var lake = lakes.Single(value => value.Id == id);
                if (lake.Shoreline.Contains(point) || DistanceToPolygon(point, lake.Shoreline) <= toleranceMeters)
                    return;
            }
            throw new InvalidDataException($"Water connection {id} is not spatially joined to its tributary.");
        }

        private static float DistanceToSpline(WorldMapPoint point, WorldMapSpline spline)
        {
            var best = float.MaxValue;
            for (var index = 1; index < spline.Points.Count; index++)
                best = Math.Min(best, DistanceToSegment(point, spline.Points[index - 1].Position,
                    spline.Points[index].Position));
            return best;
        }

        private static float DistanceToPolygon(WorldMapPoint point, WorldMapPolygon polygon)
        {
            var best = float.MaxValue;
            for (var index = 0; index < polygon.Points.Count; index++)
                best = Math.Min(best, DistanceToSegment(point, polygon.Points[index],
                    polygon.Points[(index + 1) % polygon.Points.Count]));
            return best;
        }

        private static float DistanceToSegment(WorldMapPoint point, WorldMapPoint a, WorldMapPoint b)
        {
            var dx = b.X - a.X;
            var dz = b.Z - a.Z;
            var lengthSquared = dx * dx + dz * dz;
            var t = lengthSquared <= 0f ? 0f : ((point.X - a.X) * dx + (point.Z - a.Z) * dz) / lengthSquared;
            t = Math.Max(0f, Math.Min(1f, t));
            var offsetX = point.X - (a.X + dx * t);
            var offsetZ = point.Z - (a.Z + dz * t);
            return (float)Math.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
        }

        private static void ValidateWaterIndex(Node index, WorldMapWaterCourse river,
            IReadOnlyList<WorldMapWaterBody> lakes)
        {
            if (index.String("riverId") != river.Id ||
                !SetEquals(index.StringArray("lakeIds"), lakes.Select(value => value.Id)))
                throw new InvalidDataException("Coordinate water index does not match water definitions.");
            var indexedPoints = index.Points("riverCenterline");
            if (!SamePoints(indexedPoints, river.Spline.Points.Select(value => value.Position).ToArray()))
                throw new InvalidDataException("Coordinate river index does not match the water spline.");
        }

        private static void ValidateWaterRules(Node water)
        {
            var fish = water.Object("fishPopulation");
            if (fish.Int("schoolCountPerLake") <= 0 || fish.Int("riverSchoolCount") <= 0 ||
                fish.Int("totalInitialSchoolCount") != fish.Int("schoolCountPerLake") * 4 +
                fish.Int("riverSchoolCount") || fish.Float("minimumBankWorkPointOffsetMeters") <= 0f)
                throw new InvalidDataException("Water fish-population contract is invalid.");
            ValidateRequiredTrueFlags(water.Object("hydrologyValidation"), "hydrology",
                "allLakesConnectedBySurfaceDrainage", "riverTouchesNorthwestAndSoutheastBorders");
        }

        private static IReadOnlyList<WorldMapSpline> ParseRoutes(IReadOnlyList<Node> values, WorldMapBounds bounds)
        {
            var result = values.Select(value =>
            {
                var route = value.Object();
                var width = route.NumberArray("widthMeters", 2);
                if (width[0] <= 0 || width[1] < width[0]) throw new InvalidDataException("Invalid route width range.");
                var points = route.Points("points").Select(point =>
                    new WorldMapSplinePoint(point, (width[0] + width[1]) * 0.5f));
                return new WorldMapSpline(route.String("id"), points, bounds, width[0], width[1],
                    route.String("kind"));
            }).ToArray();
            UniqueStrings(result.Select(value => value.Id), "route");
            return result;
        }

        private static void ValidateRouteIndex(IReadOnlyList<Node> index, IReadOnlyList<WorldMapSpline> routes,
            WorldMapBounds bounds)
        {
            if (index.Count != routes.Count) throw new InvalidDataException("Route index count does not match roads.");
            foreach (var value in index)
            {
                var item = value.Object();
                var route = routes.SingleOrDefault(candidate => candidate.Id == item.String("id"));
                var points = item.Points("points");
                if (route == null || points.Any(point => !bounds.Contains(point)) ||
                    !SamePoints(points, route.Points.Select(point => point.Position).ToArray()))
                    throw new InvalidDataException("Coordinate route index differs from roads.");
            }
        }

        private static IReadOnlyList<WorldMapCrossing> ParseCrossings(IReadOnlyList<Node> values,
            WorldMapBounds bounds, IReadOnlyList<WorldMapSpline> routes)
        {
            var routeIds = new HashSet<string>(routes.Select(value => value.Id), StringComparer.Ordinal);
            var result = values.Select(value =>
            {
                var crossing = value.Object();
                var position = crossing.Point2("position");
                var references = crossing.StringArray("routeIds");
                if (!bounds.Contains(position) || references.Any(reference => !routeIds.Contains(reference)))
                    throw new InvalidDataException("Crossing is out of bounds or references an unknown route.");
                return new WorldMapCrossing(crossing.String("id"), position, crossing.String("type"), references);
            }).ToArray();
            UniqueStrings(result.Select(value => value.Id), "crossing");
            return result;
        }

        private static void ValidateRoadRules(Node roads, WorldMapBounds bounds)
        {
            var ids = new List<string>();
            foreach (var value in roads.Array("chokepoints"))
            {
                var choke = value.Object();
                ids.Add(choke.String("id"));
                if (!bounds.Contains(choke.Point2("position")) || choke.Float("radiusMeters") <= 0f)
                    throw new InvalidDataException("Road chokepoint is invalid.");
            }
            UniqueStrings(ids, "chokepoint");
            var rules = roads.Object("rules");
            if (rules.Float("avoidSlopeAboveDegrees") <= 0f ||
                rules.Float("footpathAvoidSlopeAboveDegrees") < rules.Float("avoidSlopeAboveDegrees") ||
                !rules.Bool("riverCrossingRequiresAuthoredCrossing") ||
                !rules.Bool("futureBridgeSocketsUseStableIds"))
                throw new InvalidDataException("Road navigation rules are invalid.");
        }

        private static IReadOnlyList<WorldMapPointOfInterest> ParsePointsOfInterest(IReadOnlyList<Node> values,
            WorldMapBounds bounds, IReadOnlyList<WorldMapPolygon> regions)
        {
            var result = values.Select(value =>
            {
                var poi = value.Object();
                var position = poi.Point2("position");
                var regionId = poi.String("regionId");
                var region = regions.SingleOrDefault(candidate => candidate.Id == regionId);
                if (!bounds.Contains(position) || region == null || !region.Contains(position))
                    throw new InvalidDataException($"POI {poi.String("id")} is outside its referenced region.");
                return new WorldMapPointOfInterest(poi.String("id"), position, regionId,
                    poi.Float("protectionRadiusMeters"), poi.StringArray("purpose"));
            }).ToArray();
            UniqueStrings(result.Select(value => value.Id), "POI");
            return result;
        }

        private static void ValidatePoiIndex(IReadOnlyList<Node> index,
            IReadOnlyList<WorldMapPointOfInterest> points)
        {
            if (index.Count != points.Count) throw new InvalidDataException("POI index count does not match POI data.");
            foreach (var value in index)
            {
                var item = value.Object();
                var poi = points.SingleOrDefault(candidate => candidate.Id == item.String("id"));
                if (poi == null || !poi.Position.Equals(item.Point2("position")))
                    throw new InvalidDataException("Coordinate POI index differs from POI data.");
            }
        }

        private static void ValidatePoiRules(Node rules) => ValidateRequiredTrueFlags(rules, "POI",
            "uniqueObjectsPersistByStableId", "noAutomaticConstructionClearance", "discoveryStateSaved");

        private static WorldMapResourceDefinition ParseResourceDefinition(Node root, WorldMapBounds bounds)
        {
            var types = root.Array("resourceTypes").Select(value =>
            {
                var type = value.Object();
                var visible = type.Bool("visible");
                return new WorldMapResourceType(type.String("id"), type.String("era"), visible,
                    type.Has("depletion") ? type.String("depletion") : string.Empty,
                    type.Has("renewal") ? type.String("renewal") : string.Empty,
                    type.Has("unlockTechnology") ? type.String("unlockTechnology") : string.Empty);
            }).ToArray();
            var known = UniqueStrings(types.Select(value => value.Id), "resource type");
            var zones = root.Array("zones").Select(value =>
            {
                var zone = value.Object();
                var references = zone.StringArray("resources");
                if (references.Any(reference => !known.Contains(reference)))
                    throw new InvalidDataException($"Resource mask {zone.String("id")} has an unknown resource.");
                return new WorldMapResourceZone(zone.String("id"),
                    ParseShape(zone.Object("shape"), bounds, zone.String("id")), references,
                    zone.String("density"), zone.String("access"));
            }).ToArray();
            return new WorldMapResourceDefinition(types, zones);
        }

        private static IReadOnlyList<WorldMapMask> ParseWildlifeMasks(IReadOnlyList<Node> values,
            WorldMapBounds bounds, ISet<string> species)
        {
            var result = values.Select(value =>
            {
                var mask = value.Object();
                var references = mask.StringArray("species");
                if (references.Any(reference => !species.Contains(reference)))
                    throw new InvalidDataException($"Wildlife mask {mask.String("id")} has an unknown species.");
                return new WorldMapMask(mask.String("id"), "wildlife", ParseShape(mask.Object("shape"), bounds,
                    mask.String("id")), references, "concentration", mask.Float("multiplier"));
            }).ToArray();
            UniqueStrings(result.Select(value => value.Id), "wildlife mask");
            return result;
        }

        private static void ValidateWildlifeSimulation(Node simulation)
        {
            foreach (var name in new[] { "canonicalPopulationTarget", "tier1VisibleCap", "tier2ActiveCap" })
            {
                var range = simulation.NumberArray(name, 2);
                if (range[0] < 0f || range[1] < range[0])
                    throw new InvalidDataException("Wildlife population ranges are invalid.");
            }
            if (!simulation.Bool("timeOfDay")) throw new InvalidDataException("Wildlife time-of-day contract is required.");
        }

        private static WorldMapVegetationDefinition ParseVegetationDefinition(Node vegetation,
            WorldMapBounds bounds)
        {
            var classes = vegetation.Array("classes").Select(value =>
            {
                var definition = value.Object();
                var interactive = definition.NumberArray("interactiveTreesPerHa", 2);
                var decorative = definition.NumberArray("decorativeInstancesPerHa", 2);
                var canopy = definition.NumberArray("canopy", 2);
                return new WorldMapVegetationClass(definition.String("id"),
                    new WorldMapRange(interactive[0], interactive[1]),
                    new WorldMapRange(decorative[0], decorative[1]),
                    new WorldMapRange(canopy[0], canopy[1]));
            }).ToArray();
            var classIds = UniqueStrings(classes.Select(value => value.Id), "vegetation class");
            var zones = vegetation.Array("zones").Select(value =>
            {
                var zone = value.Object();
                var classId = zone.String("classId");
                if (!classIds.Contains(classId)) throw new InvalidDataException("Vegetation zone has an unknown class.");
                var clearings = new List<WorldMapVegetationClearing>();
                if (zone.Has("clearings"))
                    foreach (var clearing in zone.Array("clearings"))
                    {
                        var values = clearing.NumberArray(null, 3);
                        if (!bounds.Contains(new WorldMapPoint(values[0], values[1])) || values[2] <= 0f)
                            throw new InvalidDataException("Vegetation clearing is invalid.");
                        clearings.Add(new WorldMapVegetationClearing(
                            new WorldMapPoint(values[0], values[1]), values[2]));
                    }
                return new WorldMapVegetationZone(zone.String("id"), classId,
                    ParseShape(zone.Object("shape"), bounds, zone.String("id")),
                    zone.Float("densityMultiplier"), clearings);
            }).ToArray();
            UniqueStrings(zones.Select(value => value.Id), "vegetation mask");

            var distribution = vegetation.Object("distribution");
            var treeSpacing = distribution.NumberArray("minimumInteractiveTreeSpacingMeters", 2);
            var reedBand = distribution.NumberArray("shoreReedBandMeters", 2);
            var routeClearance = distribution.Object("routeClearanceMeters");
            var distributionDefinition = new WorldMapVegetationDistribution(distribution.String("method"),
                new WorldMapRange(treeSpacing[0], treeSpacing[1]),
                distribution.Float("startClearingExclusionRadiusMeters"),
                routeClearance.Float("mainTrail"), routeClearance.Float("footpath"),
                new WorldMapRange(reedBand[0], reedBand[1]));

            var budget = vegetation.Object("performanceBudget");
            var definitionBudget = new WorldMapVegetationBudget(
                ParseRange(budget, "canonicalInteractiveTrees"),
                ParseRange(budget, "decorativeTreeAndShrubInstances"),
                ParseRange(budget, "activeTreeCollidersWithinCameraBudget"),
                ParseRange(budget, "interactiveBushesAndHerbs"),
                ParseRange(budget, "decorativeGroundInstances"));
            return new WorldMapVegetationDefinition(classes, zones, distributionDefinition, definitionBudget);
        }

        private static WorldMapRange ParseRange(Node node, string property)
        {
            var values = node.NumberArray(property, 2);
            return new WorldMapRange(values[0], values[1]);
        }

        private static IReadOnlyList<WorldMapMask> ParseBuildabilityMasks(IReadOnlyList<Node> values,
            WorldMapBounds bounds)
        {
            var result = values.Select(value =>
            {
                var mask = value.Object();
                return new WorldMapMask(mask.String("id"), "buildability",
                    ParseShape(mask.Object("shape"), bounds, mask.String("id")), mask.StringArray("purpose"),
                    mask.String("category"), 1f, mask.Float("slopeMaxDegrees"), mask.Bool("requiresClearing"));
            }).ToArray();
            UniqueStrings(result.Select(value => value.Id), "buildability mask");
            return result;
        }

        private static void ValidateBuildabilityRules(Node buildability, WorldMapBounds bounds)
        {
            var restrictionIds = new List<string>();
            foreach (var value in buildability.Array("hardNoBuild"))
            {
                var rule = value.Object();
                restrictionIds.Add(rule.String("id"));
                if (!rule.Has("reason") && !rule.Has("predicate"))
                    throw new InvalidDataException("Hard no-build rule requires a reason or predicate.");
                if (rule.Has("sources") && rule.StringArray("sources").Count == 0)
                    throw new InvalidDataException("Hard no-build sources cannot be empty.");
                if (rule.Has("source")) rule.String("source");
            }
            UniqueStrings(restrictionIds, "hard no-build");
            var clearable = UniqueStrings(buildability.StringArray("clearable"), "clearable blocker");
            var hard = UniqueStrings(buildability.StringArray("unclearableWithoutSpecificTechnology"),
                "unclearable blocker");
            if (clearable.Overlaps(hard)) throw new InvalidDataException("Buildability blocker sets must not overlap.");
            var expansionIds = new List<string>();
            foreach (var value in buildability.Array("expansionDirections"))
            {
                var expansion = value.Object();
                expansionIds.Add(expansion.String("id"));
                if (!bounds.Contains(expansion.Point2("from")) || !bounds.Contains(expansion.Point2("to")) ||
                    expansion.Int("priority") <= 0)
                    throw new InvalidDataException("Expansion direction is invalid.");
            }
            UniqueStrings(expansionIds, "expansion direction");
            var grid = buildability.Object("grid");
            if (grid.Float("cellSizeMeters") <= 0f || !grid.Bool("natureMustNotFollowGrid"))
                throw new InvalidDataException("Buildability grid contract is invalid.");
        }

        private static WorldMapShape ParseShape(Node shape, WorldMapBounds bounds, string id)
        {
            switch (shape.String("type"))
            {
                case "polygon": return WorldMapShape.FromPolygon(new WorldMapPolygon(id + ":shape",
                    shape.Points("points"), bounds));
                case "ellipse":
                {
                    var radius = shape.NumberArray("radius", 2);
                    return WorldMapShape.Ellipse(shape.Point2("center"), radius[0], radius[1], bounds);
                }
                case "ring": return WorldMapShape.Ring(shape.Point2("center"), shape.Float("innerRadius"),
                    shape.Float("outerRadius"), bounds);
                default: throw new InvalidDataException($"Unsupported shape type in {id}.");
            }
        }

        private static void ValidateFishSources(Node resources, IReadOnlyList<WorldMapWaterBody> lakes,
            WorldMapSpline river)
        {
            var lakeIds = new HashSet<string>(lakes.Select(value => value.Id), StringComparer.Ordinal);
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in resources.Array("fishSources"))
            {
                var source = value.Object();
                if (!sourceIds.Add(source.String("id")) || !lakeIds.Contains(source.String("lakeId")) ||
                    source.Int("schoolOrdinal") < 0 || source.Int("initialCapacityUnits") <= 0)
                    throw new InvalidDataException("Invalid lake fish source.");
            }
            foreach (var value in resources.Array("riverFishSources"))
            {
                var source = value.Object();
                if (!sourceIds.Add(source.String("id")) || source.String("riverId") != river.Id ||
                    source.Int("schoolOrdinal") < 0 || source.Int("initialCapacityUnits") <= 0)
                    throw new InvalidDataException("Invalid river fish source.");
            }
        }

        private static void ValidateGlobalIds(IReadOnlyList<WorldMapPolygon> regions, WorldMapSpline river,
            IReadOnlyList<WorldMapSpline> tributaries, IReadOnlyList<WorldMapWaterBody> lakes,
            IReadOnlyList<WorldMapSpline> routes, IReadOnlyList<WorldMapCrossing> crossings,
            IReadOnlyList<WorldMapPointOfInterest> points, IReadOnlyList<WorldMapMask> resourceMasks,
            IReadOnlyList<WorldMapMask> wildlifeMasks, IReadOnlyList<WorldMapMask> buildabilityMasks,
            IReadOnlyList<WorldMapMask> vegetationMasks, IReadOnlyList<WorldMapHeightMask> heightMasks,
            IEnumerable<string> resourceTypes, IEnumerable<string> species)
        {
            var ids = regions.Select(value => value.Id)
                .Concat(new[] { river.Id })
                .Concat(tributaries.Select(value => value.Id))
                .Concat(lakes.Select(value => value.Id))
                .Concat(routes.Select(value => value.Id))
                .Concat(crossings.Select(value => value.Id))
                .Concat(points.Select(value => value.Id))
                .Concat(resourceMasks.Select(value => value.Id))
                .Concat(wildlifeMasks.Select(value => value.Id))
                .Concat(buildabilityMasks.Select(value => value.Id))
                .Concat(vegetationMasks.Select(value => value.Id))
                .Concat(heightMasks.Select(value => value.Id))
                .Concat(resourceTypes)
                .Concat(species);
            UniqueStrings(ids, "global stable");
        }

        private static void ValidateRequiredTrueFlags(Node node, string label, params string[] names)
        {
            if (names.Any(name => !node.Bool(name)))
                throw new InvalidDataException($"{label} contract requires all persistence flags.");
        }

        private static bool TouchesBoundary(WorldMapPoint point, WorldMapBounds bounds) =>
            Same(point.X, bounds.XMin) || Same(point.X, bounds.XMax) ||
            Same(point.Z, bounds.ZMin) || Same(point.Z, bounds.ZMax);

        private static bool Same(float left, float right) => Math.Abs(left - right) <= 0.001f;

        private static bool SamePoints(IReadOnlyList<WorldMapPoint> left, IReadOnlyList<WorldMapPoint> right) =>
            left.Count == right.Count && !left.Where((value, index) => !value.Equals(right[index])).Any();

        private static bool SetEquals(IEnumerable<string> left, IEnumerable<string> right) =>
            new HashSet<string>(left, StringComparer.Ordinal).SetEquals(right);

        private static HashSet<string> UniqueStrings(IEnumerable<string> values, string label)
        {
            var array = values.ToArray();
            if (array.Any(string.IsNullOrWhiteSpace)) throw new InvalidDataException($"Blank {label} ID.");
            var result = new HashSet<string>(array, StringComparer.Ordinal);
            if (result.Count != array.Length) throw new InvalidDataException($"Duplicate {label} ID.");
            return result;
        }

        private static int RequireWhole(float value, string label)
        {
            var result = (int)value;
            if (!Same(value, result)) throw new InvalidDataException($"{label} must be an integer.");
            return result;
        }

        private sealed class Node
        {
            private readonly object value;
            private readonly string path;
            public Node(object value, string path) { this.value = value; this.path = path; }

            public Node Object() => value is IDictionary<string, object>
                ? this
                : throw new InvalidDataException($"{path} must be an object.");

            public Node Object(string name) => Child(name).Object();
            public IReadOnlyList<Node> Array(string name) => Child(name).Array();
            public bool Has(string name) => value is IDictionary<string, object> dictionary &&
                                            dictionary.TryGetValue(name, out var child) && child != null;

            public IReadOnlyList<Node> Array()
            {
                if (!(value is IList<object> array)) throw new InvalidDataException($"{path} must be an array.");
                return array.Select((item, index) => new Node(item, $"{path}[{index}]")).ToArray();
            }

            public string String(string name)
            {
                var child = Child(name);
                if (!(child.value is string result) || string.IsNullOrWhiteSpace(result))
                    throw new InvalidDataException($"{child.path} must be a non-empty string.");
                return result;
            }

            public bool Bool(string name)
            {
                var child = Child(name);
                if (!(child.value is bool result)) throw new InvalidDataException($"{child.path} must be a boolean.");
                return result;
            }

            public int Int(string name)
            {
                var number = Number(name);
                if (number < int.MinValue || number > int.MaxValue || Math.Abs(number - Math.Round(number)) > 0.000001)
                    throw new InvalidDataException($"{path}.{name} must be an integer.");
                return (int)number;
            }

            public float Float(string name)
            {
                var number = Number(name);
                if (number < -float.MaxValue || number > float.MaxValue)
                    throw new InvalidDataException($"{path}.{name} is outside float range.");
                return (float)number;
            }

            public WorldMapPoint Point2(string name)
            {
                var values = NumberArray(name, 2);
                return new WorldMapPoint(values[0], values[1]);
            }

            public WorldPosition Point3(string name)
            {
                var values = NumberArray(name, 3);
                return new WorldPosition(values[0], values[1], values[2]);
            }

            public IReadOnlyList<WorldMapPoint> Points(string name) =>
                Array(name).Select(point =>
                {
                    var values = point.NumberArray(null, 2);
                    return new WorldMapPoint(values[0], values[1]);
                }).ToArray();

            public IReadOnlyList<string> StringArray(string name) => Array(name).Select(item =>
            {
                if (!(item.value is string result) || string.IsNullOrWhiteSpace(result))
                    throw new InvalidDataException($"{item.path} must be a non-empty string.");
                return result;
            }).ToArray();

            public float[] NumberArray(string name, int expected)
            {
                var node = name == null ? this : Child(name);
                var array = node.Array();
                if (array.Count != expected) throw new InvalidDataException($"{node.path} must contain {expected} numbers.");
                return array.Select(item =>
                {
                    if (!(item.value is double result) || result < -float.MaxValue || result > float.MaxValue)
                        throw new InvalidDataException($"{item.path} must be a finite number.");
                    return (float)result;
                }).ToArray();
            }

            public float[] Range(string name)
            {
                var child = Child(name);
                if (child.value is double number)
                {
                    if (number < -float.MaxValue || number > float.MaxValue)
                        throw new InvalidDataException($"{child.path} is outside float range.");
                    return new[] { (float)number, (float)number };
                }
                var result = child.NumberArray(null, 2);
                if (result[1] < result[0]) throw new InvalidDataException($"{child.path} range is inverted.");
                return result;
            }

            private double Number(string name)
            {
                var child = Child(name);
                if (!(child.value is double result)) throw new InvalidDataException($"{child.path} must be a number.");
                return result;
            }

            private Node Child(string name)
            {
                if (!(value is IDictionary<string, object> dictionary))
                    throw new InvalidDataException($"{path} must be an object.");
                if (!dictionary.TryGetValue(name, out var child) || child == null)
                    throw new InvalidDataException($"Required property {path}.{name} is missing.");
                return new Node(child, path + "." + name);
            }
        }
    }
}
