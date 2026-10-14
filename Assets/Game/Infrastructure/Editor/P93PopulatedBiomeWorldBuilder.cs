using System;
using System.Collections.Generic;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    /// <summary>Builds the deterministic P9.3 biome-density pass over the physical P9.2 Terrain.</summary>
    public static class P93PopulatedBiomeWorldBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string DressingRootName = "P9.3 Populated Biome Dressing";
        private const int ScatterSeed = 93031;
        private const float WaterLevel = 0.15f;
        private static readonly Vector3 TerrainOrigin = new Vector3(-64f, -4f, -64f);
        private const float TerrainSize = 128f;

        private static readonly Vector2[] RoutePoints =
        {
            new Vector2(0f,3f), new Vector2(8f,4f), new Vector2(16f,4f),
            new Vector2(24f,5f), new Vector2(30.67f,12.83f), new Vector2(20f,18f),
            new Vector2(3f,-17f)
        };

        private static readonly (int From, int To)[] Routes =
        {
            (0, 1), (1, 2), (2, 3), (3, 4), (1, 5), (0, 6)
        };

        private static readonly string[] TerrainLayerPaths =
        {
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_Moss_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Cliff_Mossy_E.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Pebbles_B.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_Soil_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Heather_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Tidal_Pools_B.terrainlayer"
        };

        private static readonly string[] TreePrefabPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_A/Pine_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_B/Pine_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_C/Pine_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_D/Pine_D.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Conifer/Conifer.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Cypress/Cypress_Forest_Desktop.prefab"
        };

        private static readonly string[] RockPrefabPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_B_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_B_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_C_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_C_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_D.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_Overgrown_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_Overgrown_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_Overgrown_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_Overgrown_D.prefab"
        };

        private static readonly string[] ForestCoverPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Fern_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Fern_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Fern_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Shrub.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_B.prefab"
        };

        private static readonly string[] MeadowCoverPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_D.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_B.prefab"
        };

        private static readonly string[] ShoreCoverPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_D.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/BushDry_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/BushDry_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_Twig.prefab"
        };

        [MenuItem("Serenity/P9.3/Build Populated Biome World")]
        public static void Build()
        {
            ValidateSourceAssets();
            P92RealisticGameplayRegionBuilder.Build();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var baseRegion = UnityEngine.Object.FindFirstObjectByType<P92RealisticGameplayRegion>();
            var region = UnityEngine.Object.FindFirstObjectByType<P5InhabitableRegionPresentation>();
            if (baseRegion == null || region == null)
                throw new InvalidOperationException("P9.3 requires the completed P9.2 gameplay Terrain.");

            DestroyByName(DressingRootName);
            PaintBiomeTerrain(baseRegion.Terrain);
            var dressing = new GameObject(DressingRootName);
            dressing.transform.SetParent(baseRegion.transform, false);
            var eastHills = baseRegion.Locations.Single(x => x.LocationId == "east_hills");
            eastHills.Anchor.position = GroundPoint(baseRegion.Terrain, new Vector2(30.67f, 12.83f));

            var occupied = baseRegion.Trees.Concat(baseRegion.Rocks).Where(x => x != null)
                .Select(x => new Vector2(x.transform.position.x, x.transform.position.z)).ToList();
            var resourcePositions = region.ResourceAnchors.Select(x =>
                new Vector2(x.VisualRoot.position.x, x.VisualRoot.position.z)).ToArray();

            var trees = new List<GameObject>();
            trees.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "North Old Growth", new Vector2(-4f, 25f), new Vector2(27f, 23f), 78, 93101,
                TreePrefabPaths, ScatterKind.Tree, 2.3f, 6.5f));
            trees.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "North East Pine Forest", new Vector2(23f, 30f), new Vector2(24f, 17f), 38, 93102,
                TreePrefabPaths, ScatterKind.Tree, 2.25f, 6.5f));
            trees.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "West Coastal Woodland", new Vector2(-17f, 10f), new Vector2(15f, 27f), 30, 93103,
                TreePrefabPaths, ScatterKind.Tree, 2.2f, 6.5f));
            trees.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "East Rim Woodland", new Vector2(43f, -7f), new Vector2(11f, 24f), 24, 93104,
                TreePrefabPaths, ScatterKind.Tree, 2.2f, 6.5f));

            var rocks = new List<GameObject>();
            rocks.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "East Rocky Upland", new Vector2(35f, 14f), new Vector2(19f, 25f), 55, 93201,
                RockPrefabPaths, ScatterKind.Rock, 1.7f, 5.5f));
            rocks.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "West Shore Boulder Belt", new Vector2(-22f, 0f), new Vector2(9f, 32f), 22, 93202,
                RockPrefabPaths, ScatterKind.Rock, 1.65f, 5.5f));
            rocks.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "South Moraine", new Vector2(14f, -31f), new Vector2(25f, 11f), 17, 93203,
                RockPrefabPaths, ScatterKind.Rock, 1.65f, 5.5f));

            var cover = new List<GameObject>();
            cover.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "Fern Understory", new Vector2(3f, 24f), new Vector2(35f, 28f), 330, 93301,
                ForestCoverPaths, ScatterKind.GroundCover, 0.48f, 1.35f));
            cover.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "South Meadow Grasses", new Vector2(9f, -24f), new Vector2(34f, 23f), 285, 93302,
                MeadowCoverPaths, ScatterKind.GroundCover, 0.52f, 1.15f));
            cover.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "Mixed Woodland Floor", new Vector2(22f, 8f), new Vector2(36f, 34f), 190, 93303,
                ForestCoverPaths.Concat(MeadowCoverPaths).ToArray(), ScatterKind.GroundCover, 0.5f, 1.2f));
            cover.AddRange(Scatter(dressing.transform, baseRegion.Terrain, region, occupied, resourcePositions,
                "Shore Heather And Reeds", new Vector2(-17f, -4f), new Vector2(14f, 42f), 125, 93304,
                ShoreCoverPaths, ScatterKind.GroundCover, 0.5f, 0.9f));

            var formationDecorations = new List<GameObject>();
            var formations = CreateResourceFormations(dressing.transform, baseRegion.Terrain, region,
                formationDecorations);
            var zones = CreateZones();
            var (sampleCount, coveredCount) = CalculateCoverage(baseRegion.Terrain, region,
                trees.Concat(rocks).Concat(cover).Concat(formationDecorations));

            var baseTrees = baseRegion.Trees.Where(x => x != null).ToArray();
            var baseRocks = baseRegion.Rocks.Where(x => x != null).ToArray();
            var baseFoliage = baseRegion.Foliage.Where(x => x != null).ToArray();
            var allTrees = baseTrees.Concat(trees).ToArray();
            var allRocks = baseRocks.Concat(rocks).ToArray();
            var allFoliage = baseFoliage.Concat(cover).Concat(formationDecorations).ToArray();
            baseRegion.Configure(baseRegion.Terrain, baseRegion.WaterRenderer, allTrees, allRocks, allFoliage,
                baseRegion.Locations);

            var obstacles = region.ObstacleAnchors.Concat(trees.Select(x => x.transform))
                .Concat(rocks.Select(x => x.transform)).Distinct().ToArray();
            var piles = Enumerable.Range(0, 4).Select(region.GetPileAnchor).Where(x => x != null).ToArray();
            var foliageRenderers = allTrees.Concat(allFoliage)
                .SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).Distinct().ToArray();
            var trunks = region.TrunkRenderers.Concat(trees.SelectMany(x => x.GetComponentsInChildren<Renderer>(true)))
                .Where(x => x != null && x.sharedMaterial != null).Distinct().ToArray();
            region.Configure(region.PlayerTerritoryAnchor, region.RivalTerritoryAnchor, region.RouteWaypoints,
                region.BuildableAreaAnchors, obstacles, piles, region.ResourceAnchors,
                Array.Empty<Renderer>(), foliageRenderers, baseRegion.WaterRenderer,
                allTrees.Length, allRocks.Length, allFoliage.Length);
            region.ConfigureGeography(region.LandBoundary,
                region.AuthoredPlacementAnchors.Concat(trees.Select(x => x.transform))
                    .Concat(rocks.Select(x => x.transform)).Distinct(), trunks);
            region.ConfigureTerrainProjection(baseRegion.Terrain, baseRegion.Terrain.materialTemplate);

            var marker = dressing.AddComponent<P93PopulatedBiomeWorld>();
            marker.Configure(baseRegion, ScatterSeed, zones, trees, rocks, cover, formations,
                sampleCount, coveredCount);
            baseRegion.ValidateConfiguration();
            region.ValidateConfiguration();
            region.ValidateGeography();
            marker.ValidateConfiguration();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"P93_POPULATED_BIOME_WORLD_BUILT zones={zones.Length}; trees={trees.Count}; " +
                      $"rocks={rocks.Count}; groundCover={cover.Count}; formations={formations.Count}; " +
                      $"coverage={marker.CoverageRatio:F3}; hash={marker.LayoutHash}");
        }

        private static P93PopulatedBiomeWorld.BiomeZone[] CreateZones() => new[]
        {
            new P93PopulatedBiomeWorld.BiomeZone().Configure("north_old_growth",
                P93PopulatedBiomeWorld.BiomeKind.Forest, new Vector2(-4f,25f), new Vector2(27f,23f), .88f,.78f,.16f),
            new P93PopulatedBiomeWorld.BiomeZone().Configure("northeast_pine_forest",
                P93PopulatedBiomeWorld.BiomeKind.Forest, new Vector2(23f,30f), new Vector2(24f,17f), .82f,.72f,.22f),
            new P93PopulatedBiomeWorld.BiomeZone().Configure("fern_understory",
                P93PopulatedBiomeWorld.BiomeKind.Understory, new Vector2(3f,24f), new Vector2(35f,28f), .32f,.94f,.10f),
            new P93PopulatedBiomeWorld.BiomeZone().Configure("south_meadow",
                P93PopulatedBiomeWorld.BiomeKind.Meadow, new Vector2(9f,-24f), new Vector2(34f,23f), .12f,.86f,.12f),
            new P93PopulatedBiomeWorld.BiomeZone().Configure("east_rocky_upland",
                P93PopulatedBiomeWorld.BiomeKind.RockyUpland, new Vector2(35f,14f), new Vector2(19f,25f), .22f,.46f,.90f),
            new P93PopulatedBiomeWorld.BiomeZone().Configure("central_soil_clearings",
                P93PopulatedBiomeWorld.BiomeKind.SoilClearing, new Vector2(7f,2f), new Vector2(28f,20f), .04f,.28f,.05f),
            new P93PopulatedBiomeWorld.BiomeZone().Configure("irregular_shoreline",
                P93PopulatedBiomeWorld.BiomeKind.Shoreline, new Vector2(12f,0f), new Vector2(51f,49f), .08f,.48f,.30f)
        };

        private static List<GameObject> Scatter(Transform parent, Terrain terrain,
            P5InhabitableRegionPresentation region, ICollection<Vector2> occupied,
            IReadOnlyCollection<Vector2> resources, string zoneName, Vector2 center, Vector2 radius, int count,
            int seed, IReadOnlyList<string> prefabPaths, ScatterKind kind, float minimumSpacing,
            float routeClearance)
        {
            var host = Child(parent, zoneName);
            var random = new System.Random(seed);
            var result = new List<GameObject>(count);
            for (var index = 0; index < count; index++)
            {
                var accepted = false;
                var point = Vector2.zero;
                for (var attempt = 0; attempt < 900; attempt++)
                {
                    var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    var radial = Mathf.Sqrt((float)random.NextDouble());
                    point = center + new Vector2(Mathf.Cos(angle) * radius.x * radial,
                        Mathf.Sin(angle) * radius.y * radial);
                    if (!region.ContainsLandPoint(new Vector3(point.x, 0f, point.y)) ||
                        !IsTerrainSuitable(terrain, point, kind) || IsReserved(point, kind, routeClearance) ||
                        resources.Any(resource => Vector2.Distance(resource, point) < (kind == ScatterKind.GroundCover ? 1.25f : 3.2f)) ||
                        occupied.Any(existing => Vector2.Distance(existing, point) < minimumSpacing)) continue;
                    accepted = true;
                    break;
                }
                if (!accepted) throw new InvalidOperationException($"Could not scatter {zoneName} item {index}.");

                var path = prefabPaths[(index + random.Next(prefabPaths.Count)) % prefabPaths.Count];
                var instance = Instantiate(path, host, $"P9.3 {zoneName} {index + 1:000}",
                    kind != ScatterKind.GroundCover);
                instance.transform.position = GroundPoint(terrain, point, kind == ScatterKind.Rock ? -0.08f : 0f);
                instance.transform.rotation = Quaternion.Euler(kind == ScatterKind.Rock
                        ? (float)random.NextDouble() * 8f : 0f,
                    (float)random.NextDouble() * 360f,
                    kind == ScatterKind.Rock ? (float)random.NextDouble() * 7f : 0f);
                var scale = kind switch
                {
                    ScatterKind.Tree => 0.55f + (float)random.NextDouble() * 0.62f,
                    ScatterKind.Rock => 0.36f + (float)random.NextDouble() * 0.68f,
                    _ => 0.58f + (float)random.NextDouble() * 0.86f
                };
                instance.transform.localScale = Vector3.one * scale;
                if (kind == ScatterKind.GroundCover) ConfigureGroundCover(instance);
                else EnsurePhysicalCollider(instance, kind == ScatterKind.Tree);
                occupied.Add(point);
                result.Add(instance);
            }
            return result;
        }

        private static List<P93PopulatedBiomeWorld.ResourceFormation> CreateResourceFormations(Transform parent,
            Terrain terrain, P5InhabitableRegionPresentation region, ICollection<GameObject> allDecorations)
        {
            var host = Child(parent, "Canonical Resource Formations");
            var result = new List<P93PopulatedBiomeWorld.ResourceFormation>();
            foreach (var anchor in region.ResourceAnchors.OrderBy(x => x.StableIndex))
            {
                var formation = Child(host, $"P9.3 Formation {anchor.StableIndex:00} {anchor.RecipeId}");
                var satellites = new List<GameObject>();
                var random = new System.Random(93400 + anchor.StableIndex * 17);
                var paths = ResourceDecorationPaths(anchor.RecipeId);
                for (var index = 0; index < 5; index++)
                {
                    var angle = index / 5f * Mathf.PI * 2f + (float)random.NextDouble() * 0.38f;
                    var distance = 1.55f + (float)random.NextDouble() * 1.45f;
                    var point = new Vector2(anchor.VisualRoot.position.x + Mathf.Cos(angle) * distance,
                        anchor.VisualRoot.position.z + Mathf.Sin(angle) * distance);
                    var satellite = Instantiate(paths[(index + anchor.StableIndex) % paths.Length], formation,
                        $"P9.3 Resource Satellite {anchor.StableIndex:00}-{index + 1}", false);
                    satellite.transform.position = GroundPoint(terrain, point,
                        anchor.RecipeId.Contains("stone") || anchor.RecipeId.Contains("iron") ||
                        anchor.RecipeId.Contains("gold") ? -0.04f : 0f);
                    satellite.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    satellite.transform.localScale = Vector3.one * (0.42f + (float)random.NextDouble() * 0.46f);
                    ConfigureGroundCover(satellite);
                    satellites.Add(satellite);
                    allDecorations.Add(satellite);
                }
                result.Add(new P93PopulatedBiomeWorld.ResourceFormation().Configure(anchor.StableIndex,
                    anchor.RecipeId, anchor.VisualRoot, satellites));
            }
            return result;
        }

        private static string[] ResourceDecorationPaths(string recipe) => recipe switch
        {
            "gather_berries" => new[]
            {
                "Assets/TerrainDemoScene_URP/Prefabs/Foliage/Red_Bush/Red_Bush.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_Red.prefab",
                "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_B.prefab"
            },
            "gather_fiber" => ShoreCoverPaths,
            "chop_wood" => TreePrefabPaths.Take(5).ToArray(),
            "gather_stone" => RockPrefabPaths.Take(7).ToArray(),
            "gather_iron" => new[] { RockPrefabPaths[2], RockPrefabPaths[4], RockPrefabPaths[6] },
            "gather_gold" => new[] { RockPrefabPaths[7], RockPrefabPaths[8], RockPrefabPaths[10] },
            _ => MeadowCoverPaths
        };

        private static void PaintBiomeTerrain(Terrain terrain)
        {
            var data = terrain.terrainData;
            data.terrainLayers = TerrainLayerPaths.Select(Load<TerrainLayer>).ToArray();
            var maps = new float[data.alphamapResolution, data.alphamapResolution, TerrainLayerPaths.Length];
            for (var z = 0; z < data.alphamapResolution; z++)
            for (var x = 0; x < data.alphamapResolution; x++)
            {
                var u = x / (float)(data.alphamapResolution - 1);
                var v = z / (float)(data.alphamapResolution - 1);
                var world = new Vector2(TerrainOrigin.x + u * TerrainSize, TerrainOrigin.z + v * TerrainSize);
                var altitude = data.GetInterpolatedHeight(u, v) + TerrainOrigin.y;
                var slope = Mathf.Clamp01(data.GetSteepness(u, v) / 45f);
                var forest = Mathf.Max(Ellipse(world, new Vector2(-4f, 25f), new Vector2(30f, 27f)),
                    Ellipse(world, new Vector2(23f, 30f), new Vector2(27f, 20f)));
                var meadow = Ellipse(world, new Vector2(9f, -24f), new Vector2(37f, 26f));
                var rocky = Ellipse(world, new Vector2(36f, 14f), new Vector2(24f, 29f));
                var heather = Mathf.Max(forest * 0.72f, Ellipse(world, new Vector2(-18f, 1f), new Vector2(16f, 42f)));
                var trail = 0f;
                foreach (var route in Routes)
                    trail = Mathf.Max(trail, Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(
                        DistanceToSegment(world, RoutePoints[route.From], RoutePoints[route.To]) / 2.5f)));
                var clearing = Mathf.Max(Circle(world, new Vector2(0f, 0f), 11f),
                    Mathf.Max(Circle(world, new Vector2(24f, 5f), 9f),
                        Circle(world, new Vector2(3f, -17f), 8f)));
                var shore = Mathf.SmoothStep(1f, 0f, Mathf.Abs(altitude - WaterLevel) / 1.65f);
                var tidal = Mathf.SmoothStep(1f, 0f, Mathf.Abs(altitude - WaterLevel) / 0.72f);
                var noise = 0.5f + 0.5f * Mathf.Sin(world.x * 0.23f + Mathf.Cos(world.y * 0.17f));

                var weights = new[]
                {
                    0.18f + meadow * 1.55f + (1f - forest) * 0.18f,
                    0.16f + forest * 1.45f + noise * 0.16f,
                    0.05f + slope * slope * 2.1f + rocky * 0.72f,
                    0.04f + shore * 1.25f + rocky * 0.48f + trail * 0.42f,
                    0.04f + clearing * 1.65f + trail * 1.35f,
                    0.03f + heather * 0.82f * (1f - clearing),
                    0.02f + tidal * 1.3f
                };
                var total = weights.Sum();
                for (var layer = 0; layer < weights.Length; layer++) maps[z, x, layer] = weights[layer] / total;
            }
            data.SetAlphamaps(0, 0, maps);
            terrain.detailObjectDistance = 125f;
            terrain.treeDistance = 800f;
            terrain.heightmapPixelError = 2f;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terrain);
        }

        private static (int Samples, int Covered) CalculateCoverage(Terrain terrain,
            P5InhabitableRegionPresentation region, IEnumerable<GameObject> objects)
        {
            var positions = objects.Where(x => x != null).Select(x =>
                new Vector2(x.transform.position.x, x.transform.position.z)).ToArray();
            var samples = 0;
            var covered = 0;
            for (var z = -44f; z <= 46f; z += 5f)
            for (var x = -27f; x <= 53f; x += 5f)
            {
                var point = new Vector2(x, z);
                if (!region.ContainsLandPoint(new Vector3(x, 0f, z)) ||
                    terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y <= WaterLevel + 0.18f)
                    continue;
                samples++;
                if (IsReserved(point, ScatterKind.GroundCover, 1.4f) ||
                    positions.Any(position => Vector2.Distance(position, point) <= 8.5f)) covered++;
            }
            return (samples, covered);
        }

        private static bool IsTerrainSuitable(Terrain terrain, Vector2 point, ScatterKind kind)
        {
            var world = new Vector3(point.x, 0f, point.y);
            var height = terrain.SampleHeight(world) + terrain.transform.position.y;
            if (height <= WaterLevel + (kind == ScatterKind.GroundCover ? 0.18f : 0.35f)) return false;
            var normalizedX = Mathf.InverseLerp(terrain.transform.position.x,
                terrain.transform.position.x + terrain.terrainData.size.x, point.x);
            var normalizedZ = Mathf.InverseLerp(terrain.transform.position.z,
                terrain.transform.position.z + terrain.terrainData.size.z, point.y);
            var slope = terrain.terrainData.GetSteepness(normalizedX, normalizedZ);
            return slope <= (kind == ScatterKind.Rock ? 43f : kind == ScatterKind.Tree ? 34f : 38f);
        }

        private static bool IsReserved(Vector2 point, ScatterKind kind, float routeClearance)
        {
            var playerRadius = kind == ScatterKind.Tree ? 11f : kind == ScatterKind.Rock ? 8.5f : 5.4f;
            var rivalRadius = kind == ScatterKind.Tree ? 9.5f : kind == ScatterKind.Rock ? 7.5f : 4.8f;
            var southRadius = kind == ScatterKind.Tree ? 8.5f : kind == ScatterKind.Rock ? 7f : 4.5f;
            if (Vector2.Distance(point, Vector2.zero) < playerRadius ||
                Vector2.Distance(point, new Vector2(24f, 5f)) < rivalRadius ||
                Vector2.Distance(point, new Vector2(3f, -17f)) < southRadius) return true;
            return Routes.Any(route => DistanceToSegment(point, RoutePoints[route.From], RoutePoints[route.To]) <
                                       routeClearance);
        }

        private static float Ellipse(Vector2 point, Vector2 center, Vector2 radius)
        {
            var delta = point - center;
            var distance = Mathf.Sqrt(delta.x * delta.x / (radius.x * radius.x) +
                                      delta.y * delta.y / (radius.y * radius.y));
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
        }

        private static float Circle(Vector2 point, Vector2 center, float radius) =>
            Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(Vector2.Distance(point, center) / radius));

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < 0.0001f) return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, a + delta * t);
        }

        private static Vector3 GroundPoint(Terrain terrain, Vector2 point, float offset = 0f) =>
            new Vector3(point.x, terrain.SampleHeight(new Vector3(point.x, 0f, point.y)) +
                terrain.transform.position.y + offset, point.y);

        private static void ConfigureGroundCover(GameObject instance)
        {
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            SetStaticRecursive(instance, false);
        }

        private static void EnsurePhysicalCollider(GameObject instance, bool tree)
        {
            if (instance.GetComponentInChildren<Collider>(true) == null)
            {
                if (tree)
                {
                    var collider = instance.AddComponent<CapsuleCollider>();
                    collider.radius = 0.42f;
                    collider.height = 7f;
                    collider.center = new Vector3(0f, 3.5f, 0f);
                }
                else
                {
                    var collider = instance.AddComponent<BoxCollider>();
                    collider.size = new Vector3(1.8f, 1.35f, 1.8f);
                    collider.center = new Vector3(0f, 0.6f, 0f);
                }
            }
            SetStaticRecursive(instance, true);
        }

        private static GameObject Instantiate(string path, Transform parent, string name, bool navigationStatic)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path), parent);
            instance.name = name;
            SetStaticRecursive(instance, navigationStatic);
            return instance;
        }

        private static void SetStaticRecursive(GameObject root, bool navigationStatic)
        {
            var flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
            if (navigationStatic) flags |= StaticEditorFlags.NavigationStatic;
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(transform.gameObject, flags);
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required P9.3 asset is missing: " + path);
            return asset;
        }

        private static void ValidateSourceAssets()
        {
            foreach (var path in TerrainLayerPaths.Concat(TreePrefabPaths).Concat(RockPrefabPaths)
                         .Concat(ForestCoverPaths).Concat(MeadowCoverPaths).Concat(ShoreCoverPaths)
                         .Concat(new[]
                         {
                             "Assets/TerrainDemoScene_URP/Prefabs/Foliage/Red_Bush/Red_Bush.prefab",
                             "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_Red.prefab"
                         }).Distinct())
                _ = Load<UnityEngine.Object>(path);
        }

        private static void DestroyByName(string name)
        {
            var target = GameObject.Find(name);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private enum ScatterKind
        {
            Tree,
            Rock,
            GroundCover
        }
    }
}
