using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Presentation.Environment;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Infrastructure.Editor
{
    /// <summary>Rebuilds the P5 authored projection in the approved P8.1 natural PBR direction.</summary>
    public static class P82NaturalRegionBuilder
    {
        private const string ScenePath = "Assets/Scenes/LocalGameplay.unity";
        private const string MaterialFolder = "Assets/Game/Art/Materials/P82";
        private const string MeshFolder = "Assets/Game/Art/Meshes/P82";
        private static readonly Vector2[] Boundary =
        {
            new Vector2(-24f,-30f), new Vector2(-14f,-44f), new Vector2(4f,-49f),
            new Vector2(23f,-47f), new Vector2(39f,-39f), new Vector2(50f,-25f),
            new Vector2(55f,-7f), new Vector2(53f,15f), new Vector2(46f,33f),
            new Vector2(32f,44f), new Vector2(13f,48f), new Vector2(-5f,46f),
            new Vector2(-19f,37f), new Vector2(-27f,22f), new Vector2(-29f,3f),
            new Vector2(-28f,-16f)
        };

        [MenuItem("Serenity/P8.2/Build Natural Region")]
        public static void Build()
        {
            P3ReferenceSceneBuilder.Build();
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(MeshFolder);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var composition = UnityEngine.Object.FindFirstObjectByType<LocalSceneCompositionRoot>();
            var region = composition != null ? composition.GetComponent<P5InhabitableRegionPresentation>() : null;
            var world = GameObject.Find("P3 Reference World");
            var oldGround = GameObject.Find("P3 Buildable Forest Clearing");
            if (composition == null || region == null || world == null || oldGround == null)
                throw new InvalidOperationException("P5 scene must exist before applying P8.2.");

            var meadow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Materials/P81/P81_Meadow.mat") ??
                CreateMaterial("P82_Meadow", new Color(0.22f, 0.31f, 0.15f), 0f, 0.22f);
            var shore = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Materials/P81/P81_Shore.mat") ??
                CreateMaterial("P82_Shore", new Color(0.48f, 0.38f, 0.23f), 0f, 0.28f);
            var bark = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Materials/P81/P81_BrownBark.mat") ??
                CreateMaterial("P82_BrownBark", new Color(0.20f, 0.095f, 0.038f), 0f, 0.14f);
            var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Art/Materials/P81/P81_CalmWater.mat") ??
                CreateMaterial("P82_Water", new Color(0.10f, 0.27f, 0.29f), 0.02f, 0.86f);
            var iron = CreateMaterial("P82_IronOre", new Color(0.27f, 0.30f, 0.31f), 0.42f, 0.34f);
            var gold = CreateMaterial("P82_GoldOre", new Color(0.66f, 0.43f, 0.09f), 0.58f, 0.43f);

            var landMesh = WriteIslandMesh(MeshFolder + "/P82_InhabitableLand.asset", "P82 Inhabitable Land", 0f, 1f);
            oldGround.name = "P3 Buildable Forest Clearing";
            oldGround.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            oldGround.transform.localScale = Vector3.one;
            oldGround.GetComponent<MeshFilter>().sharedMesh = landMesh;
            oldGround.GetComponent<Renderer>().sharedMaterial = meadow;
            var box = oldGround.GetComponent<BoxCollider>();
            if (box != null) UnityEngine.Object.DestroyImmediate(box);
            var landCollider = oldGround.GetComponent<MeshCollider>() ?? oldGround.AddComponent<MeshCollider>();
            landCollider.sharedMesh = landMesh;

            var oldShore = GameObject.Find("P3 Curved Shore");
            if (oldShore != null) UnityEngine.Object.DestroyImmediate(oldShore);
            var shoreObject = CreateMeshObject("P3 Curved Shore", world.transform,
                WriteShoreMesh(MeshFolder + "/P82_ShoreRing.asset"), shore);
            shoreObject.transform.position = new Vector3(0f, -0.08f, 0f);
            var waterMesh = WriteWaterMesh(MeshFolder + "/P82_BoundaryWater.asset");
            var waterObject = GameObject.Find("P3 Water Edge") ??
                CreateMeshObject("P3 Water Edge", world.transform, waterMesh, water);
            waterObject.name = "P3 Water Edge";
            waterObject.GetComponent<MeshFilter>().sharedMesh = waterMesh;
            waterObject.GetComponent<Renderer>().sharedMaterial = water;
            waterObject.transform.position = new Vector3(0f, -0.38f, 0f);
            var waterCollider = waterObject.GetComponent<MeshCollider>() ?? waterObject.AddComponent<MeshCollider>();
            waterCollider.sharedMesh = waterMesh;
            var waterModifier = waterObject.GetComponent<NavMeshModifier>() ?? waterObject.AddComponent<NavMeshModifier>();
            waterModifier.overrideArea = true;
            waterModifier.area = 1;

            var trunkMesh = WriteTrunkMesh(MeshFolder + "/P82_TaperedTrunk.asset");
            var trunkRenderers = new List<Renderer>();
            foreach (var tree in Enumerable.Range(1, region.TreeCount).Select(i => GameObject.Find("P3 Tree " + i)).Where(x => x != null))
            {
                var trunk = CreateMeshObject("Brown Bark Trunk", tree.transform, trunkMesh, bark);
                trunk.transform.localPosition = new Vector3(0f, 0f, 0f);
                trunk.transform.localScale = new Vector3(0.44f, 1.08f, 0.44f);
                trunkRenderers.Add(trunk.GetComponent<Renderer>());
            }

            var resourceAnchors = region.ResourceAnchors.ToList();
            var oreMesh = WriteOreMesh(MeshFolder + "/P82_OreCluster.asset");
            AddOreNodes(world.transform, oreMesh, iron, "Iron", "gather_iron", 16,
                new[] { new Vector3(5f,0.08f,29f), new Vector3(34f,0.08f,18f), new Vector3(25f,0.08f,-25f), new Vector3(-7f,0.08f,-28f) }, resourceAnchors);
            AddOreNodes(world.transform, oreMesh, gold, "Gold", "gather_gold", 20,
                new[] { new Vector3(-7f,0.08f,28f), new Vector3(37f,0.08f,7f), new Vector3(15f,0.08f,-31f), new Vector3(-11f,0.08f,-12f) }, resourceAnchors);

            var piles = Enumerable.Range(0, 4).Select(region.GetPileAnchor).Where(x => x != null).ToArray();
            var placements = region.RouteWaypoints.Concat(region.BuildableAreaAnchors).Concat(region.ObstacleAnchors)
                .Concat(piles).Concat(resourceAnchors.Select(x => x.VisualRoot)).Distinct().ToArray();
            var foliage = world.GetComponentsInChildren<Renderer>(true).Where(x =>
                x.gameObject.name.StartsWith("P3 Grass", StringComparison.Ordinal) ||
                x.gameObject.name.StartsWith("P3 Tree", StringComparison.Ordinal)).ToArray();
            var routeRenderers = world.GetComponentsInChildren<Renderer>(true).Where(x =>
                x.gameObject.name.StartsWith("P5 ", StringComparison.Ordinal) &&
                x.gameObject.name.Contains("Route", StringComparison.Ordinal)).ToArray();
            region.Configure(region.PlayerTerritoryAnchor, region.RivalTerritoryAnchor, region.RouteWaypoints,
                region.BuildableAreaAnchors, region.ObstacleAnchors, piles, resourceAnchors,
                new[] { oldGround.GetComponent<Renderer>(), shoreObject.GetComponent<Renderer>() }.Concat(routeRenderers),
                foliage, waterObject.GetComponent<Renderer>(), region.TreeCount, region.RockCount, region.GrassCount);
            region.ConfigureGeography(Boundary, placements, trunkRenderers);
            region.ValidateConfiguration();

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.fogColor = new Color(0.58f, 0.65f, 0.61f);
            RenderSettings.ambientIntensity = 0.82f;
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (light.type == LightType.Directional)
                {
                    light.color = new Color(1f, 0.91f, 0.75f);
                    light.intensity = 1.15f;
                    light.shadows = LightShadows.Soft;
                }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("P82_NATURAL_REGION_BUILT resources=24; finiteBoundary=16; placeholders=0");
        }

        private static void AddOreNodes(Transform parent, Mesh mesh, Material material, string label, string recipe,
            int firstIndex, IReadOnlyList<Vector3> positions,
            ICollection<P5InhabitableRegionPresentation.ResourceAnchor> anchors)
        {
            for (var i = 0; i < positions.Count; i++)
            {
                var node = CreateMeshObject($"P8.2 {label} Ore Vein {i + 1}", parent, mesh, material);
                node.transform.position = positions[i];
                node.transform.rotation = Quaternion.Euler(0f, i * 67f + 13f, i % 2 == 0 ? 4f : -5f);
                node.transform.localScale = Vector3.one * (1f + i * 0.08f);
                var collider = node.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                anchors.Add(new P5InhabitableRegionPresentation.ResourceAnchor().Configure(recipe,
                    70 + i * 15, firstIndex + i, node.transform, material.color));
            }
        }

        private static GameObject CreateMeshObject(string name, Transform parent, Mesh mesh, Material material)
        {
            var value = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            value.transform.SetParent(parent, false);
            value.GetComponent<MeshFilter>().sharedMesh = mesh;
            value.GetComponent<MeshRenderer>().sharedMaterial = material;
            return value;
        }

        private static Mesh WriteIslandMesh(string path, string name, float outerHeight, float innerHeight)
        {
            var vertices = new List<Vector3> { new Vector3(12f, innerHeight, 0f) };
            vertices.AddRange(Boundary.Select(p => new Vector3(Mathf.Lerp(12f, p.x, 0.55f),
                innerHeight + 0.35f * Mathf.Sin(p.x * 0.17f + p.y * 0.11f), Mathf.Lerp(0f, p.y, 0.55f))));
            vertices.AddRange(Boundary.Select(p => new Vector3(p.x, outerHeight + 0.12f * Mathf.Sin(p.x), p.y)));
            var triangles = new List<int>();
            for (var i = 0; i < Boundary.Length; i++)
            {
                var next = (i + 1) % Boundary.Length;
                triangles.AddRange(new[] { 0, 1 + i, 1 + next });
                var a = 1 + i; var b = 1 + next; var c = 1 + Boundary.Length + i; var d = 1 + Boundary.Length + next;
                triangles.AddRange(new[] { a, c, b, b, c, d });
            }
            for (var i = 0; i < triangles.Count; i += 3)
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            return WriteMesh(path, name, vertices, triangles);
        }

        private static Mesh WriteShoreMesh(string path)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < Boundary.Length; i++)
            {
                var p = Boundary[i];
                var direction = (p - new Vector2(12f, 0f)).normalized;
                vertices.Add(new Vector3(p.x, 0.08f, p.y));
                vertices.Add(new Vector3(p.x + direction.x * 3.2f, -0.28f, p.y + direction.y * 3.2f));
            }
            for (var i = 0; i < Boundary.Length; i++)
            {
                var next = (i + 1) % Boundary.Length;
                triangles.AddRange(new[] { i * 2, i * 2 + 1, next * 2, next * 2, i * 2 + 1, next * 2 + 1 });
            }
            return WriteMesh(path, "P82 Natural Shore Ring", vertices, triangles);
        }

        private static Mesh WriteWaterMesh(string path)
        {
            var vertices = new List<Vector3>
            {
                new Vector3(-72,0,-66), new Vector3(76,0,-66), new Vector3(-72,0,68), new Vector3(76,0,68)
            };
            return WriteMesh(path, "P82 Boundary Water", vertices, new List<int> { 0,2,1,1,2,3 });
        }

        private static Mesh WriteTrunkMesh(string path)
        {
            const int sides = 8;
            var vertices = new List<Vector3>();
            for (var ring = 0; ring < 3; ring++)
            for (var i = 0; i < sides; i++)
            {
                var radius = ring == 0 ? 0.55f : ring == 1 ? 0.42f : 0.24f;
                var angle = i * Mathf.PI * 2f / sides + ring * 0.09f;
                vertices.Add(new Vector3(Mathf.Cos(angle) * radius, ring * 2.25f, Mathf.Sin(angle) * radius));
            }
            var triangles = new List<int>();
            for (var ring = 0; ring < 2; ring++)
            for (var i = 0; i < sides; i++)
            {
                var next = (i + 1) % sides; var a = ring * sides + i; var b = ring * sides + next;
                var c = (ring + 1) * sides + i; var d = (ring + 1) * sides + next;
                triangles.AddRange(new[] { a,c,b,b,c,d });
            }
            return WriteMesh(path, "P82 Tapered Brown Trunk", vertices, triangles);
        }

        private static Mesh WriteOreMesh(string path)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var cluster = 0; cluster < 5; cluster++)
            {
                var center = new Vector3((cluster % 3 - 1) * 0.42f, 0f, (cluster / 3 - 0.5f) * 0.5f);
                var start = vertices.Count;
                vertices.Add(center + new Vector3(-0.28f,0,-0.24f)); vertices.Add(center + new Vector3(0.3f,0,-0.18f));
                vertices.Add(center + new Vector3(0.22f,0,0.28f)); vertices.Add(center + new Vector3(-0.25f,0,0.22f));
                vertices.Add(center + new Vector3(0f,0.72f + cluster * 0.09f,0f));
                triangles.AddRange(new[] { start,start+1,start+4,start+1,start+2,start+4,start+2,start+3,start+4,start+3,start,start+4,
                    start,start+3,start+2,start,start+2,start+1 });
            }
            return WriteMesh(path, "P82 Ore Cluster", vertices, triangles);
        }

        private static Mesh WriteMesh(string path, string name, IList<Vector3> vertices, IList<int> triangles)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear(); mesh.vertices = vertices.ToArray(); mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
