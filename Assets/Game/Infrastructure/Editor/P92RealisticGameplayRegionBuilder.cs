using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using Game.Presentation.Interaction;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    /// <summary>Replaces the legacy P3/P5/P8.2 scene projection with the accepted official Terrain source.</summary>
    public static class P92RealisticGameplayRegionBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string RootName = "P9.2 Realistic Gameplay Region";
        private const string DataFolder = "Assets/Game/Art/Terrain/P92";
        private const string MaterialFolder = "Assets/Game/Art/Materials/P92";
        private const string TerrainDataPath = DataFolder + "/P92_GameplayTerrain.asset";
        private const string TerrainMaterialPath = MaterialFolder + "/P92_SeasonalTerrain.mat";
        private static readonly Vector3 TerrainOrigin = new Vector3(-64f, -4f, -64f);
        private const float TerrainSize = 128f;
        private const float WaterLevel = 0.15f;

        private static readonly Vector2[] Boundary =
        {
            new Vector2(-24f,-30f), new Vector2(-14f,-44f), new Vector2(4f,-49f),
            new Vector2(23f,-47f), new Vector2(39f,-39f), new Vector2(50f,-25f),
            new Vector2(55f,-7f), new Vector2(53f,15f), new Vector2(46f,33f),
            new Vector2(32f,44f), new Vector2(13f,48f), new Vector2(-5f,46f),
            new Vector2(-19f,37f), new Vector2(-27f,22f), new Vector2(-29f,3f),
            new Vector2(-28f,-16f)
        };

        private static readonly Vector3[] RoutePoints =
        {
            new Vector3(0f,0f,3f), new Vector3(8f,0f,4f), new Vector3(16f,0f,4f),
            new Vector3(24f,0f,5f), new Vector3(31f,0f,10f), new Vector3(20f,0f,18f),
            new Vector3(3f,0f,-17f)
        };

        private static readonly (int From, int To, string Name)[] Routes =
        {
            (0, 1, "P9.2 Main Camp Trail"), (1, 2, "P9.2 Ford Trail"),
            (2, 3, "P9.2 Rival Trail"), (3, 4, "P9.2 East Hills Trail"),
            (1, 5, "P9.2 North Forest Trail"), (0, 6, "P9.2 South Meadow Trail")
        };

        private static readonly string[] TerrainLayerPaths =
        {
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Grass_Moss_A.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Cliff_Mossy_E.terrainlayer",
            "Assets/TerrainDemoScene_URP/Terrain/Layers/Pebbles_B.terrainlayer"
        };

        private static readonly string[] TreePrefabPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_A/Pine_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_B/Pine_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_C/Pine_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Pines/Pine_D/Pine_D.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Trees/Conifer/Conifer.prefab"
        };

        private static readonly string[] RockPrefabPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_A_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_B_01.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_C_02.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_Overgrown_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Rocks/Rock_Overgrown_C.prefab"
        };

        private static readonly string[] FoliagePrefabPaths =
        {
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Grass_C.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Fern_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Fern_B.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_A.prefab",
            "Assets/TerrainDemoScene_URP/Prefabs/Details/Shrub.prefab"
        };

        [MenuItem("Serenity/P9.2/Build Realistic Gameplay Region")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (GameObject.Find(RootName) == null)
            {
                P82NaturalRegionBuilder.Build();
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            Directory.CreateDirectory(DataFolder);
            Directory.CreateDirectory(MaterialFolder);
            var composition = UnityEngine.Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            var region = composition != null ? composition.GetComponent<P5InhabitableRegionPresentation>() : null;
            var referenceCamp = composition != null ? composition.GetComponent<StoneAgeReferenceCampPresentation>() : null;
            if (composition == null || region == null || referenceCamp == null)
                throw new InvalidOperationException("The P8.2 gameplay composition must exist before P9.2 migration.");

            ValidateSourceAssets();
            DestroyByName(RootName);
            DestroyByName("P9.1 Realistic World Benchmark");
            DestroyByName("P8.1 Visual Prototype");
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null)
                AssetDatabase.DeleteAsset(TerrainDataPath);

            var root = new GameObject(RootName);
            var terrainMaterial = CreateTerrainMaterial();
            var terrain = CreateTerrain(root.transform, terrainMaterial);
            ProjectLegacyInteractionAnchors(terrain);
            var water = CreateWater(root.transform);
            var locations = CreateLocations(root.transform, terrain);
            var routeAnchors = CreateRouteAnchors(root.transform, terrain);
            var buildableAnchors = CreateBuildableAnchors(root.transform, terrain);

            var environmentTrees = PopulateTrees(root.transform, terrain);
            var environmentRocks = PopulateRocks(root.transform, terrain);
            var environmentFoliage = PopulateFoliage(root.transform, terrain);
            var resources = CreateResourceAnchors(root.transform, terrain, environmentTrees,
                environmentRocks, environmentFoliage);
            var piles = CreateSupplyAnchors(root.transform, terrain);
            var campfire = CreateCamp(root.transform, terrain, locations);

            var obstacles = environmentTrees.Concat(environmentRocks).Select(x => x.transform).ToArray();
            var placements = routeAnchors.Concat(buildableAnchors).Concat(obstacles).Concat(piles)
                .Concat(resources.Select(x => x.VisualRoot)).Distinct().ToArray();
            var foliageRenderers = environmentTrees.Concat(environmentFoliage)
                .SelectMany(x => x.GetComponentsInChildren<Renderer>(true)).ToArray();
            var trunkRenderers = environmentTrees.SelectMany(x => x.GetComponentsInChildren<Renderer>(true))
                .Where(x => x.sharedMaterial != null).ToArray();

            region.Configure(locations.Single(x => x.LocationId == "starter_settlement").Anchor,
                locations.Single(x => x.LocationId == "enemy_settlement").Anchor,
                routeAnchors, buildableAnchors, obstacles, piles, resources,
                Array.Empty<Renderer>(), foliageRenderers, water, environmentTrees.Count,
                environmentRocks.Count, environmentFoliage.Count);
            region.ConfigureGeography(Boundary, placements, trunkRenderers);
            region.ConfigureTerrainProjection(terrain, terrainMaterial);
            region.ValidateConfiguration();

            var marker = root.AddComponent<P92RealisticGameplayRegion>();
            marker.Configure(terrain, water, environmentTrees, environmentRocks, environmentFoliage, locations);
            marker.ValidateConfiguration();
            ConfigureReferenceCamp(referenceCamp, campfire, water, marker);

            DestroyByName("P3 Reference World");
            DestroyByName("P3 Buildable Forest Clearing");
            DestroyByName("Ground Placeholder");
            ConfigureLighting();
            var cameraRig = GameObject.Find("RTS Camera Rig");
            if (cameraRig != null)
            {
                cameraRig.transform.position = marker.ProjectToGround(new Vector3(2f, 0f, 4f));
                cameraRig.transform.rotation = Quaternion.Euler(0f, -18f, 0f);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("P92_REALISTIC_GAMEPLAY_REGION_BUILT locations=6; resources=24; routes=6; " +
                      $"trees={environmentTrees.Count}; rocks={environmentRocks.Count}; " +
                      $"foliage={environmentFoliage.Count}; legacyVisibleMap=false");
        }

        private static Terrain CreateTerrain(Transform parent, Material material)
        {
            var data = new TerrainData
            {
                name = "P92 Realistic Gameplay Terrain",
                heightmapResolution = 513,
                alphamapResolution = 256,
                baseMapResolution = 1024,
                size = new Vector3(TerrainSize, 24f, TerrainSize)
            };
            AssetDatabase.CreateAsset(data, TerrainDataPath);
            var heights = new float[data.heightmapResolution, data.heightmapResolution];
            for (var z = 0; z < data.heightmapResolution; z++)
            for (var x = 0; x < data.heightmapResolution; x++)
            {
                var world = new Vector2(TerrainOrigin.x + x / (float)(data.heightmapResolution - 1) * TerrainSize,
                    TerrainOrigin.z + z / (float)(data.heightmapResolution - 1) * TerrainSize);
                var radial = (world - new Vector2(12f, 0f)).magnitude;
                var angle = Mathf.Atan2(world.y, world.x - 12f);
                var coastRadius = 45f + Mathf.Sin(angle * 3f) * 3.1f + Mathf.Cos(angle * 5f) * 1.8f;
                var inland = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((coastRadius - radial) / 7.5f));
                var rolling = Mathf.Sin(world.x * 0.16f + world.y * 0.055f) * 0.34f +
                              Mathf.Cos(world.y * 0.18f - world.x * 0.035f) * 0.27f +
                              Mathf.Sin((world.x + world.y) * 0.31f) * 0.11f;
                var height = Mathf.Lerp(-1.65f, 2.05f + rolling, inland);
                height += Gaussian(world, new Vector2(20f, 31f), 260f) * 4.8f;
                height += Gaussian(world, new Vector2(37f, 18f), 150f) * 3.4f;
                height += Gaussian(world, new Vector2(-13f, 25f), 180f) * 2.2f;
                foreach (var route in Routes)
                {
                    var distance = DistanceToSegment(world, To2(RoutePoints[route.From]), To2(RoutePoints[route.To]));
                    var blend = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance / 3.2f));
                    var trailHeight = 2.12f + world.x * 0.007f + world.y * 0.004f;
                    height = Mathf.Lerp(height, trailHeight, blend * 0.82f);
                }
                height = Flatten(height, world, new Vector2(0f, 0f), 10f, 2.08f);
                height = Flatten(height, world, new Vector2(24f, 5f), 8f, 2.31f);
                height = Flatten(height, world, new Vector2(3f, -17f), 7f, 1.92f);
                heights[z, x] = Mathf.Clamp01((height - TerrainOrigin.y) / data.size.y);
            }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = TerrainLayerPaths.Select(Load<TerrainLayer>).ToArray();
            PaintTerrain(data);

            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "P9.2 Canonical Gameplay Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = TerrainOrigin;
            terrainObject.AddComponent<BuildableGround>();
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.materialTemplate = material;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 1400f;
            terrain.detailObjectDistance = 110f;
            terrain.treeDistance = 700f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
            return terrain;
        }

        private static void PaintTerrain(TerrainData data)
        {
            var maps = new float[data.alphamapResolution, data.alphamapResolution, data.terrainLayers.Length];
            for (var z = 0; z < data.alphamapResolution; z++)
            for (var x = 0; x < data.alphamapResolution; x++)
            {
                var u = x / (float)(data.alphamapResolution - 1);
                var v = z / (float)(data.alphamapResolution - 1);
                var world = new Vector2(TerrainOrigin.x + u * TerrainSize, TerrainOrigin.z + v * TerrainSize);
                var slope = Mathf.Clamp01(data.GetSteepness(u, v) / 48f);
                var trail = 0f;
                foreach (var route in Routes)
                    trail = Mathf.Max(trail, Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(
                        DistanceToSegment(world, To2(RoutePoints[route.From]), To2(RoutePoints[route.To])) / 2.35f)));
                var altitude = data.GetInterpolatedHeight(u, v) + TerrainOrigin.y;
                var shore = Mathf.SmoothStep(1f, 0f, Mathf.Abs(altitude - WaterLevel) / 1.5f);
                var cliff = slope * slope;
                var pebbles = Mathf.Clamp01(trail * 1.4f + shore * 0.78f);
                var moss = Mathf.Clamp01(0.38f + Mathf.Sin(world.x * 0.19f) * 0.13f +
                                          Mathf.Cos(world.y * 0.21f) * 0.12f) * (1f - cliff);
                var grass = Mathf.Max(0.08f, 1.15f - moss - cliff - pebbles);
                var total = grass + moss + cliff + pebbles;
                maps[z, x, 0] = grass / total;
                maps[z, x, 1] = moss / total;
                maps[z, x, 2] = cliff / total;
                maps[z, x, 3] = pebbles / total;
            }
            data.SetAlphamaps(0, 0, maps);
        }

        private static Renderer CreateWater(Transform parent)
        {
            var water = Instantiate("Assets/TerrainDemoScene_URP/Prefabs/Water/WaterPlane.prefab", parent,
                "P9.2 Coastal Water");
            water.transform.position = new Vector3(12f, WaterLevel, 0f);
            water.transform.rotation = Quaternion.identity;
            water.transform.localScale = new Vector3(5.2f, 1f, 5.2f);
            foreach (var collider in water.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            CreateWaterBoundary(parent, new Vector3(-52f, 0f, 0f), new Vector3(18f, 8f, 128f));
            CreateWaterBoundary(parent, new Vector3(59f, 0f, 0f), new Vector3(18f, 8f, 128f));
            CreateWaterBoundary(parent, new Vector3(4f, 0f, -58f), new Vector3(94f, 8f, 14f));
            CreateWaterBoundary(parent, new Vector3(4f, 0f, 57f), new Vector3(94f, 8f, 14f));
            return water.GetComponentInChildren<Renderer>(true);
        }

        private static void CreateWaterBoundary(Transform parent, Vector3 position, Vector3 size)
        {
            var boundary = new GameObject("P9.2 Non-walkable Water Boundary");
            boundary.transform.SetParent(parent, false);
            boundary.transform.position = position;
            var collider = boundary.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, -3f, 0f);
            collider.size = size;
            var modifier = boundary.AddComponent<NavMeshModifier>();
            modifier.overrideArea = true;
            modifier.area = 1;
        }

        private static List<P92RealisticGameplayRegion.LocationAnchor> CreateLocations(Transform parent, Terrain terrain)
        {
            var values = new[]
            {
                ("starter_settlement", new Vector3(0f,0f,3.2f)),
                ("enemy_settlement", new Vector3(24f,0f,5f)),
                ("north_forest", new Vector3(20f,0f,18f)),
                ("river_ford", new Vector3(8f,0f,4f)),
                ("east_hills", new Vector3(31f,0f,10f)),
                ("south_field", new Vector3(3f,0f,-17f))
            };
            return values.Select(value => new P92RealisticGameplayRegion.LocationAnchor().Configure(value.Item1,
                CreateAnchor("P9.2 Location " + value.Item1, GroundPoint(terrain, value.Item2), parent))).ToList();
        }

        private static Transform[] CreateRouteAnchors(Transform parent, Terrain terrain) => Routes.Select(route =>
        {
            var midpoint = (RoutePoints[route.From] + RoutePoints[route.To]) * 0.5f;
            return CreateAnchor(route.Name, GroundPoint(terrain, midpoint, 0.03f), parent);
        }).ToArray();

        private static Transform[] CreateBuildableAnchors(Transform parent, Terrain terrain)
        {
            return new[]
            {
                CreateAnchor("P9.2 Player Building Plateau", GroundPoint(terrain, new Vector3(0f,0f,0f)), parent),
                CreateAnchor("P9.2 Rival Building Plateau", GroundPoint(terrain, new Vector3(24f,0f,5f)), parent),
                CreateAnchor("P9.2 South Building Plateau", GroundPoint(terrain, new Vector3(3f,0f,-17f)), parent)
            };
        }

        private static List<GameObject> PopulateTrees(Transform parent, Terrain terrain)
        {
            var result = new List<GameObject>();
            var random = new System.Random(92021);
            var host = Child(parent, "Official Terrain Sample Trees");
            for (var i = 0; i < 72; i++)
            {
                Vector3 point;
                var attempts = 0;
                do
                {
                    var angle = (i / 72f) * Mathf.PI * 2f + (float)(random.NextDouble() - 0.5) * 0.42f;
                    var radius = 22f + (float)random.NextDouble() * 24f;
                    point = new Vector3(12f + Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                    attempts++;
                } while ((!ContainsLandPoint(point) || IsReserved(point)) && attempts < 200);
                var tree = Instantiate(TreePrefabPaths[i % TreePrefabPaths.Length], host, $"P9.2 Tree {i + 1:00}");
                tree.transform.position = GroundPoint(terrain, point);
                tree.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                tree.transform.localScale = Vector3.one * (0.68f + (float)random.NextDouble() * 0.42f);
                EnsureCollider(tree, true);
                result.Add(tree);
            }
            return result;
        }

        private static List<GameObject> PopulateRocks(Transform parent, Terrain terrain)
        {
            var result = new List<GameObject>();
            var random = new System.Random(92022);
            var host = Child(parent, "Official Terrain Sample Rocks");
            for (var i = 0; i < 32; i++)
            {
                Vector3 point;
                var attempts = 0;
                do
                {
                    point = new Vector3(-18f + (float)random.NextDouble() * 68f, 0f,
                        -39f + (float)random.NextDouble() * 78f);
                    attempts++;
                } while ((!ContainsLandPoint(point) || IsReserved(point)) && attempts < 200);
                var rock = Instantiate(RockPrefabPaths[i % RockPrefabPaths.Length], host, $"P9.2 Rock {i + 1:00}");
                rock.transform.position = GroundPoint(terrain, point, -0.08f);
                rock.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 7f,
                    (float)random.NextDouble() * 360f, (float)random.NextDouble() * 6f);
                rock.transform.localScale = Vector3.one * (0.42f + (float)random.NextDouble() * 0.72f);
                EnsureCollider(rock, false);
                result.Add(rock);
            }
            return result;
        }

        private static List<GameObject> PopulateFoliage(Transform parent, Terrain terrain)
        {
            var result = new List<GameObject>();
            var random = new System.Random(92023);
            var host = Child(parent, "Official Terrain Sample Ground Foliage");
            for (var i = 0; i < 280; i++)
            {
                Vector3 point;
                var attempts = 0;
                do
                {
                    point = new Vector3(-22f + (float)random.NextDouble() * 70f, 0f,
                        -41f + (float)random.NextDouble() * 82f);
                    attempts++;
                } while ((!ContainsLandPoint(point) || IsReserved(point, 4.5f)) && attempts < 200);
                var plant = Instantiate(FoliagePrefabPaths[i % FoliagePrefabPaths.Length], host,
                    $"P9.2 Foliage {i + 1:000}");
                plant.transform.position = GroundPoint(terrain, point);
                plant.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                plant.transform.localScale = Vector3.one * (0.66f + (float)random.NextDouble() * 0.58f);
                foreach (var renderer in plant.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                result.Add(plant);
            }
            return result;
        }

        private static List<P5InhabitableRegionPresentation.ResourceAnchor> CreateResourceAnchors(Transform parent,
            Terrain terrain, ICollection<GameObject> trees, ICollection<GameObject> rocks,
            ICollection<GameObject> foliage)
        {
            var host = Child(parent, "Canonical Resource Projections");
            var result = new List<P5InhabitableRegionPresentation.ResourceAnchor>();
            var berry = new[] { new Vector3(-12,0,5), new Vector3(7,0,16), new Vector3(18,0,-9), new Vector3(29,0,14) };
            var fiber = new[] { new Vector3(-8,0,7), new Vector3(4,0,20), new Vector3(12,0,-15), new Vector3(31,0,-8) };
            var wood = new[] { new Vector3(-4,0,9), new Vector3(2,0,26), new Vector3(28,0,-27), new Vector3(34,0,22) };
            var stone = new[] { new Vector3(1,0,9), new Vector3(12,0,19), new Vector3(25,0,24), new Vector3(32,0,-5) };
            var iron = new[] { new Vector3(5,0,29), new Vector3(34,0,18), new Vector3(25,0,-25), new Vector3(-7,0,-28) };
            var gold = new[] { new Vector3(-7,0,28), new Vector3(37,0,7), new Vector3(15,0,-31), new Vector3(-11,0,-12) };
            AddResources(result, host, terrain, "gather_berries", 0, berry,
                "Assets/TerrainDemoScene_URP/Prefabs/Foliage/Red_Bush/Red_Bush.prefab",
                new Color(0.50f, 0.18f, 0.24f), foliage, ResourceShape.Foliage, 60, 15, 4);
            AddResources(result, host, terrain, "gather_fiber", 1, fiber,
                "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_A.prefab",
                new Color(0.48f, 0.58f, 0.22f), foliage, ResourceShape.Foliage, 60, 15, 7);
            AddResources(result, host, terrain, "chop_wood", 2, wood, TreePrefabPaths[2],
                new Color(0.34f, 0.28f, 0.17f), trees, ResourceShape.Tree, 80, 20, 10);
            AddResources(result, host, terrain, "gather_stone", 3, stone, RockPrefabPaths[1],
                new Color(0.49f, 0.50f, 0.47f), rocks, ResourceShape.Rock, 80, 20, 13);
            AddResources(result, host, terrain, "gather_iron", 16, iron, RockPrefabPaths[3],
                new Color(0.30f, 0.34f, 0.36f), rocks, ResourceShape.Rock, 70, 15);
            AddResources(result, host, terrain, "gather_gold", 20, gold, RockPrefabPaths[4],
                new Color(0.66f, 0.47f, 0.14f), rocks, ResourceShape.Rock, 70, 15);
            return result;
        }

        private static void AddResources(ICollection<P5InhabitableRegionPresentation.ResourceAnchor> result,
            Transform parent, Terrain terrain, string recipe, int firstIndex, IReadOnlyList<Vector3> positions,
            string prefabPath, Color color, ICollection<GameObject> densityCollection, ResourceShape shape,
            long baseAmount, long amountStep, int alternateFirstIndex = -1)
        {
            for (var i = 0; i < positions.Count; i++)
            {
                var stableIndex = alternateFirstIndex < 0 ? firstIndex + i : i == 0 ? firstIndex : alternateFirstIndex + i - 1;
                var root = new GameObject($"P9.2 {recipe} Resource {stableIndex:00}");
                root.transform.SetParent(parent, false);
                root.transform.position = GroundPoint(terrain, positions[i]);
                var visual = Instantiate(prefabPath, root.transform, "Official Terrain Sample Resource Visual");
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, i * 83f + stableIndex * 7f, 0f);
                visual.transform.localScale = Vector3.one * (shape == ResourceShape.Tree ? 0.72f :
                    shape == ResourceShape.Rock ? 0.68f + i * 0.08f : 1.35f + i * 0.08f);
                AddSelectionCollider(root, shape);
                densityCollection.Add(root);
                result.Add(new P5InhabitableRegionPresentation.ResourceAnchor().Configure(recipe,
                    baseAmount + i * amountStep, stableIndex, root.transform, color));
            }
        }

        private static Transform[] CreateSupplyAnchors(Transform parent, Terrain terrain)
        {
            var host = Child(parent, "Canonical Supply Projections");
            var values = new[]
            {
                ("Wood", new Vector3(-8,0,-2), "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_Twig.prefab"),
                ("Fiber", new Vector3(-6,0,-2), "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_B.prefab"),
                ("Stone", new Vector3(-4,0,-2), RockPrefabPaths[0]),
                ("Hide", new Vector3(-2,0,-2), "Assets/TerrainDemoScene_URP/Prefabs/Details/Heather_A.prefab")
            };
            return values.Select((value, index) =>
            {
                var root = new GameObject("P9.2 " + value.Item1 + " Supply");
                root.transform.SetParent(host, false);
                root.transform.position = GroundPoint(terrain, value.Item2);
                var visual = Instantiate(value.Item3, root.transform, "Official Terrain Sample Supply Visual");
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(0f, index * 47f, 0f);
                visual.transform.localScale = Vector3.one * 0.62f;
                AddSelectionCollider(root, ResourceShape.Rock);
                return root.transform;
            }).ToArray();
        }

        private static Transform CreateCamp(Transform parent, Terrain terrain,
            IReadOnlyList<P92RealisticGameplayRegion.LocationAnchor> locations)
        {
            var camp = Child(parent, "P9.2 Stone Age Camp");
            var fire = Instantiate("Assets/Game/Art/Prefabs/Props/Serenity_Campfire.prefab", camp,
                "P9.2 Canonical Campfire");
            fire.transform.position = GroundPoint(terrain, new Vector3(0f, 0f, 3.2f));
            fire.transform.rotation = Quaternion.Euler(0f, 18f, 0f);
            var rival = locations.Single(x => x.LocationId == "enemy_settlement").Anchor;
            var rivalFire = Instantiate("Assets/Game/Art/Prefabs/Props/Serenity_Campfire.prefab", rival,
                "P9.2 Rival Campfire Projection");
            rivalFire.transform.localPosition = Vector3.zero;
            for (var i = 0; i < 3; i++)
            {
                var marker = Instantiate(RockPrefabPaths[(i + 2) % RockPrefabPaths.Length], rival,
                    "P9.2 Rival Boundary Stone " + (i + 1));
                marker.transform.localPosition = new Vector3(-4f + i * 4f, 0f, 4.5f);
                marker.transform.localScale = Vector3.one * 0.72f;
                EnsureCollider(marker, false);
            }
            return fire.transform;
        }

        private static void ConfigureReferenceCamp(StoneAgeReferenceCampPresentation camp, Transform fire,
            Renderer water, P92RealisticGameplayRegion marker)
        {
            camp.Configure(fire, water,
                Load<GameObject>("Assets/Game/Art/Prefabs/Weapons/Serenity_StoneAxe.prefab"),
                Load<GameObject>("Assets/Game/Art/Prefabs/Weapons/Serenity_StoneSpear.prefab"),
                Load<Material>("Assets/Game/Art/Materials/P3/P3_Flame.mat"),
                Load<Material>("Assets/Game/Art/Materials/P3/P3_Smoke.mat"),
                Load<Material>("Assets/Game/Art/Materials/P3/P3_Rain.mat"),
                Load<Material>("Assets/Game/Art/Materials/P3/P3_ShelterHide.mat"),
                Load<Material>("Assets/Game/Art/Materials/P3/P3_StorageWicker.mat"),
                marker.Trees.Count, marker.Rocks.Count, marker.Foliage.Count);
        }

        private static Material CreateTerrainMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) throw new InvalidOperationException("URP Terrain/Lit shader is unavailable.");
            if (material == null)
            {
                material = new Material(shader) { name = "P92 Seasonal Terrain" };
                AssetDatabase.CreateAsset(material, TerrainMaterialPath);
            }
            material.shader = shader;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureLighting()
        {
            RenderSettings.skybox = Load<Material>("Assets/Game/Art/Materials/P3/P3_Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.84f;
            RenderSettings.reflectionIntensity = 0.78f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0028f;
            RenderSettings.fogColor = new Color(0.53f, 0.61f, 0.64f);
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                         .Where(x => x.type == LightType.Directional))
            {
                light.transform.rotation = Quaternion.Euler(43f, -31f, 0f);
                light.color = new Color(1f, 0.91f, 0.79f);
                light.intensity = 1.22f;
                light.shadows = LightShadows.Soft;
                RenderSettings.sun = light;
            }
        }

        private static bool IsReserved(Vector3 point, float radius = 6.5f)
        {
            var p = new Vector2(point.x, point.z);
            if (new Vector2(point.x, point.z).magnitude < 11f ||
                Vector2.Distance(p, new Vector2(24f, 5f)) < 9f ||
                Vector2.Distance(p, new Vector2(3f, -17f)) < 8f) return true;
            return Routes.Any(route => DistanceToSegment(p, To2(RoutePoints[route.From]),
                To2(RoutePoints[route.To])) < radius * 0.55f);
        }

        private static bool ContainsLandPoint(Vector3 worldPoint)
        {
            var point = new Vector2(worldPoint.x, worldPoint.z);
            var inside = false;
            for (int i = 0, j = Boundary.Length - 1; i < Boundary.Length; j = i++)
            {
                var a = Boundary[i];
                var b = Boundary[j];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        private static float Flatten(float height, Vector2 point, Vector2 center, float radius, float target)
        {
            var blend = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(Vector2.Distance(point, center) / radius));
            return Mathf.Lerp(height, target, blend * 0.96f);
        }

        private static float Gaussian(Vector2 point, Vector2 center, float denominator) =>
            Mathf.Exp(-(point - center).sqrMagnitude / denominator);

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var delta = b - a;
            if (delta.sqrMagnitude < 0.0001f) return Vector2.Distance(point, a);
            var t = Mathf.Clamp01(Vector2.Dot(point - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(point, a + delta * t);
        }

        private static Vector2 To2(Vector3 value) => new Vector2(value.x, value.z);

        private static Vector3 GroundPoint(Terrain terrain, Vector3 point, float offset = 0f)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y + offset;
            return point;
        }

        private static Transform CreateAnchor(string name, Vector3 position, Transform parent)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            return anchor;
        }

        private static void ProjectLegacyInteractionAnchors(Terrain terrain)
        {
            foreach (var marker in UnityEngine.Object.FindObjectsByType<SelectableMarker>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                marker.transform.position = GroundPoint(terrain, marker.transform.position, 0.05f);
                var modifier = marker.GetComponent<NavMeshModifier>();
                if (modifier == null) modifier = marker.gameObject.AddComponent<NavMeshModifier>();
                modifier.ignoreFromBuild = true;
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static GameObject Instantiate(string path, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(path), parent);
            instance.name = name;
            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.NavigationStatic);
            return instance;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required P9.2 asset is missing: " + path);
            return asset;
        }

        private static void ValidateSourceAssets()
        {
            foreach (var path in TerrainLayerPaths.Concat(TreePrefabPaths).Concat(RockPrefabPaths)
                         .Concat(FoliagePrefabPaths).Concat(new[]
                         {
                             "Assets/TerrainDemoScene_URP/Prefabs/Foliage/Red_Bush/Red_Bush.prefab",
                             "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_A.prefab",
                             "Assets/TerrainDemoScene_URP/Prefabs/Details/GrassDry_B.prefab",
                             "Assets/TerrainDemoScene_URP/Prefabs/Details/Bush_Twig.prefab",
                             "Assets/TerrainDemoScene_URP/Prefabs/Water/WaterPlane.prefab",
                             "Assets/TerrainDemoScene_URP/Prefabs/Skybox/Materials/Sky.mat"
                         }))
                _ = Load<UnityEngine.Object>(path);
        }

        private static void EnsureCollider(GameObject root, bool tree)
        {
            if (root.GetComponentInChildren<Collider>(true) != null) return;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = tree ? 0.45f : 0.8f;
            collider.height = tree ? 7f : 1.4f;
            collider.center = tree ? new Vector3(0f, 3.5f, 0f) : new Vector3(0f, 0.55f, 0f);
        }

        private static void AddSelectionCollider(GameObject root, ResourceShape shape)
        {
            if (shape == ResourceShape.Tree)
            {
                var collider = root.AddComponent<CapsuleCollider>();
                collider.radius = 0.7f;
                collider.height = 7f;
                collider.center = new Vector3(0f, 3.5f, 0f);
            }
            else
            {
                var collider = root.AddComponent<BoxCollider>();
                collider.size = shape == ResourceShape.Rock
                    ? new Vector3(2.4f, 1.8f, 2.4f) : new Vector3(2.2f, 1.5f, 2.2f);
                collider.center = new Vector3(0f, collider.size.y * 0.5f, 0f);
            }
        }

        private static void DestroyByName(string name)
        {
            var target = GameObject.Find(name);
            if (target != null) UnityEngine.Object.DestroyImmediate(target);
        }

        private enum ResourceShape { Foliage, Tree, Rock }
    }
}
