using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Domain.World;
using Game.Presentation.CameraControl;
using Game.Presentation.Environment;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Game.Infrastructure.Editor
{
    public static class P102ConnectedWorldBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string RootName = "P10.2 Connected Canonical World";
        private const string MaterialFolder = "Assets/Game/Generated/P102";
        private static readonly Vector3 Origin = new Vector3(-256f, -8f, -256f);
        private const float WaterLevel = 0.15f;

        private sealed class DistrictSpec
        {
            public DistrictSpec(string id, string name, string description, float x, float z)
            { Id = id; Name = name; Description = description; Position = new Vector3(x, 0f, z); }
            public string Id; public string Name; public string Description; public Vector3 Position;
        }

        private static readonly DistrictSpec[] Districts =
        {
            new DistrictSpec("starter_settlement", "Речной стан", "стартовое поселение", 0, 3),
            new DistrictSpec("enemy_settlement", "Восточный острог", "соперничающее поселение", 164, 86),
            new DistrictSpec("north_forest", "Северный лес", "густой старый лес", -82, 164),
            new DistrictSpec("river_ford", "Речной брод", "проходимая переправа", 18, 8),
            new DistrictSpec("east_hills", "Восточные холмы", "каменистая возвышенность", 142, 158),
            new DistrictSpec("south_field", "Южные поля", "широкая сухая равнина", -28, -154),
            new DistrictSpec("mirror_lake", "Зеркальное озеро", "пресная вода и заболоченный берег", 78, 132),
            new DistrictSpec("west_marsh", "Западная топь", "опасные сырые низины", -132, 62),
            new DistrictSpec("iron_ridge", "Железный кряж", "горная граница мира", 202, -54),
            new DistrictSpec("southern_coast", "Южный берег", "морская граница", 72, -218)
        };

        [MenuItem("Serenity/P10.2/Build Connected Canonical World")]
        public static void Build()
        {
            P95AtmosphereAndGlobalMapBuilder.Build();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(MaterialFolder);
            var baseRegion = UnityEngine.Object.FindFirstObjectByType<P92RealisticGameplayRegion>();
            var biome = UnityEngine.Object.FindFirstObjectByType<P93PopulatedBiomeWorld>();
            if (baseRegion == null || biome == null) throw new InvalidOperationException("P9.2/P9.3 world base is required.");

            var terrain = baseRegion.Terrain;
            ExpandTerrain(terrain);
            if (GameObject.Find(RootName) != null) UnityEngine.Object.DestroyImmediate(GameObject.Find(RootName));
            var root = new GameObject(RootName);
            var water = ExpandSea(baseRegion.WaterRenderer);
            var districtAnchors = ConfigureDistricts(root.transform, terrain, baseRegion);
            ConfigureBuildableAnchors(terrain, districtAnchors);
            var routeMarkers = CreateRoutes(root.transform, terrain, districtAnchors);
            var inlandWater = CreateInlandWater(root.transform, terrain);
            var mountains = CreateMountainLandmarks(root.transform, terrain);
            CreateWorldBoundaries(root.transform);
            RedistributeResourceAnchors(biome, terrain);

            var audit = AuditAccessibleLand(terrain);
            var marker = root.AddComponent<P102ConnectedWorldPresentation>();
            marker.Configure(terrain, districtAnchors.Select(pair => new P102ConnectedWorldPresentation.District()
                    .Configure(pair.Key.Id, pair.Key.Name, pair.Key.Description, pair.Value)),
                routeMarkers, inlandWater, mountains, audit.accessible, audit.total, biome.ResourceFormations.Count);
            var placement = root.AddComponent<P102WorldPlacementSurface>();
            placement.Configure(terrain, WaterLevel, 32f);
            ConfigureCamera(terrain);

            baseRegion.Configure(terrain, water, baseRegion.Trees, baseRegion.Rocks, baseRegion.Foliage,
                districtAnchors.Select(pair => new P92RealisticGameplayRegion.LocationAnchor()
                    .Configure(pair.Key.Id, pair.Value)));
            baseRegion.ValidateConfiguration();
            biome.ValidateConfiguration();
            marker.ValidateConfiguration();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"P102_CONNECTED_WORLD_BUILT size=512x512; sample=2m; accessibleSamples={audit.accessible}; " +
                      $"accessibleAreaM2={audit.accessible * 4}; locations={districtAnchors.Count}; resources=24; " +
                      "authority=LocalGameplayTerrain; strategicView=same-scene-camera");
        }

        private static void ExpandTerrain(Terrain terrain)
        {
            var data = terrain.terrainData;
            data.size = new Vector3(512f, 56f, 512f);
            terrain.transform.position = Origin;
            var heights = new float[data.heightmapResolution, data.heightmapResolution];
            for (var z = 0; z < data.heightmapResolution; z++) for (var x = 0; x < data.heightmapResolution; x++)
            {
                var wx = -256f + x;
                var wz = -256f + z;
                var edge = Mathf.Min(256f - Mathf.Abs(wx), 256f - Mathf.Abs(wz));
                var rolling = Mathf.Sin(wx * 0.035f) * 1.1f + Mathf.Cos(wz * 0.041f) * 0.9f +
                              Mathf.Sin((wx + wz) * 0.019f) * 0.7f;
                var height = 5.2f + rolling;
                height += Gaussian(wx, wz, 204f, -48f, 1150f) * 28f;
                height += Gaussian(wx, wz, 170f, 158f, 1700f) * 21f;
                height += Gaussian(wx, wz, -222f, 190f, 900f) * 19f;
                height = Mathf.Lerp(-2.2f, height, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 20f)));
                var lake = Gaussian(wx, wz, 12f, 122f, 780f);
                var westLake = Gaussian(wx, wz, -172f, 54f, 420f);
                height = Mathf.Lerp(height, -0.65f, Mathf.Clamp01((lake - 0.43f) * 3.8f));
                height = Mathf.Lerp(height, -0.35f, Mathf.Clamp01((westLake - 0.5f) * 4.5f));
                var riverX = 12f + (122f - wz) * 0.09f;
                var river = 1f - Mathf.Clamp01(Mathf.Abs(wx - riverX) / 5.5f);
                if (wz < 128f && wz > -230f) height = Mathf.Lerp(height, -0.3f, river * 0.92f);
                var ford = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(wx, wz), new Vector2(18f, 8f)) / 12f);
                height = Mathf.Lerp(height, 2.1f, ford);
                foreach (var district in Districts.Where(d => d.Id == "starter_settlement" || d.Id == "enemy_settlement" || d.Id == "south_field"))
                {
                    var flatten = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(wx, wz), new Vector2(district.Position.x, district.Position.z)) / 18f);
                    height = Mathf.Lerp(height, 4.6f, flatten * flatten);
                }
                heights[z, x] = Mathf.Clamp01((height - Origin.y) / data.size.y);
            }
            data.SetHeights(0, 0, heights);
            terrain.Flush();
            terrain.basemapDistance = 1600f;
            terrain.treeDistance = 900f;
        }

        private static Renderer ExpandSea(Renderer current)
        {
            if (current == null) throw new InvalidOperationException("Water renderer is required.");
            current.transform.position = new Vector3(0f, WaterLevel, 0f);
            current.transform.rotation = Quaternion.identity;
            current.transform.localScale = new Vector3(51.2f, 1f, 51.2f);
            return current;
        }

        private static List<KeyValuePair<DistrictSpec, Transform>> ConfigureDistricts(Transform parent, Terrain terrain,
            P92RealisticGameplayRegion baseRegion)
        {
            var result = new List<KeyValuePair<DistrictSpec, Transform>>();
            foreach (var district in Districts)
            {
                Transform anchor;
                if (!baseRegion.TryGetLocationAnchor(district.Id, out anchor))
                {
                    anchor = new GameObject("P10.2 District " + district.Id).transform;
                    anchor.SetParent(parent, false);
                }
                anchor.position = Ground(terrain, district.Position, 0.08f);
                var landmark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                landmark.name = "P10.2 Landmark " + district.Id;
                landmark.transform.SetParent(anchor, false);
                landmark.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                landmark.transform.localScale = new Vector3(1.2f, 0.45f, 1.2f);
                UnityEngine.Object.DestroyImmediate(landmark.GetComponent<Collider>());
                result.Add(new KeyValuePair<DistrictSpec, Transform>(district, anchor));
            }
            return result;
        }

        private static Transform[] CreateRoutes(Transform parent, Terrain terrain,
            IReadOnlyList<KeyValuePair<DistrictSpec, Transform>> districts)
        {
            var pairs = new[] { (0,2),(0,3),(0,5),(0,7),(2,6),(6,4),(3,4),(3,1),(4,1),(5,1),(5,9),(9,8),(8,1) };
            var material = GetMaterial("P102 Route.mat", new Color(0.32f, 0.21f, 0.11f), false);
            var result = new List<Transform>();
            for (var i = 0; i < pairs.Length; i++)
            {
                var go = new GameObject("P10.2 Physical Route " + i.ToString("00"));
                go.transform.SetParent(parent, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.widthMultiplier = 0.75f;
                line.numCornerVertices = 3;
                line.numCapVertices = 2;
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.textureMode = LineTextureMode.Tile;
                var a = districts[pairs[i].Item1].Value.position; var b = districts[pairs[i].Item2].Value.position;
                var direction = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
                var distance = Vector3.Distance(a, b);
                var trim = Mathf.Min(3.5f, distance * 0.08f);
                a += direction * trim;
                b -= direction * trim;
                var samples = Mathf.Max(3, Mathf.CeilToInt(Vector3.Distance(a, b) / 3f) + 1);
                line.positionCount = samples;
                for (var p = 0; p < samples; p++)
                {
                    var point = Vector3.Lerp(a, b, p / (samples - 1f));
                    point = Ground(terrain, point, 0.045f);
                    line.SetPosition(p, point);
                }
                result.Add(go.transform);
            }
            return result.ToArray();
        }

        private static void ConfigureBuildableAnchors(Terrain terrain,
            IReadOnlyList<KeyValuePair<DistrictSpec, Transform>> districts)
        {
            var region = UnityEngine.Object.FindFirstObjectByType<P5InhabitableRegionPresentation>();
            if (region == null || region.BuildableAreaAnchors.Count < 3) return;
            var ids = new[] { "starter_settlement", "enemy_settlement", "south_field" };
            for (var i = 0; i < 3; i++) region.BuildableAreaAnchors[i].position = Ground(terrain,
                districts.Single(x => x.Key.Id == ids[i]).Value.position, 0.03f);
        }

        private static Renderer[] CreateInlandWater(Transform parent, Terrain terrain)
        {
            var waterMaterial = GetMaterial("P102 Inland Water.mat", new Color(0.08f, 0.3f, 0.42f, 0.82f), true);
            var result = new List<Renderer>();
            result.Add(CreateWaterDisc(parent, "Mirror Lake", new Vector3(12f, WaterLevel + 0.03f, 122f), new Vector3(57f, 0.1f, 43f), waterMaterial));
            result.Add(CreateWaterDisc(parent, "West Marsh Lake", new Vector3(-172f, WaterLevel + 0.02f, 54f), new Vector3(41f, 0.1f, 31f), waterMaterial));
            var river = CreateWaterDisc(parent, "Connected River", new Vector3(27f, WaterLevel + 0.025f, -50f),
                new Vector3(6f, 0.08f, 178f), waterMaterial);
            river.transform.rotation = Quaternion.Euler(0f, -5f, 0f); result.Add(river);
            AddBlockedVolume(parent, "Mirror Lake Nav Blocker", new Vector3(12f, 2f, 122f), new Vector3(110f, 18f, 82f));
            AddBlockedVolume(parent, "West Lake Nav Blocker", new Vector3(-172f, 2f, 54f), new Vector3(76f, 18f, 56f));
            AddBlockedVolume(parent, "River North Nav Blocker", new Vector3(29f, 2f, 72f), new Vector3(9f, 18f, 91f));
            AddBlockedVolume(parent, "River South Nav Blocker", new Vector3(36f, 2f, -112f), new Vector3(9f, 18f, 205f));
            return result.ToArray();
        }

        private static Renderer CreateWaterDisc(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = "P10.2 " + name;
            go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>()); var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material; return renderer;
        }

        private static Transform[] CreateMountainLandmarks(Transform parent, Terrain terrain)
        {
            var points = new[] { new Vector3(204,0,-48), new Vector3(188,0,126), new Vector3(-220,0,190), new Vector3(222,0,202) };
            return points.Select((point, index) =>
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.name = "P10.2 Mountain Landmark " + index;
                go.transform.SetParent(parent, false); go.transform.position = Ground(terrain, point, 8f);
                go.transform.localScale = new Vector3(10f, 18f + index * 3f, 10f);
                var collider = go.GetComponent<Collider>(); if (collider != null) collider.enabled = true;
                return go.transform;
            }).ToArray();
        }

        private static void CreateWorldBoundaries(Transform parent)
        {
            AddBlockedVolume(parent, "North Sea Boundary", new Vector3(0,0,249), new Vector3(512,50,14));
            AddBlockedVolume(parent, "South Sea Boundary", new Vector3(0,0,-249), new Vector3(512,50,14));
            AddBlockedVolume(parent, "West Mountain Boundary", new Vector3(-249,0,0), new Vector3(14,50,512));
            AddBlockedVolume(parent, "East Mountain Boundary", new Vector3(249,0,0), new Vector3(14,50,512));
        }

        private static void AddBlockedVolume(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject("P10.2 " + name); go.transform.SetParent(parent, false); go.transform.position = center;
            var volume = go.AddComponent<NavMeshModifierVolume>(); volume.size = size; volume.area = NavMesh.GetAreaFromName("Not Walkable");
        }

        private static void RedistributeResourceAnchors(P93PopulatedBiomeWorld biome, Terrain terrain)
        {
            var starterSources = new[]
            {
                new Vector2(-12f, 5f), new Vector2(-8f, 7f),
                new Vector2(-4f, 9f), new Vector2(1f, 9f)
            };
            var centers = new[] { new Vector2(-145,-72), new Vector2(-98,128), new Vector2(116,134),
                new Vector2(172,54), new Vector2(-42,-164), new Vector2(128,-126) };
            for (var i = 0; i < biome.ResourceFormations.Count; i++)
            {
                if (i < starterSources.Length)
                {
                    var starter = starterSources[i];
                    biome.ResourceFormations[i].ClickableRoot.position = Ground(terrain,
                        new Vector3(starter.x, 0f, starter.y), 0.05f);
                    continue;
                }
                var center = centers[i % centers.Length]; var ring = i / centers.Length;
                var angle = (i * 137.5f) * Mathf.Deg2Rad;
                var point = new Vector3(center.x + Mathf.Cos(angle) * (12f + ring * 7f), 0f,
                    center.y + Mathf.Sin(angle) * (12f + ring * 7f));
                biome.ResourceFormations[i].ClickableRoot.position = Ground(terrain, point, 0.05f);
            }
        }

        private static (int accessible, int total) AuditAccessibleLand(Terrain terrain)
        {
            var accessible = 0; var total = 0;
            for (var z = -255; z <= 255; z += 2) for (var x = -255; x <= 255; x += 2)
            {
                total++; var point = new Vector3(x, 0f, z); var height = terrain.SampleHeight(point) + terrain.transform.position.y;
                var u = (x + 256f) / 512f; var v = (z + 256f) / 512f;
                if (height > WaterLevel + 0.25f && terrain.terrainData.GetSteepness(u, v) <= 32f) accessible++;
            }
            return (accessible, total);
        }

        private static void ConfigureCamera(Terrain terrain)
        {
            var camera = UnityEngine.Object.FindFirstObjectByType<RtsCameraController>(); if (camera == null) return;
            camera.Settings.BoundsMin = new Vector2(-238f, -238f); camera.Settings.BoundsMax = new Vector2(238f, 238f);
            camera.Settings.MaxZoom = 74f; camera.Settings.MoveSpeed = 34f; camera.Settings.EdgeScrollSpeed = 34f;
            camera.transform.position = Ground(terrain, new Vector3(0f, 0f, 3f), 0f);
            EditorUtility.SetDirty(camera);
        }

        private static Vector3 Ground(Terrain terrain, Vector3 point, float offset)
        { point.y = terrain.SampleHeight(point) + terrain.transform.position.y + offset; return point; }
        private static float Gaussian(float x, float z, float cx, float cz, float variance)
        { var dx = x - cx; var dz = z - cz; return Mathf.Exp(-(dx * dx + dz * dz) / variance); }

        private static Material GetMaterial(string fileName, Color color, bool transparent)
        {
            var path = MaterialFolder + "/" + fileName; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            if (transparent) { material.SetFloat("_Surface", 1f); material.renderQueue = 3000; }
            EditorUtility.SetDirty(material); return material;
        }
    }
}
