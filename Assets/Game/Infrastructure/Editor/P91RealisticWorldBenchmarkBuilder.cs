using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    /// <summary>Authors a compact, isolated benchmark from the approved Unity URP Terrain Sample.</summary>
    public static class P91RealisticWorldBenchmarkBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string RootName = "P9.1 Realistic World Benchmark";
        private const string DataFolder = "Assets/Game/Art/Terrain/P91";
        private const string TerrainDataPath = DataFolder + "/P91_RealisticBenchmarkTerrain.asset";
        private static readonly Vector3 TerrainOrigin = new Vector3(88f, -2f, 76f);
        private const float TerrainSize = 96f;

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

        [MenuItem("Serenity/P9.1/Build Realistic World Benchmark")]
        public static void Build()
        {
            Directory.CreateDirectory(DataFolder);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var existing = GameObject.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            ValidateSourceAssets();
            if (AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null)
                AssetDatabase.DeleteAsset(TerrainDataPath);

            var root = new GameObject(RootName);
            var terrain = CreateTerrain(root.transform);
            var treeParent = Child(root.transform, "Official Sample Trees");
            var rockParent = Child(root.transform, "Official Sample Rocks");
            var foliageParent = Child(root.transform, "Official Sample Foliage");
            var trees = PopulateTrees(terrain, treeParent);
            var rocks = PopulateRocks(terrain, rockParent);
            var foliage = PopulateFoliage(terrain, foliageParent);
            var water = CreateWater(root.transform);

            var center = Child(root.transform, "Benchmark Camera Target");
            center.position = GroundPoint(terrain, 48f, 48f) + Vector3.up * 2.4f;
            var marker = root.AddComponent<P91RealisticWorldBenchmark>();
            marker.Configure(center, terrain, trees, rocks, foliage, water);
            marker.ValidateConfiguration();

            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Materials/P3/P3_Skybox.mat");
            if (sky != null && sky.shader != null && sky.shader.name != "Hidden/InternalErrorShader")
                RenderSettings.skybox = sky;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.55f, 0.63f, 0.66f);
            RenderSettings.ambientIntensity = 0.78f;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                         .Where(x => x.type == LightType.Directional))
            {
                light.color = new Color(1f, 0.91f, 0.79f);
                light.intensity = 1.25f;
                light.shadows = LightShadows.Soft;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"P91_REALISTIC_WORLD_BUILT terrainLayers={terrain.terrainData.terrainLayers.Length}; " +
                      $"trees={trees.Count}; rocks={rocks.Count}; foliage={foliage.Count}; oldMapPreserved=true");
        }

        private static Terrain CreateTerrain(Transform parent)
        {
            var data = new TerrainData
            {
                name = "P91 Realistic Benchmark Terrain",
                heightmapResolution = 257,
                alphamapResolution = 128,
                baseMapResolution = 512,
                size = new Vector3(TerrainSize, 18f, TerrainSize)
            };
            AssetDatabase.CreateAsset(data, TerrainDataPath);

            var heights = new float[data.heightmapResolution, data.heightmapResolution];
            for (var z = 0; z < data.heightmapResolution; z++)
            for (var x = 0; x < data.heightmapResolution; x++)
            {
                var u = x / (float)(data.heightmapResolution - 1);
                var v = z / (float)(data.heightmapResolution - 1);
                var rolling = 0.155f + Mathf.Sin(u * 13f + v * 4f) * 0.018f +
                              Mathf.Cos(v * 16f - u * 3f) * 0.014f + Mathf.Sin((u + v) * 31f) * 0.006f;
                var northRidge = Mathf.Exp(-((u - 0.18f) * (u - 0.18f) +
                                                  (v - 0.76f) * (v - 0.76f)) / 0.055f) * 0.21f;
                var eastRidge = Mathf.Exp(-((u - 0.88f) * (u - 0.88f) +
                                                 (v - 0.78f) * (v - 0.78f)) / 0.035f) * 0.15f;
                var lakeBasin = Mathf.Exp(-((u - 0.68f) * (u - 0.68f) / 0.022f +
                                                 (v - 0.32f) * (v - 0.32f) / 0.013f)) * 0.15f;
                var clearing = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.48f, 0.52f)) / 0.15f);
                var height = rolling + northRidge + eastRidge - lakeBasin;
                height = Mathf.Lerp(height, 0.145f, clearing * 0.72f);
                heights[z, x] = Mathf.Clamp(height, 0.025f, 0.52f);
            }
            data.SetHeights(0, 0, heights);
            data.terrainLayers = TerrainLayerPaths.Select(Load<TerrainLayer>).ToArray();
            PaintTerrain(data);

            var terrainObject = Terrain.CreateTerrainGameObject(data);
            terrainObject.name = "P9.1 PBR Highland Terrain";
            terrainObject.transform.SetParent(parent, false);
            terrainObject.transform.position = TerrainOrigin;
            var terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 3f;
            terrain.basemapDistance = 1200f;
            terrain.detailObjectDistance = 90f;
            terrain.treeDistance = 600f;
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
                var slope = data.GetSteepness(u, v) / 55f;
                var lakeDistance = Vector2.Distance(new Vector2(u, v), new Vector2(0.68f, 0.32f));
                var pebbles = Mathf.Clamp01((0.25f - lakeDistance) * 7f) + Mathf.Clamp01(slope - 0.65f);
                var cliff = Mathf.Clamp01((slope - 0.34f) * 2.2f);
                var moss = Mathf.Clamp01(0.48f + Mathf.Sin(u * 18f) * 0.2f + Mathf.Cos(v * 21f) * 0.15f) *
                           (1f - cliff);
                var grass = Mathf.Max(0.15f, 1.2f - moss - cliff - pebbles * 0.7f);
                var total = grass + moss + cliff + pebbles;
                maps[z, x, 0] = grass / total;
                maps[z, x, 1] = moss / total;
                maps[z, x, 2] = cliff / total;
                maps[z, x, 3] = pebbles / total;
            }
            data.SetAlphamaps(0, 0, maps);
        }

        private static List<GameObject> PopulateTrees(Terrain terrain, Transform parent)
        {
            var result = new List<GameObject>();
            var random = new System.Random(91031);
            for (var i = 0; i < 64; i++)
            {
                var angle = (i / 64f) * Mathf.PI * 2f + (float)(random.NextDouble() - 0.5) * 0.34f;
                var radius = 19f + (float)random.NextDouble() * 26f;
                var x = 48f + Mathf.Cos(angle) * radius;
                var z = 48f + Mathf.Sin(angle) * radius;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(65f, 31f)) < 14f ||
                    Vector2.Distance(new Vector2(x, z), new Vector2(48f, 50f)) < 11f)
                {
                    x = 48f + Mathf.Cos(angle) * (34f + (float)random.NextDouble() * 10f);
                    z = 48f + Mathf.Sin(angle) * (34f + (float)random.NextDouble() * 10f);
                }
                var tree = Instantiate(TreePrefabPaths[i % TreePrefabPaths.Length], parent,
                    $"P9.1 Tree {i + 1:00}");
                tree.transform.position = GroundPoint(terrain, x, z);
                tree.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                var scale = 0.72f + (float)random.NextDouble() * 0.38f;
                tree.transform.localScale = Vector3.one * scale;
                EnsureCollider(tree, true);
                result.Add(tree);
            }
            return result;
        }

        private static List<GameObject> PopulateRocks(Terrain terrain, Transform parent)
        {
            var result = new List<GameObject>();
            var random = new System.Random(91032);
            for (var i = 0; i < 24; i++)
            {
                var angle = i / 24f * Mathf.PI * 2f + 0.19f;
                var aroundLake = i < 16;
                var radiusX = aroundLake ? 13f : 34f;
                var radiusZ = aroundLake ? 9f : 28f;
                var centerX = aroundLake ? 65f : 48f;
                var centerZ = aroundLake ? 31f : 48f;
                var x = centerX + Mathf.Cos(angle) * radiusX + (float)(random.NextDouble() - 0.5) * 3f;
                var z = centerZ + Mathf.Sin(angle) * radiusZ + (float)(random.NextDouble() - 0.5) * 3f;
                var rock = Instantiate(RockPrefabPaths[i % RockPrefabPaths.Length], parent,
                    $"P9.1 Rock {i + 1:00}");
                rock.transform.position = GroundPoint(terrain, x, z) - Vector3.up * 0.12f;
                rock.transform.rotation = Quaternion.Euler((float)random.NextDouble() * 9f,
                    (float)random.NextDouble() * 360f, (float)random.NextDouble() * 7f);
                var scale = 0.48f + (float)random.NextDouble() * (aroundLake ? 0.62f : 1.05f);
                rock.transform.localScale = Vector3.one * scale;
                EnsureCollider(rock, false);
                result.Add(rock);
            }
            return result;
        }

        private static List<GameObject> PopulateFoliage(Terrain terrain, Transform parent)
        {
            var result = new List<GameObject>();
            var random = new System.Random(91033);
            for (var i = 0; i < 240; i++)
            {
                float x;
                float z;
                do
                {
                    x = 8f + (float)random.NextDouble() * 80f;
                    z = 8f + (float)random.NextDouble() * 80f;
                } while (Vector2.Distance(new Vector2(x, z), new Vector2(65f, 31f)) < 12f ||
                         Vector2.Distance(new Vector2(x, z), new Vector2(48f, 50f)) < 5f);
                var plant = Instantiate(FoliagePrefabPaths[i % FoliagePrefabPaths.Length], parent,
                    $"P9.1 Foliage {i + 1:00}");
                plant.transform.position = GroundPoint(terrain, x, z);
                plant.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                var scale = 0.72f + (float)random.NextDouble() * 0.62f;
                plant.transform.localScale = Vector3.one * scale;
                foreach (var renderer in plant.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.TwoSided;
                result.Add(plant);
            }
            return result;
        }

        private static Renderer CreateWater(Transform parent)
        {
            var water = Instantiate("Assets/TerrainDemoScene_URP/Prefabs/Water/WaterPlane.prefab", parent,
                "P9.1 Highland Pool");
            water.transform.position = new Vector3(TerrainOrigin.x + 65f, 0.05f, TerrainOrigin.z + 31f);
            water.transform.rotation = Quaternion.identity;
            water.transform.localScale = new Vector3(2.35f, 1f, 1.55f);
            foreach (var collider in water.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(collider);
            return water.GetComponentInChildren<Renderer>(true);
        }

        private static GameObject Instantiate(string path, Transform parent, string name)
        {
            var prefab = Load<GameObject>(path);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            return instance;
        }

        private static T Load<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required P9.1 asset is missing: " + path);
            return asset;
        }

        private static void ValidateSourceAssets()
        {
            foreach (var path in TerrainLayerPaths.Concat(TreePrefabPaths).Concat(RockPrefabPaths)
                         .Concat(FoliagePrefabPaths).Append("Assets/TerrainDemoScene_URP/Prefabs/Water/WaterPlane.prefab"))
                _ = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) ??
                    throw new InvalidOperationException("Required P9.1 source asset was not imported: " + path);
        }

        private static Vector3 GroundPoint(Terrain terrain, float localX, float localZ)
        {
            var world = new Vector3(TerrainOrigin.x + localX, 0f, TerrainOrigin.z + localZ);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;
            return world;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static void EnsureCollider(GameObject root, bool tree)
        {
            if (root.GetComponentInChildren<Collider>(true) != null) return;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.radius = tree ? 0.45f : 0.8f;
            collider.height = tree ? 7f : 1.4f;
            collider.center = tree ? new Vector3(0f, 3.5f, 0f) : new Vector3(0f, 0.55f, 0f);
        }
    }
}
